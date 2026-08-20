#!/usr/bin/env python3
"""Create and verify the committed GoDo public API compatibility baseline."""

from __future__ import annotations

import argparse
import configparser
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_API_ROOT = REPOSITORY_ROOT / ".artifacts" / "docs" / "work" / "zh-cn" / "api"
DEFAULT_BASELINE_PATH = Path(__file__).resolve().parent / "public-api-baseline.json"
DEFAULT_REPORT_PATH = REPOSITORY_ROOT / ".artifacts" / "docs" / "api-compatibility.json"
PLUGIN_CONFIG = REPOSITORY_ROOT / "addons" / "godo_framework" / "plugin.cfg"
BASELINE_SCHEMA_VERSION = 1
GODOT_GENERATED_API_PATTERN = re.compile(
    r"\.(?:MethodName|PropertyName|SignalName)(?:\.|$)"
)
SCRIPT_PATH_ATTRIBUTE_PATTERN = re.compile(r"\[ScriptPath\(\"[^\"]+\"\)\]\s*")


@dataclass(frozen=True)
class ApiItem:
    uid: str
    kind: str
    signature: str


@dataclass(frozen=True)
class ChangedApiItem:
    uid: str
    baseline: ApiItem
    current: ApiItem


@dataclass(frozen=True)
class CompatibilityReport:
    removed: tuple[ApiItem, ...]
    changed: tuple[ChangedApiItem, ...]
    added: tuple[ApiItem, ...]

    @property
    def is_compatible(self) -> bool:
        return not self.removed and not self.changed


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
        sys.stderr.reconfigure(encoding="utf-8", line_buffering=True)


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="检查或显式更新 GoDo public API 兼容基线。"
    )
    parser.add_argument("command", choices=("check", "update"))
    parser.add_argument(
        "--api-root",
        type=Path,
        default=DEFAULT_API_ROOT,
        help="DocFX ManagedReference metadata 目录。",
    )
    parser.add_argument(
        "--baseline",
        type=Path,
        default=DEFAULT_BASELINE_PATH,
        help="兼容基线 JSON 路径。",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=DEFAULT_REPORT_PATH,
        help="check 结果报告路径。",
    )
    return parser.parse_args(arguments)


def unquote_yaml_scalar(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] == "'":
        return value[1:-1].replace("''", "'")
    if len(value) >= 2 and value[0] == value[-1] == '"':
        try:
            decoded = json.loads(value)
        except json.JSONDecodeError as error:
            raise RuntimeError(f"无法解析 DocFX 双引号标量：{value}") from error
        if not isinstance(decoded, str):
            raise RuntimeError(f"DocFX 标量不是字符串：{value}")
        return decoded
    return value


def extract_syntax_content(block: str, uid: str) -> str:
    lines = block.splitlines()
    content_index = next(
        (
            index
            for index, line in enumerate(lines)
            if line.startswith("    content: ")
        ),
        None,
    )
    if content_index is None:
        raise RuntimeError(f"API metadata 缺少 C# syntax.content：{uid}")

    scalar = lines[content_index].split(":", maxsplit=1)[1].strip()
    if scalar not in {">", ">-", ">+", "|", "|-", "|+"}:
        return unquote_yaml_scalar(scalar)

    content_lines: list[str] = []
    for line in lines[content_index + 1 :]:
        if line and not line.startswith("      "):
            break
        content_lines.append(line[6:] if line.startswith("      ") else "")
    return "\n".join(content_lines)


def normalize_signature(signature: str) -> str:
    signature = SCRIPT_PATH_ATTRIBUTE_PATTERN.sub("", signature)
    return " ".join(signature.split())


def collect_api_items(api_root: Path) -> tuple[ApiItem, ...]:
    api_root = api_root.expanduser().resolve()
    if not api_root.is_dir():
        raise RuntimeError(
            f"API metadata 目录不存在：{api_root}；请先运行 python Docs/build_docs.py api-audit。"
        )

    items: dict[str, ApiItem] = {}
    for api_file in sorted(api_root.glob("*.yml")):
        text = api_file.read_text(encoding="utf-8")
        items_section = text.split("references:", maxsplit=1)[0]
        for raw_block in items_section.split("- uid: ")[1:]:
            uid = unquote_yaml_scalar(raw_block.splitlines()[0])
            if not uid.startswith("GoDo.") or GODOT_GENERATED_API_PATTERN.search(uid):
                continue
            kind_match = re.search(r"(?m)^  type:\s+(.+?)\s*$", raw_block)
            if kind_match is None:
                continue
            kind = unquote_yaml_scalar(kind_match.group(1))
            if kind == "Namespace":
                continue
            signature = normalize_signature(extract_syntax_content(raw_block, uid))
            if not signature:
                raise RuntimeError(f"API metadata 的 C# 声明为空：{uid}")
            item = ApiItem(uid, kind, signature)
            previous = items.get(uid)
            if previous is not None and previous != item:
                raise RuntimeError(f"API metadata 包含冲突的重复 UID：{uid}")
            items[uid] = item

    if not items:
        raise RuntimeError(f"API metadata 中没有 GoDo public API：{api_root}")
    return tuple(items[uid] for uid in sorted(items))


