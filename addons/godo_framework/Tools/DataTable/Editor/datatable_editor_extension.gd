@tool
extends RefCounted

const TOOL_PATH := "res://addons/godo_framework/Tools/DataTable/godo_datatable.py"
const DEFAULT_BUILD_CONFIG := "res://DataTables/datatable.build.json"
const SETTINGS_SECTION := "godo_framework/datatable"
const CONFIG_METADATA_KEY := "build_config_path"
const PYTHON_SETTING := "godo_framework/datatable/python_executable"
const MAX_OUTPUT_CHARACTERS := 65536

var _context
var _dialog: AcceptDialog
var _generate_confirmation: ConfirmationDialog
var _config_file_dialog: EditorFileDialog
var _python_file_dialog: EditorFileDialog
var _config_input: LineEdit
var _python_input: LineEdit
var _table_selector: OptionButton
var _report: RichTextLabel
var _check_button: Button
var _generate_button: Button
var _generate_selected_button: Button
var _poll_timer: Timer
var _thread: Thread
var _running := false
var _detected_python := ""
var _pending_table := ""


func activate(context) -> Error:
	_context = context
	return _context.add_menu_action("open", "DataTable...", _open_dialog)


func deactivate() -> void:
	if is_instance_valid(_poll_timer):
		_poll_timer.stop()
	if _thread != null and _thread.is_started():
		_thread.wait_to_finish()
	_thread = null
	if is_instance_valid(_dialog):
		_dialog.queue_free()
	_dialog = null
	_generate_confirmation = null
	_config_file_dialog = null
	_python_file_dialog = null
	_context = null


func _open_dialog() -> void:
	if not is_instance_valid(_dialog):
		_create_dialog()
	_load_editor_settings()
	_refresh_status()
	_dialog.popup_centered(Vector2i(820, 560))


func _create_dialog() -> void:
	_dialog = AcceptDialog.new()
	_dialog.title = "GoDo DataTable"
	_dialog.ok_button_text = "关闭"
	_dialog.min_size = Vector2i(820, 560)
	_dialog.get_label().hide()

	var content := VBoxContainer.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -64
	content.add_theme_constant_override("separation", 8)
	_dialog.add_child(content)

	content.add_child(_create_label("Build Config"))
	var config_row := HBoxContainer.new()
	content.add_child(config_row)
	_config_input = LineEdit.new()
	_config_input.name = "DataTableBuildConfigInput"
	_config_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_config_input.placeholder_text = DEFAULT_BUILD_CONFIG
	_config_input.text_changed.connect(_on_input_changed)
	config_row.add_child(_config_input)
	var config_browse := Button.new()
	config_browse.text = "浏览..."
	config_browse.pressed.connect(_open_config_file_dialog)
	config_row.add_child(config_browse)

	content.add_child(_create_label("Python 3.10+（留空时自动检测 python3 / python）"))
	var python_row := HBoxContainer.new()
	content.add_child(python_row)
	_python_input = LineEdit.new()
	_python_input.name = "DataTablePythonInput"
	_python_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_python_input.placeholder_text = "自动检测"
	_python_input.text_changed.connect(_on_input_changed)
	python_row.add_child(_python_input)
	var python_browse := Button.new()
	python_browse.text = "浏览..."
	python_browse.pressed.connect(_open_python_file_dialog)
	python_row.add_child(python_browse)

	content.add_child(_create_label("单表生成"))
	_table_selector = OptionButton.new()
	_table_selector.name = "DataTableTableSelector"
	_table_selector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_child(_table_selector)

	_report = RichTextLabel.new()
	_report.name = "DataTableReport"
	_report.bbcode_enabled = false
	_report.selection_enabled = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_report)

	var refresh_button := _dialog.add_button("刷新状态", true)
	_check_button = _dialog.add_button("检查全部", true)
	_check_button.name = "DataTableCheckButton"
	_generate_button = _dialog.add_button("生成全部...", true)
	_generate_button.name = "DataTableGenerateButton"
	_generate_selected_button = _dialog.add_button("生成选中表...", true)
	_generate_selected_button.name = "DataTableGenerateSelectedButton"
	refresh_button.pressed.connect(_refresh_status)
	_check_button.pressed.connect(_request_check)
	_generate_button.pressed.connect(_request_generate)
	_generate_selected_button.pressed.connect(_request_generate_selected)

	_generate_confirmation = ConfirmationDialog.new()
	_generate_confirmation.title = "生成全部 DataTable"
	_generate_confirmation.ok_button_text = "确认生成"
	_generate_confirmation.cancel_button_text = "取消"
	_generate_confirmation.confirmed.connect(_confirm_generate)
	_dialog.add_child(_generate_confirmation)

	_config_file_dialog = EditorFileDialog.new()
	_config_file_dialog.title = "选择 DataTable Build Config"
	_config_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_config_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_config_file_dialog.filters = PackedStringArray(["*.json ; JSON"])
	_config_file_dialog.file_selected.connect(_on_config_selected)
	_dialog.add_child(_config_file_dialog)

	_python_file_dialog = EditorFileDialog.new()
	_python_file_dialog.title = "选择 Python 解释器"
	_python_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_python_file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	_python_file_dialog.file_selected.connect(_on_python_selected)
	_dialog.add_child(_python_file_dialog)

	_poll_timer = Timer.new()
	_poll_timer.wait_time = 0.1
	_poll_timer.timeout.connect(_poll_operation)
	_dialog.add_child(_poll_timer)
	_context.get_editor_interface().get_base_control().add_child(_dialog)


