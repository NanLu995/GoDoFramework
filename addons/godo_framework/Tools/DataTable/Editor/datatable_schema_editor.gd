@tool
extends RefCounted

const SUPPORTED_TYPES := ["string", "bool", "int32", "float64", "enum"]
const AUDIENCES := ["Shared", "ClientOnly", "ServerOnly"]

var _context
var _schema_path := ""
var _saved_callback := Callable()
var _dialog: AcceptDialog
var _data_set_id: LineEdit
var _namespace: LineEdit
var _protocol_version: SpinBox
var _source_directory: LineEdit
var _output_directory: LineEdit
var _csharp_output: LineEdit
var _data_files: Tree
var _table_selector: OptionButton
var _table_id: LineEdit
var _table_source: LineEdit
var _table_audience: OptionButton
var _primary_key: LineEdit
var _schema_version: Label
var _fields: Tree
var _status: Label
var _remove_confirmation: ConfirmationDialog
var _schema: Dictionary = {}
var _original_schema: Dictionary = {}
var _selected_table := -1
var _loading := false


func open(context, schema_path: String, saved_callback: Callable) -> void:
	_context = context
	_schema_path = schema_path
	_saved_callback = saved_callback
	if not _load_schema():
		return
	if not is_instance_valid(_dialog):
		_create_dialog()
	_populate_dataset()
	_refresh_table_selector(0)
	_dialog.popup_centered(Vector2i(1180, 900))


func dispose() -> void:
	if is_instance_valid(_dialog):
		_dialog.queue_free()
	_dialog = null
	_context = null


func _load_schema() -> bool:
	var file := FileAccess.open(_schema_path, FileAccess.READ)
	if file == null:
		push_error("[GoDo DataTable] 无法读取 Schema：%s" % _schema_path)
		return false
	var parsed = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		push_error("[GoDo DataTable] Schema 根节点必须是 JSON 对象：%s" % _schema_path)
		return false
	_schema = parsed.duplicate(true)
	_original_schema = parsed.duplicate(true)
	_annotate_schema_for_editing()
	return true


func _annotate_schema_for_editing() -> void:
	for table in _schema.get("tables", []):
		table["_editor_original_id"] = str(table.get("id", ""))
		table["_editor_original_source"] = str(table.get("source", ""))


