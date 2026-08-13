@tool
extends SceneTree

const INSPECTOR_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_inspector.gd")
const INSTALLER_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_installer.gd")
const ROOT := "user://godo_friflo_dependency_regression"

var _passed := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_cleanup()
	DirAccess.make_dir_recursive_absolute(ROOT)
	if not _verify_direct_reference():
		return
	if not _verify_central_version():
		return
	if not _verify_missing_reference():
		return
	if not _verify_version_mismatch():
		return
	if not _verify_unknown_property_version():
		return
	if not _verify_multiple_projects():
		return
	if not _verify_malformed_project():
		return
	if not _verify_item_group_condition():
		return
	if not _verify_install_and_backup():
		return
	if not _verify_incremental_backup():
		return
	if not _verify_central_management_blocks_install():
		return
	_cleanup()
	print("[FrifloEcsProjectDependencyRegression] PASS (%d/11)" % _passed)
	quit(0)


func _verify_direct_reference() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project><ItemGroup><PackageReference Include=\"Friflo.Engine.ECS\" Version=\"3.6.0\" /></ItemGroup></Project>")
	return _expect("ready", true, "普通 PackageReference")


func _verify_central_version() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project><ItemGroup><PackageReference Include=\"Friflo.Engine.ECS\" /></ItemGroup></Project>")
	_write("Directory.Packages.props", "<Project><ItemGroup><PackageVersion Include=\"Friflo.Engine.ECS\"><Version>3.6.0</Version></PackageVersion></ItemGroup></Project>")
	return _expect("ready", true, "中央包版本")


func _verify_missing_reference() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project />")
	var state: Dictionary = INSPECTOR_SCRIPT.new().inspect(ROOT)
	if state["code"] != "missing_reference" or not state["can_install"]:
		_fail("普通缺少引用状态没有开放安全安装：%s" % state)
		return false
	_passed += 1
	return true


func _verify_version_mismatch() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project><ItemGroup><PackageReference Include=\"Friflo.Engine.ECS\" Version=\"4.0.0\" /></ItemGroup></Project>")
	return _expect("version_mismatch", false, "版本不符")


func _verify_unknown_property_version() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project><ItemGroup><PackageReference Include=\"Friflo.Engine.ECS\" Version=\"$(FrifloVersion)\" /></ItemGroup></Project>")
	return _expect("unknown_version", false, "变量版本")


func _verify_multiple_projects() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project />")
	_write("Tools.csproj", "<Project />")
	return _expect("multiple_projects", false, "多个项目")


func _verify_malformed_project() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project><ItemGroup>")
	return _expect("invalid_project", false, "损坏项目")


func _verify_item_group_condition() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project><ItemGroup Condition=\"'$(UseEcs)' == 'true'\"><PackageReference Include=\"Friflo.Engine.ECS\" Version=\"3.6.0\" /></ItemGroup></Project>")
	var state: Dictionary = INSPECTOR_SCRIPT.new().inspect(ROOT)
	if state["code"] != "ready" or not state["detail"].contains("Condition"):
		_fail("父级 ItemGroup 条件没有进入就绪提示：%s" % state)
		return false
	_passed += 1
	return true


func _verify_install_and_backup() -> bool:
	_reset_fixture()
	var original := "<Project Sdk=\"Godot.NET.Sdk/4.7.1\">\n  <PropertyGroup />\n</Project>\n"
	_write("Game.csproj", original)
	var result: Dictionary = INSTALLER_SCRIPT.new().install(ROOT)
	var state: Dictionary = INSPECTOR_SCRIPT.new().inspect(ROOT)
	if not result["success"] or state["code"] != "ready":
		_fail("普通项目安装后未就绪：result=%s state=%s" % [result, state])
		return false
	if _read("Game.csproj.godo-backup") != original:
		_fail("安装前备份没有保留原始项目内容。")
		return false
	var updated := _read("Game.csproj")
	if updated.count("Friflo.Engine.ECS") != 1 or not updated.contains("Version=\"3.6.0\""):
		_fail("安装没有精确写入一次 Friflo 3.6.0：%s" % updated)
		return false
	var duplicate: Dictionary = INSTALLER_SCRIPT.new().install(ROOT)
	if duplicate["success"]:
		_fail("依赖已存在时仍重复安装。")
		return false
	_passed += 1
	return true


func _verify_incremental_backup() -> bool:
	_reset_fixture()
	var original := "<Project>\n</Project>\n"
	_write("Game.csproj", original)
	_write("Game.csproj.godo-backup", "older backup")
	var result: Dictionary = INSTALLER_SCRIPT.new().install(ROOT)
	if not result["success"] or not result["backup_path"].ends_with(".godo-backup.1"):
		_fail("已有备份时没有使用递增名称：%s" % result)
		return false
	if _read("Game.csproj.godo-backup") != "older backup" or _read("Game.csproj.godo-backup.1") != original:
		_fail("递增备份覆盖了旧文件或没有保存原文。")
		return false
	_passed += 1
	return true


func _verify_central_management_blocks_install() -> bool:
	_reset_fixture()
	_write("Game.csproj", "<Project />")
	_write("Directory.Packages.props", "<Project />")
	var state: Dictionary = INSPECTOR_SCRIPT.new().inspect(ROOT)
	var result: Dictionary = INSTALLER_SCRIPT.new().install(ROOT)
	if state["code"] != "missing_reference_central" or state["can_install"] or result["success"]:
		_fail("中央包管理项目错误开放了自动安装：state=%s result=%s" % [state, result])
		return false
	if _read("Game.csproj") != "<Project />":
		_fail("中央包管理拒绝安装时仍修改了项目文件。")
		return false
	_passed += 1
	return true


func _expect(code: String, healthy: bool, name: String) -> bool:
	var state: Dictionary = INSPECTOR_SCRIPT.new().inspect(ROOT)
	if state["code"] != code or state["healthy"] != healthy:
		_fail("%s 返回 %s，预期 %s。" % [name, state, code])
		return false
	_passed += 1
	return true


func _reset_fixture() -> void:
	_cleanup()
	DirAccess.make_dir_recursive_absolute(ROOT)


func _write(file_name: String, content: String) -> void:
	var file := FileAccess.open(ROOT.path_join(file_name), FileAccess.WRITE)
	if file == null:
		_fail("无法写入测试文件：%s" % file_name)
		return
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


func _fail(message: String) -> void:
	_cleanup()
	push_error("[FrifloEcsProjectDependencyRegression] FAIL: %s" % message)
	quit(1)
