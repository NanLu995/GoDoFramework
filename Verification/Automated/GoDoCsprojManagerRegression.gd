@tool
extends SceneTree

const MANAGER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_csproj_manager.gd")
const ROOT := "user://godo_csproj_manager_regression"
const ALL := {"guide": true, "phantom": true, "friflo": true, "debugger": true}

var _passed := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_cleanup()
	if not _verify_repair_and_backup():
		return
	if not _verify_ready_is_idempotent():
		return
	if not _verify_only_installed_rules():
		return
	if not _verify_multiple_projects_are_read_only():
		return
	if not _verify_central_management_is_read_only():
		return
	if not _verify_invalid_sdk_is_read_only():
		return
	if not _verify_custom_rule_is_read_only():
		return
	_cleanup()
	print("[GoDoCsprojManagerRegression] PASS (%d/7)" % _passed)
	quit(0)


func _verify_repair_and_backup() -> bool:
	_reset()
	var original := "<Project Sdk=\"Godot.NET.Sdk/4.7.1\">\n  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>\n</Project>\n"
	_write("Game.csproj", original)
	var before := MANAGER_SCRIPT.new().inspect(ROOT, ALL)
	var result := MANAGER_SCRIPT.new().repair(ROOT, ALL)
	var updated := _read("Game.csproj")
	if not before["can_repair"] or not result["success"] or _read("Game.csproj.godo-backup") != original:
		return _fail("普通项目没有完成确认修复边界：before=%s result=%s" % [before, result])
	for marker in ["GoDoIncludeGuideInput", "GoDoIncludePhantomCamera", "GoDoIncludeFrifloEcs", "DebuggerOverlay.cs"]:
		if not updated.contains(marker):
			return _fail("修复后缺少 %s。" % marker)
	if updated.contains("Demo3D") or updated.contains("Verification/") or updated.contains("PackageReference"):
		return _fail("修复错误写入了仓库专用规则或第三方包引用。")
	_passed += 1
	return true


func _verify_ready_is_idempotent() -> bool:
	var state := MANAGER_SCRIPT.new().inspect(ROOT, ALL)
	var result := MANAGER_SCRIPT.new().repair(ROOT, ALL)
	if state["code"] != "ready" or result["success"]:
		return _fail("已就绪项目仍允许重复修复：state=%s result=%s" % [state, result])
	_passed += 1
	return true


func _verify_only_installed_rules() -> bool:
	_reset()
	_write("Game.csproj", "<Project Sdk=\"Godot.NET.Sdk/4.7.1\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>")
	var installed := {"guide": false, "phantom": true, "friflo": false, "debugger": true}
	var result := MANAGER_SCRIPT.new().repair(ROOT, installed)
	var updated := _read("Game.csproj")
	if not result["success"] or not updated.contains("GoDoIncludePhantomCamera") or not updated.contains("DebuggerOverlay.cs") or updated.contains("GoDoIncludeGuideInput") or updated.contains("GoDoIncludeFrifloEcs"):
		return _fail("修复没有严格按已安装模块生成：%s" % updated)
	_passed += 1
	return true


func _verify_multiple_projects_are_read_only() -> bool:
	_reset()
	_write("Game.csproj", "<Project />")
	_write("Tools.csproj", "<Project />")
	return _expect_read_only("multiple_projects", "多个项目")


func _verify_central_management_is_read_only() -> bool:
	_reset()
	_write("Game.csproj", "<Project />")
	_write("Directory.Packages.props", "<Project />")
	return _expect_read_only("central_management", "中央包管理")


func _verify_invalid_sdk_is_read_only() -> bool:
	_reset()
	_write("Game.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>")
	return _expect_read_only("conflict", "非 Godot SDK")


func _verify_custom_rule_is_read_only() -> bool:
	_reset()
	_write("Game.csproj", "<Project Sdk=\"Godot.NET.Sdk/4.7.1\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><GoDoIncludeFrifloEcs>true</GoDoIncludeFrifloEcs></PropertyGroup></Project>")
	return _expect_read_only("conflict", "自定义同名规则")


func _expect_read_only(code: String, label: String) -> bool:
	var original := _read("Game.csproj")
	var state := MANAGER_SCRIPT.new().inspect(ROOT, ALL)
	var result := MANAGER_SCRIPT.new().repair(ROOT, ALL)
	if state["code"] != code or state["can_repair"] or result["success"] or _read("Game.csproj") != original:
		return _fail("%s 没有保持只读：state=%s result=%s" % [label, state, result])
	_passed += 1
	return true


func _reset() -> void:
	_cleanup()
	DirAccess.make_dir_recursive_absolute(ROOT)


func _write(file_name: String, content: String) -> void:
	var file := FileAccess.open(ROOT.path_join(file_name), FileAccess.WRITE)
	if file != null:
		file.store_string(content)
		file.close()


func _read(file_name: String) -> String:
	return FileAccess.get_file_as_string(ROOT.path_join(file_name))


func _cleanup() -> void:
	if not DirAccess.dir_exists_absolute(ROOT):
		return
	for file_name in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT.path_join(file_name))
	DirAccess.remove_absolute(ROOT)


func _fail(message: String) -> bool:
	_cleanup()
	push_error("[GoDoCsprojManagerRegression] FAIL: %s" % message)
	quit(1)
	return false