func _create_dialog() -> void:
	_dialog = AcceptDialog.new()
	_dialog.exclusive = false
	_dialog.title = "DataTable Schema 编辑器"
	_dialog.ok_button_text = "关闭"
	_dialog.min_size = Vector2i(1180, 900)
	_dialog.get_label().hide()

	var content := VBoxContainer.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -64
	content.add_theme_constant_override("separation", 8)
	_dialog.add_child(content)

	var dataset_grid := GridContainer.new()
	dataset_grid.columns = 4
	content.add_child(dataset_grid)
	_data_set_id = _add_line_setting(dataset_grid, "数据集 ID")
	_data_set_id.name = "DataTableSchemaDataSetId"
	_namespace = _add_line_setting(dataset_grid, "C# 命名空间")
	_protocol_version = SpinBox.new()
	_protocol_version.min_value = 1
	_protocol_version.max_value = 2147483647
	dataset_grid.add_child(_label("协议版本"))
	dataset_grid.add_child(_protocol_version)
	_source_directory = _add_line_setting(dataset_grid, "原始表目录")
	_source_directory.text_submitted.connect(_on_source_directory_submitted)
	_source_directory.focus_exited.connect(_refresh_data_files)
	_output_directory = _add_line_setting(dataset_grid, "运行时目录")
	_csharp_output = _add_line_setting(dataset_grid, "C# 输出")

	var data_file_bar := HBoxContainer.new()
	content.add_child(data_file_bar)
	data_file_bar.add_child(_label("数据文件（未加入 Schema 即为排除）"))
	var refresh_data_files := Button.new()
	refresh_data_files.text = "刷新"
	refresh_data_files.pressed.connect(_refresh_data_files)
	data_file_bar.add_child(refresh_data_files)
	var add_data_file := Button.new()
	add_data_file.name = "DataTableSchemaAddCsvButton"
	add_data_file.text = "加入选中 CSV"
	add_data_file.pressed.connect(_add_selected_csv)
	data_file_bar.add_child(add_data_file)
	var open_data_directory := Button.new()
	open_data_directory.name = "DataTableSchemaOpenDataDirectoryButton"
	open_data_directory.text = "打开数据目录"
	open_data_directory.pressed.connect(_open_data_directory)
	data_file_bar.add_child(open_data_directory)

	_data_files = Tree.new()
	_data_files.name = "DataTableSchemaDataFiles"
	_data_files.columns = 2
	_data_files.column_titles_visible = true
	_data_files.hide_root = true
	_data_files.custom_minimum_size.y = 120
	_data_files.set_column_title(0, "文件")
	_data_files.set_column_title(1, "状态")
	_data_files.set_column_expand(0, true)
	_data_files.set_column_expand(1, true)
	content.add_child(_data_files)

	var table_bar := HBoxContainer.new()
	content.add_child(table_bar)
	table_bar.add_child(_label("数据表"))
	_table_selector = OptionButton.new()
	_table_selector.name = "DataTableSchemaTableSelector"
	_table_selector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_table_selector.item_selected.connect(_on_table_selected)
	table_bar.add_child(_table_selector)
	var add_table := Button.new()
	add_table.text = "新增表"
	add_table.pressed.connect(_add_table)
	table_bar.add_child(add_table)
	var remove_table := Button.new()
	remove_table.text = "移除表..."
	remove_table.pressed.connect(_request_remove_table)
	table_bar.add_child(remove_table)

	var table_grid := GridContainer.new()
	table_grid.columns = 4
	content.add_child(table_grid)
	_table_id = _add_line_setting(table_grid, "表 ID")
	_table_source = _add_line_setting(table_grid, "CSV 文件")
	_primary_key = _add_line_setting(table_grid, "主键字段")
	table_grid.add_child(_label("受众"))
	_table_audience = OptionButton.new()
	for audience in AUDIENCES:
		_table_audience.add_item(audience)
	table_grid.add_child(_table_audience)
	table_grid.add_child(_label("Schema 版本"))
	_schema_version = Label.new()
	table_grid.add_child(_schema_version)

	var field_bar := HBoxContainer.new()
	content.add_child(field_bar)
	field_bar.add_child(_label("字段（双击单元格编辑）"))
	var add_field := Button.new()
	add_field.text = "新增字段"
	add_field.pressed.connect(_add_field)
	field_bar.add_child(add_field)
	var remove_field := Button.new()
	remove_field.text = "移除字段..."
	remove_field.pressed.connect(_request_remove_field)
	field_bar.add_child(remove_field)

	_fields = Tree.new()
	_fields.name = "DataTableSchemaFields"
	_fields.columns = 12
	_fields.column_titles_visible = true
	_fields.hide_root = true
	_fields.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var titles := ["名称", "类型", "必填", "默认值", "Min", "Max", "最短", "最长", "枚举值（逗号）", "外键 Table.field", "允许空字符串", "Null Token"]
	for index in titles.size():
		_fields.set_column_title(index, titles[index])
		_fields.set_column_custom_minimum_width(index, 92 if index < 8 or index == 10 else 150)
	content.add_child(_fields)

	_status = Label.new()
	_status.name = "DataTableSchemaStatus"
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(_status)
	var save_button := _dialog.add_button("保存 Schema", true)
	save_button.name = "DataTableSchemaSaveButton"
	save_button.pressed.connect(_save)

	_remove_confirmation = ConfirmationDialog.new()
	_remove_confirmation.title = "确认移除"
	_remove_confirmation.ok_button_text = "确认移除"
	_remove_confirmation.cancel_button_text = "取消"
	_dialog.add_child(_remove_confirmation)
	_context.get_editor_interface().get_base_control().add_child(_dialog)


func _add_line_setting(parent: GridContainer, title: String) -> LineEdit:
	parent.add_child(_label(title))
	var input := LineEdit.new()
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(input)
	return input


func _label(text: String) -> Label:
	var result := Label.new()
	result.text = text
	return result


