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
GODOT_GENERATED_API_PATTERN = re.compile(
    r"\.(?:MethodName|PropertyName|SignalName)(?:\.|$)"
)
API_REFERENCE_STATUSES = {"pending", "verified"}


@dataclass(frozen=True)
class Page:
    logical_id: str
    locale: str
    title: str
    source: Path
    destination: Path
    group: str
    translation_source: Path | None = None


@dataclass(frozen=True)
class ApiReferenceIssue:
    entry_id: str
    status: str
    source: str
    uid: str
    rule: str
    detail: str


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
        choices=(
            "lint",
            "api-audit",
            "prepare",
            "check",
            "build",
            "serve",
            "clean",
        ),
        help=(
            "lint 只检查 Markdown 与翻译状态；api-audit 只生成并审计 API metadata；"
            "prepare 生成 DocFX 工作区；"
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
        api_reference_status = entry.get("api_reference_status")
        api_reference_reason = entry.get("api_reference_reason")
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
        if api_reference_status not in API_REFERENCE_STATUSES:
            errors.append(
                f"{entry_id}：api_reference_status 必须是 "
                f"{', '.join(sorted(API_REFERENCE_STATUSES))}"
            )
        if api_reference_status == "pending" and (
            not isinstance(api_reference_reason, str)
            or not api_reference_reason.strip()
        ):
            errors.append(f"{entry_id}：API Reference 待审计条目必须说明 api_reference_reason")

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
        "- exclude:\n"
        "    uidRegex: '\\.(MethodName|PropertyName|SignalName)($|\\.)'\n"
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
        "build",
        str(WORK_ROOT / locale / "docfx.json"),
    ]
    if warnings_as_errors:
        arguments.append("--warningsAsErrors")
    run(arguments)


def run_docfx_metadata(locale: str, warnings_as_errors: bool) -> None:
    arguments = [
        "dotnet",
        "tool",
        "run",
        "docfx",
        "--",
        "metadata",
        str(WORK_ROOT / locale / "docfx.json"),
    ]
    if warnings_as_errors:
        arguments.append("--warningsAsErrors")
    run(arguments)