def read_framework_version() -> str:
    config = configparser.ConfigParser()
    if not config.read(PLUGIN_CONFIG, encoding="utf-8"):
        raise RuntimeError(f"无法读取插件配置：{PLUGIN_CONFIG}")
    version = config.get("plugin", "version", fallback="").strip().strip('"')
    if not version:
        raise RuntimeError("plugin.cfg 缺少框架版本。")
    return version


def write_baseline(path: Path, items: Sequence[ApiItem], framework_version: str) -> None:
    ordered_items = sorted(items, key=lambda item: item.uid)
    payload = {
        "schema_version": BASELINE_SCHEMA_VERSION,
        "framework_version": framework_version,
        "items": [
            {"uid": item.uid, "kind": item.kind, "signature": item.signature}
            for item in ordered_items
        ],
    }
    path = path.expanduser().resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def read_baseline(path: Path) -> tuple[str, tuple[ApiItem, ...]]:
    path = path.expanduser().resolve()
    if not path.is_file():
        raise RuntimeError(
            f"API 兼容基线不存在：{path}；只有确认当前 API 后才能执行 update。"
        )
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise RuntimeError(f"无法读取 API 兼容基线：{path}") from error
    if not isinstance(payload, dict) or payload.get("schema_version") != BASELINE_SCHEMA_VERSION:
        raise RuntimeError(f"API 兼容基线 Schema 版本不受支持：{path}")
    framework_version = payload.get("framework_version")
    raw_items = payload.get("items")
    if not isinstance(framework_version, str) or not framework_version:
        raise RuntimeError("API 兼容基线缺少 framework_version。")
    if not isinstance(raw_items, list) or not raw_items:
        raise RuntimeError("API 兼容基线没有 API 项。")

    items: list[ApiItem] = []
    seen: set[str] = set()
    for raw_item in raw_items:
        if not isinstance(raw_item, dict):
            raise RuntimeError("API 兼容基线包含无效 API 项。")
        uid = raw_item.get("uid")
        kind = raw_item.get("kind")
        signature = raw_item.get("signature")
        if not all(isinstance(value, str) and value for value in (uid, kind, signature)):
            raise RuntimeError("API 兼容基线包含字段不完整的 API 项。")
        if uid in seen:
            raise RuntimeError(f"API 兼容基线包含重复 UID：{uid}")
        seen.add(uid)
        items.append(ApiItem(uid, kind, signature))
    return framework_version, tuple(items)


def compare_api(
    baseline_items: Sequence[ApiItem],
    current_items: Sequence[ApiItem],
) -> CompatibilityReport:
    baseline = {item.uid: item for item in baseline_items}
    current = {item.uid: item for item in current_items}
    removed = tuple(baseline[uid] for uid in sorted(baseline.keys() - current.keys()))
    added = tuple(current[uid] for uid in sorted(current.keys() - baseline.keys()))
    changed = tuple(
        ChangedApiItem(uid, baseline[uid], current[uid])
        for uid in sorted(baseline.keys() & current.keys())
        if baseline[uid] != current[uid]
    )
    return CompatibilityReport(removed, changed, added)


def write_report(
    path: Path,
    baseline_version: str,
    baseline_count: int,
    current_count: int,
    report: CompatibilityReport,
) -> None:
    payload = {
        "compatible": report.is_compatible,
        "baseline_version": baseline_version,
        "baseline_count": baseline_count,
        "current_count": current_count,
        "removed": [item.uid for item in report.removed],
        "changed": [
            {
                "uid": item.uid,
                "baseline": item.baseline.signature,
                "current": item.current.signature,
            }
            for item in report.changed
        ],
        "added": [item.uid for item in report.added],
    }
    path = path.expanduser().resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def print_breaking_changes(report: CompatibilityReport) -> None:
    for item in report.removed[:50]:
        print(f"[API COMPAT] REMOVED {item.uid}: {item.signature}", file=sys.stderr)
    for item in report.changed[:50]:
        print(f"[API COMPAT] CHANGED {item.uid}", file=sys.stderr)
        print(f"  baseline: {item.baseline.signature}", file=sys.stderr)
        print(f"  current:  {item.current.signature}", file=sys.stderr)
    hidden = max(0, len(report.removed) - 50) + max(0, len(report.changed) - 50)
    if hidden > 0:
        print(f"[API COMPAT] 另有 {hidden} 项破坏性变化未展开。", file=sys.stderr)


def main(arguments: Sequence[str] | None = None) -> int:
    configure_console_encoding()
    options = parse_arguments(arguments)
    current_items = collect_api_items(options.api_root)
    if options.command == "update":
        version = read_framework_version()
        write_baseline(options.baseline, current_items, version)
        print(
            f"[API COMPAT] BASELINE UPDATED ({len(current_items)} items, version {version}): "
            f"{options.baseline.expanduser().resolve()}"
        )
        return 0

    baseline_version, baseline_items = read_baseline(options.baseline)
    report = compare_api(baseline_items, current_items)
    write_report(
        options.report,
        baseline_version,
        len(baseline_items),
        len(current_items),
        report,
    )
    if not report.is_compatible:
        print_breaking_changes(report)
        print(
            f"[API COMPAT] FAIL ({len(report.removed)} removed, "
            f"{len(report.changed)} changed, {len(report.added)} added)",
            file=sys.stderr,
        )
        return 1
    print(
        f"[API COMPAT] PASS ({len(baseline_items)} baseline, "
        f"{len(report.added)} added, version {baseline_version})"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[API COMPAT] ERROR {error}", file=sys.stderr)
        raise SystemExit(1)