func _populate_dataset() -> void:
	_loading = true
	_data_set_id.text = str(_schema.get("data_set_id", ""))
	_namespace.text = str(_schema.get("namespace", ""))
	_protocol_version.value = int(_schema.get("protocol_version", 1))
	_source_directory.text = str(_schema.get("source_directory", ".datafiles"))
	_output_directory.text = str(_schema.get("output_directory", "Runtime"))
	_csharp_output.text = str(_schema.get("csharp_output", "DataTables.g.cs"))
	_loading = false
	_refresh_data_files()


func _on_source_directory_submitted(_value: String) -> void:
	_refresh_data_files()


func _source_root() -> String:
	var relative := _source_directory.text.strip_edges()
	if not _is_safe_relative_path(relative):
		return ""
	return _schema_path.get_base_dir().path_join(relative).simplify_path()


func _refresh_data_files() -> void:
	if not is_instance_valid(_data_files):
		return
	_data_files.clear()
	var root_item := _data_files.create_item()
	var configured := {}
	for table in _schema.get("tables", []):
		configured[str(table.get("source", ""))] = str(table.get("id", ""))
	var source_root := _source_root()
	var found := {}
	if not source_root.is_empty():
		_collect_csv_files(source_root, "", found)
		var file_names := found.keys()
		file_names.sort()
		for file_name in file_names:
			_add_data_file_item(root_item, file_name, "已加入 Schema（%s）" % configured[file_name] if configured.has(file_name) else "未加入（已排除）")
	for source in configured:
		if not found.has(source):
			_add_data_file_item(root_item, source, "Schema 引用但文件缺失")


func _collect_csv_files(root: String, relative: String, result: Dictionary) -> void:
	var path := root.path_join(relative).simplify_path() if not relative.is_empty() else root
	var directory := DirAccess.open(path)
	if directory == null:
		return
	directory.list_dir_begin()
	var entry := directory.get_next()
	while not entry.is_empty():
		var child := relative.path_join(entry) if not relative.is_empty() else entry
		if directory.current_is_dir():
			if not entry.begins_with("."):
				_collect_csv_files(root, child, result)
		elif entry.get_extension().to_lower() == "csv":
			result[child] = true
		entry = directory.get_next()
	directory.list_dir_end()


func _add_data_file_item(root: TreeItem, file_name: String, state: String) -> void:
	var item := _data_files.create_item(root)
	item.set_text(0, file_name)
	item.set_text(1, state)
	item.set_metadata(0, file_name)


func _add_selected_csv() -> void:
	var field_error := _validate_field_inputs()
	if not field_error.is_empty():
		_status.text = "导入失败：%s" % field_error
		return
	_commit_current_table()
	var selected := _data_files.get_selected()
	if selected == null:
		_status.text = "请先选择一个未加入 Schema 的 CSV。"
		return
	var source := str(selected.get_metadata(0))
	for table in _schema.get("tables", []):
		if str(table.get("source", "")) == source:
			_status.text = "%s 已加入 Schema。" % source
			return
	var source_root := _source_root()
	var path := source_root.path_join(source).simplify_path() if not source_root.is_empty() else ""
	if path.is_empty() or not FileAccess.file_exists(path):
		_status.text = "无法导入不存在的 CSV：%s" % source
		return
	var input := FileAccess.open(path, FileAccess.READ)
	if input == null or input.get_length() == 0:
		_status.text = "无法读取 CSV 表头：%s" % source
		return
	var header := input.get_csv_line()
	var header_error := _validate_csv_header(header)
	if not header_error.is_empty():
		_status.text = "导入失败：%s" % header_error
		return
	var table_id := _unique_table_id(_table_id_from_source(source))
	var fields: Array = []
	for name in header:
		fields.append({
			"name": name,
			"type": "string",
			"required": name == "id",
		})
	var primary_key := "id" if header.has("id") else str(header[0])
	for field in fields:
		if field.name == primary_key:
			field.required = true
			field.min_length = 1
	var tables: Array = _schema.get("tables", [])
	tables.append({
		"id": table_id,
		"source": source,
		"schema_version": 1,
		"audience": "Shared",
		"primary_key": primary_key,
		"fields": fields,
	})
	_schema["tables"] = tables
	_refresh_table_selector(tables.size() - 1)
	_refresh_data_files()
	_status.text = "%s 已读取表头并加入 Schema；请设置真实字段类型后保存。" % source


