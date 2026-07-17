#!/usr/bin/env python3
"""Verify generated DataTables before starting a Godot command-line export."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
COMPILER_PATH = SCRIPT_DIR / "godo_datatable.py"
EXPORT_ARGUMENTS = {
    "release": "--export-release",
    "debug": "--export-debug",
    "pack": "--export-pack",
}


def run(command: list[str], *, cwd: Path) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if result.stdout:
        print(result.stdout, end="" if result.stdout.endswith("\n") else "\n")
    if result.stderr:
        print(result.stderr, end="" if result.stderr.endswith("\n") else "\n", file=sys.stderr)
    return result


def resolve_executable(value: str) -> str:
    candidate = Path(value).expanduser()
    if candidate.is_file():
        return str(candidate.resolve())
    resolved = shutil.which(value)
    if resolved:
        return resolved
    raise RuntimeError(f"Godot 可执行文件不存在或不在 PATH 中：{value}")


def discover_build_configs(project: Path, explicit: list[Path]) -> list[Path]:
    if explicit:
        configs = [path.expanduser().resolve() for path in explicit]
    else:
        configs = sorted((project / "DataTables").glob("*.build.json"))
    if not configs:
        raise RuntimeError(f"未发现 DataTable Build Config：{project / 'DataTables' / '*.build.json'}")
    missing = [path for path in configs if not path.is_file()]
    if missing:
        raise RuntimeError("DataTable Build Config 不存在：\n- " + "\n- ".join(map(str, missing)))
    return configs


def verify_generated(project: Path, configs: list[Path]) -> bool:
    for config in configs:
        print(f"[GoDo DataTable Export] 校验：{config}")
        result = run(
            [
                sys.executable,
                "-X",
                "utf8",
                str(COMPILER_PATH),
                "verify-generated",
                "--build-config",
                str(config),
            ],
            cwd=project,
        )
        if result.returncode != 0:
            print("[GoDo DataTable Export] 校验失败，未启动 Godot 导出。", file=sys.stderr)
            return False
    return True


def main() -> int:
    parser = argparse.ArgumentParser(
        description="校验全部 DataTable 生成产物，成功后再执行 Godot 命令行导出。"
    )
    parser.add_argument("--godot", required=True, help="Godot 控制台可执行文件或 PATH 中的命令名。")
    parser.add_argument("--project", type=Path, required=True, help="包含 project.godot 的项目根目录。")
    parser.add_argument("--preset", required=True, help="Godot export_presets.cfg 中的 preset 名称。")
    parser.add_argument("--output", type=Path, required=True, help="Godot 导出目标路径。")
    parser.add_argument(
        "--mode",
        choices=tuple(EXPORT_ARGUMENTS),
        default="release",
        help="release、debug 或仅导出 PCK；默认为 release。",
    )
    parser.add_argument(
        "--build-config",
        type=Path,
        action="append",
        default=[],
        help="显式 Build Config，可重复；省略时扫描项目 DataTables/*.build.json。",
    )
    arguments = parser.parse_args()

    if sys.version_info < (3, 10):
        raise RuntimeError("DataTable 导出门禁需要 Python 3.10+。")
    project = arguments.project.expanduser().resolve()
    if not (project / "project.godot").is_file():
        raise RuntimeError(f"项目根目录缺少 project.godot：{project}")
    godot = resolve_executable(arguments.godot)
    configs = discover_build_configs(project, arguments.build_config)
    if not verify_generated(project, configs):
        return 1

    output = arguments.output.expanduser().resolve()
    print(f"[GoDo DataTable Export] 校验通过，开始 {arguments.mode} 导出：{output}")
    result = run(
        [
            godot,
            "--headless",
            "--path",
            str(project),
            EXPORT_ARGUMENTS[arguments.mode],
            arguments.preset,
            str(output),
        ],
        cwd=project,
    )
    return result.returncode


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[GoDo DataTable Export] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
