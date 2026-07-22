@tool
extends RefCounted

const GUIDE_PLUGIN := "guideCS/guide"
const GUIDE_CS_PLUGIN := "guideCS"
const GUIDE_AUTOLOAD_NAME := "GUIDE"
const GUIDE_CS_AUTOLOAD_NAME := "GuideCs"
const GODO_AUTOLOAD_NAME := "GoDoRuntime"
const GUIDE_AUTOLOAD_PATH := "res://addons/guideCS/guide/guide.gd"
const GUIDE_CS_AUTOLOAD_PATH := "res://addons/guideCS/Guide.cs"
const GODO_AUTOLOAD_PATH := "res://addons/godo_framework/Core/GoDoRuntime.tscn"
const REQUIRED_GLOBAL_CLASS := "GUIDEActionMapping"

var _dialog: AcceptDialog
var _confirmation: ConfirmationDialog
var _report: RichTextLabel
var _repair_button: Button
var _message_label: RichTextLabel
var _context


func activate(context) -> Error:
	_context = context
	return _context.add_menu_action("settings", "输入映射配置 (GUIDE Input Settings)...", _open_dialog)


func deactivate() -> void:
	if is_instance_valid(_dialog):
		_dialog.queue_free()
	_dialog = null
	_confirmation = null
	_context = null


func _create_dialogs() -> void:
	_dialog = AcceptDialog.new()
	_dialog.title = "GoDo GUIDE Input 设置"
	_dialog.ok_button_text = "关闭"
	_dialog.min_size = Vector2i(720, 460)
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
	_report.name = "GuideInputReport"
	_report.bbcode_enabled = true
	_report.fit_content = false
	_report.scroll_active = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_report)

	_message_label = RichTextLabel.new()
	_message_label.name = "GuideInputMessage"
	_message_label.bbcode_enabled = true
	_message_label.custom_minimum_size.y = 44
	_message_label.scroll_active = false
	_message_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	content.add_child(_message_label)

	var refresh_button := _dialog.add_button("重新检查", true)
	_repair_button = _dialog.add_button("安装 / 修复顺序...", true)
	_repair_button.name = "GuideInputRepairButton"
	refresh_button.pressed.connect(_refresh)
	_repair_button.pressed.connect(_request_repair)
	_context.get_editor_interface().get_base_control().add_child(_dialog)

	_confirmation = ConfirmationDialog.new()
	_confirmation.title = "安装 / 修复 GUIDE Input"
	_confirmation.dialog_text = (
		"将依次启用基础 GUIDE 与 GUIDE-CSharp，并把相关 Autoload 调整为：\n\n"
		+ "GUIDE → GuideCs → GoDoRuntime\n\n"
		+ "只修改项目插件与 Autoload 配置，不修改第三方源码。"
	)
	_confirmation.confirmed.connect(_perform_repair)
	_dialog.add_child(_confirmation)


func _open_dialog() -> void:
	if not is_instance_valid(_dialog):
		_create_dialogs()
	_refresh()
	_dialog.popup_centered(Vector2i(720, 460))


func _refresh(message: String = "", message_color: String = "") -> void:
	var state := _inspect_state()
	_report.text = _format_report(state)
	_repair_button.disabled = not state["can_repair"] or not state["needs_repair"]
	if message.is_empty():
		var hint := _hint_for_state(state)
		_set_hint(str(hint.text), str(hint.color))
	else:
		_set_hint(message, message_color)