func _validate_csv_header(header: PackedStringArray) -> String:
	if header.is_empty():
		return "CSV 表头不能为空。"
	var names := {}
	for raw_name in header:
		var name := raw_name.strip_edges()
		if name != raw_name or not name.is_valid_identifier():
			return "CSV 字段名必须是无首尾空格的有效标识符：%s。" % raw_name
		if names.has(name):
			return "CSV 字段名不能重复：%s。" % name
		names[name] = true
	return ""


func _table_id_from_source(source: String) -> String:
	var result := ""
	var capitalize_next := true
	for character in source.get_basename():
		if character == "_" or character == "-" or character == " ":
			capitalize_next = true
			continue
		result += character.to_upper() if capitalize_next else character
		capitalize_next = false
	if result.is_empty() or not result.is_valid_identifier():
		return "Table"
	return result


func _unique_table_id(candidate: String) -> String:
	var result := candidate
	var suffix := 2
	while _contains_table_id(_schema.get("tables", []), result):
		result = "%s%d" % [candidate, suffix]
		suffix += 1
	return result


func _open_data_directory() -> void:
	var source_root := _source_root()
	if source_root.is_empty():
		_status.text = "原始表目录不是安全相对路径。"
		return
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(source_root)):
		_status.text = "数据目录尚不存在，请先保存 Schema。"
		return
	var error := OS.shell_show_in_file_manager(ProjectSettings.globalize_path(source_root), true)
	if error != OK:
		_status.text = "无法打开数据目录：%s" % error_string(error)


func _refresh_table_selector(preferred: int) -> void:
	_loading = true
	_table_selector.clear()
	var tables: Array = _schema.get("tables", [])
	for table in tables:
		_table_selector.add_item(str(table.get("id", "Unnamed")))
	_selected_table = clampi(preferred, 0, tables.size() - 1) if not tables.is_empty() else -1
	if _selected_table >= 0:
		_table_selector.select(_selected_table)
	_loading = false
	_populate_table()


func _on_table_selected(index: int) -> void:
	if _loading:
		return
	var error := _validate_field_inputs()
	if not error.is_empty():
		_loading = true
		_table_selector.select(_selected_table)
		_loading = false
		_status.text = "切换失败：%s" % error
		return
	_commit_current_table()
	_selected_table = index
	_populate_table()
	_refresh_data_files()


func _populate_table() -> void:
	_loading = true
	_fields.clear()
	var root := _fields.create_item()
	var tables: Array = _schema.get("tables", [])
	var enabled := _selected_table >= 0 and _selected_table < tables.size()
	for control in [_table_id, _table_source, _primary_key]:
		control.editable = enabled
	_table_audience.disabled = not enabled
	_fields.mouse_filter = Control.MOUSE_FILTER_STOP if enabled else Control.MOUSE_FILTER_IGNORE
	if not enabled:
		_table_id.text = ""
		_table_source.text = ""
		_primary_key.text = ""
		_schema_version.text = "-"
		_loading = false
		return
	var table: Dictionary = tables[_selected_table]
	_table_id.text = str(table.get("id", ""))
	_table_source.text = str(table.get("source", ""))
	_primary_key.text = str(table.get("primary_key", "id"))
	_table_audience.select(maxi(0, AUDIENCES.find(str(table.get("audience", "Shared")))))
	_schema_version.text = str(table.get("schema_version", 1))
	for field in table.get("fields", []):
		_add_field_item(root, field)
	_loading = false


