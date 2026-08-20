#!/usr/bin/env python3
"""Verify core package install, replacement upgrade, and safe removal."""

from __future__ import annotations

import argparse
import importlib.util
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
RELEASE_SCRIPT = REPOSITORY_ROOT / "release" / "release.py"
PROJECT_NAME = "GoDoCorePackageLifecycle"
FRAMEWORK_RELATIVE_PATH = Path("addons") / "godo_framework"
RUNTIME_SCENE_PATH = "res://addons/godo_framework/Core/GoDoRuntime.tscn"
PLUGIN_CONFIG_PATH = "res://addons/godo_framework/plugin.cfg"
CONFLICT_SCENE_PATH = "res://ConflictRuntime.tscn"
PASS_MARKERS = {
    "install": "[CorePackageLifecycle] INSTALL PASS",
    "health": "[CorePackageLifecycle] HEALTH PASS",
    "conflict": "[CorePackageLifecycle] CONFLICT PASS",
    "uninstall": "[CorePackageLifecycle] UNINSTALL PASS",
}


def load_release_module():
    spec = importlib.util.spec_from_file_location("godo_release_lifecycle", RELEASE_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载发布脚本：{RELEASE_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def read_godot_version() -> str:
    project = (REPOSITORY_ROOT / "GoDoFramework.csproj").read_text(encoding="utf-8")
    match = re.search(r"Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+)", project)
    if match is None:
        raise RuntimeError("GoDoFramework.csproj 未声明 Godot.NET.Sdk 版本。")
    return match.group(1)


GODOT_VERSION = read_godot_version()

PROJECT_FILE = f"""<Project Sdk=\"Godot.NET.Sdk/{GODOT_VERSION}\">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
</Project>
"""

SCENE_TEMPLATE = """[gd_scene load_steps=2 format=3]

[ext_resource type=\"Script\" path=\"res://{script}\" id=\"1_runner\"]

[node name=\"{name}\" type=\"Node\"]
script = ExtResource(\"1_runner\")
"""

CORE_SMOKE_SCRIPT = """using System;
using Godot;
using GoDo;

#nullable enable

public sealed partial class CoreSmoke : Node
{
    public override void _Ready() => CallDeferred(MethodName.VerifyRuntime);

    private void VerifyRuntime()
    {
        try
        {
            _ = Services.Get<ISceneService>();
            _ = Services.Get<ICameraService>();
            _ = Services.Get<IInputService>();
            _ = Services.Get<IAudioService>();
            _ = Services.Get<ILocalizationService>();
            _ = Services.Get<IUiService>();
            _ = Services.Get<ISaveService>();
            _ = Services.Get<ISettingsService>();
            _ = Services.Get<IProcedureService>();

            GD.Print("[CorePackageLifecycle] RUNTIME PASS (9/9 services)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[CorePackageLifecycle] RUNTIME FAIL: {exception}");
            GetTree().Quit(1);
        }
    }
}
"""

NEUTRAL_SMOKE_SCRIPT = """using Godot;

public sealed partial class NeutralSmoke : Node
{
    public override void _Ready()
    {
        GD.Print("[CorePackageLifecycle] REMOVED HOST PASS");
        GetTree().Quit(0);
    }
}
"""

CONFLICT_SCENE = """[gd_scene format=3]

[node name=\"ConflictRuntime\" type=\"Node\"]
"""

LIFECYCLE_PROBE = """@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"
const AUTOLOAD_SETTING := "autoload/GoDoRuntime"
const RUNTIME_SCENE_PATH := "res://addons/godo_framework/Core/GoDoRuntime.tscn"
const CONFLICT_SCENE_PATH := "res://ConflictRuntime.tscn"
const PLUGIN_ID := "godo_framework"
const MODE_PATH := "res://lifecycle-mode.txt"


func _initialize() -> void:
    call_deferred("_run")


func _run() -> void:
    await process_frame
    await process_frame
    await process_frame
    var mode := FileAccess.get_file_as_string(MODE_PATH).strip_edges()
    match mode:
        "install":
            await _verify_install()
        "health":
            await _verify_health()
        "conflict":
            await _verify_conflict_protection()
        "uninstall":
            await _verify_uninstall()
        _:
            _fail("未知验证阶段：%s" % mode)


func _open_runtime_page() -> Window:
    var menu_button := root.find_child(MENU_BUTTON_NAME, true, false) as MenuButton
    if menu_button == null:
        _fail("未找到 GoDo Framework 工具栏菜单。")
        return null
    var menu := menu_button.get_popup()
    if menu.item_count != 1:
        _fail("GoDo Framework 工具栏菜单结构异常。")
        return null
    menu.id_pressed.emit(menu.get_item_id(0))
    await process_frame
    await process_frame
    var window := root.find_child("GoDoFrameworkWindow", true, false) as Window
    if window == null or not window.visible:
        _fail("无法打开 GoDo Framework 窗口。")
        return null
    var navigation := window.find_child("GoDoFrameworkNavigation", true, false) as Tree
    if navigation == null or navigation.get_root() == null:
        _fail("GoDo Framework 窗口缺少导航树。")
        return null
    var runtime_item := _find_tree_item_by_metadata(navigation.get_root(), "runtime")
    if runtime_item == null:
        _fail("GoDo Framework 窗口缺少 Runtime 页面。")
        return null
    navigation.set_selected(runtime_item, 0)
    navigation.item_selected.emit()
    await process_frame
    return window


func _verify_install() -> void:
    if ProjectSettings.has_setting(AUTOLOAD_SETTING):
        _fail("安装前已存在 GoDoRuntime Autoload。")
        return
    var window := await _open_runtime_page()
    if window == null:
        return
    var install_button := window.find_child("GoDoRuntimeInstallButton", true, false) as Button
    if install_button == null or install_button.disabled:
        _fail("健康检查通过后安装按钮仍不可用：%s" % _report_text(window))
        return
    install_button.pressed.emit()
    await process_frame
    await process_frame
    if _autoload_path() != RUNTIME_SCENE_PATH:
        _fail("安装后 GoDoRuntime Autoload 路径不正确：%s" % _autoload_path())
        return
    install_button.pressed.emit()
    await process_frame
    if _autoload_path() != RUNTIME_SCENE_PATH or _runtime_registration_count() != 1:
        _fail("重复安装没有保持唯一且幂等。")
        return
    _pass("[CorePackageLifecycle] INSTALL PASS")


func _verify_health() -> void:
    var window := await _open_runtime_page()
    if window == null:
        return
    var install_button := window.find_child("GoDoRuntimeInstallButton", true, false) as Button
    var uninstall_button := window.find_child("GoDoRuntimeUninstallButton", true, false) as Button
    if (
        _autoload_path() != RUNTIME_SCENE_PATH
        or install_button == null
        or not install_button.disabled
        or uninstall_button == null
        or uninstall_button.disabled
        or not _report_text(window).contains("已正确安装")
    ):
        _fail("完整替换后插件未识别已有 Runtime：%s" % _report_text(window))
        return
    _pass("[CorePackageLifecycle] HEALTH PASS")


func _verify_conflict_protection() -> void:
    var window := await _open_runtime_page()
    if window == null:
        return
    var uninstall_button := window.find_child("GoDoRuntimeUninstallButton", true, false) as Button
    if uninstall_button == null or not uninstall_button.disabled:
        _fail("路径冲突时卸载按钮没有保持禁用。")
        return
    uninstall_button.pressed.emit()
    await process_frame
    await process_frame
    if _autoload_path() != CONFLICT_SCENE_PATH:
        _fail("插件错误移除了不属于框架的同名 Autoload。")
        return
    _pass("[CorePackageLifecycle] CONFLICT PASS")


func _verify_uninstall() -> void:
    var window := await _open_runtime_page()
    if window == null:
        return
    var uninstall_button := window.find_child("GoDoRuntimeUninstallButton", true, false) as Button
    if uninstall_button == null or uninstall_button.disabled:
        _fail("精确匹配的 Runtime 无法卸载：%s" % _report_text(window))
        return
    uninstall_button.pressed.emit()
    await process_frame
    await process_frame
    var confirmation := _find_confirmation(root, "卸载 GoDoRuntime")
    if confirmation == null or not confirmation.visible:
        _fail("卸载操作没有显示确认对话框。")
        return
    confirmation.confirmed.emit()
    await process_frame
    await process_frame
    if ProjectSettings.has_setting(AUTOLOAD_SETTING):
        _fail("确认卸载后 GoDoRuntime Autoload 仍然存在。")
        return
    EditorInterface.set_plugin_enabled(PLUGIN_ID, false)
    await process_frame
    await process_frame
    if EditorInterface.is_plugin_enabled(PLUGIN_ID):
        _fail("GoDo Framework 插件禁用失败。")
        return
    _pass("[CorePackageLifecycle] UNINSTALL PASS")


func _autoload_path() -> String:
    return str(ProjectSettings.get_setting(AUTOLOAD_SETTING, "")).trim_prefix("*")


func _runtime_registration_count() -> int:
    var count := 0
    for property in ProjectSettings.get_property_list():
        var setting_name := str(property.name)
        if not setting_name.begins_with("autoload/"):
            continue
        var path := str(ProjectSettings.get_setting(setting_name)).trim_prefix("*")
        if path == RUNTIME_SCENE_PATH:
            count += 1
    return count


func _report_text(window: Window) -> String:
    var report := window.find_child("GoDoRuntimeReport", true, false) as RichTextLabel
    return "<missing>" if report == null else report.get_parsed_text()


func _find_tree_item_by_metadata(item: TreeItem, value: String) -> TreeItem:
    var child := item.get_first_child()
    while child != null:
        if str(child.get_metadata(0)) == value:
            return child
        var nested := _find_tree_item_by_metadata(child, value)
        if nested != null:
            return nested
        child = child.get_next()
    return null


func _find_confirmation(node: Node, title: String) -> ConfirmationDialog:
    for child in node.get_children():
        if child is ConfirmationDialog and child.title == title:
            return child
        var nested := _find_confirmation(child, title)
        if nested != null:
            return nested
    return null


func _fail(message: String) -> void:
    push_error("[CorePackageLifecycle] FAIL: %s" % message)
    quit(1)


func _pass(marker: String) -> void:
    var save_error := ProjectSettings.save()
    if save_error != OK:
        _fail("无法在 Headless 验证退出前保存 project.godot：%s" % error_string(save_error))
        return
    print(marker)
    quit(0)
"""


def configure_console_encoding() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
        sys.stderr.reconfigure(encoding="utf-8", line_buffering=True)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="在临时 Godot C# 项目中验证核心 ZIP 的安装、替换升级与安全移除。"
    )
    parser.add_argument(
        "--godot",
        type=Path,
        help=f"Godot {GODOT_VERSION} Mono Console 可执行文件；也可设置 GODOT_PATH。",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=60,
        help="每个 Headless Editor 或场景阶段的超时秒数，默认 60。",
    )
    parser.add_argument(
        "--keep",
        action="store_true",
        help="失败时保留临时目录并输出路径。",
    )
    return parser.parse_args()


def resolve_godot_path(argument: Path | None) -> Path:
    candidates: list[str | Path | None] = [
        argument,
        os.environ.get("GODOT_PATH"),
        shutil.which("godot"),
        shutil.which("godot4"),
        shutil.which(f"Godot_v{GODOT_VERSION}-stable_mono_win64_console.exe"),
    ]
    for candidate in candidates:
        if candidate is None:
            continue
        path = Path(candidate).expanduser().resolve()
        if path.is_file():
            return path
    raise RuntimeError(
        f"未找到 Godot {GODOT_VERSION} Mono Console；请使用 --godot <exe路径> 或设置 GODOT_PATH。"
    )


def render_project_config(
    autoload_path: str | None = None,
    plugin_enabled: bool = True,
) -> str:
    sections = [
        "; Engine configuration file.",
        "; Generated by verify_core_package_lifecycle.py.",
        "config_version=5",
        "",
        "[application]",
        "",
        'config/name="GoDo Core Package Lifecycle"',
        'run/main_scene="res://NeutralSmoke.tscn"',
        'config/features=PackedStringArray("4.7", "C#")',
    ]
    if autoload_path is not None:
        sections.extend(
            [
                "",
                "[autoload]",
                "",
                f'GoDoRuntime="*{autoload_path}"',
            ]
        )
    sections.extend(
        [
            "",
            "[dotnet]",
            "",
            f'project/assembly_name="{PROJECT_NAME}"',
        ]
    )
    if plugin_enabled:
        sections.extend(
            [
                "",
                "[editor_plugins]",
                "",
                f'enabled=PackedStringArray("{PLUGIN_CONFIG_PATH}")',
            ]
        )
    sections.extend(
        [
            "",
            "[rendering]",
            "",
            'renderer/rendering_method="gl_compatibility"',
            'renderer/rendering_method.mobile="gl_compatibility"',
            "",
        ]
    )
    return "\n".join(sections)


def create_host_project(project_root: Path) -> None:
    (project_root / "project.godot").write_text(
        render_project_config(), encoding="utf-8"
    )
    (project_root / f"{PROJECT_NAME}.csproj").write_text(
        PROJECT_FILE, encoding="utf-8"
    )
    (project_root / "CoreSmoke.tscn").write_text(
        SCENE_TEMPLATE.format(script="CoreSmoke.cs", name="CoreSmoke"),
        encoding="utf-8",
    )
    (project_root / "CoreSmoke.cs").write_text(CORE_SMOKE_SCRIPT, encoding="utf-8")
    (project_root / "NeutralSmoke.tscn").write_text(
        SCENE_TEMPLATE.format(script="NeutralSmoke.cs", name="NeutralSmoke"),
        encoding="utf-8",
    )
    (project_root / "NeutralSmoke.cs").write_text(
        NEUTRAL_SMOKE_SCRIPT, encoding="utf-8"
    )
    (project_root / "ConflictRuntime.tscn").write_text(
        CONFLICT_SCENE, encoding="utf-8"
    )
    (project_root / "LifecycleProbe.gd").write_text(
        LIFECYCLE_PROBE, encoding="utf-8"
    )


def extract_core_archive(archive_path: Path, project_root: Path) -> None:
    root = project_root.resolve()
    with zipfile.ZipFile(archive_path, mode="r") as archive:
        invalid_entry = archive.testzip()
        if invalid_entry is not None:
            raise RuntimeError(f"核心 ZIP 完整性校验失败：{invalid_entry}")
        for entry in archive.infolist():
            relative = Path(entry.filename)
            if relative.is_absolute() or ".." in relative.parts:
                raise RuntimeError(f"核心 ZIP 包含不安全路径：{entry.filename}")
            target = (project_root / relative).resolve()
            if not target.is_relative_to(root):
                raise RuntimeError(f"核心 ZIP 条目逃逸临时项目：{entry.filename}")
        archive.extractall(project_root)


def remove_framework_directory(project_root: Path) -> None:
    root = project_root.resolve()
    target = (project_root / FRAMEWORK_RELATIVE_PATH).resolve()
    if target != root / FRAMEWORK_RELATIVE_PATH or not target.is_relative_to(root):
        raise RuntimeError(f"拒绝删除非预期框架目录：{target}")
    if target.is_dir():
        shutil.rmtree(target)


def replace_framework_directory(archive_path: Path, project_root: Path) -> None:
    remove_framework_directory(project_root)
    extract_core_archive(archive_path, project_root)


def write_project_state(
    project_root: Path,
    autoload_path: str | None,
    plugin_enabled: bool = True,
) -> None:
    (project_root / "project.godot").write_text(
        render_project_config(autoload_path, plugin_enabled), encoding="utf-8"
    )


def run(command: list[str], cwd: Path, timeout: int | None = None) -> str:
    try:
        result = subprocess.run(
            command,
            cwd=cwd,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
        )
    except subprocess.TimeoutExpired as exception:
        output = (exception.stdout or "") + (exception.stderr or "")
        raise RuntimeError(f"命令超时：{subprocess.list2cmdline(command)}\n{output}") from exception
    output = result.stdout + result.stderr
    if result.returncode == 0:
        return output
    raise RuntimeError(
        f"命令失败（exit={result.returncode}）：{subprocess.list2cmdline(command)}\n{output}"
    )


def build_project(project_root: Path) -> None:
    print(f"[BUILD] {PROJECT_NAME}.csproj")
    run(["dotnet", "build", f"{PROJECT_NAME}.csproj", "--nologo"], project_root)


def run_editor_import(project_root: Path, godot_path: Path, timeout: int) -> None:
    print("[EDITOR] import package and load plugin")
    run(
        [
            str(godot_path),
            "--headless",
            "--editor",
            "--path",
            str(project_root),
            "--quit-after",
            "3",
        ],
        project_root,
        timeout,
    )


def run_probe(
    project_root: Path,
    godot_path: Path,
    mode: str,
    timeout: int,
) -> None:
    marker = PASS_MARKERS[mode]
    (project_root / "lifecycle-mode.txt").write_text(mode, encoding="utf-8")
    print(f"[EDITOR] lifecycle {mode}")
    output = run(
        [
            str(godot_path),
            "--headless",
            "--editor",
            "--path",
            str(project_root),
            "--script",
            "res://LifecycleProbe.gd",
        ],
        project_root,
        timeout,
    )
    if marker not in output:
        raise RuntimeError(f"{mode} 阶段缺少成功标记。\n{output}")


def run_scene(
    project_root: Path,
    godot_path: Path,
    scene_path: str,
    marker: str,
    timeout: int,
) -> None:
    print(f"[RUN] {scene_path}")
    output = run(
        [str(godot_path), "--headless", "--path", str(project_root), scene_path],
        project_root,
        timeout,
    )
    if marker not in output:
        raise RuntimeError(f"{scene_path} 缺少成功标记。\n{output}")


def verify_lifecycle(project_root: Path, godot_path: Path, timeout: int) -> None:
    release = load_release_module()
    version, _, _ = release.read_plugin_metadata()
    archive_root = project_root / "archive"
    archive_path = release.build_archive(version, "core", archive_root)

    create_host_project(project_root)
    extract_core_archive(archive_path, project_root)
    framework_root = project_root / FRAMEWORK_RELATIVE_PATH
    if not framework_root.is_dir():
        raise RuntimeError("核心 ZIP 未生成 addons/godo_framework 目录。")
    if (framework_root / "Integrations").exists():
        raise RuntimeError("核心 ZIP 错误包含了 Integrations。")

    build_project(project_root)
    run_editor_import(project_root, godot_path, timeout)
    run_probe(project_root, godot_path, "install", timeout)
    run_scene(
        project_root,
        godot_path,
        "res://CoreSmoke.tscn",
        "[CorePackageLifecycle] RUNTIME PASS (9/9 services)",
        timeout,
    )

    legacy_file = framework_root / "LegacyOnly.cs"
    legacy_file.write_text("// Synthetic obsolete file.\n", encoding="utf-8")
    replace_framework_directory(archive_path, project_root)
    if legacy_file.exists():
        raise RuntimeError("完整替换后旧版本残留文件仍然存在。")
    build_project(project_root)
    run_probe(project_root, godot_path, "health", timeout)
    run_scene(
        project_root,
        godot_path,
        "res://CoreSmoke.tscn",
        "[CorePackageLifecycle] RUNTIME PASS (9/9 services)",
        timeout,
    )

    write_project_state(project_root, CONFLICT_SCENE_PATH)
    run_probe(project_root, godot_path, "conflict", timeout)
    write_project_state(project_root, RUNTIME_SCENE_PATH)
    run_probe(project_root, godot_path, "uninstall", timeout)

    project_config = (project_root / "project.godot").read_text(encoding="utf-8")
    if "GoDoRuntime=" in project_config:
        raise RuntimeError("卸载后 project.godot 仍包含 GoDoRuntime。")
    if PLUGIN_CONFIG_PATH in project_config:
        raise RuntimeError("禁用后 project.godot 仍启用 GoDo Framework 插件。")

    (project_root / "CoreSmoke.cs").unlink()
    (project_root / "CoreSmoke.tscn").unlink()
    remove_framework_directory(project_root)
    if framework_root.exists():
        raise RuntimeError("移除阶段结束后框架目录仍然存在。")
    build_project(project_root)
    run_scene(
        project_root,
        godot_path,
        "res://NeutralSmoke.tscn",
        "[CorePackageLifecycle] REMOVED HOST PASS",
        timeout,
    )
    print("[PASS] 核心 ZIP 安装、替换升级、安全卸载与宿主移除验证通过")


def main() -> int:
    configure_console_encoding()
    arguments = parse_arguments()
    if arguments.timeout <= 0:
        raise RuntimeError("--timeout 必须大于 0。")
    godot_path = resolve_godot_path(arguments.godot)

    if arguments.keep:
        project_root = Path(tempfile.mkdtemp(prefix="godo-core-lifecycle-"))
        try:
            verify_lifecycle(project_root, godot_path, arguments.timeout)
        except Exception:
            print(f"[KEEP] 临时目录保留在：{project_root}", file=sys.stderr)
            raise
        shutil.rmtree(project_root)
        return 0

    with tempfile.TemporaryDirectory(prefix="godo-core-lifecycle-") as temporary:
        verify_lifecycle(Path(temporary), godot_path, arguments.timeout)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, subprocess.SubprocessError, zipfile.BadZipFile) as error:
        print(f"核心包生命周期验证失败：{error}", file=sys.stderr)
        raise SystemExit(1)
