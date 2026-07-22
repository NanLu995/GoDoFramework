@tool
extends RefCounted

const TOOL_PATH := "res://addons/godo_framework/Tools/DataTable/godo_datatable.py"
const EXPORT_PLUGIN_SCRIPT := preload("res://addons/godo_framework/Tools/DataTable/Editor/datatable_export_plugin.gd")
const SCHEMA_EDITOR_SCRIPT := preload("res://addons/godo_framework/Tools/DataTable/Editor/datatable_schema_editor.gd")
const DEFAULT_SCHEMA := "res://DataTables/Base/.datatable.schema.json"
const SETTINGS_SECTION := "godo_framework/datatable"
const CONFIG_METADATA_KEY := "schema_path"
const PYTHON_SETTING := "godo_framework/datatable/python_executable"
const MAX_OUTPUT_CHARACTERS := 65536

var _context
var _export_plugin: EditorExportPlugin
var _schema_editor
var _dialog: AcceptDialog
var _generate_confirmation: ConfirmationDialog
var _config_file_dialog: EditorFileDialog
var _python_file_dialog: EditorFileDialog
var _config_input: LineEdit
var _python_input: LineEdit
var _table_selector: OptionButton
var _report: RichTextLabel
var _message_label: RichTextLabel
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
	var menu_error: Error = _context.add_menu_action("open", "数据表配置 (DataTable Configuration)...", _open_dialog)
	if menu_error != OK:
		return menu_error
	_export_plugin = EXPORT_PLUGIN_SCRIPT.new()
	_context.add_export_plugin(_export_plugin)
	return OK


func deactivate() -> void:
	if is_instance_valid(_poll_timer):
		_poll_timer.stop()
	if _thread != null and _thread.is_started():
		_thread.wait_to_finish()
	_thread = null
	if _export_plugin != null and _context != null:
		_context.remove_export_plugin(_export_plugin)
	_export_plugin = null
	if _schema_editor != null:
		_schema_editor.dispose()
	_schema_editor = null
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

	var config_row := HBoxContainer.new()
	content.add_child(config_row)
	config_row.add_child(_create_label("DataTable Schema"))
	_config_input = LineEdit.new()
	_config_input.name = "DataTableBuildConfigInput"
	_config_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_config_input.placeholder_text = "例如：%s" % DEFAULT_SCHEMA
	_config_input.text_changed.connect(_on_input_changed)
	config_row.add_child(_config_input)
	var config_browse := Button.new()
	config_browse.text = "浏览..."
	config_browse.pressed.connect(_open_config_file_dialog)
	config_row.add_child(config_browse)

	var python_row := HBoxContainer.new()
	content.add_child(python_row)
	python_row.add_child(_create_label("Python 3.10+"))
	_python_input = LineEdit.new()
	_python_input.name = "DataTablePythonInput"
	_python_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_python_input.placeholder_text = "可留空，将自动检测 python3 / python"
	_python_input.text_changed.connect(_on_input_changed)
	python_row.add_child(_python_input)
	var python_browse := Button.new()
	python_browse.text = "浏览..."
	python_browse.pressed.connect(_open_python_file_dialog)
	python_row.add_child(python_browse)

	content.add_child(HSeparator.new())

	var table_row := HBoxContainer.new()
	content.add_child(table_row)
	table_row.add_child(_create_label("数据表导出"))
	_table_selector = OptionButton.new()
	_table_selector.name = "DataTableTableSelector"
	_table_selector.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_table_selector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	table_row.add_child(_table_selector)
	_generate_selected_button = Button.new()
	_generate_selected_button.text = "导出当前表..."
	_generate_selected_button.name = "DataTableGenerateSelectedButton"
	_generate_selected_button.pressed.connect(_request_generate_selected)
	table_row.add_child(_generate_selected_button)
	_generate_button = Button.new()
	_generate_button.text = "导出全部表..."
	_generate_button.name = "DataTableGenerateButton"
	_generate_button.pressed.connect(_request_generate)
	table_row.add_child(_generate_button)

	_report = RichTextLabel.new()
	_report.name = "DataTableReport"
	_report.bbcode_enabled = true
	_report.selection_enabled = true
	_report.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_report)

	_message_label = RichTextLabel.new()
	_message_label.name = "DataTableMessage"
	_message_label.bbcode_enabled = true
	_message_label.custom_minimum_size.y = 44
	_message_label.scroll_active = false
	_message_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	content.add_child(_message_label)

	_check_button = _dialog.add_button("校验全部数据", false)
	_check_button.name = "DataTableCheckButton"
	var create_button := _dialog.add_button("新建 Schema", true)
	create_button.name = "DataTableCreateSchemaButton"
	var edit_button := _dialog.add_button("编辑 Schema...", true)
	edit_button.name = "DataTableEditSchemaButton"
	create_button.pressed.connect(_create_schema)
	edit_button.pressed.connect(_open_schema_editor)
	_check_button.pressed.connect(_request_check)

	_generate_confirmation = ConfirmationDialog.new()
	_generate_confirmation.title = "导出全部数据表"
	_generate_confirmation.ok_button_text = "确认导出"
	_generate_confirmation.cancel_button_text = "取消"
	_generate_confirmation.confirmed.connect(_confirm_generate)
	_dialog.add_child(_generate_confirmation)

	_config_file_dialog = EditorFileDialog.new()
	_config_file_dialog.title = "选择 DataTable Schema"
	_config_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_config_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_config_file_dialog.show_hidden_files = true
	_config_file_dialog.filters = PackedStringArray(["*.datatable.schema.json ; DataTable Schema"])
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
	label.custom_minimum_size.x = 150
	return label