func _create_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label


func _load_editor_settings() -> void:
	var settings = _context.get_editor_interface().get_editor_settings()
	_config_input.text = str(settings.get_project_metadata(
		SETTINGS_SECTION,
		CONFIG_METADATA_KEY,
		DEFAULT_BUILD_CONFIG
	))
	_python_input.text = (
		str(settings.get_setting(PYTHON_SETTING))
		if settings.has_setting(PYTHON_SETTING)
		else ""
	)


func _save_editor_settings() -> void:
	var settings = _context.get_editor_interface().get_editor_settings()
	settings.set_project_metadata(
		SETTINGS_SECTION,
		CONFIG_METADATA_KEY,
		_config_input.text.strip_edges()
	)
	settings.set_setting(PYTHON_SETTING, _python_input.text.strip_edges())


func _open_config_file_dialog() -> void:
	_config_file_dialog.current_path = _config_input.text
	_config_file_dialog.popup_centered(Vector2i(760, 520))


func _open_python_file_dialog() -> void:
	_python_file_dialog.popup_centered(Vector2i(760, 520))


func _on_config_selected(path: String) -> void:
	_config_input.text = path
	_save_editor_settings()
	_refresh_status()


func _on_python_selected(path: String) -> void:
	_python_input.text = path
	_save_editor_settings()
	_refresh_status()


func _on_input_changed(_value: String) -> void:
	_detected_python = ""
	_refresh_status()


func _refresh_status() -> void:
	if not is_instance_valid(_report):
		return
	var state := _inspect_build_config()
	_refresh_table_options(state)
	var lines := PackedStringArray()
	lines.append("DataTable 编辑器构建")
	lines.append("")
	lines.append(_status_line("编译前端", FileAccess.file_exists(TOOL_PATH), TOOL_PATH))
	lines.append(_status_line("Build Config", state.valid, state.detail))
	var python_value := _python_input.text.strip_edges()
	var python_detail := python_value if not python_value.is_empty() else "自动检测 python3 / python"
	if not _detected_python.is_empty():
		python_detail = "已检测：%s" % _detected_python
	lines.append(_status_line("Python", true, python_detail))
	if state.valid:
		lines.append("")
		lines.append("Profile：%s" % state.profile)
		lines.append("CSV：%s" % state.source)
		lines.append("数据输出：%s" % state.output)
		lines.append("C# 输出：%s" % state.csharp)
	_report.text = "\n".join(lines)
	_set_actions_enabled(state.valid and FileAccess.file_exists(TOOL_PATH) and not _running)


func _status_line(name: String, healthy: bool, detail: String) -> String:
	return "[%s] %s：%s" % ["正常" if healthy else "错误", name, detail]