func _add_field_item(root: TreeItem, field: Dictionary) -> void:
	var item := _fields.create_item(root)
	item.set_metadata(0, field.duplicate(true))
	for column in [0, 3, 4, 5, 6, 7, 8, 9, 11]:
		item.set_editable(column, true)
	item.set_text(0, str(field.get("name", "field")))
	item.set_cell_mode(1, TreeItem.CELL_MODE_RANGE)
	item.set_text(1, ",".join(SUPPORTED_TYPES))
	item.set_range_config(1, 0, SUPPORTED_TYPES.size() - 1, 1)
	item.set_range(1, maxi(0, SUPPORTED_TYPES.find(str(field.get("type", "string")))))
	item.set_editable(1, true)
	item.set_cell_mode(2, TreeItem.CELL_MODE_CHECK)
	item.set_checked(2, bool(field.get("required", false)))
	item.set_editable(2, true)
	item.set_text(3, _format_optional(field, "default"))
	item.set_text(4, _format_optional(field, "min"))
	item.set_text(5, _format_optional(field, "max"))
	item.set_text(6, _format_optional(field, "min_length"))
	item.set_text(7, _format_optional(field, "max_length"))
	item.set_text(8, ",".join(field.get("values", [])))
	item.set_text(9, str(field.get("foreign_key", "")))
	item.set_cell_mode(10, TreeItem.CELL_MODE_CHECK)
	item.set_checked(10, bool(field.get("allow_empty", false)))
	item.set_editable(10, true)
	item.set_text(11, str(field.get("null_token", "")))


func _format_optional(value: Dictionary, key: String) -> String:
	return str(value[key]) if value.has(key) else ""


func _commit_current_table() -> void:
	if _loading or _selected_table < 0:
		return
	var tables: Array = _schema.get("tables", [])
	if _selected_table >= tables.size():
		return
	var table: Dictionary = tables[_selected_table]
	table["id"] = _table_id.text.strip_edges()
	table["source"] = _table_source.text.strip_edges()
	table["primary_key"] = _primary_key.text.strip_edges()
	table["audience"] = AUDIENCES[_table_audience.selected]
	var fields: Array = []
	var root := _fields.get_root()
	if root != null:
		var item := root.get_first_child()
		while item != null:
			fields.append(_field_from_item(item))
			item = item.get_next()
	table["fields"] = fields
	tables[_selected_table] = table
	_schema["tables"] = tables


func _field_from_item(item: TreeItem) -> Dictionary:
	var field = item.get_metadata(0)
	var result: Dictionary = field.duplicate(true) if field is Dictionary else {}
	result["_editor_original_name"] = str(result.get("_editor_original_name", result.get("name", "")))
	result["name"] = item.get_text(0).strip_edges()
	result["type"] = SUPPORTED_TYPES[clampi(int(item.get_range(1)), 0, SUPPORTED_TYPES.size() - 1)]
	result["required"] = item.is_checked(2)
	_set_optional_value(result, "default", item.get_text(3), result["type"])
	_set_optional_number(result, "min", item.get_text(4), result["type"])
	_set_optional_number(result, "max", item.get_text(5), result["type"])
	_set_optional_integer(result, "min_length", item.get_text(6))
	_set_optional_integer(result, "max_length", item.get_text(7))
	var values := PackedStringArray()
	for value in item.get_text(8).split(",", false):
		if not value.strip_edges().is_empty():
			values.append(value.strip_edges())
	if result["type"] == "enum":
		result["values"] = Array(values)
	else:
		result.erase("values")
	_set_optional_string(result, "foreign_key", item.get_text(9))
	if result["type"] == "string" and item.is_checked(10):
		result["allow_empty"] = true
	else:
		result.erase("allow_empty")
	_set_optional_string(result, "null_token", item.get_text(11))
	return result


func _set_optional_value(target: Dictionary, key: String, text: String, type: String) -> void:
	var value := text.strip_edges()
	if value.is_empty():
		target.erase(key)
	elif type == "bool":
		target[key] = value.to_lower() == "true"
	elif type == "int32":
		target[key] = _parse_integer(value)
	elif type == "float64":
		target[key] = float(value)
	else:
		target[key] = text


func _set_optional_number(target: Dictionary, key: String, text: String, type: String) -> void:
	var value := text.strip_edges()
	if value.is_empty():
		target.erase(key)
	elif type == "int32":
		target[key] = _parse_integer(value)
	else:
		target[key] = float(value)


func _set_optional_integer(target: Dictionary, key: String, text: String) -> void:
	if text.strip_edges().is_empty():
		target.erase(key)
	else:
		target[key] = _parse_integer(text.strip_edges())


