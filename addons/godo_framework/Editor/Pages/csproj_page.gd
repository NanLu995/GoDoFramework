@tool
extends VBoxContainer

var _controller: RefCounted


func setup(controller: RefCounted) -> void:
	_controller = controller
	add_theme_constant_override("separation", 10)
	var description := Label.new()
	description.name = "GoDoCsprojDescription"
	description.text = "检查 GoDo 条件编译与 Release 裁剪；Godot SDK、目标框架及第三方包版本保持项目自行维护。"
	description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(description)
	var report := RichTextLabel.new()
	report.name = "GoDoCsprojReport"
	report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	report.bbcode_enabled = false
	report.selection_enabled = true
	add_child(report)
	var message := RichTextLabel.new()
	message.name = "GoDoCsprojMessage"
	message.custom_minimum_size.y = 48
	message.bbcode_enabled = false
	message.scroll_active = false
	message.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	add_child(message)
	var actions := HBoxContainer.new()
	actions.name = "GoDoCsprojActions"
	actions.add_theme_constant_override("separation", 8)
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
	repair_button.text = "修复 C# 项目配置..."
	repair_button.tooltip_text = "仅补齐 GoDo 维护的 .csproj 条件编译与 Release 裁剪规则。"
	actions.add_child(repair_button)
	_controller.bind_view(report, message, repair_button)
	refresh_button.pressed.connect(_controller.refresh)
	repair_button.pressed.connect(_controller.request_repair)


func refresh() -> void:
	_controller.refresh()
