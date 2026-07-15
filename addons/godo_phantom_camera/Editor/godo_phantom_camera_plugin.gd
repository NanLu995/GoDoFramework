@tool
extends EditorPlugin

const TOOL_MENU_NAME := "[GoDo] 校验 Phantom Camera 集成"
const PHANTOM_PLUGIN_CONFIG := "res://addons/phantom_camera/plugin.cfg"
const PHANTOM_PLUGIN_PATH := "res://addons/phantom_camera/plugin.cfg"
const MINIMUM_PHANTOM_VERSION := "0.11"

var _report_dialog: AcceptDialog
var _report_label: RichTextLabel


func _enter_tree() -> void:

	_create_report_dialog()
	add_tool_menu_item(TOOL_MENU_NAME, _open_validation_report)
	_print_validation_report()


func _exit_tree() -> void:

	remove_tool_menu_item(TOOL_MENU_NAME)
	if is_instance_valid(_report_dialog):
		_report_dialog.queue_free()


func _create_report_dialog() -> void:

	_report_dialog = AcceptDialog.new()
	_report_dialog.title = "GoDo Phantom Camera 集成校验"
	_report_dialog.ok_button_text = "关闭"
	_report_dialog.min_size = Vector2i(640, 300)
	_report_dialog.get_label().hide()
	_report_label = RichTextLabel.new()
	_report_label.bbcode_enabled = false
	_report_label.selection_enabled = true
	_report_dialog.add_child(_report_label)
	_report_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_report_label.offset_left = 16
	_report_label.offset_top = 16
	_report_label.offset_right = -16
	_report_label.offset_bottom = -48
	get_editor_interface().get_base_control().add_child(_report_dialog)


func _open_validation_report() -> void:

	_report_label.text = "\n".join(_validation_lines())
	_report_dialog.popup_centered(Vector2i(640, 300))


func _print_validation_report() -> void:

	for line in _validation_lines():
		if line.begins_with("[错误]"):
			push_error("GoDo Phantom Camera：%s" % line)
		elif line.begins_with("[警告]"):
			push_warning("GoDo Phantom Camera：%s" % line)


func _validation_lines() -> PackedStringArray:

	var lines := PackedStringArray(["GoDo Phantom Camera 可选集成"])
	if not FileAccess.file_exists(PHANTOM_PLUGIN_CONFIG):
		lines.append("[错误] 未找到 Phantom Camera：%s" % PHANTOM_PLUGIN_CONFIG)
		return lines

	var phantom_config := ConfigFile.new()
	if phantom_config.load(PHANTOM_PLUGIN_CONFIG) != OK:
		lines.append("[错误] 无法读取 Phantom Camera 的 plugin.cfg")
		return lines

	var version := str(phantom_config.get_value("plugin", "version", "未知"))
	if version != MINIMUM_PHANTOM_VERSION:
		lines.append("[警告] 当前 Phantom Camera 版本为 %s；本集成首版按 %s 验证" % [version, MINIMUM_PHANTOM_VERSION])
	else:
		lines.append("[正常] Phantom Camera %s 已找到" % version)

	var project_config := ConfigFile.new()
	if project_config.load("res://project.godot") != OK:
		lines.append("[错误] 无法读取 project.godot，不能确认 Phantom Camera 是否已启用")
		return lines

	var enabled_plugins: PackedStringArray = project_config.get_value("editor_plugins", "enabled", PackedStringArray())
	if enabled_plugins.has(PHANTOM_PLUGIN_PATH):
		lines.append("[正常] Phantom Camera EditorPlugin 已启用")
	else:
		lines.append("[错误] Phantom Camera 未启用；请在 项目设置 → 插件 中启用")

	lines.append("[正常] 预设：res://addons/godo_phantom_camera/ThirdPerson/GoDoPhantomThirdPersonRig.tscn")
	return lines
