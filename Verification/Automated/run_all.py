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
CORE_PACKAGE_SCRIPT = REPOSITORY_ROOT / "Verification" / "Package" / "verify_core_package.py"
WORKBENCH_REGRESSION_SCENES = (
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
)
SUITE_SCENES = {
    "core": WORKBENCH_REGRESSION_SCENES,
    "guide": ("GuideInputBackendRegression.tscn",),
    "phantom": ("PhantomCameraRigRegression.tscn",),
    "demo": ("Demo3DInputProfileRegression.tscn",),
    "all": WORKBENCH_REGRESSION_SCENES + (
        "GuideInputBackendRegression.tscn",
        "Demo3DInputProfileRegression.tscn",
        "PhantomCameraRigRegression.tscn",
    ),
}
OPTIONAL_DEPENDENCIES = {
    "GUIDE / G.U.I.D.E-CSharp": (
        "addons/guideCS/plugin.cfg",
        "addons/guideCS/guide/plugin.cfg",
    ),
    "Phantom Camera": ("addons/phantom_camera/plugin.cfg",),
}
SUITE_DEPENDENCIES = {
    "guide": ("GUIDE / G.U.I.D.E-CSharp",),
    "phantom": ("Phantom Camera",),
    "demo": ("GUIDE / G.U.I.D.E-CSharp", "Phantom Camera"),
}


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
        help="跳过集成工作区的 dotnet build；仅在已经完成编译时使用。",
    )
    parser.add_argument(
        "--suite",
        choices=("core", "guide", "phantom", "demo", "all"),
        default="all",
        help="验证分组；默认 all。",
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


def find_missing_optional_dependencies(dependency_names: tuple[str, ...] | None = None) -> list[str]:
    missing: list[str] = []
    for name, paths in OPTIONAL_DEPENDENCIES.items():
        if dependency_names is not None and name not in dependency_names:
            continue
        absent_paths = [path for path in paths if not (REPOSITORY_ROOT / path).is_file()]
        if absent_paths:
            missing.append(f"{name}（缺少：{', '.join(absent_paths)}）")
    return missing


def run_core_package(godot_path: Path, timeout: int) -> bool:
    print("[CORE] 验证干净核心包")
    result = subprocess.run(
        [
            sys.executable,
            str(CORE_PACKAGE_SCRIPT),
            "--godot",
            str(godot_path),
            "--timeout",
            str(timeout),
        ],
        cwd=REPOSITORY_ROOT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode == 0:
        print("[CORE] PASS")
        return True

    print(f"[CORE] FAIL (exit={result.returncode})", file=sys.stderr)
    return False


def run_demo_scene(godot_path: Path, timeout: int) -> tuple[bool, str]:
    try:
        result = subprocess.run(
            [
                str(godot_path),
                "--headless",
                "--path",
                str(REPOSITORY_ROOT),
                "--quit-after",
                "5",
                "res://Templates/Demo3D/Boot/Boot.tscn",
            ],
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

    return result.returncode == 0, result.stdout + result.stderr


def run_workbench_suite(
    suite: str,
    godot_path: Path,
    skip_build: bool,
    timeout: int,
) -> bool:
    if not skip_build and not build_project():
        return False

    failures: list[str] = []
    for scene_name in SUITE_SCENES[suite]:
        print(f"[RUN] {scene_name}")
        passed, output = run_scene(godot_path, scene_name, timeout)
        if passed:
            print(f"[PASS] {scene_name}: {find_summary(output)}")
            continue

        failures.append(scene_name)
        print(f"[FAIL] {scene_name}", file=sys.stderr)
        print(output, file=sys.stderr)

    if suite in ("demo", "all"):
        print("[RUN] Demo3D Boot")
        passed, output = run_demo_scene(godot_path, timeout)
        if passed:
            print("[PASS] Demo3D Boot")
        else:
            failures.append("Demo3D Boot")
            print("[FAIL] Demo3D Boot", file=sys.stderr)
            print(output, file=sys.stderr)

    passed_count = len(SUITE_SCENES[suite]) + (1 if suite in ("demo", "all") else 0) - len(failures)
    total_count = len(SUITE_SCENES[suite]) + (1 if suite in ("demo", "all") else 0)
    print(f"[SUMMARY] {passed_count}/{total_count} workbench checks passed")
    if failures:
        print(f"[SUMMARY] failed: {', '.join(failures)}", file=sys.stderr)
        return False
    return True


def run_all_suites(godot_path: Path, skip_build: bool, timeout: int) -> bool:
    if not skip_build and not build_project():
        return False

    success = run_workbench_suite("core", godot_path, True, timeout)
    for suite in ("guide", "phantom", "demo"):
        missing_dependencies = find_missing_optional_dependencies(SUITE_DEPENDENCIES[suite])
        if missing_dependencies:
            print(f"[SKIP] {suite} 集成验证未配置：")
            for dependency in missing_dependencies:
                print(f"[SKIP] {dependency}")
            continue

        success = run_workbench_suite(suite, godot_path, True, timeout) and success
    return success


def main() -> int:
    configure_console_encoding()
    arguments = parse_arguments()
    if arguments.timeout <= 0:
        raise RuntimeError("--timeout 必须大于 0。")

    godot_path = resolve_godot_path(arguments.godot)
    print(f"[GODOT] {godot_path}")

    if arguments.suite == "core":
        return 0 if run_core_package(godot_path, arguments.timeout) else 1

    if arguments.suite == "all":
        if not run_core_package(godot_path, arguments.timeout):
            return 1

        return 0 if run_all_suites(godot_path, arguments.skip_build, arguments.timeout) else 1

    missing_dependencies = find_missing_optional_dependencies(SUITE_DEPENDENCIES[arguments.suite])
    if missing_dependencies:
        details = "；".join(missing_dependencies)
        raise RuntimeError(
            f"{arguments.suite} 集成验证缺少所需依赖：{details}。"
        )

    return 0 if run_workbench_suite(
        arguments.suite,
        godot_path,
        arguments.skip_build,
        arguments.timeout,
    ) else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"自动回归失败：{error}", file=sys.stderr)
        raise SystemExit(1)