def load_coverage_entries(
    coverage_path: Path = COVERAGE_PATH,
) -> dict[str, dict[str, object]]:
    try:
        coverage = json.loads(coverage_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        raise RuntimeError(f"无法读取文档覆盖清单 {coverage_path}：{exception}") from exception
    entries = coverage.get("entries")
    if not isinstance(entries, dict):
        raise RuntimeError("coverage.json 缺少 entries")
    return {
        entry_id: entry
        for entry_id, entry in entries.items()
        if isinstance(entry_id, str) and isinstance(entry, dict)
    }


API_TOC_GROUPS = {
    "error-hub": "Core",
    "event-channel": "Core",
    "log-hub": "Core",
    "services": "Core",
    "debugger": "Diagnostics",
    "procedure": "Runtime / Procedure",
    "scene": "Runtime / Scene",
    "ui": "Runtime / UI",
    "audio": "Runtime / Audio",
    "input": "Runtime / Input",
    "camera": "Runtime / Camera",
    "resources": "Runtime / Resources",
    "config": "Runtime / Config",
    "data-table-runtime": "Runtime / DataTable",
    "save": "Runtime / Save",
    "settings": "Runtime / Settings",
    "localization": "Runtime / Localization",
    "scheduler": "Runtime / Scheduler",
    "pool": "Runtime / Pool",
    "guide-input": "Integrations / GUIDE Input",
    "phantom-camera": "Integrations / Phantom Camera",
    "data-table": "Tools / DataTable",
    "framework-setup": "Editor setup",
}


def write_api_toc(locale: str) -> None:
    """Group API types by their registered framework module instead of namespace."""
    api_root = WORK_ROOT / locale / "api"
    coverage_entries = load_coverage_entries()
    groups: dict[str, list[tuple[str, str]]] = {}
    unknown: list[str] = []

    for api_file in sorted(api_root.glob("*.yml")):
        text = api_file.read_text(encoding="utf-8")
        uid_match = re.search(r"(?m)^\s*-\s+uid:\s+(.+?)\s*$", text)
        name_match = re.search(r"(?m)^\s+name:\s+(.+?)\s*$", text)
        source = extract_api_source(text)
        if uid_match is None or name_match is None or source is None:
            continue
        owner = find_api_reference_owner(source, coverage_entries)
        if owner is None:
            unknown.append(uid_match.group(1).strip("\"'"))
            continue
        entry_id, _ = owner
        group = API_TOC_GROUPS.get(entry_id)
        if group is None:
            unknown.append(uid_match.group(1).strip("\"'"))
            continue
        groups.setdefault(group, []).append((
            uid_match.group(1).strip("\"'"),
            name_match.group(1).strip("\"'"),
        ))

    if unknown:
        raise RuntimeError(
            "API TOC 存在未分组公开类型：" + ", ".join(sorted(unknown))
        )

    if not groups:
        raise RuntimeError("API TOC 未生成任何模块分组。")

    lines = ["### YamlMime:TableOfContent", "items:"]
    for group, items in groups.items():
        lines.append(f"- name: {yaml_string(group)}")
        lines.append("  items:")
        for uid, name in items:
            lines.append(f"  - uid: {yaml_string(uid)}")
            lines.append(f"    name: {yaml_string(name)}")
    lines.append("memberLayout: SamePage")
    (api_root / "toc.yml").write_text("\n".join(lines) + "\n", encoding="utf-8")


def find_api_reference_owner(
    source: str,
    coverage_entries: dict[str, dict[str, object]],
) -> tuple[str, dict[str, object]] | None:
    candidates: list[tuple[int, str, dict[str, object]]] = []
    for entry_id, entry in coverage_entries.items():
        contract = entry.get("contract")
        if not isinstance(contract, str):
            continue
        source_root = Path(contract).parent.as_posix().rstrip("/") + "/"
        if source.startswith(source_root):
            candidates.append((len(source_root), entry_id, entry))
    if not candidates:
        return None
    _, entry_id, entry = max(candidates, key=lambda candidate: candidate[0])
    return entry_id, entry


def extract_api_source(block: str) -> str | None:
    for match in re.finditer(r"(?m)^\s+path:\s+(.+?)\s*$", block):
        value = match.group(1).strip().strip("\"'").replace("\\", "/")
        marker = "addons/godo_framework/"
        marker_index = value.find(marker)
        if marker_index >= 0:
            return value[marker_index:]
    return None


def described_syntax_ids(syntax: str, section_name: str) -> list[tuple[str, bool]]:
    section_match = re.search(
        rf"(?ms)^    {re.escape(section_name)}:\s*\n(.*?)(?=^    [A-Za-z][A-Za-z0-9.]*:|\Z)",
        syntax,
    )
    if section_match is None:
        return []
    section = section_match.group(1)
    matches = list(re.finditer(r"(?m)^    - id:\s+(.+?)\s*$", section))
    result: list[tuple[str, bool]] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(section)
        item = section[match.start():end]
        has_description = re.search(r"(?m)^      description:\s*\S", item) is not None
        result.append((match.group(1).strip("\"'"), has_description))
    return result


def collect_api_reference_issues(
    api_root: Path,
    coverage_entries: dict[str, dict[str, object]],
) -> tuple[list[ApiReferenceIssue], int]:
    issues: list[ApiReferenceIssue] = []
    own_items = 0
    for api_file in api_root.glob("*.yml"):
        text = api_file.read_text(encoding="utf-8")
        items_section = text.split("references:", maxsplit=1)[0]
        for block in items_section.split("- uid: ")[1:]:
            uid = block.splitlines()[0].strip()
            source = extract_api_source(block)
            if source is None:
                continue
            own_items += 1
            owner = find_api_reference_owner(source, coverage_entries)
            if owner is None:
                issues.append(ApiReferenceIssue(
                    "<unregistered>",
                    "verified",
                    source,
                    uid,
                    "missing-owner",
                    "API 源文件没有对应的 USAGE.md coverage 条目。",
                ))
                continue
            entry_id, entry = owner
            status_value = entry.get("api_reference_status")
            status = status_value if isinstance(status_value, str) else "verified"

            if GODOT_GENERATED_API_PATTERN.search(uid):
                issues.append(ApiReferenceIssue(
                    entry_id,
                    status,
                    source,
                    uid,
                    "generated-godot-api",
                    "Godot 生成的名称辅助类型不应进入公开 API Reference。",
                ))
                continue
            if "\n  summary: " not in block:
                issues.append(ApiReferenceIssue(
                    entry_id,
                    status,
                    source,
                    uid,
                    "missing-summary",
                    "缺少 XML <summary>。",
                ))

            syntax_marker = "\n  syntax:\n"
            if syntax_marker not in block:
                continue
            syntax = block.split(syntax_marker, maxsplit=1)[1]
            for parameter_id, documented in described_syntax_ids(syntax, "parameters"):
                if not documented:
                    issues.append(ApiReferenceIssue(
                        entry_id,
                        status,
                        source,
                        uid,
                        "missing-param",
                        f"参数 {parameter_id} 缺少 XML <param> 说明。",
                    ))
            for type_parameter_id, documented in described_syntax_ids(
                syntax, "typeParameters"
            ):
                if not documented:
                    issues.append(ApiReferenceIssue(
                        entry_id,
                        status,
                        source,
                        uid,
                        "missing-typeparam",
                        f"泛型参数 {type_parameter_id} 缺少 XML <typeparam> 说明。",
                    ))

            item_type_match = re.search(r"(?m)^  type:\s+(.+?)\s*$", block)
            item_type = item_type_match.group(1).strip() if item_type_match else ""
            return_match = re.search(
                r"(?ms)^    return:\s*\n(.*?)(?=^    [A-Za-z][A-Za-z0-9.]*:|\Z)",
                syntax,
            )
            if item_type in {"Method", "Delegate"} and return_match is not None:
                return_block = return_match.group(1)
                return_type_match = re.search(
                    r"(?m)^      type:\s+(.+?)\s*$", return_block
                )
                return_type = (
                    return_type_match.group(1).strip()
                    if return_type_match is not None
                    else ""
                )
                has_return_description = (
                    re.search(
                        r"(?m)^      description:\s*\S",
                        return_block,
                    )
                    is not None
                )
                if return_type not in {"", "System.Void"} and not has_return_description:
                    issues.append(ApiReferenceIssue(
                        entry_id,
                        status,
                        source,
                        uid,
                        "missing-returns",
                        "非 void 成员缺少 XML <returns> 说明。",
                    ))
    return issues, own_items


def write_api_audit_report(issues: list[ApiReferenceIssue]) -> Path:
    report_path = ARTIFACT_ROOT / "api-audit.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "issues": [
            {
                "entry": issue.entry_id,
                "status": issue.status,
                "source": issue.source,
                "uid": issue.uid,
                "rule": issue.rule,
                "detail": issue.detail,
            }
            for issue in issues
        ]
    }
    report_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return report_path