func _inspect_build_config() -> Dictionary:
	var path := _config_input.text.strip_edges()
	if not path.begins_with("res://"):
		return {"valid": false, "detail": "必须选择项目内 res:// JSON 文件。"}
	if not FileAccess.file_exists(path):
		return {"valid": false, "detail": "文件不存在：%s" % path}
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {"valid": false, "detail": "无法读取：%s" % path}
	var parsed = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		return {"valid": false, "detail": "根节点必须是 JSON 对象。"}
	if int(parsed.get("format_version", 0)) != 1:
		return {"valid": false, "detail": "format_version 必须为 1。"}
	var allowed_fields := ["format_version", "profile", "source", "output", "csharp"]
	for key in parsed.keys():
		if not allowed_fields.has(str(key)):
			return {"valid": false, "detail": "包含未知字段：%s。" % key}
	for field in ["profile", "source", "output", "csharp"]:
		if not (parsed.get(field, null) is String) or str(parsed[field]).strip_edges().is_empty():
			return {"valid": false, "detail": "字段 %s 必须是非空字符串。" % field}
		if not _is_safe_relative_path(str(parsed[field])):
			return {"valid": false, "detail": "字段 %s 必须是配置目录内的正斜杠相对路径。" % field}
	var root := path.get_base_dir()
	var profile := root.path_join(str(parsed.profile)).simplify_path()
	var source := root.path_join(str(parsed.source)).simplify_path()
	var output := root.path_join(str(parsed.output)).simplify_path()
	var csharp := root.path_join(str(parsed.csharp)).simplify_path()
	if _is_same_or_child(profile, output) or _is_same_or_child(source, output):
		return {"valid": false, "detail": "数据输出目录不能包含 Profile 或 CSV 源目录。"}
	if _is_same_or_child(csharp, output):
		return {"valid": false, "detail": "C# 输出必须位于数据输出目录之外。"}
	if not FileAccess.file_exists(profile):
		return {"valid": false, "detail": "Profile 不存在：%s" % profile}
	if not DirAccess.dir_exists_absolute(source):
		return {"valid": false, "detail": "CSV 源目录不存在：%s" % source}
	var table_ids := _read_table_ids(profile)
	if table_ids.is_empty():
		return {"valid": false, "detail": "Profile 必须包含至少一个具有非空 id 的数据表。"}
	return {
		"valid": true,
		"detail": path,
		"config": path,
		"profile": profile,
		"source": source,
		"output": output,
		"csharp": csharp,
		"table_ids": table_ids,
	}


func _read_table_ids(profile_path: String) -> PackedStringArray:
	var file := FileAccess.open(profile_path, FileAccess.READ)
	if file == null:
		return PackedStringArray()
	var parsed = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary or not parsed.get("tables", null) is Array:
		return PackedStringArray()
	var result := PackedStringArray()
	for table in parsed.tables:
		if not table is Dictionary:
			return PackedStringArray()
		var table_id := str(table.get("id", "")).strip_edges()
		if table_id.is_empty() or result.has(table_id):
			return PackedStringArray()
		result.append(table_id)
	return result


func _refresh_table_options(state: Dictionary) -> void:
	if not is_instance_valid(_table_selector):
		return
	var previous := _table_selector.get_item_text(_table_selector.selected) if _table_selector.selected >= 0 else ""
	_table_selector.clear()
	if state.valid:
		for table_id in state.table_ids:
			_table_selector.add_item(str(table_id))
			if str(table_id) == previous:
				_table_selector.select(_table_selector.item_count - 1)


func _is_safe_relative_path(value: String) -> bool:
	if value.begins_with("/") or value.contains("\\") or value.contains("://"):
		return false
	for part in value.split("/", true):
		if part == "..":
			return false
	return true


func _is_same_or_child(path: String, parent: String) -> bool:
	var normalized_parent := parent.trim_suffix("/")
	return path == normalized_parent or path.begins_with(normalized_parent + "/")


func _set_actions_enabled(enabled: bool) -> void:
	_check_button.disabled = not enabled
	_generate_button.disabled = not enabled
	_generate_selected_button.disabled = not enabled or _table_selector.item_count == 0
	_table_selector.disabled = not enabled or _table_selector.item_count == 0
	_config_input.editable = not _running
	_python_input.editable = not _running


func _request_check() -> void:
	_start_operation("check")


func _request_generate() -> void:
	var state := _inspect_build_config()
	if not state.valid or _running:
		_refresh_status()
		return
	_pending_table = ""
	_generate_confirmation.title = "生成全部 DataTable"
	_generate_confirmation.dialog_text = (
		"将校验全部 DataTable，并替换以下生成产物：\n\n"
		+ "数据目录：%s\n" % state.output
		+ "C# 文件：%s\n\n" % state.csharp
		+ "CSV、Profile 和其他项目文件不会被修改。"
	)
	_generate_confirmation.popup_centered(Vector2i(700, 300))


