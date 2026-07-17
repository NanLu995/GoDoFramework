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

LOCALES = ("zh-cn", "en-us")
LOCALE_SETTINGS = {
    "zh-cn": {
        "html_lang": "zh-CN",
        "app_title": "GoDoFramework 文档",
        "switch_label": "English",
        "section_names": {
            "Start": "开始使用",
            "Recipes": "教程与配方",
            "Core": "核心模块",
            "Runtime": "运行时服务",
            "Tools": "工具模块",
            "Debugger": "调试工具",
            "Integrations": "可选集成",
            "Reference": "参考资料",
        },
        "api_label": "API Reference",
    },
    "en-us": {
        "html_lang": "en-US",
        "app_title": "GoDoFramework Documentation",
        "switch_label": "中文",
        "section_names": {
            "Start": "Getting Started",
            "Recipes": "Recipes",
            "Core": "Core",
            "Runtime": "Runtime Services",
            "Tools": "Tools",
            "Debugger": "Debugger",
            "Integrations": "Optional Integrations",
            "Reference": "Reference",
        },
        "api_label": "API Reference (Chinese descriptions)",
    },
}

CURATED_PAGES = {
    "zh-cn": (
        ("home", "Docs/i18n/zh-cn/index.md", "index.md", "Home", None),
        (
            "quick-start",
            "Docs/i18n/zh-cn/getting-started/quick-start.md",
            "getting-started/index.md",
            "Start",
            None,
        ),
        (
            "installation",
            "addons/godo_framework/USAGE.md",
            "getting-started/installation.md",
            "Start",
            "安装、升级与移除",
        ),
        (
            "game-development",
            "AI/AI_GAMEDEV_GUIDE.md",
            "guides/game-development.md",
            "Start",
            "使用框架制作游戏",
        ),
        (
            "project-structure",
            "AI/PROJECT_STRUCTURE.md",
            "guides/project-structure.md",
            "Start",
            "推荐项目结构",
        ),
        (
            "troubleshooting",
            "AI/GODOT_GOTCHAS.md",
            "reference/godot-csharp-troubleshooting.md",
            "Reference",
            "Godot / C# 故障排查",
        ),
        (
            "changelog",
            "CHANGELOG.md",
            "reference/changelog.md",
            "Reference",
            "版本记录",
        ),
        (
            "architecture",
            "AI/ARCHITECTURE.md",
            "contributing/architecture.md",
            "Reference",
            "框架架构",
        ),
    ),
    "en-us": (
        (
            "home",
            "Docs/i18n/en-us/index.md",
            "index.md",
            "Home",
            None,
        ),
        (
            "quick-start",
            "Docs/i18n/en-us/getting-started/quick-start.md",
            "getting-started/index.md",
            "Start",
            None,
        ),
    ),
}

GROUP_ORDER = {
    "Home": 0,
    "Start": 1,
    "Recipes": 2,
    "Core": 3,
    "Runtime": 4,
    "Tools": 5,
    "Debugger": 6,
    "Integrations": 7,
    "Reference": 8,
}

HEADING_PATTERN = re.compile(r"^(#{1,6})\s+(.+?)\s*$")
FENCE_PATTERN = re.compile(r"^\s*```(.*)$")
FRONT_MATTER_BOUNDARY = "---"
LANGUAGE_SWITCH_MARKER = "<!-- godo-language-switch -->"
NAVIGATION_TITLE_SUFFIXES = (" 使用指南", " 使用说明", " 可选集成")


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


def discover_curated_pages(locale: str) -> list[Page]:
    pages: list[Page] = []
    for logical_id, source_value, destination_value, group, title in CURATED_PAGES[locale]:
        source = REPOSITORY_ROOT / source_value
        translation_source: Path | None = None
        if locale == "en-us":
            zh_match = next(
                (
                    item
                    for item in CURATED_PAGES["zh-cn"]
                    if item[0] == logical_id
                ),
                None,
            )
            translation_source = REPOSITORY_ROOT / zh_match[1] if zh_match else None
        pages.append(
            create_page(
                logical_id,
                locale,
                source,
                Path(destination_value),
                group,
                title,
                translation_source,
            )
        )
    return pages


