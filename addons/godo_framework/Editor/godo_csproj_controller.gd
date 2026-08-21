@tool
extends RefCounted

signal state_changed(message: String, color: Color)

const MANAGER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_csproj_manager.gd")
const NORMAL_COLOR := Color("#8BD49C")
const WARNING_COLOR := Color("#FFD166")
const ERROR_COLOR := Color("#FF6B6B")

var _plugin: EditorPlugin
var _report: RichTextLabel
var _message: RichTextLabel
var _repair_button: Button
var _confirmation: ConfirmationDialog


func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
	_confirmation = ConfirmationDialog.new()
	_confirmation.name = "GoDoCsprojRepairConfirmation"
	_confirmation.exclusive = true
	_confirmation.transient_to_focused = true
	_confirmation.title = "修复 GoDo C# 项目配置"
	_confirmation.ok_button_text = "创建备份并修复"
	_confirmation.confirmed.connect(_repair)
	_plugin.get_editor_interface().get_base_control().add_child(_confirmation)


func set_window_parent(window_parent: Window) -> void:
	_confirmation.reparent(window_parent)


func dispose() -> void:
	if is_instance_valid(_confirmation):
		_confirmation.queue_free()
	_plugin = null
	_report = null
	_message = null
	_repair_button = null
	_confirmation = null


func bind_view(report: RichTextLabel, message: RichTextLabel, repair_button: Button) -> void:
	_report = report
	_message = message
	_repair_button = repair_button


func refresh() -> void:
	var state := inspect()
	_render_report(state)
	_repair_button.disabled = not state["can_repair"]


func inspect() -> Dictionary:
	return MANAGER_SCRIPT.new().inspect()


func request_repair() -> void:
	var state := MANAGER_SCRIPT.new().inspect()
	if not state["can_repair"]:
		refresh()
		var message := "项目状态已变化，当前不能安全修复。"
		_show_message(message, WARNING_COLOR)
		state_changed.emit(message, WARNING_COLOR)
		return
	var preview := PackedStringArray()
	for block in state["additions"]:
		preview.append(str(block))
	_confirmation.dialog_text = "将修改：%s\n\n新增以下配置：\n\n%s\n\n写入前创建非覆盖 .godo-backup；不会修改 SDK、TargetFramework、Android 配置，也不会执行 restore/build。" % [state["project_path"].get_file(), "\n\n".join(preview)]
	_confirmation.popup_centered()


func _repair() -> void:
	var result := MANAGER_SCRIPT.new().repair()
	refresh()
	var message: String = result["detail"] + (
		"\n备份：%s" % result["backup_path"] if not result["backup_path"].is_empty() else ""
	)
	var color := NORMAL_COLOR if result["success"] else ERROR_COLOR
	_show_message(message, color)
	state_changed.emit(message, color)


func _render_report(state: Dictionary) -> void:
	_report.clear()
	var status_name := "已就绪" if state["healthy"] else "可以修复" if state["can_repair"] else "需要人工处理"
	var status_color := NORMAL_COLOR if state["healthy"] else WARNING_COLOR if state["can_repair"] else ERROR_COLOR
	_report.add_text("当前状态：")
	_report.push_color(status_color)
	_report.add_text(status_name)
	_report.pop()
	_report.add_text("\n\n项目文件：%s\n检查结果：%s" % [
		state["project_path"].get_file() if not state["project_path"].is_empty() else "未确定",
		state["detail"],
	])


func _show_message(message: String, color: Color) -> void:
	_message.clear()
	_message.push_paragraph(HORIZONTAL_ALIGNMENT_CENTER)
	_message.push_color(color)
	_message.add_text("提示：")
	_message.pop()
	_message.add_text(message)
	_message.pop()