func _request_generate_selected() -> void:
	var state := _inspect_build_config()
	if not state.valid or _running or _table_selector.selected < 0:
		_refresh_status()
		return
	_pending_table = _table_selector.get_item_text(_table_selector.selected)
	_generate_confirmation.title = "生成选中 DataTable"
	_generate_confirmation.dialog_text = (
		"将校验全部 DataTable，并仅提交选中表及数据集元数据：\n\n"
		+ "数据表：%s\n" % _pending_table
		+ "目标二进制：%s/%s.gdtb\n" % [state.output, _pending_table]
		+ "数据集元数据目录：%s\n" % state.output
		+ "聚合 C#：%s\n\n" % state.csharp
		+ "未选表缺失、过期或结构变化时会拒绝生成，并要求先生成全部。"
	)
	_generate_confirmation.popup_centered(Vector2i(700, 340))


func _confirm_generate() -> void:
	_start_operation("generate", _pending_table)


func _start_operation(action: String, selected_table := "") -> void:
	var state := _inspect_build_config()
	if _running or not state.valid or not FileAccess.file_exists(TOOL_PATH):
		_refresh_status()
		return
	_save_editor_settings()
	_running = true
	_set_actions_enabled(false)
	_report.text = "DataTable %s 正在运行，请稍候..." % ("检查" if action == "check" else "生成")
	var payload := {
		"action": action,
		"tool": ProjectSettings.globalize_path(TOOL_PATH),
		"config": ProjectSettings.globalize_path(str(state.config)),
		"python": _python_input.text.strip_edges(),
		"table": selected_table,
	}
	_thread = Thread.new()
	var start_error := _thread.start(_execute_operation.bind(payload))
	if start_error != OK:
		_running = false
		_thread = null
		_report.text = "DataTable 后台线程启动失败：%s" % error_string(start_error)
		_refresh_status()
		return
	_poll_timer.start()


func _execute_operation(payload: Dictionary) -> Dictionary:
	var candidates := PackedStringArray()
	if not str(payload.python).is_empty():
		candidates.append(str(payload.python))
	else:
		candidates.append("python3")
		candidates.append("python")

	var failures := PackedStringArray()
	for executable in candidates:
		var version_output: Array = []
		var version_exit := OS.execute(executable, PackedStringArray(["--version"]), version_output, true)
		var version_text := "\n".join(version_output)
		if version_exit != 0:
			failures.append("%s：无法执行（退出码 %d）" % [executable, version_exit])
			continue
		if not _is_supported_python(version_text):
			failures.append("%s：需要 Python 3.10+，实际为 %s" % [executable, version_text.strip_edges()])
			continue

		var output: Array = []
		var arguments := PackedStringArray([
			"-X",
			"utf8",
			str(payload.tool),
			str(payload.action),
			"--build-config",
			str(payload.config),
		])
		if not str(payload.table).is_empty():
			arguments.append("--table")
			arguments.append(str(payload.table))
		var exit_code := OS.execute(executable, arguments, output, true)
		return {
			"exit_code": exit_code,
			"output": "\n".join(output),
			"python": executable,
			"version": version_text.strip_edges(),
			"action": payload.action,
			"table": payload.table,
		}
	return {
		"exit_code": -1,
		"output": "未找到可用的 Python 3.10+：\n%s" % "\n".join(failures),
		"python": "",
		"version": "",
		"action": payload.action,
		"table": payload.table,
	}


func _is_supported_python(version_text: String) -> bool:
	var marker := version_text.find("Python ")
	if marker < 0:
		return false
	var version := version_text.substr(marker + 7).strip_edges().split(" ", false, 1)[0]
	var parts := version.split(".")
	if parts.size() < 2 or not parts[0].is_valid_int() or not parts[1].is_valid_int():
		return false
	return int(parts[0]) > 3 or (int(parts[0]) == 3 and int(parts[1]) >= 10)


func _poll_operation() -> void:
	if _thread == null or _thread.is_alive():
		return
	_poll_timer.stop()
	var result: Dictionary = _thread.wait_to_finish()
	_thread = null
	_running = false
	_detected_python = str(result.python)
	var output := str(result.output)
	if output.length() > MAX_OUTPUT_CHARACTERS:
		output = output.left(MAX_OUTPUT_CHARACTERS) + "\n... 输出已截断。"
	var succeeded := int(result.exit_code) == 0
	_report.text = "%s DataTable %s（%s，%s）\n\n%s" % [
		"[成功]" if succeeded else "[失败]",
		"检查" if str(result.action) == "check" else "生成",
		str(result.python) if not str(result.python).is_empty() else "Python 不可用",
		str(result.version),
		output,
	]
	if succeeded and str(result.action) == "generate":
		_context.get_editor_interface().get_resource_filesystem().scan()
	var state := _inspect_build_config()
	_set_actions_enabled(state.valid and FileAccess.file_exists(TOOL_PATH))