func _set_optional_string(target: Dictionary, key: String, text: String) -> void:
	if text.strip_edges().is_empty():
		target.erase(key)
	else:
		target[key] = text.strip_edges()


func _add_table() -> void:
	var error := _validate_field_inputs()
	if not error.is_empty():
		_status.text = "新增失败：%s" % error
		return
	_commit_current_table()
	var tables: Array = _schema.get("tables", [])
	var suffix := tables.size() + 1
	var id := "Table%d" % suffix
	while _contains_table_id(tables, id):
		suffix += 1
		id = "Table%d" % suffix
	tables.append({
		"id": id,
		"source": "%s.csv" % id,
		"schema_version": 1,
		"audience": "Shared",
		"primary_key": "id",
		"fields": [{"name": "id", "type": "string", "required": true, "min_length": 1}],
	})
	_schema["tables"] = tables
	_refresh_table_selector(tables.size() - 1)
	_refresh_data_files()


func _contains_table_id(tables: Array, id: String) -> bool:
	for table in tables:
		if str(table.get("id", "")) == id:
			return true
	return false


func _request_remove_table() -> void:
	if _selected_table < 0:
		return
	_remove_confirmation.dialog_text = "只从 Schema 移除数据表；CSV 文件会保留。确认继续？"
	var callback := _remove_table
	_reset_confirmation(callback)
	_remove_confirmation.popup_centered(Vector2i(560, 180))


func _remove_table() -> void:
	var tables: Array = _schema.get("tables", [])
	if _selected_table >= 0 and _selected_table < tables.size():
		tables.remove_at(_selected_table)
	_schema["tables"] = tables
	_refresh_table_selector(mini(_selected_table, tables.size() - 1))
	_refresh_data_files()


func _add_field() -> void:
	if _selected_table < 0:
		return
	var root := _fields.get_root()
	_add_field_item(root, {"name": "field", "type": "string", "required": false})


func _request_remove_field() -> void:
	var selected := _fields.get_selected()
	if selected == null:
		_status.text = "请先选择需要移除的字段。"
		return
	_remove_confirmation.dialog_text = "移除字段会在保存时删除 CSV 中对应列。确认继续？"
	var callback := _remove_field
	_reset_confirmation(callback)
	_remove_confirmation.popup_centered(Vector2i(560, 180))


func _remove_field() -> void:
	var selected := _fields.get_selected()
	if selected != null:
		selected.free()


func _reset_confirmation(callback: Callable) -> void:
	for connection in _remove_confirmation.confirmed.get_connections():
		_remove_confirmation.confirmed.disconnect(connection.callable)
	_remove_confirmation.confirmed.connect(callback, CONNECT_ONE_SHOT)


func _save() -> void:
	var field_error := _validate_field_inputs()
	if not field_error.is_empty():
		_status.text = "保存失败：%s" % field_error
		return
	_commit_current_table()
	_schema["format_version"] = 2
	_schema["data_set_id"] = _data_set_id.text.strip_edges()
	_schema["namespace"] = _namespace.text.strip_edges()
	_schema["protocol_version"] = int(_protocol_version.value)
	_schema["source_directory"] = _source_directory.text.strip_edges()
	_schema["output_directory"] = _output_directory.text.strip_edges()
	_schema["csharp_output"] = _csharp_output.text.strip_edges()
	var error := _validate_schema()
	if not error.is_empty():
		_status.text = "保存失败：%s" % error
		return
	_apply_schema_versions()
	if not _sync_csv_files():
		return
	_normalize_schema_numbers()
	_strip_editor_metadata()
	var file := FileAccess.open(_schema_path, FileAccess.WRITE)
	if file == null:
		_status.text = "保存失败：无法写入 %s" % _schema_path
		return
	file.store_string(JSON.stringify(_schema, "\t") + "\n")
	_original_schema = _schema.duplicate(true)
	_annotate_schema_for_editing()
	_status.text = "Schema 已保存；CSV 表头已同步，结构变化的表版本已自动递增。"
	_refresh_table_selector(_selected_table)
	_refresh_data_files()
	if _saved_callback.is_valid():
		_saved_callback.call()


