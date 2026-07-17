#!/usr/bin/env python3
"""Verify DataTable export filtering in an isolated temporary Godot project."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[2]
ARTIFACT_ROOT = SCRIPT_DIR / "Artifacts"
FIXTURE_ROOT = ARTIFACT_ROOT / "scratch" / "export-targets"
TEMP_ROOT = ARTIFACT_ROOT / "export-plugin-verification"
EXPORT_WRAPPER = (
    PROJECT_ROOT
    / "addons"
    / "godo_framework"
    / "Tools"
    / "DataTable"
    / "godo_datatable_export.py"
)


def run(
    command: list[str],
    *,
    cwd: Path,
    expected_exit: int | None = 0,
) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if expected_exit is not None and result.returncode != expected_exit:
        raise RuntimeError(
            f"命令退出码错误；期望 {expected_exit}，实际 {result.returncode}。\n"
            f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )
    return result


def write_project(project: Path) -> None:
    project.mkdir(parents=True)
    framework = project / "addons" / "godo_framework"
    editor = framework / "Tools" / "DataTable" / "Editor"
    editor.mkdir(parents=True)
    shutil.copy2(
        PROJECT_ROOT / "addons" / "godo_framework" / "Tools" / "DataTable" / "godo_datatable.py",
        framework / "Tools" / "DataTable" / "godo_datatable.py",
    )
    shutil.copy2(
        PROJECT_ROOT
        / "addons"
        / "godo_framework"
        / "Tools"
        / "DataTable"
        / "Editor"
        / "datatable_export_plugin.gd",
        editor / "datatable_export_plugin.gd",
    )
    (framework / "plugin.cfg").write_text(
        """[plugin]

name="GoDo DataTable Export Verification"
description="Loads the real DataTable export plugin in an isolated project."
author="GoDo"
version="1.0"
script="export_verification_plugin.gd"
""",
        encoding="utf-8",
    )
    (framework / "export_verification_plugin.gd").write_text(
        """@tool
extends EditorPlugin

const EXPORT_PLUGIN := preload("res://addons/godo_framework/Tools/DataTable/Editor/datatable_export_plugin.gd")

var _export_plugin: EditorExportPlugin


func _enter_tree() -> void:
	_export_plugin = EXPORT_PLUGIN.new()
	add_export_plugin(_export_plugin)


func _exit_tree() -> void:
	if _export_plugin != null:
		remove_export_plugin(_export_plugin)
	_export_plugin = null
""",
        encoding="utf-8",
    )
    shutil.copytree(FIXTURE_ROOT, project / "DataTables")
    (project / "project.godot").write_text(
        """[application]
config/name="DataTable Export Verification"

[editor_plugins]
enabled=PackedStringArray("res://addons/godo_framework/plugin.cfg")
""",
        encoding="utf-8",
    )
    (project / "export_presets.cfg").write_text(
        """[preset.0]
name="DataTable Client"
platform="Windows Desktop"
runnable=false
dedicated_server=false
custom_features=""
export_filter="all_resources"
include_filter="*.gdtb,*.json,*.csv,*.txt"
exclude_filter=""
export_path=""
script_export_mode=2

[preset.0.options]
binary_format/embed_pck=false

[preset.1]
name="DataTable Server"
platform="Windows Desktop"
runnable=false
dedicated_server=false
custom_features="dedicated_server"
export_filter="all_resources"
include_filter="*.gdtb,*.json,*.csv,*.txt"
exclude_filter=""
export_path=""
script_export_mode=2

[preset.1.options]
binary_format/embed_pck=false
""",
        encoding="utf-8",
    )


def write_probe(project: Path) -> None:
    project.mkdir(parents=True)
    (project / "project.godot").write_text(
        '[application]\nconfig/name="DataTable Pack Probe"\n',
        encoding="utf-8",
    )
    (project / "verify_pack.gd").write_text(
        """extends SceneTree