def discover_recipe_pages(locale: str) -> list[Page]:
    pages: list[Page] = []
    recipes_root = REPOSITORY_ROOT / "AI" / "Recipes"
    for chinese_source in sorted(
        (path for path in recipes_root.glob("*.md") if not path.name.endswith(".en.md")),
        key=lambda path: path.name.lower(),
    ):
        if locale == "zh-cn":
            source = chinese_source
            destination_name = chinese_source.name
            translation_source = None
        else:
            source = chinese_source.with_name(f"{chinese_source.stem}.en.md")
            if not source.is_file():
                continue
            destination_name = chinese_source.name
            translation_source = chinese_source

        pages.append(
            create_page(
                f"recipe:{chinese_source.stem}",
                locale,
                source,
                Path("recipes") / destination_name,
                "Recipes",
                translation_source=translation_source,
            )
        )
    return pages


def module_location(relative: Path) -> tuple[str, Path]:
    parts = relative.parts
    if parts[0] != "godo_framework":
        return (
            "Integrations",
            Path("modules") / "integrations" / parts[0] / "index.md",
        )

    group = parts[1]
    if group in ("Core", "Runtime", "Tools"):
        module_parts = parts[2:-1]
    elif group == "Integrations":
        module_parts = parts[2:-1]
    else:
        module_parts = parts[1:-1]
    if not module_parts:
        module_parts = (group,)
    return group, Path("modules") / group.lower() / Path(*module_parts) / "index.md"


def navigation_title(title: str) -> str:
    for suffix in NAVIGATION_TITLE_SUFFIXES:
        if title.endswith(suffix):
            return title.removesuffix(suffix)
    return title


def discover_module_pages(locale: str) -> list[Page]:
    pages: list[Page] = []
    addons_root = REPOSITORY_ROOT / "addons"
    for chinese_source in addons_root.glob("**/USAGE.md"):
        relative = chinese_source.relative_to(addons_root)
        if relative.as_posix() == "godo_framework/USAGE.md":
            continue

        if locale == "zh-cn":
            source = chinese_source
            translation_source = None
        else:
            source = chinese_source.with_name("USAGE.en.md")
            if not source.is_file():
                continue
            translation_source = chinese_source

        group, destination = module_location(relative)
        pages.append(
            create_page(
                f"module:{relative.parent.as_posix()}",
                locale,
                source,
                destination,
                group,
                navigation_title(read_title(source)),
                translation_source=translation_source,
            )
        )
    return pages


def discover_pages(locale: str) -> list[Page]:
    pages = (
        discover_curated_pages(locale)
        + discover_recipe_pages(locale)
        + discover_module_pages(locale)
    )
    pages.sort(
        key=lambda page: (
            GROUP_ORDER.get(page.group, 99),
            page.destination.as_posix().lower(),
        )
    )
    validate_destinations(pages)
    return pages


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

    for group in GROUP_ORDER:
        if group == "Home":
            continue
        group_pages = [page for page in pages if page.group == group]
        if not group_pages:
            continue
        lines.append(f"- name: {yaml_string(settings['section_names'][group])}")
        lines.append("  items:")
        for page in group_pages:
            append_toc_page(lines, page, indent=4)

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
        SITE_ROOT / "zh-cn" / "modules" / "runtime" / "Localization" / "index.html",
        SITE_ROOT / "zh-cn" / "modules" / "integrations" / "GuideInput" / "index.html",
        SITE_ROOT / "zh-cn" / "modules" / "integrations" / "PhantomCamera" / "index.html",
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

    duplicate_integration_root = (
        SITE_ROOT / "zh-cn" / "modules" / "integrations" / "Integrations"
    )
    if duplicate_integration_root.exists():
        errors.append("可选集成输出路径重复包含 Integrations")

    chinese_toc = SITE_ROOT / "zh-cn" / "toc.html"
    if chinese_toc.is_file():
        toc_text = chinese_toc.read_text(encoding="utf-8")
        if 'title="GoDoFramework">GoDoFramework</a>' in toc_text:
            errors.append("导航仍包含与顶部品牌重复的首页入口")
        for suffix in NAVIGATION_TITLE_SUFFIXES:
            if suffix in toc_text:
                errors.append(f"导航标签仍包含冗余后缀：{suffix.strip()}")

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

    pages_by_locale = {locale: discover_pages(locale) for locale in LOCALES}
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
