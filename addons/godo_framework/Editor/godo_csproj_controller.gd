@tool
extends RefCounted

const MANAGER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_csproj_manager.gd")

var _plugin: EditorPlugin
var _report: RichTextLabel
var _message: RichTextLabel
var _repair_button: Button
var _confirmation: ConfirmationDialog


func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
	_confirmation = ConfirmationDialog.new()
	_confirmation.name = "GoDoCsprojRepairConfirmation"
	_confirmation.title = "修复 GoDo 项目配置"
	_confirmation.ok_button_text = "创建备份并修复"
	_confirmation.confirmed.connect(_repair)
	_plugin.get_editor_interface().get_base_control().add_child(_confirmation)


func dispose() -> void:
	if is_instance_valid(_confirmation):
		_confirmation.queue_free()


func bind_view(report: RichTextLabel, message: RichTextLabel, repair_button: Button) -> void:
	_report = report
	_message = message
	_repair_button = repair_button


func refresh() -> void:
	var state := MANAGER_SCRIPT.new().inspect()
	_report.text = "项目文件：%s\n检查结果：%s" % [state["project_path"].get_file() if not state["project_path"].is_empty() else "未确定", state["detail"]]
	_repair_button.disabled = not state["can_repair"]


func request_repair() -> void:
	var state := MANAGER_SCRIPT.new().inspect()
	if not state["can_repair"]:
		refresh()
		_message.text = "项目状态已变化，当前不能安全修复。"
		return
	var preview := PackedStringArray()
	for block in state["additions"]:
		preview.append(str(block))
	_confirmation.dialog_text = "将修改：%s\n\n新增以下配置：\n\n%s\n\n写入前创建非覆盖 .godo-backup；不会修改 SDK、TargetFramework、Android 配置，也不会执行 restore/build。" % [state["project_path"].get_file(), "\n\n".join(preview)]
	_confirmation.popup_centered()


func _repair() -> void:
	var result := MANAGER_SCRIPT.new().repair()
	refresh()
	_message.text = result["detail"] + ("\n备份：%s" % result["backup_path"] if not result["backup_path"].is_empty() else "")
