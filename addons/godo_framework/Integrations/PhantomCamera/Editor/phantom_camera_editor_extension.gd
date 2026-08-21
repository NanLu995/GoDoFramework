@tool
extends RefCounted

const PHANTOM_PLUGIN := "phantom_camera"
const PHANTOM_PLUGIN_CONFIG := "res://addons/phantom_camera/plugin.cfg"
const SUPPORTED_VERSION := "0.11"
const ASSET_LIBRARY_URL := "https://godotengine.org/asset-library/asset/1822"
const REQUIRED_FILES := [
	"res://addons/phantom_camera/plugin.cfg",
	"res://addons/phantom_camera/plugin.gd",
	"res://addons/phantom_camera/scripts/phantom_camera/PhantomCamera3D.cs",
	"res://addons/godo_framework/Integrations/PhantomCamera/Runtime/PhantomCameraRig.cs",
	"res://addons/godo_framework/Integrations/PhantomCamera/ThirdPerson/GoDoPhantomThirdPersonRig.tscn",
]

var _context
var _page: VBoxContainer
var _confirmation: ConfirmationDialog
var _report: RichTextLabel
var _enable_button: Button
var _message_label: RichTextLabel


func activate(context) -> Error:
	_context = context
	return _context.register_embedded_page(_create_page, _refresh)


func deactivate() -> void:
	_page = null
	_confirmation = null
	_report = null
	_enable_button = null
	_message_label = null
	_context = null


func _create_page() -> Control:
	_page = VBoxContainer.new()
	_page.add_theme_constant_override("separation", 10)

	_report = RichTextLabel.new()
	_report.name = "PhantomCameraReport"
	_report.bbcode_enabled = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_page.add_child(_report)

	_message_label = RichTextLabel.new()
	_message_label.name = "PhantomCameraMessage"
	_message_label.bbcode_enabled = true
	_message_label.custom_minimum_size.y = 48
	_message_label.scroll_active = false
	_message_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_page.add_child(_message_label)

	var actions := HBoxContainer.new()
	actions.name = "PhantomCameraActions"
	actions.add_theme_constant_override("separation", 8)
	_page.add_child(actions)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	var refresh_button := Button.new()
	refresh_button.name = "PhantomCameraRefreshButton"
	refresh_button.text = "重新检查"
	actions.add_child(refresh_button)
	var source_button := Button.new()
	source_button.text = "打开 Godot 商店..."
	source_button.name = "PhantomCameraOfficialSourceButton"
	source_button.tooltip_text = ASSET_LIBRARY_URL
	actions.add_child(source_button)
	_enable_button = Button.new()
	_enable_button.text = "启用 Phantom Camera..."
	_enable_button.name = "PhantomCameraEnableButton"
	actions.add_child(_enable_button)
	refresh_button.pressed.connect(_on_refresh_pressed)
	source_button.pressed.connect(_open_official_source)
	_enable_button.pressed.connect(_request_enable)

	_confirmation = ConfirmationDialog.new()
	_confirmation.exclusive = true
	_confirmation.transient_to_focused = true
	_confirmation.title = "启用 Phantom Camera"
	_confirmation.dialog_text = "只启用已安装的第三方 Phantom Camera 编辑器插件，不修改场景、运行时配置或第三方源码。"
	_confirmation.confirmed.connect(_enable_plugin)
	_page.add_child(_confirmation)
	return _page


func _on_refresh_pressed() -> void:
	_refresh("重新检查完成。", "#8bd49c")


func _refresh(message: String = "", message_color: String = "") -> void:
	var state := _inspect_state()
	var lines := PackedStringArray()
	var configured: bool = state["files_ready"] and state["version_supported"] and state["plugin_enabled"]
	lines.append(
		"当前状态：[color=#8bd49c]已正确配置[/color]"
		if configured
		else "当前状态：[color=#ffd166]需要处理[/color]"
	)
	lines.append("")
	lines.append(_status_line("第三方与适配文件", state["files_ready"], "已找到" if state["files_ready"] else "文件不完整"))
	lines.append(_status_line("第三方版本", state["version_supported"], state["version"] if not state["version"].is_empty() else "未知"))
	lines.append("安装位置：addons/phantom_camera/（GoDo 当前验证 plugin.cfg 版本 %s）" % SUPPORTED_VERSION)
	lines.append(_status_line("第三方插件", state["plugin_enabled"], "已启用" if state["plugin_enabled"] else "未启用"))
	_report.text = "\n".join(lines)
	_enable_button.disabled = state["plugin_enabled"] or not state["can_enable"]
	if message.is_empty():
		var hint := _hint_for_state(state)
		_set_hint(str(hint.text), str(hint.color))
	else:
		_set_hint(message, message_color)


func _hint_for_state(state: Dictionary) -> Dictionary:
	if not state["files_ready"]:
		return {"text": "请先完整安装 Phantom Camera 及 GoDo 适配文件。", "color": "#ff6b6b"}
	if not state["version_supported"]:
		return {
			"text": "当前仅验证 Phantom Camera %s；其他版本需要重新完成兼容验证。" % SUPPORTED_VERSION,
			"color": "#ffd166",
		}
	if not state["plugin_enabled"]:
		return {"text": "点击“启用 Phantom Camera...”完成编辑器插件配置。", "color": "#ffd166"}
	return {"text": "Phantom Camera 已正确配置，可以使用 GoDo 相机适配。", "color": "#8bd49c"}


func _set_hint(message: String, color: String) -> void:
	_message_label.text = "[center][color=%s]提示：%s[/color][/center]" % [color, message]


func _open_official_source() -> void:
	var result := OS.shell_open(ASSET_LIBRARY_URL)
	if result == OK:
		_refresh("已交给系统浏览器打开 Godot Asset Library；下载与安装仍由开发者完成。", "#8bd49c")
	else:
		_refresh("无法打开 Godot Asset Library：%s" % error_string(result), "#ff6b6b")


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
		_refresh("当前状态无需启用，或暂时不允许自动启用。", "#ffd166")
		return
	_confirmation.popup_centered()


func _enable_plugin() -> void:
	var state := _inspect_state()
	if state["plugin_enabled"]:
		_refresh("Phantom Camera 已启用，无需重复操作。", "#8bd49c")
		return
	if not state["can_enable"]:
		_refresh("当前状态已变化，无法安全启用；请重新检查。", "#ff6b6b")
		return

	var editor_interface = _context.get_editor_interface()
	editor_interface.set_plugin_enabled(PHANTOM_PLUGIN, true)
	await editor_interface.get_base_control().get_tree().process_frame
	if editor_interface.is_plugin_enabled(PHANTOM_PLUGIN):
		_refresh("Phantom Camera 已启用；请继续执行编译、自动回归和真实镜头验证。", "#8bd49c")
	else:
		_refresh("Phantom Camera 启用失败，请检查编辑器输出。", "#ff6b6b")


func _status_line(name: String, healthy: bool, detail: String) -> String:
	var color := "#8bd49c" if healthy else "#ffd166"
	var status := "正常" if healthy else "待处理"
	return "[color=%s][%s][/color] %s：%s" % [color, status, name, detail]
