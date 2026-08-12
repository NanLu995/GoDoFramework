@tool
extends VBoxContainer

var _controller: RefCounted


func setup(controller: RefCounted) -> void:
	_controller = controller
	add_theme_constant_override("separation", 12)
	var description := Label.new()
	description.text = "检查框架版本、C# 编译状态和 GoDoRuntime Autoload。"
	description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(description)

	var report := RichTextLabel.new()
	report.name = "GoDoRuntimeReport"
	report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	report.bbcode_enabled = false
	report.selection_enabled = true
	add_child(report)

	var message := RichTextLabel.new()
	message.name = "GoDoRuntimeMessage"
	message.custom_minimum_size.y = 44
	message.bbcode_enabled = false
	message.scroll_active = false
	message.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	add_child(message)

	var actions := HBoxContainer.new()
	actions.name = "GoDoRuntimeActions"
	actions.add_theme_constant_override("separation", 8)
	add_child(actions)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	var check_button := Button.new()
	check_button.name = "GoDoRuntimeCheckButton"
	check_button.text = "重新检查"
	actions.add_child(check_button)
	var install_button := Button.new()
	install_button.name = "GoDoRuntimeInstallButton"
	install_button.text = "安装 Runtime"
	actions.add_child(install_button)
	var uninstall_button := Button.new()
	uninstall_button.name = "GoDoRuntimeUninstallButton"
	uninstall_button.text = "卸载 Runtime"
	actions.add_child(uninstall_button)

	_controller.bind_view(report, message, install_button, uninstall_button)
	check_button.pressed.connect(_controller.check)
	install_button.pressed.connect(_controller.install)
	uninstall_button.pressed.connect(_controller.request_uninstall)


func refresh() -> void:
	_controller.refresh()
