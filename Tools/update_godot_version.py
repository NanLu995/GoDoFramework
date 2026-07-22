#!/usr/bin/env python3
"""Update GoDoFramework's Godot patch version from one command."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
PROJECT_PATH = REPOSITORY_ROOT / "GoDoFramework.csproj"
CONTROLLER_PATH = (
    REPOSITORY_ROOT
    / "addons"
    / "godo_framework"
    / "Editor"
    / "godo_runtime_setup_controller.gd"
)
COVERAGE_PATH = REPOSITORY_ROOT / "Docs" / "coverage.json"
MANUAL_ROOT = REPOSITORY_ROOT / "Docs" / "Manual"
VERSION_PATTERN = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+$")
SDK_PATTERN = re.compile(r'Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+)')
MIN_VERSION_PATTERN = re.compile(
    r"const MIN_GODOT_VERSION := Vector3i\([0-9]+, [0-9]+, [0-9]+\)"
)
VERSIONED_EXECUTABLE_PATTERN = re.compile(r"Godot_v([0-9]+\.[0-9]+(?:\.[0-9]+)?)")
TEXT_EXTENSIONS = {
    ".cfg",
    ".cs",
    ".csproj",
    ".gd",
    ".godot",
    ".json",
    ".md",
    ".ps1",
    ".py",
    ".tscn",
    ".tres",
    ".yaml",
    ".yml",
}
UPDATE_ROOTS = (
    ".github",
    ".vscode",
    "AI",
    "Docs",
    "Verification",
    "addons/godo_framework",
)
ROOT_FILES = ("AGENTS.md", "GoDoFramework.csproj", "README.md")


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="统一升级 GoDoFramework 的 Godot SDK、最低版本、工具路径和文档哈希。"
    )
    parser.add_argument("version", nargs="?", help="目标版本，例如 4.7.2。")
    parser.add_argument(
        "--godot",
        type=Path,
        help="目标版本的 Godot Mono 可执行文件；同时更新本机 VS Code 路径。",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="只检查当前版本引用、文档和派生配置，不修改文件。",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="显示会修改的文件，但不写入。",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="升级后额外执行 dotnet build 与完整 Godot 自动回归。",
    )
    arguments = parser.parse_args()
    if arguments.check and (arguments.version or arguments.godot or arguments.dry_run or arguments.verify):
        parser.error("--check 不能与版本、--godot、--dry-run 或 --verify 同时使用。")
    if not arguments.check and not arguments.version:
        parser.error("升级时必须提供目标版本；只检查请使用 --check。")
    if arguments.version and not VERSION_PATTERN.fullmatch(arguments.version):
        parser.error("版本必须使用 major.minor.patch 格式，例如 4.7.2。")
    if arguments.verify and arguments.godot is None:
        parser.error("--verify 需要同时提供 --godot。")
    return arguments


def read_text(path: Path, updates: dict[Path, str] | None = None) -> str:
    if updates is not None and path in updates:
        return updates[path]
    return path.read_text(encoding="utf-8")


def read_project_version(updates: dict[Path, str] | None = None) -> str:
    match = SDK_PATTERN.search(read_text(PROJECT_PATH, updates))
    if match is None:
        raise RuntimeError("GoDoFramework.csproj 未声明 Godot.NET.Sdk patch 版本。")
    return match.group(1)


def collect_update_files() -> list[Path]:
    result = [REPOSITORY_ROOT / name for name in ROOT_FILES]
    for root_name in UPDATE_ROOTS:
        root = REPOSITORY_ROOT / root_name
        if not root.exists():
            continue
        result.extend(
            path
            for path in root.rglob("*")
            if path.is_file() and path.suffix.lower() in TEXT_EXTENSIONS
        )
    return sorted(set(result))


def replace_current_version(
    old_version: str,
    new_version: str,
    updates: dict[Path, str],
) -> None:
    for path in collect_update_files():
        content = read_text(path, updates)
        changed = content.replace(old_version, new_version)
        if changed != content:
            updates[path] = changed

    controller = read_text(CONTROLLER_PATH, updates)
    major, minor, patch = (int(part) for part in new_version.split("."))
    replacement = f"const MIN_GODOT_VERSION := Vector3i({major}, {minor}, {patch})"
    controller, count = MIN_VERSION_PATTERN.subn(replacement, controller, count=1)
    if count != 1:
        raise RuntimeError("无法定位 MIN_GODOT_VERSION。")
    updates[CONTROLLER_PATH] = controller


def validate_godot_executable(path: Path, expected_version: str) -> Path:
    resolved = path.expanduser().resolve()
    if not resolved.is_file():
        raise RuntimeError(f"Godot 可执行文件不存在：{resolved}")
    result = subprocess.run(
        [str(resolved), "--version"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=20,
    )
    output = (result.stdout + result.stderr).strip()
    if result.returncode != 0 or not output.startswith(expected_version + "."):
        raise RuntimeError(
            f"Godot 版本不匹配：期望 {expected_version}，实际输出 {output or '<empty>'}。"
        )
    return resolved


def update_vscode_path(godot_path: Path, updates: dict[Path, str]) -> None:
    settings_path = REPOSITORY_ROOT / ".vscode" / "settings.json"
    if not settings_path.is_file():
        return
    editor_path = godot_path
    if editor_path.name.lower().endswith("_console.exe"):
        candidate = editor_path.with_name(editor_path.name[:-12] + ".exe")
        if candidate.is_file():
            editor_path = candidate
    settings = json.loads(read_text(settings_path, updates))
    current_path = str(settings.get("godotTools.editorPath.godot4", ""))
    if os.path.normcase(current_path) == os.path.normcase(str(editor_path)):
        return
    settings["godotTools.editorPath.godot4"] = str(editor_path)
    updates[settings_path] = json.dumps(settings, ensure_ascii=False, indent=4) + "\n"


def refresh_document_hashes(updates: dict[Path, str]) -> None:
    coverage = json.loads(read_text(COVERAGE_PATH, updates))
    for entry in coverage["entries"].values():
        contract_path = REPOSITORY_ROOT / entry["contract"]
        digest = hashlib.sha256(read_text(contract_path, updates).encode("utf-8")).hexdigest()
        entry["reviewed_contract_hash"] = f"sha256:{digest}"
    updates[COVERAGE_PATH] = json.dumps(coverage, ensure_ascii=False, indent=2) + "\n"

    for translation_path in sorted((MANUAL_ROOT / "en-us").rglob("*.md")):
        content = read_text(translation_path, updates)
        source_match = re.search(r"(?m)^translation_of:\s*(.+?)\s*$", content)
        hash_match = re.search(
            r"(?m)^translation_source_hash:\s*sha256:[0-9a-f]{64}\s*$", content
        )
        if source_match is None or hash_match is None:
            continue
        source_path = REPOSITORY_ROOT / source_match.group(1)
        digest = hashlib.sha256(read_text(source_path, updates).encode("utf-8")).hexdigest()
        replacement = f"translation_source_hash: sha256:{digest}"
        changed = content[: hash_match.start()] + replacement + content[hash_match.end() :]
        updates[translation_path] = changed


def atomic_write_updates(updates: dict[Path, str]) -> None:
    temporary_paths: list[tuple[Path, Path]] = []
    try:
        for path, content in updates.items():
            if path.is_file() and path.read_text(encoding="utf-8") == content:
                continue
            temporary = path.with_name(path.name + ".godo-version.tmp")
            with temporary.open("w", encoding="utf-8", newline="") as output:
                output.write(content)
            temporary_paths.append((path, temporary))
        for path, temporary in temporary_paths:
            os.replace(temporary, path)
    finally:
        for _, temporary in temporary_paths:
            if temporary.exists():
                temporary.unlink()


def check_consistency() -> str:
    version = read_project_version()
    major, minor, patch = version.split(".")
    expected_constant = f"const MIN_GODOT_VERSION := Vector3i({major}, {minor}, {patch})"
    if expected_constant not in CONTROLLER_PATH.read_text(encoding="utf-8"):
        raise RuntimeError("EditorPlugin 最低 Godot 版本与 csproj 不一致。")

    workflow = (REPOSITORY_ROOT / ".github" / "workflows" / "core-verification.yml").read_text(
        encoding="utf-8"
    )
    if "Godot.NET.Sdk" not in workflow or re.search(r"releases/download/[0-9]", workflow):
        raise RuntimeError("CI 未从 csproj 动态读取 Godot 版本。")

    for script_path in (
        REPOSITORY_ROOT / "Verification" / "Automated" / "run_all.py",
        REPOSITORY_ROOT / "Verification" / "Package" / "verify_core_package.py",
    ):
        if "read_godot_version()" not in script_path.read_text(encoding="utf-8"):
            raise RuntimeError(f"验证脚本未动态读取 Godot 版本：{script_path}")

    mismatches: list[str] = []
    executable_consumers = (
        REPOSITORY_ROOT / ".vscode" / "settings.json",
        REPOSITORY_ROOT / "Verification" / "Automated" / "run_all.py",
        REPOSITORY_ROOT / "Verification" / "Package" / "verify_core_package.py",
    )
    for path in executable_consumers:
        if not path.is_file():
            continue
        for match in VERSIONED_EXECUTABLE_PATTERN.finditer(path.read_text(encoding="utf-8")):
            if match.group(1) != version:
                mismatches.append(f"{path.relative_to(REPOSITORY_ROOT)} -> {match.group(1)}")
    if mismatches:
        raise RuntimeError("发现旧 Godot 可执行文件版本：\n- " + "\n- ".join(mismatches))
    return version


def run_command(command: list[str]) -> None:
    result = subprocess.run(command, cwd=REPOSITORY_ROOT)
    if result.returncode != 0:
        raise RuntimeError(f"命令失败（{result.returncode}）：{' '.join(command)}")


def main() -> int:
    configure_console_encoding()
    arguments = parse_arguments()
    if arguments.check:
        version = check_consistency()
        run_command([sys.executable, "Docs/build_docs.py", "lint"])
        print(f"[GodotVersion] CHECK PASS ({version})")
        return 0

    old_version = read_project_version()
    new_version: str = arguments.version
    if old_version.split(".")[:2] != new_version.split(".")[:2]:
        raise RuntimeError(
            "该工具只处理同一 major.minor 内的 patch 升级；主次版本升级还需要人工复核 "
            "project.godot、离线 API 文档和导出行为。"
        )
    godot_path = None
    if arguments.godot is not None:
        godot_path = validate_godot_executable(arguments.godot, new_version)

    updates: dict[Path, str] = {}
    replace_current_version(old_version, new_version, updates)
    if godot_path is not None:
        update_vscode_path(godot_path, updates)
    refresh_document_hashes(updates)
    changed_paths = [
        path for path, content in updates.items() if not path.is_file() or path.read_text(encoding="utf-8") != content
    ]
    for path in sorted(changed_paths):
        print(f"[UPDATE] {path.relative_to(REPOSITORY_ROOT)}")
    if arguments.dry_run:
        print(f"[GodotVersion] DRY RUN ({old_version} -> {new_version}, {len(changed_paths)} files)")
        return 0

    atomic_write_updates(updates)
    version = check_consistency()
    run_command([sys.executable, "Docs/build_docs.py", "lint"])
    if arguments.verify:
        run_command(["dotnet", "build", "GoDoFramework.csproj", "-c", "Debug"])
        run_command(
            [
                sys.executable,
                "Verification/Automated/run_all.py",
                "--suite",
                "all",
                "--skip-build",
                "--godot",
                str(godot_path),
                "--timeout",
                "60",
            ]
        )
    print(f"[GodotVersion] UPDATE PASS ({old_version} -> {version})")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as exception:
        print(f"[GodotVersion] FAIL: {exception}", file=sys.stderr)
        raise SystemExit(1)
