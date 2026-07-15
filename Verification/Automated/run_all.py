#!/usr/bin/env python3
"""Build the project and run all permanent Headless regression scenes."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SOLUTION_PATH = REPOSITORY_ROOT / "GoDoFramework.sln"
REGRESSION_SCENES = (
    "EventChannelRegression.tscn",
    "ErrorHubRegression.tscn",
    "LogHubRegression.tscn",
    "ServicesRegression.tscn",
    "ResourceKeyRegression.tscn",
    "ResourceRegistryRegression.tscn",
    "ResourceHubRegression.tscn",
    "NodePoolRegression.tscn",
    "SaveServiceRegression.tscn",
    "ConfigRegression.tscn",
    "ProcedureRegression.tscn",
    "CameraServiceRegression.tscn",
    "InputServiceRegression.tscn",
    "InputRuntimeRegression.tscn",
    "GuideInputBackendRegression.tscn",
    "Demo3DInputProfileRegression.tscn",
    "PhantomCameraRigRegression.tscn",
)


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="编译项目并依次运行 GoDoFramework Headless 自动回归。"
    )
    parser.add_argument(
        "--godot",
        type=Path,
        help="Godot 4.7 Mono Console 可执行文件；也可设置 GODOT_PATH。",
    )
    parser.add_argument(
        "--skip-build",
        action="store_true",
        help="跳过 dotnet build；仅在已经完成编译时使用。",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=60,
        help="每个场景的超时秒数，默认 60。",
    )
    return parser.parse_args()


def resolve_godot_path(argument: Path | None) -> Path:
    candidates: list[str | Path | None] = [
        argument,
        os.environ.get("GODOT_PATH"),
        shutil.which("godot"),
        shutil.which("godot4"),
        shutil.which("Godot_v4.7-stable_mono_win64_console.exe"),
    ]
    for candidate in candidates:
        if candidate is None:
            continue
        path = Path(candidate).expanduser().resolve()
        if path.is_file():
            return path

    raise RuntimeError(
        "未找到 Godot 4.7 Mono Console；请使用 --godot <exe路径> 或设置 GODOT_PATH。"
    )


def build_project() -> bool:
    print("[BUILD] dotnet build GoDoFramework.sln")
    result = subprocess.run(
        ["dotnet", "build", str(SOLUTION_PATH)],
        cwd=REPOSITORY_ROOT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode == 0:
        print("[BUILD] PASS")
        return True

    print(f"[BUILD] FAIL (exit={result.returncode})", file=sys.stderr)
    return False


def run_scene(godot_path: Path, scene_name: str, timeout: int) -> tuple[bool, str]:
    scene_path = f"res://Verification/Automated/{scene_name}"
    try:
        result = subprocess.run(
            [str(godot_path), "--headless", "--path", str(REPOSITORY_ROOT), scene_path],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
        )
    except subprocess.TimeoutExpired as exception:
        output = (exception.stdout or "") + (exception.stderr or "")
        return False, output + f"\n超时：{timeout} 秒"

    output = result.stdout + result.stderr
    return result.returncode == 0, output


def find_summary(output: str) -> str:
    summaries = [
        line.strip()
        for line in output.splitlines()
        if "Regression] PASS (" in line
    ]
    return summaries[-1] if summaries else "未找到 PASS 汇总行"


def main() -> int:
    configure_console_encoding()
    arguments = parse_arguments()
    if arguments.timeout <= 0:
        raise RuntimeError("--timeout 必须大于 0。")

    godot_path = resolve_godot_path(arguments.godot)
    print(f"[GODOT] {godot_path}")

    if not arguments.skip_build and not build_project():
        return 1

    failures: list[str] = []
    for scene_name in REGRESSION_SCENES:
        print(f"[RUN] {scene_name}")
        passed, output = run_scene(godot_path, scene_name, arguments.timeout)
        if passed:
            print(f"[PASS] {scene_name}: {find_summary(output)}")
            continue

        failures.append(scene_name)
        print(f"[FAIL] {scene_name}", file=sys.stderr)
        print(output, file=sys.stderr)

    passed_count = len(REGRESSION_SCENES) - len(failures)
    print(f"[SUMMARY] {passed_count}/{len(REGRESSION_SCENES)} scenes passed")
    if failures:
        print(f"[SUMMARY] failed: {', '.join(failures)}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"自动回归失败：{error}", file=sys.stderr)
        raise SystemExit(1)