func _load_editor_settings() -> void:
	var settings = _context.get_editor_interface().get_editor_settings()
	var schema_path := str(settings.get_project_metadata(
		SETTINGS_SECTION,
		CONFIG_METADATA_KEY,
		DEFAULT_SCHEMA
	))
	_config_input.text = schema_path if FileAccess.file_exists(schema_path) else DEFAULT_SCHEMA
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


func _create_schema() -> void:
	if _running:
		return
	var schema_path := DEFAULT_SCHEMA
	if FileAccess.file_exists(schema_path):
		_report.text = "默认 Schema 已存在：%s\n可通过“浏览...”选择它或其他 Schema。" % schema_path
		return
	var directory_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(schema_path.get_base_dir()))
	if directory_error != OK:
		_report.text = "无法创建 DataTables 目录：%s" % error_string(directory_error)
		return
	var file := FileAccess.open(schema_path, FileAccess.WRITE)
	if file == null:
		_report.text = "无法创建 Schema：%s" % schema_path
		return
	file.store_string(JSON.stringify({
		"format_version": 2,
		"data_set_id": "game.base",
		"protocol_version": 1,
		"namespace": "Game.DataTables.Base",
		"source_directory": ".datafiles",
		"output_directory": "Runtime",
		"csharp_output": "BaseDataTables.g.cs",
		"tables": [{
			"id": "Example",
			"source": "Example.csv",
			"schema_version": 1,
			"audience": "Shared",
			"primary_key": "id",
			"fields": [{"name": "id", "type": "string", "required": true, "min_length": 1}]
		}]
	}, "\t") + "\n")
	var source_directory := schema_path.get_base_dir().path_join(".datafiles")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(source_directory))
	var ignore := FileAccess.open(source_directory.path_join(".gdignore"), FileAccess.WRITE)
	if ignore != null:
		ignore.store_string("\n")
	var csv := FileAccess.open(source_directory.path_join("Example.csv"), FileAccess.WRITE)
	if csv != null:
		csv.store_string("id\n")
	_config_input.text = schema_path
	_save_editor_settings()
	_context.get_editor_interface().get_resource_filesystem().scan()
	_refresh_status()
	_open_schema_editor()


func _open_schema_editor() -> void:
	var state := _inspect_schema()
	if not state.valid:
		_refresh_status()
		return
	if _schema_editor == null:
		_schema_editor = SCHEMA_EDITOR_SCRIPT.new()
	_schema_editor.open(_context, str(state.schema), _on_schema_saved)