def blocking_api_reference_issues(
    issues: list[ApiReferenceIssue],
) -> list[ApiReferenceIssue]:
    always_blocking_rules = {"missing-summary", "missing-owner", "generated-godot-api"}
    return [
        issue
        for issue in issues
        if issue.rule in always_blocking_rules or issue.status == "verified"
    ]


def validate_api_reference() -> None:
    """Audit generated GoDo API items and enforce verified module quality."""
    api_root = WORK_ROOT / "zh-cn" / "api"
    coverage_entries = load_coverage_entries()
    issues, own_items = collect_api_reference_issues(api_root, coverage_entries)
    report_path = write_api_audit_report(issues)
    errors = blocking_api_reference_issues(issues)

    required_items = (
        "GoDo.GuideInput.GuideInputBackendInstaller.yml",
        "GoDo.PhantomCameraRig.yml",
    )
    for filename in required_items:
        if not (api_root / filename).is_file():
            errors.append(ApiReferenceIssue(
                "<generation>",
                "verified",
                filename,
                filename,
                "missing-required-api",
                "可选集成 API 未生成。",
            ))

    if errors:
        details = "\n".join(
            f"- [{error.entry_id}/{error.rule}] {error.uid}: {error.detail}"
            for error in errors
        )
        raise RuntimeError(f"API Reference 校验失败：\n{details}")
    pending_count = sum(1 for issue in issues if issue.status == "pending")
    print(
        f"[API] PASS ({own_items} GoDo items, {pending_count} pending issues)"
    )
    print(f"[API] 审计报告：{report_path}")


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
    <p>Opening your preferred documentation language… / 正在打开你的首选文档语言…</p>
    <nav>
      <a href="zh-cn/index.html" lang="zh-CN" data-godo-locale="zh-cn">简体中文</a>
      <a href="en-us/index.html" lang="en-US" data-godo-locale="en-us">English</a>
    </nav>
  </main>
  <script>
    (() => {
      const storageKey = "godo-docs-locale";
      const savedLocale = localStorage.getItem(storageKey);
      const browserLanguages = navigator.languages || [navigator.language || ""];
      const preferredLocale = browserLanguages.some((language) => language.toLowerCase().startsWith("zh"))
        ? "zh-cn"
        : "en-us";
      const locale = savedLocale === "zh-cn" || savedLocale === "en-us"
        ? savedLocale
        : preferredLocale;

      document.querySelectorAll("[data-godo-locale]").forEach((link) => {
        link.addEventListener("click", () => localStorage.setItem(storageKey, link.dataset.godoLocale));
      });
      window.location.replace(`${locale}/index.html`);
    })();
  </script>
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
<a class="godo-language-switch" href="{html.escape(href, quote=True)}" hreflang="{other_locale}" onclick="localStorage.setItem('godo-docs-locale', '{other_locale}')">{html.escape(label)}</a>
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

    root_landing = SITE_ROOT / "index.html"
    if root_landing.is_file() and "godo-docs-locale" not in root_landing.read_text(
        encoding="utf-8"
    ):
        errors.append("根页面缺少自动语言选择逻辑")

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
        run_docfx_metadata(locale, warnings_as_errors)
        write_api_toc(locale)
    validate_api_reference()
    for locale in LOCALES:
        run_docfx(locale, warnings_as_errors)
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
    if arguments.command == "api-audit":
        restore_tools_and_project()
        run_docfx_metadata("zh-cn", warnings_as_errors=False)
        validate_api_reference()
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
