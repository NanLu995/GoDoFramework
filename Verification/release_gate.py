#!/usr/bin/env python3
"""Run the GoDoFramework release verification stages from one entry point."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import time
from collections.abc import Callable, Sequence
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
PROJECT_PATH = REPOSITORY_ROOT / "GoDoFramework.csproj"
STAGE_ORDER = (
    "release-tests",
    "api-compatibility-tests",
    "datatable-generated",
    "debug-build",
    "regressions",
    "datatable-export-plugin",
    "release-build",
    "docs",
    "api-compatibility",
    "packages",
    "package-lifecycle",
    "datatable-export-release",
)
GODOT_STAGES = {
    "regressions",
    "datatable-export-plugin",
    "package-lifecycle",
    "datatable-export-release",
}
StageRunner = Callable[..., subprocess.CompletedProcess[object]]


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
        sys.stderr.reconfigure(encoding="utf-8", line_buffering=True)


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="按固定顺序执行 GoDoFramework 发布前验收门禁。"
    )
    parser.add_argument(
        "--stage",
        action="append",
        choices=STAGE_ORDER,
        help="只执行指定阶段；可重复传入。默认执行全部阶段。",
    )
    parser.add_argument(
        "--list-stages",
        action="store_true",
        help="列出阶段顺序后退出。",
    )
    parser.add_argument(
        "--godot",
        type=Path,
        help="Godot Mono Console 可执行文件；也可设置 GODOT_PATH。",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=60,
        help="传给每个 Headless 回归场景的超时秒数，默认 60。",
    )
    return parser.parse_args(arguments)


def select_stages(requested: Sequence[str] | None) -> tuple[str, ...]:
    if not requested:
        return STAGE_ORDER
    requested_set = set(requested)
    return tuple(stage for stage in STAGE_ORDER if stage in requested_set)


def resolve_godot_path(argument: Path | None) -> Path:
    candidates: list[str | Path | None] = [
        argument,
        os.environ.get("GODOT_PATH"),
        shutil.which("godot"),
        shutil.which("godot4"),
        shutil.which("Godot_v4.7.2-stable_mono_win64_console.exe"),
    ]
    for candidate in candidates:
        if candidate is None:
            continue
        path = Path(candidate).expanduser().resolve()
        if path.is_file():
            return path
    raise RuntimeError(
        "发布门禁未找到 Godot Mono Console；请使用 --godot <exe路径> 或设置 GODOT_PATH。"
    )


def create_stage_commands(
    godot_path: Path | None,
    timeout: int,
    export_output_root: Path,
) -> dict[str, list[str]]:
    python = sys.executable
    commands = {
        "release-tests": [
            python,
            "-m",
            "unittest",
            "discover",
            "-s",
            str(REPOSITORY_ROOT / "Verification" / "Package"),
            "-p",
            "test_*.py",
            "--quiet",
        ],
        "api-compatibility-tests": [
            python,
            str(
                REPOSITORY_ROOT
                / "Verification"
                / "ApiCompatibility"
                / "test_api_compatibility.py"
            ),
            "--quiet",
        ],
        "datatable-generated": [
            python,
            str(
                REPOSITORY_ROOT
                / "addons"
                / "godo_framework"
                / "Tools"
                / "DataTable"
                / "godo_datatable.py"
            ),
            "verify-generated",
            "--schema",
            str(REPOSITORY_ROOT / "DataTables" / "Base" / ".datatable.schema.json"),
        ],
        "debug-build": [
            "dotnet",
            "build",
            str(PROJECT_PATH),
            "--configuration",
            "Debug",
        ],
        "release-build": [
            "dotnet",
            "build",
            str(PROJECT_PATH),
            "--configuration",
            "Release",
        ],
        "docs": [
            python,
            str(REPOSITORY_ROOT / "Docs" / "build_docs.py"),
            "check",
        ],
        "api-compatibility": [
            python,
            str(
                REPOSITORY_ROOT
                / "Verification"
                / "ApiCompatibility"
                / "api_compatibility.py"
            ),
            "check",
        ],
        "packages": [
            python,
            str(REPOSITORY_ROOT / "release" / "release.py"),
        ],
    }
    if godot_path is not None:
        commands.update(
            {
                "regressions": [
                    python,
                    str(REPOSITORY_ROOT / "Verification" / "Automated" / "run_all.py"),
                    "--godot",
                    str(godot_path),
                    "--skip-build",
                    "--suite",
                    "all",
                    "--timeout",
                    str(timeout),
                ],
                "datatable-export-plugin": [
                    python,
                    str(
                        REPOSITORY_ROOT
                        / "Verification"
                        / "Experimental"
                        / "DataTable"
                        / "verify_export_plugin.py"
                    ),
                    "--godot",
                    str(godot_path),
                ],
                "datatable-export-release": [
                    python,
                    str(
                        REPOSITORY_ROOT
                        / "Verification"
                        / "Experimental"
                        / "DataTable"
                        / "verify_export_release.py"
                    ),
                    "--godot",
                    str(godot_path),
                    "--output-root",
                    str(export_output_root),
                ],
                "package-lifecycle": [
                    python,
                    str(
                        REPOSITORY_ROOT
                        / "Verification"
                        / "Package"
                        / "verify_core_package_lifecycle.py"
                    ),
                    "--godot",
                    str(godot_path),
                    "--timeout",
                    str(timeout),
                ],
            }
        )
    return commands


def run_stages(
    stages: Sequence[str],
    commands: dict[str, list[str]],
    runner: StageRunner = subprocess.run,
) -> int:
    passed: list[tuple[str, float]] = []
    for stage in stages:
        command = commands.get(stage)
        if command is None:
            raise RuntimeError(f"阶段 {stage} 缺少可执行命令。")

        print(f"\n[GATE] RUN {stage}")
        print(f"[GATE] COMMAND {subprocess.list2cmdline(command)}")
        started = time.perf_counter()
        child_environment = os.environ.copy()
        child_environment["PYTHONUTF8"] = "1"
        result = runner(
            command,
            cwd=REPOSITORY_ROOT,
            check=False,
            env=child_environment,
        )
        duration = time.perf_counter() - started
        if result.returncode != 0:
            print(f"[GATE] FAIL {stage} ({duration:.1f} s)", file=sys.stderr)
            print(f"[GATE] SUMMARY {len(passed)}/{len(stages)} stages passed", file=sys.stderr)
            return result.returncode or 1

        passed.append((stage, duration))
        print(f"[GATE] PASS {stage} ({duration:.1f} s)")

    print(f"\n[GATE] SUMMARY {len(passed)}/{len(stages)} stages passed")
    for stage, duration in passed:
        print(f"[GATE]   {stage}: {duration:.1f} s")
    return 0


def main(arguments: Sequence[str] | None = None) -> int:
    configure_console_encoding()
    options = parse_arguments(arguments)
    if options.list_stages:
        for stage in STAGE_ORDER:
            print(stage)
        return 0
    if options.timeout <= 0:
        raise RuntimeError("--timeout 必须大于 0。")

    stages = select_stages(options.stage)
    godot_path = None
    if any(stage in GODOT_STAGES for stage in stages):
        godot_path = resolve_godot_path(options.godot)
        print(f"[GATE] GODOT {godot_path}")

    with tempfile.TemporaryDirectory(prefix="godo-release-gate-") as temporary_directory:
        export_output_root = Path(temporary_directory) / "datatable-export-release"
        commands = create_stage_commands(godot_path, options.timeout, export_output_root)
        return run_stages(stages, commands)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[GATE] ERROR {error}", file=sys.stderr)
        raise SystemExit(1)