func _validate_schema() -> String:
	if _data_set_id.text.strip_edges().is_empty():
		return "数据集 ID 不能为空。"
	if _namespace.text.strip_edges().is_empty():
		return "C# 命名空间不能为空。"
	for path in [_source_directory.text, _output_directory.text, _csharp_output.text]:
		if not _is_safe_relative_path(path):
			return "目录和输出文件必须是 Schema 目录内的安全相对路径。"
	var tables: Array = _schema.get("tables", [])
	if tables.is_empty():
		return "至少需要一张数据表。"
	var ids := {}
	var sources := {}
	for table in tables:
		var table_id := str(table.get("id", ""))
		if not table_id.is_valid_identifier() or ids.has(table_id):
			return "表 ID 必须是唯一的有效标识符：%s。" % table_id
		ids[table_id] = true
		var source := str(table.get("source", ""))
		if not _is_safe_relative_path(source) or sources.has(source):
			return "CSV 文件必须是唯一的安全相对路径：%s。" % source
		sources[source] = true
		var names := {}
		for field in table.get("fields", []):
			var name := str(field.get("name", ""))
			if not name.is_valid_identifier() or names.has(name):
				return "字段名必须是唯一的有效标识符：%s.%s。" % [table_id, name]
			names[name] = true
			if not SUPPORTED_TYPES.has(str(field.get("type", ""))):
				return "字段类型不受支持：%s.%s。" % [table_id, name]
			if field.get("type") == "enum" and field.get("values", []).is_empty():
				return "enum 字段必须至少有一个枚举值：%s.%s。" % [table_id, name]
		if not names.has(str(table.get("primary_key", ""))):
			return "主键字段不存在：%s.%s。" % [table_id, table.get("primary_key", "")]
	return ""


func _validate_field_inputs() -> String:
	if _selected_table < 0:
		return ""
	var root := _fields.get_root()
	if root == null:
		return ""
	var item := root.get_first_child()
	while item != null:
		var name := item.get_text(0).strip_edges()
		var type: String = SUPPORTED_TYPES[clampi(int(item.get_range(1)), 0, SUPPORTED_TYPES.size() - 1)]
		var default_value := item.get_text(3).strip_edges()
		if type == "bool" and not default_value.is_empty() and default_value.to_lower() not in ["true", "false"]:
			return "%s 的默认值必须是 true 或 false。" % name
		if type == "int32" and not default_value.is_empty() and not _is_integer_text(default_value):
			return "%s 的默认值必须是整数。" % name
		if type == "float64" and not default_value.is_empty() and not default_value.is_valid_float():
			return "%s 的默认值必须是数字。" % name
		for column in [4, 5]:
			var number := item.get_text(column).strip_edges()
			if number.is_empty():
				continue
			if type not in ["int32", "float64"]:
				return "%s 只有数值类型才能设置 Min/Max。" % name
			if type == "int32" and not _is_integer_text(number):
				return "%s 的 Min/Max 必须是整数。" % name
			if type == "float64" and not number.is_valid_float():
				return "%s 的 Min/Max 必须是数字。" % name
		for column in [6, 7]:
			var length := item.get_text(column).strip_edges()
			if not length.is_empty() and not _is_integer_text(length):
				return "%s 的长度限制必须是整数。" % name
		item = item.get_next()
	return ""


func _is_integer_text(value: String) -> bool:
	if value.is_valid_int():
		return true
	if not value.is_valid_float():
		return false
	var number := float(value)
	return is_finite(number) and number == floor(number)


func _parse_integer(value: String) -> int:
	return int(value) if value.is_valid_int() else int(float(value))


func _apply_schema_versions() -> void:
	var old_tables: Array = _original_schema.get("tables", [])
	var old_by_id := {}
	for table in old_tables:
		old_by_id[str(table.get("id", ""))] = table
	for table in _schema.get("tables", []):
		var original_id := str(table.get("_editor_original_id", table.get("id", "")))
		var old: Dictionary = old_by_id.get(original_id, {})
		if old.is_empty():
			table["schema_version"] = 1
			continue
		var old_structure := _without_editor_metadata(old)
		var new_structure := _without_editor_metadata(table)
		old_structure.erase("schema_version")
		new_structure.erase("schema_version")
		if old_structure != new_structure:
			table["schema_version"] = int(old.get("schema_version", 1)) + 1
		else:
			table["schema_version"] = int(old.get("schema_version", 1))