func _on_schema_saved() -> void:
	_context.get_editor_interface().get_resource_filesystem().scan()
	_refresh_status()
	_request_check()


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
	var state := _inspect_schema()
	_refresh_table_options(state)
	var tool_ready := FileAccess.file_exists(TOOL_PATH)
	var ready: bool = state.valid and tool_ready
	var lines := PackedStringArray()
	lines.append(
		"当前状态：[color=#8bd49c]配置有效[/color]"
		if ready
		else "当前状态：[color=#ff6b6b]需要处理[/color]"
	)
	lines.append("")
	lines.append(_status_line("编译工具", tool_ready, TOOL_PATH if tool_ready else "文件缺失"))
	lines.append(_status_line("Schema", state.valid, state.detail))
	var python_value := _python_input.text.strip_edges()
	var python_detail := python_value if not python_value.is_empty() else "自动检测 python3 / python"
	if not _detected_python.is_empty():
		python_detail = "已检测：%s" % _detected_python
	lines.append(_status_line("Python", true, python_detail))
	if state.valid:
		lines.append("")
		lines.append("CSV 源目录：%s" % state.source)
		lines.append("运行数据目录：%s" % state.output)
		lines.append("C# 代码文件：%s" % state.csharp)
	_report.text = "\n".join(lines)
	_set_hint(
		"配置有效，可以校验或导出运行数据。"
		if ready
		else "请先处理上方标记的配置问题。",
		"#8bd49c" if ready else "#ff6b6b"
	)
	_set_actions_enabled(ready and not _running)


func _status_line(name: String, healthy: bool, detail: String) -> String:
	var color := "#8bd49c" if healthy else "#ff6b6b"
	return "[color=%s][%s][/color] %s：%s" % [color, "正常" if healthy else "错误", name, detail]


func _set_hint(message: String, color: String) -> void:
	_message_label.text = "[center][color=%s]提示：%s[/color][/center]" % [color, message]


func _append_diagnostics(output: String) -> void:
	if output.strip_edges().is_empty():
		return
	_report.append_text("\n\n[color=#ff6b6b][错误] 诊断信息[/color]\n")
	_report.add_text(output)


func _inspect_schema() -> Dictionary:
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
	if int(parsed.get("format_version", 0)) != 2:
		return {"valid": false, "detail": "format_version 必须为 2。"}
	var allowed_fields := ["format_version", "data_set_id", "protocol_version", "namespace", "source_directory", "output_directory", "csharp_output", "tables"]
	for key in parsed.keys():
		if not allowed_fields.has(str(key)):
			return {"valid": false, "detail": "包含未知字段：%s。" % key}
	for field in ["source_directory", "output_directory", "csharp_output"]:
		if not (parsed.get(field, null) is String) or str(parsed[field]).strip_edges().is_empty():
			return {"valid": false, "detail": "字段 %s 必须是非空字符串。" % field}
		if not _is_safe_relative_path(str(parsed[field])):
			return {"valid": false, "detail": "字段 %s 必须是 Schema 目录内的正斜杠相对路径。" % field}
	var root := path.get_base_dir()
	var source := root.path_join(str(parsed.source_directory)).simplify_path()
	var output := root.path_join(str(parsed.output_directory)).simplify_path()
	var csharp := root.path_join(str(parsed.csharp_output)).simplify_path()
	if _is_same_or_child(source, output):
		return {"valid": false, "detail": "数据输出目录不能包含 CSV 源目录。"}
	if _is_same_or_child(csharp, output):
		return {"valid": false, "detail": "C# 输出必须位于数据输出目录之外。"}
	if not DirAccess.dir_exists_absolute(source):
		return {"valid": false, "detail": "CSV 源目录不存在：%s" % source}
	var table_ids := _read_table_ids(path)
	if table_ids.is_empty():
		return {"valid": false, "detail": "Schema 必须包含至少一个具有非空 id 的数据表。"}
	return {
		"valid": true,
		"detail": path,
		"schema": path,
		"source": source,
		"output": output,
		"csharp": csharp,
		"table_ids": table_ids,
	}


func _read_table_ids(schema_path: String) -> PackedStringArray:
	var file := FileAccess.open(schema_path, FileAccess.READ)
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
	var state := _inspect_schema()
	if not state.valid or _running:
		_refresh_status()
		return
	_pending_table = ""
	_generate_confirmation.title = "导出全部数据表"
	_generate_confirmation.dialog_text = (
		"将校验全部 DataTable，并替换以下运行数据产物：\n\n"
		+ "数据目录：%s\n" % state.output
		+ "C# 文件：%s\n\n" % state.csharp
		+ "CSV、Schema 和其他项目文件不会被修改。"
	)
	_generate_confirmation.popup_centered(Vector2i(700, 300))


