@tool
extends RefCounted

const NORMAL_COLOR := Color("#8BD49C")
const PENDING_COLOR := Color("#AEB6C2")
const WARNING_COLOR := Color("#FFD166")
const ERROR_COLOR := Color("#FF6B6B")

var _runtime_controller: RefCounted
var _csproj_controller: RefCounted
var _report: RichTextLabel
var _message: RichTextLabel
var _repair_button: Button
var _install_button: Button
var _uninstall_button: Button


func initialize(runtime_controller: RefCounted, csproj_controller: RefCounted) -> void:
	_runtime_controller = runtime_controller
	_csproj_controller = csproj_controller
	_runtime_controller.state_changed.connect(_on_runtime_state_changed)
	_csproj_controller.state_changed.connect(_on_csproj_state_changed)


func bind_view(
	report: RichTextLabel,
	message: RichTextLabel,
	repair_button: Button,
	install_button: Button,
	uninstall_button: Button
) -> void:
	_report = report
	_message = message
	_repair_button = repair_button
	_install_button = install_button
	_uninstall_button = uninstall_button
	_runtime_controller.bind_view(report, message, install_button, uninstall_button)
	_csproj_controller.bind_view(report, message, repair_button)


func dispose() -> void:
	if (
		_runtime_controller != null
		and _runtime_controller.state_changed.is_connected(_on_runtime_state_changed)
	):
		_runtime_controller.state_changed.disconnect(_on_runtime_state_changed)
	if (
		_csproj_controller != null
		and _csproj_controller.state_changed.is_connected(_on_csproj_state_changed)
	):
		_csproj_controller.state_changed.disconnect(_on_csproj_state_changed)
	_runtime_controller = null
	_csproj_controller = null
	_report = null
	_message = null
	_repair_button = null
	_install_button = null
	_uninstall_button = null


func refresh() -> void:
	var state := _refresh_view()
	_show_message(state.advice, state.level)


func check() -> void:
	var state := _refresh_view()
	_show_message("检查已刷新。%s" % state.advice, state.level)


func _refresh_view() -> Dictionary:
	var runtime_state: Dictionary = _runtime_controller.inspect()
	var csproj_state: Dictionary = _csproj_controller.inspect()
	var status := _combined_status(runtime_state, csproj_state)
	_render_report(runtime_state, csproj_state, status)
	_repair_button.disabled = not csproj_state.can_repair
	_install_button.disabled = not _runtime_controller.can_install(runtime_state)
	_uninstall_button.disabled = not runtime_state.autoload_healthy or not runtime_state.project_config_readable
	return status


func _render_report(runtime_state: Dictionary, csproj_state: Dictionary, status: Dictionary) -> void:
	_report.clear()
	_report.add_text("当前状态：")
	_report.push_color(_level_color(status.level))
	_report.add_text(status.name)
	_report.pop()
	_report.add_text("\n\n")
	for item in runtime_state.items:
		_append_item(item.level, item.name, item.message)
	_append_item(
		0 if csproj_state.healthy else 2 if csproj_state.can_repair else 3,
		"C# 项目配置",
		"%s；%s" % [
			csproj_state.project_path.get_file() if not csproj_state.project_path.is_empty() else "项目文件未确定",
			csproj_state.detail,
		]
	)


func _append_item(level: int, item_name: String, detail: String) -> void:
	_report.push_color(_level_color(level))
	_report.add_text("[%s]" % _level_name(level))
	_report.pop()
	_report.add_text(" %s：%s\n" % [item_name, detail])


func _combined_status(runtime_state: Dictionary, csproj_state: Dictionary) -> Dictionary:
	var runtime_status: Dictionary = _runtime_controller.get_status(runtime_state)
	var csproj_level := 0 if csproj_state.healthy else 2 if csproj_state.can_repair else 3
	if runtime_status.level == 0 and runtime_state.autoload_healthy and csproj_state.healthy:
		return {
			"name": "项目配置已就绪",
			"level": 0,
			"advice": "框架、C# 项目配置和 Runtime 均已就绪。",
		}
	var advice := PackedStringArray()
	if runtime_status.level > 0 or not runtime_state.autoload_healthy:
		advice.append("Runtime：%s" % runtime_status.advice)
	if not csproj_state.healthy:
		advice.append(
			"C# 项目配置：可以使用“修复 C# 项目配置...”补齐 GoDo 维护的项目规则。"
			if csproj_state.can_repair
			else "C# 项目配置：%s" % csproj_state.detail
		)
	var level: int = maxi(runtime_status.level, csproj_level)
	return {
		"name": (
			"项目配置需要处理" if level == 3
			else "项目配置有待处理项" if level == 2
			else "项目配置可以继续"
		),
		"level": level,
		"advice": " ".join(advice),
	}


func _on_runtime_state_changed(message: String, level: int) -> void:
	_refresh_view()
	_show_message(message, level)


func _on_csproj_state_changed(message: String, color: Color) -> void:
	_refresh_view()
	_show_colored_message(message, color)


func _show_message(message: String, level: int) -> void:
	_show_colored_message(message, _level_color(level))


func _show_colored_message(message: String, color: Color) -> void:
	_message.clear()
	_message.push_paragraph(HORIZONTAL_ALIGNMENT_CENTER)
	_message.push_color(color)
	_message.add_text("提示：")
	_message.pop()
	_message.add_text(message)
	_message.pop()


func _level_color(level: int) -> Color:
	match level:
		0:
			return NORMAL_COLOR
		1:
			return PENDING_COLOR
		2:
			return WARNING_COLOR
		_:
			return ERROR_COLOR


func _level_name(level: int) -> String:
	match level:
		0:
			return "正常"
		1:
			return "待检查"
		2:
			return "警告"
		_:
			return "错误"
