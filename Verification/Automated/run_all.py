#!/usr/bin/env python3
"""Build the project and run all permanent Headless regression scenes."""

from __future__ import annotations

import argparse
import os
import re
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
    "LocalizationRegression.tscn",
    "ConfigRegression.tscn",
    "ProcedureRegression.tscn",
    "SchedulerCoreRegression.tscn",
    "SchedulerRuntimeRegression.tscn",
    "CameraServiceRegression.tscn",
    "UiServiceRegression.tscn",
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
GUIDE_AUTOLOAD_SEQUENCE = (
    ("GUIDE", "res://addons/guideCS/guide/guide.gd"),
    ("GuideCs", "res://addons/guideCS/Guide.cs"),
    ("GoDoRuntime", "res://addons/godo_framework/Core/GoDoRuntime.tscn"),
)
GUIDE_EDITOR_PLUGINS = (
    "res://addons/guideCS/guide/plugin.cfg",
    "res://addons/guideCS/plugin.cfg",
)
PHANTOM_EDITOR_PLUGIN = "res://addons/phantom_camera/plugin.cfg"
SUPPORTED_PHANTOM_VERSION = "0.11"


def read_godot_version() -> str:
    project = (REPOSITORY_ROOT / "GoDoFramework.csproj").read_text(encoding="utf-8")
    match = re.search(r'Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+)', project)
    if match is None:
        raise RuntimeError("GoDoFramework.csproj 未声明 Godot.NET.Sdk 版本。")
    return match.group(1)


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")