func _inspect_state() -> Dictionary:
	var editor_interface = _context.get_editor_interface()
	var filesystem = editor_interface.get_resource_filesystem()
	var scanning: bool = filesystem != null and filesystem.is_scanning()
	var files_ready := (
		FileAccess.file_exists(GUIDE_AUTOLOAD_PATH)
		and FileAccess.file_exists(GUIDE_CS_AUTOLOAD_PATH)
		and FileAccess.file_exists("res://addons/guideCS/guide/plugin.cfg")
		and FileAccess.file_exists("res://addons/guideCS/plugin.cfg")
	)
	var guide_path := _autoload_path(GUIDE_AUTOLOAD_NAME)
	var guide_cs_path := _autoload_path(GUIDE_CS_AUTOLOAD_NAME)
	var godo_path := _autoload_path(GODO_AUTOLOAD_NAME)
	var has_conflict := (
		(not guide_path.is_empty() and guide_path != GUIDE_AUTOLOAD_PATH)
		or (not guide_cs_path.is_empty() and guide_cs_path != GUIDE_CS_AUTOLOAD_PATH)
		or (not godo_path.is_empty() and godo_path != GODO_AUTOLOAD_PATH)
	)
	var class_ready := _has_global_class(REQUIRED_GLOBAL_CLASS)
	var guide_plugin_enabled: bool = editor_interface.is_plugin_enabled(GUIDE_PLUGIN)
	var guide_cs_plugin_enabled: bool = editor_interface.is_plugin_enabled(GUIDE_CS_PLUGIN)
	var order_ready := _is_autoload_order_ready()
	var runtime_present := godo_path == GODO_AUTOLOAD_PATH
	var needs_repair: bool = (
		not guide_plugin_enabled
		or not guide_cs_plugin_enabled
		or guide_path != GUIDE_AUTOLOAD_PATH
		or guide_cs_path != GUIDE_CS_AUTOLOAD_PATH
		or not order_ready
	)
	return {
		"scanning": scanning,
		"files_ready": files_ready,
		"class_ready": class_ready,
		"guide_plugin_enabled": guide_plugin_enabled,
		"guide_cs_plugin_enabled": guide_cs_plugin_enabled,
		"guide_path": guide_path,
		"guide_cs_path": guide_cs_path,
		"godo_path": godo_path,
		"runtime_present": runtime_present,
		"order_ready": order_ready,
		"has_conflict": has_conflict,
		"needs_repair": needs_repair,
		"can_repair": files_ready and not scanning and class_ready and not has_conflict,
	}


func _format_report(state: Dictionary) -> String:
	var lines := PackedStringArray()
	var configured: bool = not state["needs_repair"] and state["runtime_present"] and not state["has_conflict"]
	lines.append(
		"当前状态：[color=#8bd49c]已正确配置[/color]"
		if configured
		else "当前状态：[color=#ffd166]需要处理[/color]"
	)
	lines.append("")
	lines.append(_status_line("第三方文件", state["files_ready"], "已找到固定目录" if state["files_ready"] else "缺少 addons/guideCS/ 完整文件"))
	lines.append(_status_line("文件扫描", not state["scanning"], "已完成" if not state["scanning"] else "仍在扫描，请等待"))
	lines.append(_status_line("全局脚本类型", state["class_ready"], REQUIRED_GLOBAL_CLASS if state["class_ready"] else "%s 尚未注册" % REQUIRED_GLOBAL_CLASS))
	lines.append(_status_line("基础 GUIDE 插件", state["guide_plugin_enabled"], "已启用" if state["guide_plugin_enabled"] else "未启用"))
	lines.append(_status_line("GUIDE-CSharp 插件", state["guide_cs_plugin_enabled"], "已启用" if state["guide_cs_plugin_enabled"] else "未启用或 C# 尚未编译"))
	lines.append(_autoload_line(GUIDE_AUTOLOAD_NAME, state["guide_path"], GUIDE_AUTOLOAD_PATH))
	lines.append(_autoload_line(GUIDE_CS_AUTOLOAD_NAME, state["guide_cs_path"], GUIDE_CS_AUTOLOAD_PATH))
	lines.append(_autoload_line(GODO_AUTOLOAD_NAME, state["godo_path"], GODO_AUTOLOAD_PATH))
	lines.append(_status_line("Autoload 顺序", state["order_ready"], "GUIDE → GuideCs → GoDoRuntime" if state["order_ready"] else "尚未满足要求"))
	return "\n".join(lines)


func _hint_for_state(state: Dictionary) -> Dictionary:
	if state["has_conflict"]:
		return {"text": "检测到同名 Autoload 冲突，请先手动处理。", "color": "#ff6b6b"}
	if not state["files_ready"]:
		return {"text": "请先完整安装 addons/guideCS/，然后重新检查。", "color": "#ff6b6b"}
	if state["scanning"]:
		return {"text": "Godot 正在扫描文件，请等待完成后重新检查。", "color": "#ffd166"}
	if not state["class_ready"]:
		return {"text": "请等待脚本类扫描完成，并确认 C# 已成功编译。", "color": "#ffd166"}
	if state["needs_repair"]:
		return {"text": "点击“安装 / 修复顺序...”完成 GUIDE Input 配置。", "color": "#ffd166"}
	if not state["runtime_present"]:
		return {"text": "GUIDE Input 已就绪；请通过 GoDo 配置安装 Runtime。", "color": "#ffd166"}
	return {"text": "GUIDE Input 已正确配置，无需重复安装。", "color": "#8bd49c"}


func _set_hint(message: String, color: String) -> void:
	_message_label.text = "[center][color=%s]提示：%s[/color][/center]" % [color, message]


func _status_line(name: String, healthy: bool, detail: String) -> String:
	var color := "#8bd49c" if healthy else "#ffd166"
	var status := "正常" if healthy else "待处理"
	return "[color=%s][%s][/color] %s：%s" % [color, status, name, detail]


