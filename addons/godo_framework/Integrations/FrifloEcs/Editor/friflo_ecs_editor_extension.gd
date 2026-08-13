@tool
extends RefCounted

const PROJECT_INSPECTOR_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_inspector.gd")
const PROJECT_INSTALLER_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_installer.gd")

var _context
var _dialog: AcceptDialog
var _report: RichTextLabel
var _install_button: Button
var _confirmation: ConfirmationDialog


func activate(context) -> Error:
	_context = context
	return _context.add_menu_action("dependency", "Friflo ECS 依赖检查...", _open_dialog)


func deactivate() -> void:
	if is_instance_valid(_dialog):
		_dialog.queue_free()
	_dialog = null
	_confirmation = null
	_install_button = null
	_context = null


func _open_dialog() -> void:
	if not is_instance_valid(_dialog):
		_create_dialog()
	_refresh()
	_dialog.popup_centered(Vector2i(680, 360))


func _create_dialog() -> void:
	_dialog = AcceptDialog.new()
	_dialog.title = "GoDo Friflo ECS 依赖检查"
	_dialog.ok_button_text = "关闭"
	_dialog.min_size = Vector2i(680, 360)
	_dialog.get_label().hide()

	var content := VBoxContainer.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -56
	content.add_theme_constant_override("separation", 10)
	_dialog.add_child(content)

	_report = RichTextLabel.new()
	_report.name = "FrifloEcsDependencyReport"
	_report.bbcode_enabled = true
	_report.fit_content = false
	_report.scroll_active = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_report)

	var note := Label.new()
	note.name = "FrifloEcsInstallBoundaryNote"
	note.text = "检查不执行 restore。只有根目录唯一普通 .csproj 缺少依赖时，才可经确认写入；中央包管理始终只读。"
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(note)

	var refresh_button := _dialog.add_button("重新检查", true)
	refresh_button.name = "FrifloEcsRefreshButton"
	refresh_button.pressed.connect(_refresh)
	_install_button = _dialog.add_button("添加依赖...", true)
	_install_button.name = "FrifloEcsInstallButton"
	_install_button.pressed.connect(_request_install)
	_context.get_editor_interface().get_base_control().add_child(_dialog)

	_confirmation = ConfirmationDialog.new()
	_confirmation.title = "添加 Friflo ECS 依赖"
	_confirmation.dialog_text = _build_confirmation_text("根目录唯一的 .csproj")
	_confirmation.confirmed.connect(_perform_install)
	_dialog.add_child(_confirmation)


func _refresh() -> void:
	var state: Dictionary = PROJECT_INSPECTOR_SCRIPT.new().inspect()
	var color := "#8bd49c" if state["healthy"] else "#ffd166"
	var status := "已就绪" if state["healthy"] else "需要处理"
	var lines := PackedStringArray([
		"当前状态：[color=%s]%s[/color]" % [color, status],
		"",
		"项目文件：%s" % (state["project_path"].get_file() if not state["project_path"].is_empty() else "未确定"),
		"依赖版本：%s" % (state["version"] if not state["version"].is_empty() else "未确定"),
		"检查结果：%s" % state["detail"],
	])
	_report.text = "\n".join(lines)
	_install_button.disabled = not state["can_install"]


func _request_install() -> void:
	var state: Dictionary = PROJECT_INSPECTOR_SCRIPT.new().inspect()
	if not state["can_install"]:
		_refresh()
		return
	_confirmation.dialog_text = _build_confirmation_text(state["project_path"].get_file())
	_confirmation.popup_centered()


func _build_confirmation_text(project_file: String) -> String:
	return (
		"将修改：%s\n\n" % project_file
		+ "<PackageReference Include=\"Friflo.Engine.ECS\" Version=\"3.6.0\" />\n\n"
		+ "写入前会在同目录创建不覆盖旧文件的 .godo-backup 备份；已有备份时递增编号。\n"
		+ "不会执行 restore 或 build。"
	)


func _perform_install() -> void:
	var result: Dictionary = PROJECT_INSTALLER_SCRIPT.new().install()
	_refresh()
	var color := "#8bd49c" if result["success"] else "#ff6b6b"
	var backup := ""
	if not result["backup_path"].is_empty():
		backup = "\n备份：%s" % result["backup_path"]
	_report.text += "\n\n[color=%s]%s%s[/color]" % [color, result["detail"], backup]