func _sync_csv_files() -> bool:
	var schema_root := _schema_path.get_base_dir()
	var source_root := schema_root.path_join(str(_schema.source_directory)).simplify_path()
	var original_source_root := schema_root.path_join(str(_original_schema.get("source_directory", _schema.source_directory))).simplify_path()
	var directory_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(source_root))
	if directory_error != OK:
		_status.text = "保存失败：无法创建原始表目录。"
		return false
	var ignore := FileAccess.open(source_root.path_join(".gdignore"), FileAccess.WRITE)
	if ignore != null:
		ignore.store_string("\n")
	for table in _schema.get("tables", []):
		var path := source_root.path_join(str(table.source)).simplify_path()
		var original_source := str(table.get("_editor_original_source", table.source))
		var input_path := original_source_root.path_join(original_source).simplify_path()
		if not FileAccess.file_exists(input_path):
			input_path = path
		if table.has("_editor_original_id") and not FileAccess.file_exists(input_path):
			_status.text = "保存失败：Schema 引用的 CSV 已缺失，请恢复文件或移除数据表：%s" % table.source
			return false
		if not _sync_csv(input_path, path, table):
			return false
	return true


func _sync_csv(input_path: String, output_path: String, table: Dictionary) -> bool:
	var rows: Array[PackedStringArray] = []
	var old_header := PackedStringArray()
	if FileAccess.file_exists(input_path):
		var input := FileAccess.open(input_path, FileAccess.READ)
		if input == null:
			_status.text = "保存失败：无法读取 CSV：%s" % input_path
			return false
		if input.get_position() < input.get_length():
			old_header = input.get_csv_line()
		while input.get_position() < input.get_length():
			rows.append(input.get_csv_line())
	var fields: Array = table.get("fields", [])
	var new_header := PackedStringArray()
	for field in fields:
		new_header.append(str(field.name))
	var output := FileAccess.open(output_path, FileAccess.WRITE)
	if output == null:
		_status.text = "保存失败：无法写入 CSV：%s" % output_path
		return false
	output.store_csv_line(new_header)
	for old_row in rows:
		var new_row := PackedStringArray()
		for index in new_header.size():
			var field: Dictionary = fields[index]
			var old_name := str(field.get("_editor_original_name", new_header[index]))
			var old_index := old_header.find(old_name)
			new_row.append(old_row[old_index] if old_index >= 0 and old_index < old_row.size() else "")
		output.store_csv_line(new_row)
	return true


func _without_editor_metadata(value: Dictionary) -> Dictionary:
	var result := value.duplicate(true)
	result.erase("_editor_original_id")
	result.erase("_editor_original_source")
	for field in result.get("fields", []):
		field.erase("_editor_original_name")
		var type := str(field.get("type", ""))
		for key in ["default", "min", "max"]:
			if not field.has(key):
				continue
			if type == "int32":
				field[key] = int(field[key])
			elif type == "float64":
				field[key] = float(field[key])
		for key in ["min_length", "max_length"]:
			if field.has(key):
				field[key] = int(field[key])
	return result


func _normalize_schema_numbers() -> void:
	for table in _schema.get("tables", []):
		table["schema_version"] = int(table.get("schema_version", 1))
		for field in table.get("fields", []):
			var type := str(field.get("type", ""))
			for key in ["default", "min", "max"]:
				if not field.has(key):
					continue
				if type == "int32":
					field[key] = int(field[key])
				elif type == "float64":
					field[key] = float(field[key])
			for key in ["min_length", "max_length"]:
				if field.has(key):
					field[key] = int(field[key])


func _strip_editor_metadata() -> void:
	for table in _schema.get("tables", []):
		table.erase("_editor_original_id")
		table.erase("_editor_original_source")
		for field in table.get("fields", []):
			field.erase("_editor_original_name")


func _is_safe_relative_path(value: String) -> bool:
	if value.strip_edges().is_empty() or value.begins_with("/") or value.contains("\\") or value.contains("://"):
		return false
	for part in value.split("/", true):
		if part == "..":
			return false
	return true
