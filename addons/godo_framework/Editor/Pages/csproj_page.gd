@tool
extends VBoxContainer

var _controller: RefCounted


func setup(controller: RefCounted) -> void:
	_controller = controller
	add_theme_constant_override("separation", 12)
	var description := Label.new()
	description.text = "检查 GoDo 条件编译与 Release 裁剪；Godot SDK、目标框架及第三方包版本保持项目自行维护。"
	description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(description)
	var report := RichTextLabel.new()
	report.name = "GoDoCsprojReport"
	report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	report.selection_enabled = true
	add_child(report)
	var message := RichTextLabel.new()
	message.name = "GoDoCsprojMessage"
	message.custom_minimum_size.y = 54
	message.scroll_active = false
	add_child(message)
	var actions := HBoxContainer.new()
	add_child(actions)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	var refresh_button := Button.new()
	refresh_button.name = "GoDoCsprojRefreshButton"
	refresh_button.text = "重新检查"
	actions.add_child(refresh_button)
	var repair_button := Button.new()
	repair_button.name = "GoDoCsprojRepairButton"
	repair_button.text = "修复配置..."
	actions.add_child(repair_button)
	_controller.bind_view(report, message, repair_button)
	refresh_button.pressed.connect(_controller.refresh)
	repair_button.pressed.connect(_controller.request_repair)


func refresh() -> void:
	_controller.refresh()
