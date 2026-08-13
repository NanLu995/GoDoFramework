@tool
extends RefCounted

const PROJECT_INSPECTOR_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_inspector.gd")
const PACKAGE_ID := "Friflo.Engine.ECS"
const REQUIRED_VERSION := "3.6.0"


func install(project_root: String = "res://") -> Dictionary:
	var inspector = PROJECT_INSPECTOR_SCRIPT.new()
	var state: Dictionary = inspector.inspect(project_root)
	if not state["can_install"]:
		return _result(false, "当前项目状态不允许自动添加依赖。")

	var project_path: String = state["project_path"]
	var content := FileAccess.get_file_as_string(project_path)
	if content.is_empty() or FileAccess.get_open_error() != OK:
		return _result(false, "无法读取 %s。" % project_path.get_file())

	var closing_index := content.rfind("</Project>")
	if closing_index < 0:
		return _result(false, "%s 缺少标准 </Project> 结束标签。" % project_path.get_file())

	var backup_path := _next_backup_path(project_path)
	var copy_error := DirAccess.copy_absolute(project_path, backup_path)
	if copy_error != OK:
		return _result(false, "无法创建备份：%s" % error_string(copy_error))

	var newline := "\r\n" if "\r\n" in content else "\n"
	var prefix := content.substr(0, closing_index).rstrip("\r\n")
	var suffix := content.substr(closing_index)
	var item_group := (
		"  <ItemGroup>" + newline
		+ "    <PackageReference Include=\"%s\" Version=\"%s\" />" % [PACKAGE_ID, REQUIRED_VERSION] + newline
		+ "  </ItemGroup>" + newline
	)
	var updated := prefix + newline + newline + item_group + suffix
	var file := FileAccess.open(project_path, FileAccess.WRITE)
	if file == null:
		_restore(project_path, backup_path)
		return _result(false, "无法写入 %s；已保留备份。" % project_path.get_file(), backup_path)
	file.store_string(updated)
	file.close()

	var verified: Dictionary = inspector.inspect(project_root)
	if verified["code"] != "ready" or verified["version"] != REQUIRED_VERSION:
		var restore_error := _restore(project_path, backup_path)
		var detail := "写入后复查失败，已从备份恢复。"
		if restore_error != OK:
			detail = "写入后复查失败，且自动恢复失败：%s；请使用备份。" % error_string(restore_error)
		return _result(false, detail, backup_path)

	return _result(
		true,
		"已添加 %s %s；请执行 restore/build。" % [PACKAGE_ID, REQUIRED_VERSION],
		backup_path
	)


func _next_backup_path(project_path: String) -> String:
	var base_path := project_path + ".godo-backup"
	if not FileAccess.file_exists(base_path):
		return base_path
	var index := 1
	while FileAccess.file_exists("%s.%d" % [base_path, index]):
		index += 1
	return "%s.%d" % [base_path, index]


func _restore(project_path: String, backup_path: String) -> Error:
	return DirAccess.copy_absolute(backup_path, project_path)


func _result(success: bool, detail: String, backup_path: String = "") -> Dictionary:
	return {
		"success": success,
		"detail": detail,
		"backup_path": backup_path,
	}
