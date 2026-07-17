@tool
extends RefCounted

const PHANTOM_PLUGIN := "phantom_camera"
const PHANTOM_PLUGIN_CONFIG := "res://addons/phantom_camera/plugin.cfg"
const SUPPORTED_VERSION := "0.11"
const REQUIRED_FILES := [
	"res://addons/phantom_camera/plugin.cfg",
	"res://addons/phantom_camera/plugin.gd",
	"res://addons/phantom_camera/scripts/phantom_camera/PhantomCamera3D.cs",
	"res://addons/godo_framework/Integrations/PhantomCamera/Runtime/PhantomCameraRig.cs",
	"res://addons/godo_framework/Integrations/PhantomCamera/ThirdPerson/GoDoPhantomThirdPersonRig.tscn",
]

var _context
var _dialog: AcceptDialog
var _confirmation: ConfirmationDialog
var _report: RichTextLabel
var _enable_button: Button
var _message_label: Label


func activate(context) -> Error:
	_context = context
	return _context.add_menu_action("status", "Phantom Camera 设置...", _open_dialog)


func deactivate() -> void:
	if is_instance_valid(_dialog):
		_dialog.queue_free()
	_dialog = null
	_confirmation = null
	_context = null


func _open_dialog() -> void:
	if not is_instance_valid(_dialog):
		_create_dialogs()
	_message_label.text = ""
	_refresh()
	_dialog.popup_centered(Vector2i(680, 400))


func _create_dialogs() -> void:
	_dialog = AcceptDialog.new()
	_dialog.title = "GoDo Phantom Camera 设置"
	_dialog.ok_button_text = "关闭"
	_dialog.min_size = Vector2i(680, 400)
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
	_report.name = "PhantomCameraReport"
	_report.bbcode_enabled = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_report)

	_message_label = Label.new()
	_message_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(_message_label)

	var refresh_button := _dialog.add_button("重新检查", true)
	_enable_button = _dialog.add_button("启用 Phantom Camera...", true)
	_enable_button.name = "PhantomCameraEnableButton"
	refresh_button.pressed.connect(_refresh)
	_enable_button.pressed.connect(_request_enable)
	_context.get_editor_interface().get_base_control().add_child(_dialog)

	_confirmation = ConfirmationDialog.new()
	_confirmation.title = "启用 Phantom Camera"
	_confirmation.dialog_text = "只启用已安装的第三方 Phantom Camera 编辑器插件，不修改场景、运行时配置或第三方源码。"
	_confirmation.confirmed.connect(_enable_plugin)
	_dialog.add_child(_confirmation)


func _refresh() -> void:
	var state := _inspect_state()
	var lines := PackedStringArray()
	lines.append("[font_size=20]Phantom Camera 集成状态[/font_size]")
	lines.append("")
	lines.append(_status_line("第三方与适配文件", state["files_ready"], "已找到" if state["files_ready"] else "文件不完整"))
	lines.append(_status_line("第三方版本", state["version_supported"], state["version"] if not state["version"].is_empty() else "未知"))
	lines.append(_status_line("第三方插件", state["plugin_enabled"], "已启用" if state["plugin_enabled"] else "未启用"))
	if not state["version_supported"] and not state["version"].is_empty():
		lines.append("")
		lines.append("[color=#ffd166]当前仅验证 Phantom Camera %s；其他版本必须先完成编译、回归和真实镜头验证。[/color]" % SUPPORTED_VERSION)
	_report.text = "\n".join(lines)
	_enable_button.disabled = state["plugin_enabled"] or not state["can_enable"]


func _inspect_state() -> Dictionary:
	var files_ready := true
	for path in REQUIRED_FILES:
		if not FileAccess.file_exists(path):
			files_ready = false
			break
	var version := _read_plugin_version()
	var version_supported := version == SUPPORTED_VERSION
	var plugin_enabled: bool = _context.get_editor_interface().is_plugin_enabled(PHANTOM_PLUGIN)
	return {
		"files_ready": files_ready,
		"version": version,
		"version_supported": version_supported,
		"plugin_enabled": plugin_enabled,
		"can_enable": files_ready and version_supported,
	}


func _read_plugin_version() -> String:
	var config := ConfigFile.new()
	if config.load(PHANTOM_PLUGIN_CONFIG) != OK:
		return ""
	return str(config.get_value("plugin", "version", "")).strip_edges()


func _request_enable() -> void:
	var state := _inspect_state()
	if state["plugin_enabled"] or not state["can_enable"]:
		_refresh()
		return
	_confirmation.popup_centered()


func _enable_plugin() -> void:
	var state := _inspect_state()
	if state["plugin_enabled"]:
		_message_label.text = "Phantom Camera 已启用，无需重复操作。"
		_refresh()
		return
	if not state["can_enable"]:
		_message_label.text = "当前状态已变化，无法安全启用；请重新检查。"
		_refresh()
		return

	var editor_interface = _context.get_editor_interface()
	editor_interface.set_plugin_enabled(PHANTOM_PLUGIN, true)
	await editor_interface.get_base_control().get_tree().process_frame
	if editor_interface.is_plugin_enabled(PHANTOM_PLUGIN):
		_message_label.text = "Phantom Camera 已启用。请继续执行编译、自动回归和真实镜头验证。"
	else:
		_message_label.text = "Phantom Camera 启用失败，请检查编辑器输出。"
	_refresh()


func _status_line(name: String, healthy: bool, detail: String) -> String:
	var color := "#8bd49c" if healthy else "#ffd166"
	var status := "正常" if healthy else "待处理"
	return "[color=%s][%s][/color] %s：%s" % [color, status, name, detail]
