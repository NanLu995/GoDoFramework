@tool
extends RefCounted

const PROJECT_INSPECTOR_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_inspector.gd")
const PROJECT_INSTALLER_SCRIPT := preload("res://addons/godo_framework/Integrations/FrifloEcs/Editor/friflo_ecs_project_installer.gd")
const OFFICIAL_PACKAGE_URL := "https://www.nuget.org/packages/Friflo.Engine.ECS/3.6.0"

var _context
var _page: VBoxContainer
var _report: RichTextLabel
var _message_label: RichTextLabel
var _install_button: Button
var _confirmation: ConfirmationDialog


func activate(context) -> Error:
	_context = context
	return _context.register_embedded_page(_create_page, _refresh)


func deactivate() -> void:
	_page = null
	_confirmation = null
	_report = null
	_message_label = null
	_install_button = null
	_context = null


func _create_page() -> Control:
	_page = VBoxContainer.new()
	_page.add_theme_constant_override("separation", 10)

	_report = RichTextLabel.new()
	_report.name = "FrifloEcsDependencyReport"
	_report.bbcode_enabled = true
	_report.fit_content = false
	_report.scroll_active = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_page.add_child(_report)

	_message_label = RichTextLabel.new()
	_message_label.name = "FrifloEcsMessage"
	_message_label.bbcode_enabled = true
	_message_label.custom_minimum_size.y = 48
	_message_label.scroll_active = false
	_message_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_page.add_child(_message_label)

	var actions := HBoxContainer.new()
	actions.name = "FrifloEcsActions"
	actions.add_theme_constant_override("separation", 8)
	_page.add_child(actions)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	var refresh_button := Button.new()
	refresh_button.text = "重新检查"
	refresh_button.name = "FrifloEcsRefreshButton"
	actions.add_child(refresh_button)
	refresh_button.pressed.connect(_on_refresh_pressed)
	var source_button := Button.new()
	source_button.text = "查看 NuGet 包..."
	source_button.name = "FrifloEcsOfficialSourceButton"
	source_button.tooltip_text = OFFICIAL_PACKAGE_URL
	actions.add_child(source_button)
	source_button.pressed.connect(_open_official_source)
	_install_button = Button.new()
	_install_button.text = "添加依赖..."
	_install_button.name = "FrifloEcsInstallButton"
	actions.add_child(_install_button)
	_install_button.pressed.connect(_request_install)

	_confirmation = ConfirmationDialog.new()
	_confirmation.exclusive = true
	_confirmation.transient_to_focused = true
	_confirmation.title = "添加 Friflo ECS 依赖"
	_confirmation.dialog_text = _build_confirmation_text("根目录唯一的 .csproj")
	_confirmation.confirmed.connect(_perform_install)
	_page.add_child(_confirmation)
	return _page


func _on_refresh_pressed() -> void:
	_refresh("重新检查完成。", "#8bd49c")


func _refresh(message: String = "", message_color: String = "") -> void:
	var state: Dictionary = PROJECT_INSPECTOR_SCRIPT.new().inspect()
	var color := "#8bd49c" if state["healthy"] else "#ffd166"
	var status := "已就绪" if state["healthy"] else "需要处理"
	var lines := PackedStringArray([
		"当前状态：[color=%s]%s[/color]" % [color, status],
		"",
		"项目文件：%s" % (state["project_path"].get_file() if not state["project_path"].is_empty() else "未确定"),
		"依赖版本：%s" % (state["version"] if not state["version"].is_empty() else "未确定"),
		"安装方式：根目录 .csproj 添加 Friflo.Engine.ECS 3.6.0，再由 restore 下载程序集",
		"检查结果：%s" % state["detail"],
	])
	_report.text = "\n".join(lines)
	_install_button.disabled = not state["can_install"]
	if message.is_empty():
		_set_hint(_hint_for_state(state), "#8bd49c" if state["healthy"] else "#ffd166")
	else:
		_set_hint(message, message_color)


func _hint_for_state(state: Dictionary) -> String:
	if state["healthy"]:
		return "依赖已就绪；检查不执行 restore，中央包管理始终只读。"
	if state["can_install"]:
		return "可在确认后添加依赖；检查不执行 restore，中央包管理始终只读。"
	return "当前项目保持只读；检查不执行 restore，中央包管理始终只读。"


func _set_hint(message: String, color: String) -> void:
	_message_label.text = "[center][color=%s]提示：%s[/color][/center]" % [color, message]


func _open_official_source() -> void:
	var result := OS.shell_open(OFFICIAL_PACKAGE_URL)
	if result == OK:
		_refresh("已交给系统浏览器打开 NuGet 官方页面；下载和 restore 仍由开发者完成。", "#8bd49c")
	else:
		_refresh("无法打开 NuGet 官方页面：%s" % error_string(result), "#ff6b6b")


func _request_install() -> void:
	var state: Dictionary = PROJECT_INSPECTOR_SCRIPT.new().inspect()
	if not state["can_install"]:
		_refresh("当前状态无需添加，或暂时不允许自动添加。", "#ffd166")
		return
	_confirmation.dialog_text = _build_confirmation_text(state["project_path"].get_file())
	_confirmation.popup_centered()


func _build_confirmation_text(project_file: String) -> String:
	return (
		"将修改：%s\n\n" % project_file
		+ "<PackageReference Include=\"Friflo.Engine.ECS\" Version=\"3.6.0\" />\n\n"
		+ "写入前会在同目录创建不覆盖旧文件的 .godo-backup 备份；已有备份时递增编号。\n"
		+ "不会执行 restore 或 build。"
	)


func _perform_install() -> void:
	var result: Dictionary = PROJECT_INSTALLER_SCRIPT.new().install()
	var color := "#8bd49c" if result["success"] else "#ff6b6b"
	var backup := ""
	if not result["backup_path"].is_empty():
		backup = "\n备份：%s" % result["backup_path"]
	_refresh("%s%s" % [result["detail"], backup], color)