func _initialize() -> void:
	var arguments := OS.get_cmdline_user_args()
	if arguments.size() != 2:
		_fail("需要 PCK 路径和目标参数。")
		return
	if not ProjectSettings.load_resource_pack(arguments[0], true):
		_fail("PCK 加载失败。")
		return
	var target := arguments[1]
	var included := "ClientSetting" if target == "client" else "ServerSetting"
	var excluded := "ServerSetting" if target == "client" else "ClientSetting"
	var root := "res://DataTables/output"
	if not FileAccess.file_exists(root.path_join("%s.gdtb" % included)):
		_fail("缺少目标专属表。")
		return
	if FileAccess.file_exists(root.path_join("%s.gdtb" % excluded)):
		_fail("包含了另一端专属表。")
		return
	for shared in ["Item.gdtb", "ItemCategory.gdtb", "manifest.json"]:
		if not FileAccess.file_exists(root.path_join(shared)):
			_fail("缺少运行时文件：%s。" % shared)
			return
	for excluded_path in [
		"res://DataTables/datatable.build.json",
		"res://DataTables/profile.json",
		"res://DataTables/source/Items.csv",
		root.path_join("normalized.ir.json"),
		root.path_join("build-report.json"),
		root.path_join("debug.json"),
		root.path_join("manifest.client.json"),
		root.path_join("manifest.server.json"),
	]:
		if FileAccess.file_exists(excluded_path):
			_fail("Release PCK 泄漏构建期文件：%s。" % excluded_path)
			return
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(root.path_join("manifest.json")))
	var expected_target := "Client" if target == "client" else "Server"
	if not manifest is Dictionary or manifest.get("target", "") != expected_target:
		_fail("PCK Manifest 目标错误。")
		return
	print("[DataTableExportPackProbe] PASS: %s" % target)
	quit(0)

func _fail(message: String) -> void:
	push_error("[DataTableExportPackProbe] FAIL: %s" % message)
	quit(1)
""",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot", type=Path, required=True)
    arguments = parser.parse_args()
    if not FIXTURE_ROOT.is_dir():
        raise RuntimeError("缺少 export-targets fixture；请先运行 verify_prototype.py。")
    if TEMP_ROOT.exists():
        shutil.rmtree(TEMP_ROOT)
    export_project = TEMP_ROOT / "export-project"
    probe_project = TEMP_ROOT / "probe-project"
    packs = TEMP_ROOT / "packs"
    packs.mkdir(parents=True)
    write_project(export_project)
    write_probe(probe_project)
    godot = str(arguments.godot.resolve())
    run([godot, "--headless", "--path", str(export_project), "--import"], cwd=PROJECT_ROOT)
    for target, preset in (("client", "DataTable Client"), ("server", "DataTable Server")):
        pack = packs / f"{target}.pck"
        run(
            [
                sys.executable,
                "-X",
                "utf8",
                str(EXPORT_WRAPPER),
                "--godot",
                godot,
                "--project",
                str(export_project),
                "--preset",
                preset,
                "--output",
                str(pack),
                "--mode",
                "pack",
            ],
            cwd=PROJECT_ROOT,
        )
        run(
            [godot, "--headless", "--path", str(probe_project), "--script", "res://verify_pack.gd", "--", str(pack), target],
            cwd=PROJECT_ROOT,
        )

    items = export_project / "DataTables" / "source" / "Items.csv"
    items.write_text(items.read_text(encoding="utf-8").replace("测试物品 1", "stale", 1), encoding="utf-8")
    stale_pack = packs / "stale.pck"
    stale_pack.unlink(missing_ok=True)
    stale = run(
        [
            sys.executable,
            "-X",
            "utf8",
            str(EXPORT_WRAPPER),
            "--godot",
            godot,
            "--project",
            str(export_project),
            "--preset",
            "DataTable Client",
            "--output",
            str(stale_pack),
            "--mode",
            "pack",
        ],
        cwd=PROJECT_ROOT,
        expected_exit=1,
    )
    if "校验失败，未启动 Godot 导出" not in stale.stdout + stale.stderr:
        raise RuntimeError("过期产物未产生 DataTable 导出错误。")
    if stale_pack.exists():
        raise RuntimeError("DataTable 校验失败后仍启动了 Godot 导出。")
    shutil.rmtree(TEMP_ROOT)
    print("[DataTableExportPluginVerification] PASS (3/3)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[DataTableExportPluginVerification] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
