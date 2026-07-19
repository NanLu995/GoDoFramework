#!/usr/bin/env python3
"""Generate, validate, and serve the GoDoFramework documentation website."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT_ROOT = REPOSITORY_ROOT / ".artifacts" / "docs"
WORK_ROOT = ARTIFACT_ROOT / "work"
SITE_ROOT = ARTIFACT_ROOT / "site"
PROJECT_PATH = REPOSITORY_ROOT / "GoDoFramework.csproj"
TOOL_MANIFEST = REPOSITORY_ROOT / ".config" / "dotnet-tools.json"
LINT_ONLY_FILES = (
    "Docs/README.md",
    "Docs/STYLE_GUIDE.md",
    "Docs/Templates/MODULE_TEMPLATE.md",
    "Docs/Templates/RECIPE_TEMPLATE.md",
)
MANUAL_ROOT = REPOSITORY_ROOT / "Docs" / "Manual"
NAVIGATION_FILES = {
    locale: REPOSITORY_ROOT / "Docs" / f"navigation.{locale}.json"
    for locale in ("zh-cn", "en-us")
}
COVERAGE_PATH = REPOSITORY_ROOT / "Docs" / "coverage.json"

LOCALES = ("zh-cn", "en-us")
LOCALE_SETTINGS = {
    "zh-cn": {
        "html_lang": "zh-CN",
        "app_title": "GoDoFramework 文档",
        "switch_label": "English",
        "api_label": "API Reference",
    },
    "en-us": {
        "html_lang": "en-US",
        "app_title": "GoDoFramework Documentation",
        "switch_label": "中文",
        "api_label": "API Reference (Chinese descriptions)",
    },
}

HEADING_PATTERN = re.compile(r"^(#{1,6})\s+(.+?)\s*$")
FENCE_PATTERN = re.compile(r"^\s*```(.*)$")
FRONT_MATTER_BOUNDARY = "---"
LANGUAGE_SWITCH_MARKER = "<!-- godo-language-switch -->"


@dataclass(frozen=True)
class Page:
    logical_id: str
    locale: str
    title: str
    source: Path
    destination: Path
    group: str
    translation_source: Path | None = None


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="生成、检查或预览 GoDoFramework 中英文 DocFX 文档站。"
    )
    parser.add_argument(
        "command",
        choices=("lint", "prepare", "check", "build", "serve", "clean"),
        help=(
            "lint 只检查 Markdown 与翻译状态；prepare 生成 DocFX 工作区；"
            "check 将 DocFX 警告视为错误；build 生成站点；"
            "serve 生成并启动本地预览；clean 删除文档生成物。"
        ),
    )
    parser.add_argument("--host", default="127.0.0.1", help="serve 监听地址。")
    parser.add_argument("--port", type=int, default=8080, help="serve 监听端口。")
    return parser.parse_args()


def split_front_matter(text: str) -> tuple[dict[str, str], list[str]]:
    lines = text.splitlines()
    if not lines or lines[0].strip() != FRONT_MATTER_BOUNDARY:
        return {}, lines

    try:
        end_index = next(
            index
            for index, line in enumerate(lines[1:], start=1)
            if line.strip() == FRONT_MATTER_BOUNDARY
        )
    except StopIteration as exception:
        raise RuntimeError("front matter 缺少结束分隔符") from exception

    metadata: dict[str, str] = {}
    for line in lines[1:end_index]:
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if ":" not in line:
            raise RuntimeError(f"无法解析 front matter：{line}")
        key, value = line.split(":", 1)
        metadata[key.strip()] = value.strip().strip('"\'')
    return metadata, lines[end_index + 1 :]


def read_document(path: Path) -> tuple[dict[str, str], list[str]]:
    try:
        return split_front_matter(path.read_text(encoding="utf-8"))
    except RuntimeError as exception:
        raise RuntimeError(
            f"{path.relative_to(REPOSITORY_ROOT)}：{exception}"
        ) from exception


def read_title(path: Path) -> str:
    _, lines = read_document(path)
    headings = [match.group(2) for line in lines if (match := HEADING_PATTERN.match(line))]
    h1_headings = [
        match.group(2)
        for line in lines
        if (match := HEADING_PATTERN.match(line)) and len(match.group(1)) == 1
    ]
    if len(h1_headings) != 1:
        raise RuntimeError(
            f"文档必须恰好包含一个一级标题：{path.relative_to(REPOSITORY_ROOT)}"
        )
    return h1_headings[0] if headings else path.stem


def create_page(
    logical_id: str,
    locale: str,
    source: Path,
    destination: Path,
    group: str,
    title: str | None = None,
    translation_source: Path | None = None,
) -> Page:
    return Page(
        logical_id=logical_id,
        locale=locale,
        title=title or read_title(source),
        source=source,
        destination=destination,
        group=group,
        translation_source=translation_source,
    )


def discover_pages(locale: str) -> list[Page]:
    locale_root = MANUAL_ROOT / locale
    if not locale_root.is_dir():
        raise RuntimeError(f"用户手册目录不存在：{locale_root}")

    pages: list[Page] = []
    for source in sorted(locale_root.rglob("*.md")):
        destination = source.relative_to(locale_root)
        translation_source = None
        if locale == "en-us":
            candidate = MANUAL_ROOT / "zh-cn" / destination
            translation_source = candidate if candidate.is_file() else None
        pages.append(
            create_page(
                destination.with_suffix("").as_posix(),
                locale,
                source,
                destination,
                "Manual",
                translation_source=translation_source,
            )
        )
    validate_destinations(pages)
    load_navigation(locale, pages)
    return pages


def load_navigation(
    locale: str,
    pages: list[Page],
    navigation_path: Path | None = None,
) -> dict[str, object]:
    navigation_path = navigation_path or NAVIGATION_FILES[locale]
    try:
        navigation = json.loads(navigation_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        raise RuntimeError(f"无法读取导航配置 {navigation_path}：{exception}") from exception

    home = navigation.get("home")
    sections = navigation.get("sections")
    if not isinstance(home, str) or not isinstance(sections, list):
        raise RuntimeError(f"导航配置格式错误：{navigation_path}")

    listed: list[str] = [home]
    for section in sections:
        if not isinstance(section, dict) or not isinstance(section.get("name"), str):
            raise RuntimeError(f"导航分组格式错误：{navigation_path}")
        section_pages = section.get("pages")
        if not isinstance(section_pages, list) or not all(
            isinstance(value, str) for value in section_pages
        ):
            raise RuntimeError(f"导航分组 pages 格式错误：{navigation_path}")
        listed.extend(section_pages)

    if len(listed) != len(set(listed)):
        raise RuntimeError(f"导航包含重复页面：{navigation_path}")
    for value in listed:
        path = Path(value)
        if path.is_absolute() or path.suffix.lower() != ".md" or ".." in path.parts:
            raise RuntimeError(f"导航页面路径无效：{value}")

    discovered = {page.destination.as_posix() for page in pages}
    listed_set = set(listed)
    missing = sorted(discovered - listed_set)
    unknown = sorted(listed_set - discovered)
    if missing or unknown:
        details = []
        if missing:
            details.append(f"未加入导航：{', '.join(missing)}")
        if unknown:
            details.append(f"页面不存在：{', '.join(unknown)}")
        raise RuntimeError(f"导航配置与用户手册不一致：{'；'.join(details)}")
    return navigation


def validate_destinations(pages: list[Page]) -> None:
    destinations: dict[str, Path] = {}
    logical_ids: set[str] = set()
    for page in pages:
        if not page.source.is_file():
            raise RuntimeError(f"文档源文件不存在：{page.source}")
        key = page.destination.as_posix().lower()
        previous = destinations.get(key)
        if previous is not None:
            raise RuntimeError(
                f"文档输出路径冲突：{previous} 与 {page.source} -> {page.destination}"
            )
        if page.logical_id in logical_ids:
            raise RuntimeError(f"逻辑页面 ID 重复：{page.logical_id}")
        destinations[key] = page.source
        logical_ids.add(page.logical_id)


def validate_public_sources(pages_by_locale: dict[str, list[Page]]) -> None:
    manual_root = MANUAL_ROOT.resolve()
    errors: list[str] = []
    for pages in pages_by_locale.values():
        for page in pages:
            source = page.source.resolve()
            if not source.is_relative_to(manual_root):
                errors.append(str(page.source.relative_to(REPOSITORY_ROOT)))
            if page.source.name.upper() == "USAGE.MD":
                errors.append(str(page.source.relative_to(REPOSITORY_ROOT)))
    if errors:
        raise RuntimeError("公开站点包含内部文档源：" + ", ".join(sorted(set(errors))))


def validate_coverage(
    repository_root: Path = REPOSITORY_ROOT,
    coverage_path: Path = COVERAGE_PATH,
    manual_root: Path = MANUAL_ROOT,
) -> None:
    try:
        coverage = json.loads(coverage_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        raise RuntimeError(f"无法读取文档覆盖清单 {coverage_path}：{exception}") from exception

    entries = coverage.get("entries")
    allow_pending = coverage.get("allow_pending")
    if not isinstance(entries, dict) or not isinstance(allow_pending, bool):
        raise RuntimeError("coverage.json 必须包含布尔值 allow_pending 和对象 entries")

    actual_contracts = {
        path.relative_to(repository_root).as_posix()
        for path in (repository_root / "addons" / "godo_framework").rglob("USAGE.md")
    }
    registered_contracts: set[str] = set()
    errors: list[str] = []
    valid_statuses = {"pending", "documented", "reference-only"}

    for entry_id, entry in entries.items():
        if not isinstance(entry, dict):
            errors.append(f"{entry_id}：条目必须是对象")
            continue
        contract = entry.get("contract")
        status = entry.get("status")
        reason = entry.get("reason")
        reviewed_hash = entry.get("reviewed_contract_hash")
        if not isinstance(contract, str):
            errors.append(f"{entry_id}：缺少 contract")
            continue
        if contract in registered_contracts:
            errors.append(f"{entry_id}：contract 重复：{contract}")
        registered_contracts.add(contract)
        if status not in valid_statuses:
            errors.append(f"{entry_id}：status 必须是 {', '.join(sorted(valid_statuses))}")
        if status == "pending" and not allow_pending:
            errors.append(f"{entry_id}：当前不允许 pending")
        if status in {"pending", "reference-only"} and (
            not isinstance(reason, str) or not reason.strip()
        ):
            errors.append(f"{entry_id}：{status} 条目必须说明 reason")

        contract_path = repository_root / contract
        if contract not in actual_contracts:
            errors.append(f"{entry_id}：contract 不存在或不是框架 USAGE.md：{contract}")
        elif not isinstance(reviewed_hash, str):
            errors.append(f"{entry_id}：缺少 reviewed_contract_hash")
        else:
            actual_hash = hashlib.sha256(contract_path.read_bytes()).hexdigest()
            if reviewed_hash.lower() != f"sha256:{actual_hash}":
                errors.append(
                    f"{entry_id}：技术契约已变化；复核用户文档后更新哈希为 sha256:{actual_hash}"
                )

        manual_pages = entry.get("manual_pages")
        if status == "documented" and (
            not isinstance(manual_pages, list) or not manual_pages
        ):
            errors.append(f"{entry_id}：documented 条目必须列出 manual_pages")
        if manual_pages is not None:
            if not isinstance(manual_pages, list):
                errors.append(f"{entry_id}：manual_pages 必须是字符串列表")
                continue
            for page_value in manual_pages:
                if not isinstance(page_value, str):
                    errors.append(f"{entry_id}：manual_pages 必须是字符串列表")
                    continue
                page = Path(page_value)
                if page.is_absolute() or ".." in page.parts or page.suffix.lower() != ".md":
                    errors.append(f"{entry_id}：用户手册路径无效：{page_value}")
                elif not (manual_root / "zh-cn" / page).is_file():
                    errors.append(f"{entry_id}：中文用户手册不存在：{page_value}")

    missing = sorted(actual_contracts - registered_contracts)
    stale = sorted(registered_contracts - actual_contracts)
    if missing:
        errors.append(f"新增技术契约尚未登记：{', '.join(missing)}")
    if stale:
        errors.append(f"覆盖清单包含失效契约：{', '.join(stale)}")
    if errors:
        raise RuntimeError("文档覆盖检查失败：\n" + "\n".join(f"- {error}" for error in errors))
    print(f"[COVERAGE] PASS ({len(actual_contracts)} contracts)")


def lint_markdown(page: Page) -> list[str]:
    errors: list[str] = []
    relative = page.source.relative_to(REPOSITORY_ROOT)
    try:
        _, lines = read_document(page.source)
    except RuntimeError as exception:
        return [str(exception)]

    h1_count = 0
    previous_heading_level = 0
    inside_fence = False
    fence_start_line = 0
    for line_number, line in enumerate(lines, start=1):
        fence_match = FENCE_PATTERN.match(line)
        if fence_match:
            suffix = fence_match.group(1).strip()
            if inside_fence:
                if not suffix:
                    inside_fence = False
                continue
            inside_fence = True
            fence_start_line = line_number
            if not suffix:
                errors.append(f"{relative}:{line_number}：代码块缺少语言标识")
            continue

        if inside_fence:
            continue
        heading_match = HEADING_PATTERN.match(line)
        if not heading_match:
            continue
        level = len(heading_match.group(1))
        if level == 1:
            h1_count += 1
        if previous_heading_level and level > previous_heading_level + 1:
            errors.append(
                f"{relative}:{line_number}：标题层级从 H{previous_heading_level} 跳到 H{level}"
            )
        previous_heading_level = level

    if inside_fence:
        errors.append(f"{relative}:{fence_start_line}：代码块没有闭合")
    if h1_count != 1:
        errors.append(f"{relative}：必须恰好包含一个一级标题，当前为 {h1_count}")
    return errors


def validate_translation(page: Page) -> list[str]:
    if page.locale != "en-us" or page.translation_source is None:
        return []

    metadata, _ = read_document(page.source)
    relative = page.source.relative_to(REPOSITORY_ROOT)
    declared_source = metadata.get("translation_of")
    declared_hash = metadata.get("translation_source_hash", "").lower()
    expected_source = page.translation_source.relative_to(REPOSITORY_ROOT).as_posix()
    expected_hash = hashlib.sha256(page.translation_source.read_bytes()).hexdigest()

    errors: list[str] = []
    if declared_source != expected_source:
        errors.append(
            f"{relative}：translation_of 应为 {expected_source}，当前为 {declared_source or '<missing>'}"
        )
    if declared_hash != f"sha256:{expected_hash}":
        errors.append(
            f"{relative}：翻译已过期；复核后将 translation_source_hash 更新为 sha256:{expected_hash}"
        )
    return errors


def lint_pages(pages_by_locale: dict[str, list[Page]]) -> None:
    errors: list[str] = []
    unique_sources: set[Path] = set()
    for pages in pages_by_locale.values():
        for page in pages:
            if page.source not in unique_sources:
                errors.extend(lint_markdown(page))
                unique_sources.add(page.source)
            errors.extend(validate_translation(page))

    for source_value in LINT_ONLY_FILES:
        source = REPOSITORY_ROOT / source_value
        if source in unique_sources:
            continue
        lint_page = Page(
            logical_id=f"lint:{source_value}",
            locale="zh-cn",
            title=source.stem,
            source=source,
            destination=Path(source.name),
            group="LintOnly",
        )
        errors.extend(lint_markdown(lint_page))
        unique_sources.add(source)

    if errors:
        details = "\n".join(f"- {error}" for error in errors)
        raise RuntimeError(f"文档质量检查失败：\n{details}")
    print(f"[LINT] PASS ({len(unique_sources)} files)")


def yaml_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def append_toc_page(lines: list[str], page: Page, indent: int = 0) -> None:
    prefix = " " * indent
    lines.append(f"{prefix}- name: {yaml_string(page.title)}")
    lines.append(f"{prefix}  href: {page.destination.as_posix()}")


def write_toc(locale: str, pages: list[Page], locale_work_root: Path) -> None:
    settings = LOCALE_SETTINGS[locale]
    lines: list[str] = []
    navigation = load_navigation(locale, pages)
    pages_by_destination = {page.destination.as_posix(): page for page in pages}

    for section in navigation["sections"]:
        lines.append(f"- name: {yaml_string(section['name'])}")
        lines.append("  items:")
        for destination in section["pages"]:
            append_toc_page(lines, pages_by_destination[destination], indent=4)

    lines.extend(
        (
            f"- name: {yaml_string(settings['api_label'])}",
            "  href: api/",
        )
    )
    (locale_work_root / "toc.yml").write_text(
        "\n".join(lines) + "\n", encoding="utf-8"
    )


def write_docfx_config(locale: str, locale_work_root: Path) -> None:
    locale_site_root = SITE_ROOT / locale
    repository_relative = Path(
        os.path.relpath(REPOSITORY_ROOT, locale_work_root)
    ).as_posix()
    output_relative = Path(
        os.path.relpath(locale_site_root, locale_work_root)
    ).as_posix()
    settings = LOCALE_SETTINGS[locale]
    config = {
        "metadata": [
            {
                "src": [{"files": [PROJECT_PATH.name], "src": repository_relative}],
                "dest": "api",
                "filter": "filterConfig.yml",
                "properties": {
                    "TargetFramework": "net8.0",
                    "GoDoIncludeGuideInput": "true",
                    "GoDoIncludePhantomCamera": "true",
                },
                "memberLayout": "samePage",
                "namespaceLayout": "nested",
            }
        ],
        "build": {
            "content": [
                {
                    "files": ["**/*.{md,yml}"],
                    "exclude": ["filterConfig.yml"],
                }
            ],
            "output": output_relative,
            "template": [
                "default",
                "modern",
                f"{repository_relative}/Docs/Templates/Site",
            ],
            "globalMetadata": {
                "_appName": "GoDoFramework",
                "_appTitle": settings["app_title"],
                "_enableSearch": True,
                "_disableContribution": True,
                "_lang": settings["html_lang"],
            },
            "sitemap": {
                "baseUrl": f"https://nanlu995.github.io/GoDoFramework/{locale}/",
                "changefreq": "weekly",
            },
        },
    }
    (locale_work_root / "docfx.json").write_text(
        json.dumps(config, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    (locale_work_root / "filterConfig.yml").write_text(
        "apiRules:\n"
        "- include:\n"
        "    uidRegex: ^GoDo($|\\.)\n"
        "- exclude:\n"
        "    uidRegex: .*\n",
        encoding="utf-8",
    )


def prepare_workspace(
    pages_by_locale: dict[str, list[Page]], clear_site: bool = False
) -> None:
    if WORK_ROOT.exists():
        shutil.rmtree(WORK_ROOT)
    if clear_site and SITE_ROOT.exists():
        shutil.rmtree(SITE_ROOT)

    for locale, pages in pages_by_locale.items():
        locale_work_root = WORK_ROOT / locale
        locale_work_root.mkdir(parents=True)
        for page in pages:
            destination = locale_work_root / page.destination
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(page.source, destination)
        write_toc(locale, pages, locale_work_root)
        write_docfx_config(locale, locale_work_root)
        print(f"[PREPARE] {locale}: {len(pages)} Markdown files")
    print(f"[PREPARE] 工作区：{WORK_ROOT}")


def run(command: list[str]) -> None:
    printable = " ".join(command)
    print(f"[RUN] {printable}")
    result = subprocess.run(command, cwd=REPOSITORY_ROOT)
    if result.returncode != 0:
        raise RuntimeError(f"命令失败（exit={result.returncode}）：{printable}")


def run_doc_tests() -> None:
    run([sys.executable, "-m", "unittest", "Verification/Docs/test_build_docs.py"])


def restore_tools_and_project() -> None:
    if not TOOL_MANIFEST.is_file():
        raise RuntimeError(f"缺少 .NET 工具清单：{TOOL_MANIFEST}")
    run(["dotnet", "tool", "restore"])
    run(
        [
            "dotnet",
            "restore",
            str(PROJECT_PATH),
            "--nologo",
            "-p:GoDoIncludeGuideInput=false",
            "-p:GoDoIncludePhantomCamera=false",
        ]
    )


def run_docfx(locale: str, warnings_as_errors: bool) -> None:
    arguments = [
        "dotnet",
        "tool",
        "run",
        "docfx",
        "--",
        str(WORK_ROOT / locale / "docfx.json"),
    ]
    if warnings_as_errors:
        arguments.append("--warningsAsErrors")
    run(arguments)


def validate_api_reference() -> None:
    """Reject generated GoDo API items that lost their XML summary."""
    api_root = WORK_ROOT / "zh-cn" / "api"
    errors: list[str] = []
    own_items = 0
    for api_file in api_root.glob("*.yml"):
        text = api_file.read_text(encoding="utf-8")
        items_section = text.split("references:", maxsplit=1)[0]
        for block in items_section.split("- uid: ")[1:]:
            if "addons/godo_framework/" not in block:
                continue
            own_items += 1
            uid = block.splitlines()[0].strip()
            if "\n  summary: " not in block:
                errors.append(f"{api_file.name}: {uid} 缺少 XML <summary>")

    required_items = (
        "GoDo.GuideInput.GuideInputBackendInstaller.yml",
        "GoDo.PhantomCameraRig.yml",
    )
    for filename in required_items:
        if not (api_root / filename).is_file():
            errors.append(f"可选集成 API 未生成：{filename}")

    if errors:
        details = "\n".join(f"- {error}" for error in errors)
        raise RuntimeError(f"API Reference 校验失败：\n{details}")
    print(f"[API] PASS ({own_items} GoDo items)")


def write_root_landing() -> None:
    SITE_ROOT.mkdir(parents=True, exist_ok=True)
    landing = """<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>GoDoFramework Documentation</title>
  <style>
    :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
    body { min-height: 100vh; margin: 0; display: grid; place-items: center; background: #111827; color: #f9fafb; }
    main { width: min(560px, calc(100% - 48px)); text-align: center; }
    p { color: #cbd5e1; line-height: 1.7; }
    nav { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-top: 32px; }
    a { padding: 16px; border: 1px solid #475569; border-radius: 10px; color: #f9fafb; text-decoration: none; background: #1e293b; }
    a:hover { border-color: #60a5fa; background: #263449; }
  </style>
</head>
<body>
  <main>
    <h1>GoDoFramework</h1>
    <p>Choose documentation language / 选择文档语言</p>
    <nav>
      <a href="zh-cn/index.html" lang="zh-CN">简体中文</a>
      <a href="en-us/index.html" lang="en-US">English</a>
    </nav>
  </main>
</body>
</html>
"""
    (SITE_ROOT / "index.html").write_text(landing, encoding="utf-8")
    (SITE_ROOT / ".nojekyll").write_text("", encoding="utf-8")


def inject_language_switches() -> None:
    for locale in LOCALES:
        other_locale = "en-us" if locale == "zh-cn" else "zh-cn"
        locale_root = SITE_ROOT / locale
        other_root = SITE_ROOT / other_locale
        label = LOCALE_SETTINGS[locale]["switch_label"]
        for html_path in locale_root.rglob("*.html"):
            relative = html_path.relative_to(locale_root)
            matching_target = other_root / relative
            target = matching_target if matching_target.is_file() else other_root / "index.html"
            href = Path(os.path.relpath(target, html_path.parent)).as_posix()
            text = html_path.read_text(encoding="utf-8")
            if LANGUAGE_SWITCH_MARKER in text:
                continue
            snippet = f"""
{LANGUAGE_SWITCH_MARKER}
<a class="godo-language-switch" href="{html.escape(href, quote=True)}" hreflang="{other_locale}">{html.escape(label)}</a>
<style>
.godo-language-switch {{ position: fixed; right: 1rem; bottom: 1rem; z-index: 1080; padding: .45rem .75rem; border: 1px solid var(--bs-border-color); border-radius: .5rem; background: var(--bs-body-bg); color: var(--bs-link-color); text-decoration: none; box-shadow: 0 .2rem .8rem rgba(0,0,0,.15); }}
.godo-language-switch:hover {{ text-decoration: none; filter: brightness(.95); }}
</style>
"""
            if "</body>" not in text:
                continue
            html_path.write_text(
                text.replace("</body>", f"{snippet}</body>", 1),
                encoding="utf-8",
            )


def validate_site() -> None:
    required_files = (
        SITE_ROOT / ".nojekyll",
        SITE_ROOT / "index.html",
        SITE_ROOT / "zh-cn" / "index.html",
        SITE_ROOT / "zh-cn" / "index.json",
        SITE_ROOT / "zh-cn" / "sitemap.xml",
        SITE_ROOT / "zh-cn" / "public" / "main.css",
        SITE_ROOT / "zh-cn" / "getting-started" / "index.html",
        SITE_ROOT / "zh-cn" / "api" / "toc.html",
        SITE_ROOT / "zh-cn" / "api" / "GoDo.Services.html",
        SITE_ROOT / "en-us" / "index.html",
        SITE_ROOT / "en-us" / "index.json",
        SITE_ROOT / "en-us" / "sitemap.xml",
        SITE_ROOT / "en-us" / "public" / "main.css",
        SITE_ROOT / "en-us" / "getting-started" / "index.html",
        SITE_ROOT / "en-us" / "api" / "toc.html",
        SITE_ROOT / "en-us" / "api" / "GoDo.Services.html",
    )
    errors = [
        f"缺少发布产物：{path.relative_to(SITE_ROOT)}"
        for path in required_files
        if not path.is_file()
    ]

    chinese_toc = SITE_ROOT / "zh-cn" / "toc.html"
    if chinese_toc.is_file():
        toc_text = chinese_toc.read_text(encoding="utf-8")
        if 'title="GoDoFramework">GoDoFramework</a>' in toc_text:
            errors.append("导航仍包含与顶部品牌重复的首页入口")

    for locale in LOCALES:
        theme_css = SITE_ROOT / locale / "public" / "main.css"
        if theme_css.is_file() and theme_css.stat().st_size == 0:
            errors.append(f"自定义主题为空：{theme_css.relative_to(SITE_ROOT)}")

    conceptual_page = SITE_ROOT / "zh-cn" / "getting-started" / "index.html"
    if conceptual_page.is_file() and "toc-offcanvas" not in conceptual_page.read_text(
        encoding="utf-8"
    ):
        errors.append("概念文档未生成桌面侧栏与移动端目录容器")

    checked_pages = 0
    site_root = SITE_ROOT.resolve()
    for locale in LOCALES:
        locale_root = SITE_ROOT / locale
        for html_path in locale_root.rglob("*.html"):
            text = html_path.read_text(encoding="utf-8")
            if "</body>" not in text:
                continue
            checked_pages += 1
            if LANGUAGE_SWITCH_MARKER not in text:
                errors.append(
                    f"完整 HTML 页面缺少语言切换：{html_path.relative_to(SITE_ROOT)}"
                )
                continue
            switch_match = re.search(
                rf"{re.escape(LANGUAGE_SWITCH_MARKER)}\s*<a[^>]+href=\"([^\"]+)\"",
                text,
            )
            if switch_match is None:
                errors.append(
                    f"无法解析语言切换目标：{html_path.relative_to(SITE_ROOT)}"
                )
                continue
            target_value = html.unescape(switch_match.group(1)).split("#", 1)[0]
            target = (html_path.parent / target_value).resolve()
            if not target.is_relative_to(site_root) or not target.is_file():
                errors.append(
                    f"语言切换目标不存在：{html_path.relative_to(SITE_ROOT)} -> {target_value}"
                )

    if errors:
        details = "\n".join(f"- {error}" for error in errors)
        raise RuntimeError(f"发布产物检查失败：\n{details}")
    print(f"[SITE] PASS ({checked_pages} full HTML pages)")


def build_sites(warnings_as_errors: bool) -> None:
    restore_tools_and_project()
    for locale in LOCALES:
        run_docfx(locale, warnings_as_errors)
    validate_api_reference()
    write_root_landing()
    inject_language_switches()
    validate_site()
    print(f"[PASS] 文档站：{SITE_ROOT}")


def serve_site(host: str, port: int) -> None:
    handler = partial(SimpleHTTPRequestHandler, directory=str(SITE_ROOT))
    with ThreadingHTTPServer((host, port), handler) as server:
        print(f"[SERVE] http://{host}:{port}/")
        print("[SERVE] Press Ctrl+C to stop")
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            print("\n[SERVE] stopped")


def clean() -> None:
    if ARTIFACT_ROOT.exists():
        shutil.rmtree(ARTIFACT_ROOT)
    print(f"[CLEAN] 已清理：{ARTIFACT_ROOT}")


def main() -> int:
    configure_console_encoding()
    arguments = parse_arguments()
    if arguments.port <= 0 or arguments.port > 65535:
        raise RuntimeError("--port 必须在 1–65535 之间。")
    if arguments.command == "clean":
        clean()
        return 0

    if arguments.command == "check":
        run_doc_tests()
    validate_coverage()
    pages_by_locale = {locale: discover_pages(locale) for locale in LOCALES}
    validate_public_sources(pages_by_locale)
    lint_pages(pages_by_locale)
    if arguments.command == "lint":
        return 0

    prepare_workspace(
        pages_by_locale,
        clear_site=arguments.command in ("check", "build", "serve"),
    )
    if arguments.command == "prepare":
        return 0

    build_sites(warnings_as_errors=arguments.command == "check")
    if arguments.command == "serve":
        serve_site(arguments.host, arguments.port)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as exception:
        print(f"[FAIL] {exception}", file=sys.stderr)
        raise SystemExit(1)