func _request_generate_selected() -> void:
	var state := _inspect_schema()
	if not state.valid or _running or _table_selector.selected < 0:
		_refresh_status()
		return
	_pending_table = _table_selector.get_item_text(_table_selector.selected)
	_generate_confirmation.title = "导出当前数据表"
	_generate_confirmation.dialog_text = (
		"将校验全部 DataTable，并仅提交选中表及数据集元数据：\n\n"
		+ "数据表：%s\n" % _pending_table
		+ "目标二进制：%s/%s.gdtb\n" % [state.output, _pending_table]
		+ "数据集元数据目录：%s\n" % state.output
		+ "聚合 C#：%s\n\n" % state.csharp
		+ "未选表缺失、过期或结构变化时会拒绝导出，并要求先导出全部。"
	)
	_generate_confirmation.popup_centered(Vector2i(700, 340))


func _confirm_generate() -> void:
	_start_operation("generate", _pending_table)


func _start_operation(action: String, selected_table := "") -> void:
	var state := _inspect_schema()
	if _running or not state.valid or not FileAccess.file_exists(TOOL_PATH):
		_refresh_status()
		return
	_save_editor_settings()
	_running = true
	_set_actions_enabled(false)
	_set_hint(
		"正在%s，请稍候..." % ("校验全部数据" if action == "check" else "导出运行数据"),
		"#aeb6c2"
	)
	var payload := {
		"action": action,
		"tool": ProjectSettings.globalize_path(TOOL_PATH),
		"schema": ProjectSettings.globalize_path(str(state.schema)),
		"python": _python_input.text.strip_edges(),
		"table": selected_table,
		"output_file": ProjectSettings.globalize_path(
			"user://godo_datatable_editor_output_%d.txt" % Time.get_ticks_msec()
		),
	}
	_thread = Thread.new()
	var start_error := _thread.start(_execute_operation.bind(payload))
	if start_error != OK:
		_running = false
		_thread = null
		_refresh_status()
		_set_hint("后台任务启动失败：%s" % error_string(start_error), "#ff6b6b")
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

		var arguments := PackedStringArray([
			"-X",
			"utf8",
			str(payload.tool),
			"--editor-output-file",
			str(payload.output_file),
			str(payload.action),
			"--schema",
			str(payload.schema),
		])
		if not str(payload.table).is_empty():
			arguments.append("--table")
			arguments.append(str(payload.table))
		var exit_code := OS.execute(executable, arguments)
		return {
			"exit_code": exit_code,
			"output": "",
			"output_file": payload.output_file,
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
		"output_file": payload.output_file,
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
	var output_file := str(result.get("output_file", ""))
	if not output_file.is_empty() and FileAccess.file_exists(output_file):
		var encoded_output := FileAccess.get_file_as_string(output_file).strip_edges()
		DirAccess.remove_absolute(output_file)
		if not encoded_output.is_empty():
			output = Marshalls.base64_to_utf8(encoded_output)
			if output.is_empty():
				output = "[DataTableCompiler] FAIL: 编辑器诊断传输解码失败。"
	if output.is_empty() and int(result.exit_code) != 0:
		output = "[DataTableCompiler] FAIL: 编译器未返回诊断信息（退出码 %d）。" % int(result.exit_code)
	if output.length() > MAX_OUTPUT_CHARACTERS:
		output = output.left(MAX_OUTPUT_CHARACTERS) + "\n... 输出已截断。"
	var succeeded := int(result.exit_code) == 0
	if succeeded and str(result.action) == "generate":
		_context.get_editor_interface().get_resource_filesystem().scan()
	_refresh_status()
	if succeeded:
		var action_text := "全部数据校验通过，可以导出运行数据。"
		if str(result.action) == "generate":
			action_text = (
				"当前表导出完成，Godot 正在刷新文件。"
				if not str(result.table).is_empty()
				else "全部数据表导出完成，Godot 正在刷新文件。"
			)
		_set_hint(action_text, "#8bd49c")
	else:
		_append_diagnostics(output)
		_set_hint("操作失败，请查看上方诊断信息。", "#ff6b6b")