def parse_arguments() -> argparse.Namespace:
    godot_version = read_godot_version()
    parser = argparse.ArgumentParser(
        description="编译项目并依次运行 GoDoFramework Headless 自动回归。"
    )
    parser.add_argument(
        "--godot",
        type=Path,
        help=f"Godot {godot_version} Mono Console 可执行文件；也可设置 GODOT_PATH。",
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
    godot_version = read_godot_version()
    candidates: list[str | Path | None] = [
        argument,
        os.environ.get("GODOT_PATH"),
        shutil.which("godot"),
        shutil.which("godot4"),
        shutil.which(f"Godot_v{godot_version}-stable_mono_win64_console.exe"),
    ]
    for candidate in candidates:
        if candidate is None:
            continue
        path = Path(candidate).expanduser().resolve()
        if path.is_file():
            return path

    raise RuntimeError(
        f"未找到 Godot {godot_version} Mono Console；请使用 --godot <exe路径> 或设置 GODOT_PATH。"
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
            continue
        if name == "GUIDE / G.U.I.D.E-CSharp":
            autoload_issue = find_guide_autoload_issue()
            if autoload_issue:
                missing.append(f"{name}（{autoload_issue}）")
                continue
            disabled_plugin = next(
                (path for path in GUIDE_EDITOR_PLUGINS if not is_editor_plugin_enabled(path)),
                None,
            )
            if disabled_plugin:
                missing.append(f"{name}（编辑器插件未启用：{disabled_plugin}）")
        if name == "Phantom Camera":
            if not is_editor_plugin_enabled(PHANTOM_EDITOR_PLUGIN):
                missing.append(f"{name}（编辑器插件未启用）")
                continue
            version = read_plugin_version(REPOSITORY_ROOT / "addons/phantom_camera/plugin.cfg")
            if version != SUPPORTED_PHANTOM_VERSION:
                missing.append(
                    f"{name}（版本 {version or '未知'}，当前仅验证 {SUPPORTED_PHANTOM_VERSION}）"
                )
    return missing


def is_editor_plugin_enabled(plugin_path: str) -> bool:
    project_config = REPOSITORY_ROOT / "project.godot"
    in_editor_plugins_section = False
    for raw_line in project_config.read_text(encoding="utf-8-sig").splitlines():
        line = raw_line.strip()
        if line.startswith("[") and line.endswith("]"):
            in_editor_plugins_section = line == "[editor_plugins]"
            continue
        if in_editor_plugins_section and line.startswith("enabled="):
            return f'"{plugin_path}"' in line
    return False


def read_plugin_version(plugin_config: Path) -> str:
    in_plugin_section = False
    for raw_line in plugin_config.read_text(encoding="utf-8-sig").splitlines():
        line = raw_line.strip()
        if line.startswith("[") and line.endswith("]"):
            in_plugin_section = line == "[plugin]"
            continue
        if in_plugin_section and line.startswith("version="):
            return line.split("=", 1)[1].strip().strip('"')
    return ""


def find_guide_autoload_issue() -> str | None:
    project_config = REPOSITORY_ROOT / "project.godot"
    autoloads: list[tuple[str, str]] = []
    in_autoload_section = False
    for raw_line in project_config.read_text(encoding="utf-8-sig").splitlines():
        line = raw_line.strip()
        if line.startswith("[") and line.endswith("]"):
            in_autoload_section = line == "[autoload]"
            continue
        if not in_autoload_section or "=" not in line:
            continue
        name, value = line.split("=", 1)
        path = value.strip().strip('"').removeprefix("*")
        autoloads.append((name.strip(), path))

    positions: list[int] = []
    for expected_name, expected_path in GUIDE_AUTOLOAD_SEQUENCE:
        for index, (actual_name, actual_path) in enumerate(autoloads):
            if actual_name != expected_name:
                continue
            if not autoload_locator_matches(actual_path, expected_path):
                return f"Autoload {expected_name} 指向 {actual_path}"
            positions.append(index)
            break
        else:
            return f"尚未安装 Autoload {expected_name}"

    if positions != sorted(positions) or len(set(positions)) != len(positions):
        return "Autoload 顺序必须为 GUIDE → GuideCs → GoDoRuntime"
    return None


def autoload_locator_matches(actual: str, expected_path: str) -> bool:
    if actual == expected_path:
        return True
    if not actual.startswith("uid://"):
        return False

    uid_sidecar = REPOSITORY_ROOT / f"{expected_path.removeprefix('res://')}.uid"
    if not uid_sidecar.is_file():
        return False
    return actual == uid_sidecar.read_text(encoding="utf-8-sig").strip()


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


def run_editor_extension_check(godot_path: Path, timeout: int) -> bool:
    print("[EDITOR] 验证 GoDo 编辑器扩展")
    try:
        result = subprocess.run(
            [
                str(godot_path),
                "--headless",
                "--editor",
                "--path",
                str(REPOSITORY_ROOT),
                "--script",
                "res://Verification/Automated/EditorExtensionUiRegression.gd",
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
        print(output + f"\n编辑器扩展验证超时：{timeout} 秒", file=sys.stderr)
        return False

    output = result.stdout + result.stderr
    has_script_error = any(
        marker in output
        for marker in (
            "SCRIPT ERROR:",
            "[GoDo Editor Extension]",
            "Unrecognized UID:",
            "Internal script error!",
            "Invalid access to property or key",
        )
    )
    has_pass_summary = "[EditorExtensionUiRegression] PASS (4/4)" in output
    if result.returncode == 0 and not has_script_error and has_pass_summary:
        try:
            transport_result = subprocess.run(
                [
                    str(godot_path),
                    "--headless",
                    "--editor",
                    "--path",
                    str(REPOSITORY_ROOT),
                    "--script",
                    "res://Verification/Automated/DataTableEditorTransportRegression.gd",
                ],
                cwd=REPOSITORY_ROOT,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout,
            )
        except subprocess.TimeoutExpired as exception:
            transport_output = (exception.stdout or "") + (exception.stderr or "")
            print(transport_output + f"\nDataTable 诊断传输验证超时：{timeout} 秒", file=sys.stderr)
            return False
        transport_output = transport_result.stdout + transport_result.stderr
        if (
            transport_result.returncode == 0
            and "[DataTableEditorTransportRegression] PASS" in transport_output
            and "SCRIPT ERROR:" not in transport_output
        ):
            try:
                schema_save_result = subprocess.run(
                    [
                        str(godot_path),
                        "--headless",
                        "--editor",
                        "--path",
                        str(REPOSITORY_ROOT),
                        "--script",
                        "res://Verification/Automated/DataTableSchemaEditorSaveRegression.gd",
                    ],
                    cwd=REPOSITORY_ROOT,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=timeout,
                )
            except subprocess.TimeoutExpired as exception:
                schema_save_output = (exception.stdout or "") + (exception.stderr or "")
                print(schema_save_output + f"\nDataTable Schema 保存验证超时：{timeout} 秒", file=sys.stderr)
                return False
            schema_save_output = schema_save_result.stdout + schema_save_result.stderr
            if (
                schema_save_result.returncode == 0
                and "[DataTableSchemaEditorSaveRegression] PASS (4/4)" in schema_save_output
                and "SCRIPT ERROR:" not in schema_save_output
            ):
                print("[EDITOR] PASS")
                return True
            print(
                f"[EDITOR] DataTable Schema 保存 FAIL (exit={schema_save_result.returncode})",
                file=sys.stderr,
            )
            print(schema_save_output, file=sys.stderr)
            return False
        print(f"[EDITOR] DataTable 诊断传输 FAIL (exit={transport_result.returncode})", file=sys.stderr)
        print(transport_output, file=sys.stderr)
        return False

    print(f"[EDITOR] FAIL (exit={result.returncode})", file=sys.stderr)
    print(output, file=sys.stderr)
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
        if not run_editor_extension_check(godot_path, arguments.timeout):
            return 1

        return 0 if run_all_suites(godot_path, arguments.skip_build, arguments.timeout) else 1

    if not run_editor_extension_check(godot_path, arguments.timeout):
        return 1

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