func _autoload_line(name: String, actual_path: String, expected_path: String) -> String:
	if actual_path == expected_path:
		return _status_line("Autoload %s" % name, true, actual_path)
	if actual_path.is_empty():
		return _status_line("Autoload %s" % name, false, "未安装")
	return "[color=#ff6b6b][冲突][/color] Autoload %s：%s" % [name, actual_path]


func _request_repair() -> void:
	var state := _inspect_state()
	if not state["can_repair"] or not state["needs_repair"]:
		_refresh("当前状态无需修复，或暂时不允许自动修复。", "#ffd166")
		return
	_confirmation.popup_centered()


func _perform_repair() -> void:
	var state := _inspect_state()
	if not state["can_repair"] or not state["needs_repair"]:
		_refresh("当前状态已变化，无法安全修复；请重新检查。", "#ff6b6b")
		return

	var editor_interface = _context.get_editor_interface()
	if not state["guide_plugin_enabled"]:
		editor_interface.set_plugin_enabled(GUIDE_PLUGIN, true)
	if not state["guide_cs_plugin_enabled"]:
		editor_interface.set_plugin_enabled(GUIDE_CS_PLUGIN, true)
	await editor_interface.get_base_control().get_tree().process_frame

	if not editor_interface.is_plugin_enabled(GUIDE_PLUGIN):
		_refresh("基础 GUIDE 插件启用失败，请检查编辑器输出。", "#ff6b6b")
		return
	if not editor_interface.is_plugin_enabled(GUIDE_CS_PLUGIN):
		_refresh("GUIDE-CSharp 插件启用失败，请先编译 C#，然后重试。", "#ff6b6b")
		return

	state = _inspect_state()
	if state["has_conflict"]:
		_refresh("插件启用后检测到同名 Autoload 冲突，已停止修复。", "#ff6b6b")
		return
	if (
		state["guide_path"] != GUIDE_AUTOLOAD_PATH
		or state["guide_cs_path"] != GUIDE_CS_AUTOLOAD_PATH
		or not state["order_ready"]
	):
		_rewrite_autoload_order(state["runtime_present"])
	_refresh("安装与顺序修复完成；首次安装后建议重启编辑器。", "#8bd49c")


func _rewrite_autoload_order(runtime_present: bool) -> void:
	_remove_autoload_if_present(GUIDE_AUTOLOAD_NAME)
	_remove_autoload_if_present(GUIDE_CS_AUTOLOAD_NAME)
	if runtime_present:
		_remove_autoload_if_present(GODO_AUTOLOAD_NAME)

	_context.add_autoload_singleton(GUIDE_AUTOLOAD_NAME, GUIDE_AUTOLOAD_PATH)
	_context.add_autoload_singleton(GUIDE_CS_AUTOLOAD_NAME, GUIDE_CS_AUTOLOAD_PATH)
	if runtime_present:
		_context.add_autoload_singleton(GODO_AUTOLOAD_NAME, GODO_AUTOLOAD_PATH)


func _remove_autoload_if_present(name: String) -> void:
	if ProjectSettings.has_setting("autoload/%s" % name):
		_context.remove_autoload_singleton(name)


func _autoload_path(name: String) -> String:
	var locator := str(ProjectSettings.get_setting("autoload/%s" % name, "")).trim_prefix("*")
	if not locator.begins_with("uid://"):
		return locator
	for resource_path in [GUIDE_AUTOLOAD_PATH, GUIDE_CS_AUTOLOAD_PATH, GODO_AUTOLOAD_PATH]:
		if _read_resource_uid(resource_path) == locator:
			return resource_path
	return locator


func _read_resource_uid(resource_path: String) -> String:
	var uid_file := FileAccess.open(resource_path + ".uid", FileAccess.READ)
	return "" if uid_file == null else uid_file.get_as_text().strip_edges()


func _is_autoload_order_ready() -> bool:
	var config := ConfigFile.new()
	if config.load("res://project.godot") != OK or not config.has_section("autoload"):
		return false
	var names := Array(config.get_section_keys("autoload"))
	var guide_index := names.find(GUIDE_AUTOLOAD_NAME)
	var guide_cs_index := names.find(GUIDE_CS_AUTOLOAD_NAME)
	var godo_index := names.find(GODO_AUTOLOAD_NAME)
	if guide_index < 0 or guide_cs_index < 0 or guide_index >= guide_cs_index:
		return false
	return godo_index < 0 or guide_cs_index < godo_index


func _has_global_class(class_name_value: String) -> bool:
	for info in ProjectSettings.get_global_class_list():
		if str(info.get("class", "")) == class_name_value:
			return true
	return false
