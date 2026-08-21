@tool
extends VBoxContainer

const PROJECT_CONFIG_CONTROLLER_SCRIPT := preload(
	"res://addons/godo_framework/Editor/godo_project_config_controller.gd"
)

var _controller: RefCounted


func setup(runtime_controller: RefCounted, csproj_controller: RefCounted) -> void:
	_controller = PROJECT_CONFIG_CONTROLLER_SCRIPT.new()
	_controller.initialize(runtime_controller, csproj_controller)
	add_theme_constant_override("separation", 10)

	var report := RichTextLabel.new()
	report.name = "GoDoProjectConfigReport"
	report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	report.bbcode_enabled = false
	report.selection_enabled = true
	add_child(report)

	var message := RichTextLabel.new()
	message.name = "GoDoProjectConfigMessage"
	message.custom_minimum_size.y = 48
	message.bbcode_enabled = false
	message.scroll_active = false
	message.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	add_child(message)

	var actions := HBoxContainer.new()
	actions.name = "GoDoProjectConfigActions"
	actions.add_theme_constant_override("separation", 8)
	add_child(actions)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	var check_button := Button.new()
	check_button.name = "GoDoProjectConfigCheckButton"
	check_button.text = "重新检查"
	actions.add_child(check_button)
	var repair_button := Button.new()
	repair_button.name = "GoDoCsprojRepairButton"
	repair_button.text = "修复 C# 项目配置..."
	repair_button.tooltip_text = "仅补齐 GoDo 维护的 .csproj 条件编译与 Release 裁剪规则。"
	actions.add_child(repair_button)
	var install_button := Button.new()
	install_button.name = "GoDoRuntimeInstallButton"
	install_button.text = "安装 Runtime"
	actions.add_child(install_button)
	var uninstall_button := Button.new()
	uninstall_button.name = "GoDoRuntimeUninstallButton"
	uninstall_button.text = "卸载 Runtime"
	actions.add_child(uninstall_button)

	_controller.bind_view(report, message, repair_button, install_button, uninstall_button)
	check_button.pressed.connect(_controller.check)
	repair_button.pressed.connect(csproj_controller.request_repair)
	install_button.pressed.connect(runtime_controller.install)
	uninstall_button.pressed.connect(runtime_controller.request_uninstall)


func refresh() -> void:
	_controller.refresh()


func dispose() -> void:
	if _controller != null:
		_controller.dispose()
		_controller = null
