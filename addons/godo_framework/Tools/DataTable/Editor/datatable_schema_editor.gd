@tool
extends RefCounted

const SUPPORTED_TYPES := ["string", "bool", "int32", "float64", "enum"]
const AUDIENCES := ["Shared", "ClientOnly", "ServerOnly"]
const INLINE_EDITABLE_FIELD_COLUMNS := [0, 1, 3, 4, 5, 6, 7, 8, 9, 11]
const NORMAL_COLOR := Color("#8BD49C")
const PENDING_COLOR := Color("#FFD166")
const ERROR_COLOR := Color("#FF6B6B")
const FIELD_ROW_SELECTION_COLOR := Color("#354052")

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
var _advanced_settings: GridContainer
var _advanced_settings_button: Button
var _data_files: Tree
var _add_data_file_button: Button
var _remove_data_file_button: Button
var _table_selector: OptionButton
var _table_id: LineEdit
var _table_source: LineEdit
var _table_audience: OptionButton
var _primary_key: OptionButton
var _schema_version: LineEdit
var _fields: Tree
var _status: Label
var _remove_confirmation: ConfirmationDialog
var _table_value_dialog: ConfirmationDialog
var _table_value_input: LineEdit
var _table_value_mode := ""
var _schema: Dictionary = {}
var _original_schema: Dictionary = {}
var _removed_tables_by_source: Dictionary = {}
var _selected_table := -1
var _selected_field_column := 0
var _editing_field_item: TreeItem
var _editing_field_column := -1
var _editing_field_previous_text := ""
var _selected_field_item: TreeItem
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
	_dialog.popup_centered(Vector2i(1500, 900))


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
	_removed_tables_by_source.clear()
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
	_dialog.min_size = Vector2i(1500, 900)
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
	dataset_grid.name = "DataTableSchemaDatasetGrid"
	dataset_grid.columns = 4
	dataset_grid.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	content.add_child(dataset_grid)
	_data_set_id = _add_line_setting(dataset_grid, "数据集 ID")
	_data_set_id.name = "DataTableSchemaDataSetId"
	_data_set_id.custom_minimum_size.x = 360
	_data_set_id.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_data_set_id.tooltip_text = "稳定数据集标识；修改后会影响生成的数据集入口。"
	_namespace = _add_line_setting(dataset_grid, "C# 命名空间")
	_namespace.custom_minimum_size.x = 480
	_namespace.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_namespace.tooltip_text = "生成的行类型、表类型和数据集入口所在的 C# 命名空间。"

	var dataset_options := HBoxContainer.new()
	dataset_options.name = "DataTableSchemaDatasetOptions"
	content.add_child(dataset_options)
	dataset_options.add_child(_label("协议版本"))
	_protocol_version = SpinBox.new()
	_protocol_version.name = "DataTableSchemaProtocolVersion"
	_protocol_version.min_value = 1
	_protocol_version.max_value = 2147483647
	_protocol_version.custom_minimum_size.x = 120
	_protocol_version.tooltip_text = "客户端与服务器共享的数据表结构发生不兼容变化时手动递增。"
	dataset_options.add_child(_protocol_version)
	var protocol_version_hint := _label("客户端与服务器共享数据结构不兼容时手动递增")
	protocol_version_hint.name = "DataTableSchemaProtocolVersionHint"
	dataset_options.add_child(protocol_version_hint)
	var dataset_options_spacer := Control.new()
	dataset_options_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	dataset_options.add_child(dataset_options_spacer)

	_advanced_settings_button = Button.new()
	_advanced_settings_button.name = "DataTableSchemaAdvancedSettingsButton"
	_advanced_settings_button.text = "显示高级设置"
	_advanced_settings_button.toggle_mode = true
	_advanced_settings_button.toggled.connect(_toggle_advanced_settings)
	dataset_options.add_child(_advanced_settings_button)

	_advanced_settings = GridContainer.new()
	_advanced_settings.name = "DataTableSchemaAdvancedSettings"
	_advanced_settings.columns = 6
	_advanced_settings.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_advanced_settings.visible = false
	content.add_child(_advanced_settings)
	_source_directory = _add_line_setting(_advanced_settings, "原始表目录")
	_source_directory.custom_minimum_size.x = 300
	_source_directory.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_source_directory.text_submitted.connect(_on_source_directory_submitted)
	_source_directory.focus_exited.connect(_refresh_data_files)
	_output_directory = _add_line_setting(_advanced_settings, "运行数据目录")
	_output_directory.custom_minimum_size.x = 300
	_output_directory.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_csharp_output = _add_line_setting(_advanced_settings, "C# 代码文件")
	_csharp_output.custom_minimum_size.x = 300
	_csharp_output.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	var data_files_top_separator := HSeparator.new()
	data_files_top_separator.name = "DataTableSchemaDataFilesTopSeparator"
	content.add_child(data_files_top_separator)

	var data_file_bar := HBoxContainer.new()
	content.add_child(data_file_bar)
	data_file_bar.add_child(_label("数据文件"))
	var refresh_data_files := Button.new()
	refresh_data_files.text = "刷新"
	refresh_data_files.pressed.connect(_refresh_data_files)
	data_file_bar.add_child(refresh_data_files)
	var open_data_directory := Button.new()
	open_data_directory.name = "DataTableSchemaOpenDataDirectoryButton"
	open_data_directory.text = "打开数据目录"
	open_data_directory.pressed.connect(_open_data_directory)
	data_file_bar.add_child(open_data_directory)
	var data_file_spacer := Control.new()
	data_file_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	data_file_bar.add_child(data_file_spacer)
	_add_data_file_button = Button.new()
	_add_data_file_button.name = "DataTableSchemaAddCsvButton"
	_add_data_file_button.text = "加入 Schema"
	_add_data_file_button.disabled = true
	_add_data_file_button.pressed.connect(_add_selected_csv)
	data_file_bar.add_child(_add_data_file_button)
	_remove_data_file_button = Button.new()
	_remove_data_file_button.name = "DataTableSchemaRemoveCsvButton"
	_remove_data_file_button.text = "移出 Schema..."
	_remove_data_file_button.disabled = true
	_remove_data_file_button.pressed.connect(_request_remove_selected_csv)
	data_file_bar.add_child(_remove_data_file_button)

	_data_files = Tree.new()
	_data_files.name = "DataTableSchemaDataFiles"
	_data_files.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_data_files.columns = 3
	_data_files.column_titles_visible = true
	_data_files.hide_root = true
	_data_files.select_mode = Tree.SELECT_ROW
	_data_files.custom_minimum_size.y = 120
	_data_files.set_column_title(0, "文件")
	_data_files.set_column_title(1, "状态")
	_data_files.set_column_title(2, "数据表 ID")
	_data_files.set_column_expand(0, true)
	_data_files.set_column_expand(1, false)
	_data_files.set_column_expand(2, false)
	_data_files.set_column_custom_minimum_width(1, 100)
	_data_files.set_column_custom_minimum_width(2, 300)
	_data_files.item_selected.connect(_update_data_file_actions)
	content.add_child(_data_files)
	var data_files_bottom_separator := HSeparator.new()
	data_files_bottom_separator.name = "DataTableSchemaDataFilesBottomSeparator"
	content.add_child(data_files_bottom_separator)

	var table_bar := HBoxContainer.new()
	content.add_child(table_bar)
	table_bar.add_child(_label("数据表"))
	_table_selector = OptionButton.new()
	_table_selector.name = "DataTableSchemaTableSelector"
	_table_selector.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_table_selector.custom_minimum_size.x = 420
	_table_selector.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_table_selector.item_selected.connect(_on_table_selected)
	table_bar.add_child(_table_selector)
	var add_table := Button.new()
	add_table.name = "DataTableSchemaCreateTableButton"
	add_table.text = "新建数据表..."
	add_table.pressed.connect(_request_add_table)
	table_bar.add_child(add_table)

	var table_details := HBoxContainer.new()
	table_details.name = "DataTableSchemaTableDetails"
	table_details.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	table_details.add_theme_constant_override("separation", 20)
	content.add_child(table_details)
	var table_left := GridContainer.new()
	table_left.columns = 2
	table_left.custom_minimum_size.x = 580
	table_left.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	table_details.add_child(table_left)
	var table_right := GridContainer.new()
	table_right.columns = 2
	table_right.custom_minimum_size.x = 620
	table_right.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	table_details.add_child(table_right)

	var table_id_label := _label("数据表 ID")
	table_id_label.name = "DataTableSchemaTableIdLabel"
	table_left.add_child(table_id_label)
	var table_id_row := HBoxContainer.new()
	table_id_row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	table_left.add_child(table_id_row)
	_table_id = LineEdit.new()
	_table_id.name = "DataTableSchemaTableId"
	_table_id.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_table_id.editable = false
	_table_id.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_table_id.tooltip_text = "用于生成 C# 类型、数据集入口、Manifest 数据表 ID 和 .gdtb 文件名。"
	table_id_row.add_child(_table_id)
	var rename_table_id := Button.new()
	rename_table_id.name = "DataTableSchemaRenameTableIdButton"
	rename_table_id.text = "重命名..."
	rename_table_id.pressed.connect(_request_rename_table_id)
	table_id_row.add_child(rename_table_id)

	table_right.add_child(_label("CSV 文件"))
	var table_source_row := HBoxContainer.new()
	table_source_row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	table_right.add_child(table_source_row)
	_table_source = LineEdit.new()
	_table_source.name = "DataTableSchemaTableSource"
	_table_source.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_table_source.editable = false
	_table_source.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	table_source_row.add_child(_table_source)
	var change_table_source := Button.new()
	change_table_source.name = "DataTableSchemaChangeTableSourceButton"
	change_table_source.text = "修改路径..."
	change_table_source.pressed.connect(_request_change_table_source)
	table_source_row.add_child(change_table_source)

	table_left.add_child(_label("主键字段"))
	_primary_key = OptionButton.new()
	_primary_key.name = "DataTableSchemaPrimaryKey"
	_primary_key.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_primary_key.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	table_left.add_child(_primary_key)
	var export_scope_label := _label("数据导出范围")
	export_scope_label.name = "DataTableSchemaExportScopeLabel"
	table_right.add_child(export_scope_label)
	_table_audience = OptionButton.new()
	_table_audience.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_table_audience.tooltip_text = "Shared 同时导出到客户端和服务器；ClientOnly 仅客户端；ServerOnly 仅服务器。"
	for audience in AUDIENCES:
		_table_audience.add_item(audience)
	_table_audience.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	table_right.add_child(_table_audience)
	table_left.add_child(_label("当前表结构版本"))
	var schema_version_row := HBoxContainer.new()
	table_left.add_child(schema_version_row)
	_schema_version = LineEdit.new()
	_schema_version.name = "DataTableSchemaVersion"
	_schema_version.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_schema_version.editable = false
	_schema_version.custom_minimum_size.x = 120
	_schema_version.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_schema_version.tooltip_text = "字段、主键、数据导出范围或数据表标识发生结构变化时由工具自动递增。"
	schema_version_row.add_child(_schema_version)
	var schema_version_hint := _label("保存结构变更时由工具自动递增")
	schema_version_hint.name = "DataTableSchemaVersionHint"
	schema_version_row.add_child(schema_version_hint)
	table_right.add_child(Control.new())
	table_right.add_child(Control.new())

	content.add_child(HSeparator.new())

	var field_bar := HBoxContainer.new()
	content.add_child(field_bar)
	field_bar.add_child(_label("字段（单击选择整行；双击文本单元格，类型和复选框单击操作）"))
	var add_field := Button.new()
	add_field.text = "新增字段..."
	add_field.pressed.connect(_add_field)
	field_bar.add_child(add_field)
	var remove_field := Button.new()
	remove_field.text = "移除字段..."
	remove_field.pressed.connect(_request_remove_field)
	field_bar.add_child(remove_field)
	var empty_value_hint := Label.new()
	empty_value_hint.name = "DataTableSchemaEmptyValueHint"
	empty_value_hint.text = "空白表示未配置，不会自动采用 0、false 或空字符串。"
	empty_value_hint.tooltip_text = "CSV 空单元格只有在字段配置了默认值时才使用默认值；否则按必填、允许空字符串和可空规则处理。"
	content.add_child(empty_value_hint)

	_fields = Tree.new()
	_fields.name = "DataTableSchemaFields"
	_fields.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_fields.columns = 12
	_fields.column_titles_visible = true
	_fields.hide_root = true
	_fields.select_mode = Tree.SELECT_SINGLE
	_fields.scroll_horizontal_enabled = false
	_fields.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_fields.item_mouse_selected.connect(_on_field_mouse_selected)
	_fields.item_activated.connect(_edit_selected_field_cell)
	_fields.item_edited.connect(_on_field_item_edited)
	var titles := ["名称", "类型", "必填", "默认值", "最小值", "最大值", "最短长度", "最长长度", "枚举值", "外键", "允许空字符串", "Null Token"]
	var widths := [140, 90, 64, 100, 80, 80, 84, 84, 160, 170, 100, 110]
	for index in titles.size():
		_fields.set_column_title(index, titles[index])
		_fields.set_column_custom_minimum_width(index, widths[index])
		_fields.set_column_expand(index, index in [0, 8, 9])
		if index in [2, 10]:
			_fields.set_column_title_alignment(index, HORIZONTAL_ALIGNMENT_CENTER)
	_fields.set_column_title_tooltip_text(1, "字段类型；新字段默认使用 string。")
	_fields.set_column_title_tooltip_text(2, "关闭时字段可空；CSV 空单元格在没有默认值时读取为 null。")
	_fields.set_column_title_tooltip_text(3, "CSV 单元格为空时使用；留空表示不配置默认值。")
	_fields.set_column_title_tooltip_text(4, "仅数值类型可用；留空表示不限制最小值。")
	_fields.set_column_title_tooltip_text(5, "仅数值类型可用；留空表示不限制最大值。")
	_fields.set_column_title_tooltip_text(6, "仅字符串可用；留空表示不限制最短长度。")
	_fields.set_column_title_tooltip_text(7, "仅字符串可用；留空表示不限制最长长度。")
	_fields.set_column_title_tooltip_text(8, "仅 enum 类型使用，多个值用逗号分隔。")
	_fields.set_column_title_tooltip_text(9, "格式为 Table.field；留空表示不校验外键。")
	_fields.set_column_title_tooltip_text(10, "仅字符串可用；关闭时空单元格不会自动读取为空字符串。")
	_fields.set_column_title_tooltip_text(11, "匹配该文本时读取为 null；留空表示不启用显式 null 标记。")
	content.add_child(_fields)
	content.add_child(HSeparator.new())

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

	_table_value_dialog = ConfirmationDialog.new()
	_table_value_dialog.ok_button_text = "确认修改"
	_table_value_dialog.cancel_button_text = "取消"
	_table_value_dialog.get_label().hide()
	_table_value_dialog.confirmed.connect(_apply_table_value_change)
	_table_value_input = LineEdit.new()
	_table_value_input.name = "DataTableSchemaTableValueInput"
	_table_value_input.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_table_value_dialog.add_child(_table_value_input)
	_table_value_input.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	_table_value_input.offset_left = 16
	_table_value_input.offset_right = -16
	_table_value_input.offset_top = 16
	_table_value_input.offset_bottom = 48
	_dialog.add_child(_table_value_dialog)
	_context.get_editor_interface().get_base_control().add_child(_dialog)


func _add_line_setting(parent: GridContainer, title: String) -> LineEdit:
	parent.add_child(_label(title))
	var input := LineEdit.new()
	input.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(input)
	return input


func _label(text: String) -> Label:
	var result := Label.new()
	result.text = text
	return result


func _toggle_advanced_settings(visible: bool) -> void:
	_advanced_settings.visible = visible
	_advanced_settings_button.text = "隐藏高级设置" if visible else "显示高级设置"


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
			if configured.has(file_name):
				_add_data_file_item(root_item, file_name, "已加入", str(configured[file_name]), NORMAL_COLOR)
			else:
				_add_data_file_item(root_item, file_name, "未加入", "—", PENDING_COLOR)
	for source in configured:
		if not found.has(source):
			_add_data_file_item(root_item, source, "文件缺失", str(configured[source]), ERROR_COLOR)
	_update_data_file_actions()


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


func _add_data_file_item(
	root: TreeItem,
	file_name: String,
	state: String,
	table_id: String,
	state_color: Color
) -> void:
	var item := _data_files.create_item(root)
	item.set_text(0, file_name)
	item.set_text(1, state)
	item.set_text(2, table_id)
	item.set_custom_color(1, state_color)
	item.set_metadata(0, file_name)


func _update_data_file_actions() -> void:
	if not is_instance_valid(_add_data_file_button) or not is_instance_valid(_remove_data_file_button):
		return
	var selected := _data_files.get_selected()
	var configured := false
	if selected != null:
		configured = _table_index_for_source(str(selected.get_metadata(0))) >= 0
	_add_data_file_button.disabled = selected == null or configured
	_remove_data_file_button.disabled = selected == null or not configured


func _table_index_for_source(source: String) -> int:
	var tables: Array = _schema.get("tables", [])
	for index in tables.size():
		if str(tables[index].get("source", "")) == source:
			return index
	return -1


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
	if _removed_tables_by_source.has(source):
		var restored: Dictionary = _removed_tables_by_source[source].duplicate(true)
		var restored_tables: Array = _schema.get("tables", [])
		if not _contains_table_id(restored_tables, str(restored.get("id", ""))):
			restored_tables.append(restored)
			_schema["tables"] = restored_tables
			_removed_tables_by_source.erase(source)
			_refresh_table_selector(restored_tables.size() - 1)
			_refresh_data_files()
			_status.text = "%s 已恢复到 Schema，原数据表 ID、字段类型和约束均已保留。" % source
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
		if name != raw_name or not _is_ascii_identifier(name):
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
	if not _is_ascii_identifier(result):
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


func _request_rename_table_id() -> void:
	if _selected_table < 0:
		return
	_table_value_mode = "table_id"
	_table_value_dialog.title = "重命名数据表 ID"
	_table_value_dialog.dialog_text = ""
	_table_value_input.placeholder_text = "例如：Item"
	_table_value_input.text = _table_id.text
	_table_value_dialog.popup_centered(Vector2i(520, 130))
	_table_value_input.grab_focus()
	_table_value_input.select_all()


func _request_change_table_source() -> void:
	if _selected_table < 0:
		return
	_table_value_mode = "table_source"
	_table_value_dialog.title = "修改 CSV 相对路径"
	_table_value_dialog.dialog_text = ""
	_table_value_input.placeholder_text = "例如：Items.csv"
	_table_value_input.text = _table_source.text
	_table_value_dialog.popup_centered(Vector2i(520, 130))
	_table_value_input.grab_focus()
	_table_value_input.select_all()


func _apply_table_value_change() -> void:
	var value := _table_value_input.text.strip_edges()
	if _table_value_mode == "new_table":
		_add_table(value)
		return
	if _selected_table < 0:
		return
	if _table_value_mode == "table_id":
		if not _is_ascii_identifier(value):
			_status.text = "重命名失败：数据表 ID 必须是有效标识符。"
			return
		if _table_value_used_by_other("id", value):
			_status.text = "重命名失败：数据表 ID 已存在：%s。" % value
			return
		if value == _table_id.text:
			return
		var previous_table_id := _table_id.text
		_table_id.text = value
		_commit_current_table()
		_replace_foreign_key_target(previous_table_id, "", value, "")
		_refresh_table_selector(_selected_table)
		_refresh_data_files()
		_status.text = "数据表 ID 已改为 %s；保存后生成代码和运行数据文件名会同步变化。" % value
		return
	if _table_value_mode != "table_source":
		return
	if not _is_safe_relative_path(value) or value.get_extension().to_lower() != "csv":
		_status.text = "修改失败：CSV 文件必须是原始表目录内的安全 .csv 相对路径。"
		return
	if _table_value_used_by_other("source", value):
		_status.text = "修改失败：CSV 文件已被其他数据表使用：%s。" % value
		return
	if value == _table_source.text:
		return
	var source_root := _source_root()
	var target_path := source_root.path_join(value).simplify_path() if not source_root.is_empty() else ""
	if not target_path.is_empty() and FileAccess.file_exists(target_path):
		_status.text = "修改失败：目标 CSV 已存在，不会自动覆盖：%s。" % value
		return
	_table_source.text = value
	_commit_current_table()
	_refresh_data_files()
	_status.text = "CSV 路径已改为 %s；保存时写入新文件，旧文件会保留。" % value


func _table_value_used_by_other(key: String, value: String) -> bool:
	var tables: Array = _schema.get("tables", [])
	for index in tables.size():
		if index != _selected_table and str(tables[index].get(key, "")) == value:
			return true
	return false


func _replace_foreign_key_target(
	old_table_id: String,
	old_field_name: String,
	new_table_id: String,
	new_field_name: String
) -> void:
	var tables: Array = _schema.get("tables", [])
	for table in tables:
		for field in table.get("fields", []):
			var foreign_key := str(field.get("foreign_key", ""))
			var separator := foreign_key.find(".")
			if separator < 0:
				continue
			var target_table := foreign_key.left(separator)
			var target_field := foreign_key.substr(separator + 1)
			if target_table != old_table_id:
				continue
			if not old_field_name.is_empty() and target_field != old_field_name:
				continue
			field["foreign_key"] = "%s.%s" % [
				new_table_id,
				new_field_name if not old_field_name.is_empty() else target_field,
			]
	var root := _fields.get_root()
	var item := root.get_first_child() if root != null else null
	while item != null:
		var foreign_key := item.get_text(9)
		var separator := foreign_key.find(".")
		if separator >= 0:
			var target_table := foreign_key.left(separator)
			var target_field := foreign_key.substr(separator + 1)
			if target_table == old_table_id and (old_field_name.is_empty() or target_field == old_field_name):
				item.set_text(9, "%s.%s" % [
					new_table_id,
					new_field_name if not old_field_name.is_empty() else target_field,
				])
		item = item.get_next()


func _foreign_key_referrers(target_table_id: String, target_field_name := "") -> PackedStringArray:
	var result := PackedStringArray()
	for table in _schema.get("tables", []):
		for field in table.get("fields", []):
			var foreign_key := str(field.get("foreign_key", ""))
			var expected := "%s.%s" % [target_table_id, target_field_name]
			if (
				foreign_key == expected
				or (target_field_name.is_empty() and foreign_key.begins_with(target_table_id + "."))
			):
				result.append("%s.%s" % [table.get("id", ""), field.get("name", "")])
	return result


func _populate_table() -> void:
	_loading = true
	_selected_field_item = null
	_editing_field_item = null
	_fields.clear()
	var root := _fields.create_item()
	var tables: Array = _schema.get("tables", [])
	var enabled := _selected_table >= 0 and _selected_table < tables.size()
	_table_audience.disabled = not enabled
	_primary_key.disabled = not enabled
	_fields.mouse_filter = Control.MOUSE_FILTER_STOP if enabled else Control.MOUSE_FILTER_IGNORE
	if not enabled:
		_table_id.text = ""
		_table_source.text = ""
		_primary_key.clear()
		_schema_version.text = "-"
		_loading = false
		return
	var table: Dictionary = tables[_selected_table]
	_table_id.text = str(table.get("id", ""))
	_table_source.text = str(table.get("source", ""))
	_refresh_primary_key_options(table.get("fields", []), str(table.get("primary_key", "id")))
	_table_audience.select(maxi(0, AUDIENCES.find(str(table.get("audience", "Shared")))))
	_schema_version.text = str(int(table.get("schema_version", 1)))
	for field in table.get("fields", []):
		_add_field_item(root, field)
	_loading = false


func _refresh_primary_key_options(fields: Array, selected_name: String) -> void:
	_primary_key.clear()
	for field in fields:
		_primary_key.add_item(str(field.get("name", "")))
	var selected_index := -1
	for index in _primary_key.item_count:
		if _primary_key.get_item_text(index) == selected_name:
			selected_index = index
			break
	if selected_index >= 0:
		_primary_key.select(selected_index)
	elif _primary_key.item_count > 0:
		_primary_key.select(0)


func _selected_primary_key() -> String:
	if _primary_key.selected < 0:
		return ""
	return _primary_key.get_item_text(_primary_key.selected)


func _add_field_item(root: TreeItem, field: Dictionary) -> TreeItem:
	var item := _fields.create_item(root)
	item.set_metadata(0, field.duplicate(true))
	item.set_text(0, str(field.get("name", "field")))
	item.set_cell_mode(1, TreeItem.CELL_MODE_RANGE)
	item.set_text(1, ",".join(SUPPORTED_TYPES))
	item.set_range_config(1, 0, SUPPORTED_TYPES.size() - 1, 1)
	item.set_range(1, maxi(0, SUPPORTED_TYPES.find(str(field.get("type", "string")))))
	item.set_editable(1, true)
	item.set_tooltip_text(1, "字段类型；新字段默认使用 string。")
	item.set_cell_mode(2, TreeItem.CELL_MODE_CHECK)
	item.set_checked(2, bool(field.get("required", false)))
	item.set_editable(2, true)
	item.set_text_alignment(2, HORIZONTAL_ALIGNMENT_CENTER)
	item.set_text(3, _format_optional(field, "default"))
	item.set_tooltip_text(3, "留空表示不配置默认值，不会自动采用类型默认值。")
	item.set_text(4, _format_optional(field, "min"))
	item.set_tooltip_text(4, "留空表示不限制最小值。")
	item.set_text(5, _format_optional(field, "max"))
	item.set_tooltip_text(5, "留空表示不限制最大值。")
	item.set_text(6, _format_optional(field, "min_length"))
	item.set_tooltip_text(6, "留空表示不限制最短长度。")
	item.set_text(7, _format_optional(field, "max_length"))
	item.set_tooltip_text(7, "留空表示不限制最长长度。")
	item.set_text(8, ",".join(field.get("values", [])))
	item.set_tooltip_text(8, "仅 enum 类型使用，多个值用逗号分隔。")
	item.set_text(9, str(field.get("foreign_key", "")))
	item.set_tooltip_text(9, "格式为 Table.field；留空表示不校验外键。")
	item.set_cell_mode(10, TreeItem.CELL_MODE_CHECK)
	item.set_checked(10, bool(field.get("allow_empty", false)))
	item.set_editable(10, true)
	item.set_text_alignment(10, HORIZONTAL_ALIGNMENT_CENTER)
	item.set_text(11, str(field.get("null_token", "")))
	item.set_tooltip_text(11, "留空表示不启用显式 null 标记。")
	return item


func _on_field_mouse_selected(mouse_position: Vector2, mouse_button_index: int) -> void:
	if mouse_button_index != MOUSE_BUTTON_LEFT:
		return
	_restore_field_row_selection()
	_selected_field_column = clampi(_fields.get_column_at_position(mouse_position), 0, _fields.columns - 1)
	_select_field_row(_fields.get_selected(), _selected_field_column)


func _edit_selected_field_cell() -> void:
	var item := _selected_field_item
	if item == null or _selected_field_column not in INLINE_EDITABLE_FIELD_COLUMNS:
		return
	_editing_field_item = item
	_editing_field_column = _selected_field_column
	_editing_field_previous_text = item.get_text(_editing_field_column)
	item.set_editable(_editing_field_column, true)
	_select_field_row(item, _editing_field_column)
	if not _fields.edit_selected():
		_restore_field_row_selection()


func _on_field_item_edited() -> void:
	if _editing_field_item == null:
		return
	if _editing_field_column == 0:
		var selected_primary := _selected_primary_key()
		var new_field_name := _editing_field_item.get_text(0).strip_edges()
		var renamed_primary := new_field_name if selected_primary == _editing_field_previous_text else selected_primary
		if new_field_name != _editing_field_previous_text and _selected_table >= 0:
			_replace_foreign_key_target(
				_table_id.text,
				_editing_field_previous_text,
				_table_id.text,
				new_field_name
			)
		_refresh_primary_key_options(_field_option_values(), renamed_primary)
	call_deferred("_restore_field_row_selection")


func _restore_field_row_selection() -> void:
	if _editing_field_item != null and is_instance_valid(_editing_field_item):
		if _editing_field_column in INLINE_EDITABLE_FIELD_COLUMNS and _editing_field_column != 1:
			_editing_field_item.set_editable(_editing_field_column, false)
		_select_field_row(_editing_field_item, _editing_field_column)
	_editing_field_item = null
	_editing_field_column = -1
	_editing_field_previous_text = ""


func _select_field_row(item: TreeItem, focus_column: int) -> void:
	if item == null:
		return
	if _selected_field_item != null and is_instance_valid(_selected_field_item):
		for column in _fields.columns:
			_selected_field_item.clear_custom_bg_color(column)
	_selected_field_item = item
	for column in _fields.columns:
		item.set_custom_bg_color(column, FIELD_ROW_SELECTION_COLOR)
	_fields.set_selected(item, clampi(focus_column, 0, _fields.columns - 1))


func _field_option_values() -> Array:
	var result: Array = []
	var root := _fields.get_root()
	if root == null:
		return result
	var item := root.get_first_child()
	while item != null:
		result.append({"name": item.get_text(0)})
		item = item.get_next()
	return result


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
	var primary_key := _selected_primary_key()
	table["audience"] = AUDIENCES[_table_audience.selected]
	var fields: Array = []
	var renamed_fields: Array[Dictionary] = []
	var root := _fields.get_root()
	if root != null:
		var item := root.get_first_child()
		while item != null:
			var field := _field_from_item(item)
			var original_name := str(field.get("_editor_reference_name", field.get("name", "")))
			var current_name := str(field.get("name", ""))
			if original_name != current_name:
				renamed_fields.append({"old": original_name, "new": current_name})
			field["_editor_reference_name"] = current_name
			fields.append(field)
			item = item.get_next()
	if not _contains_field_name(fields, primary_key):
		for field in fields:
			if str(field.get("_editor_original_name", "")) == primary_key:
				primary_key = str(field.get("name", ""))
				break
	table["primary_key"] = primary_key
	table["fields"] = fields
	tables[_selected_table] = table
	_schema["tables"] = tables
	for rename in renamed_fields:
		_replace_foreign_key_target(table.id, rename.old, table.id, rename.new)


func _contains_field_name(fields: Array, field_name: String) -> bool:
	for field in fields:
		if str(field.get("name", "")) == field_name:
			return true
	return false


func _field_from_item(item: TreeItem) -> Dictionary:
	var field = item.get_metadata(0)
	var result: Dictionary = field.duplicate(true) if field is Dictionary else {}
	result["_editor_original_name"] = str(result.get("_editor_original_name", result.get("name", "")))
	result["_editor_reference_name"] = str(result.get("_editor_reference_name", result.get("name", "")))
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


func _request_add_table() -> void:
	var error := _validate_field_inputs()
	if not error.is_empty():
		_status.text = "新增失败：%s" % error
		return
	_table_value_mode = "new_table"
	_table_value_dialog.title = "新建数据表"
	_table_value_dialog.dialog_text = ""
	_table_value_input.placeholder_text = "输入数据表 ID，例如：Quest"
	_table_value_input.text = ""
	_table_value_dialog.popup_centered(Vector2i(520, 130))
	_table_value_input.grab_focus()


func _add_table(id: String) -> void:
	if not _is_ascii_identifier(id):
		_status.text = "新增失败：数据表 ID 必须是有效标识符。"
		return
	_commit_current_table()
	var tables: Array = _schema.get("tables", [])
	if _contains_table_id(tables, id):
		_status.text = "新增失败：数据表 ID 已存在：%s。" % id
		return
	var source := "%s.csv" % id
	if _table_index_for_source(source) >= 0:
		_status.text = "新增失败：CSV 文件已被其他数据表使用：%s。" % source
		return
	var source_root := _source_root()
	var source_path := source_root.path_join(source).simplify_path() if not source_root.is_empty() else ""
	if not source_path.is_empty() and FileAccess.file_exists(source_path):
		_status.text = "新增失败：%s 已存在，请在数据文件列表中将它加入 Schema。" % source
		return
	tables.append({
		"id": id,
		"source": source,
		"schema_version": 1,
		"audience": "Shared",
		"primary_key": "id",
		"fields": [{"name": "id", "type": "string", "required": true, "min_length": 1}],
	})
	_schema["tables"] = tables
	_refresh_table_selector(tables.size() - 1)
	_refresh_data_files()
	_status.text = "已新建数据表 %s；保存 Schema 时会创建 %s。" % [id, source]


func _contains_table_id(tables: Array, id: String) -> bool:
	for table in tables:
		if str(table.get("id", "")) == id:
			return true
	return false


func _request_remove_selected_csv() -> void:
	var selected := _data_files.get_selected()
	if selected == null:
		_status.text = "请先选择一个已加入 Schema 的 CSV。"
		return
	var source := str(selected.get_metadata(0))
	var table_index := _table_index_for_source(source)
	if table_index < 0:
		_status.text = "%s 尚未加入 Schema。" % source
		return
	var tables: Array = _schema.get("tables", [])
	var table_id := str(tables[table_index].get("id", ""))
	_remove_confirmation.dialog_text = (
		"确认将 %s（%s）移出 Schema？\nCSV 文件会保留。" % [source, table_id]
	)
	var callback := _remove_table.bind(table_index)
	_reset_confirmation(callback)
	_remove_confirmation.popup_centered(Vector2i(560, 180))


func _remove_table(table_index: int) -> void:
	var field_error := _validate_field_inputs()
	if not field_error.is_empty():
		_status.text = "移出失败：%s" % field_error
		return
	_commit_current_table()
	var tables: Array = _schema.get("tables", [])
	if table_index < 0 or table_index >= tables.size():
		return
	var table_id := str(tables[table_index].get("id", ""))
	var referrers := _foreign_key_referrers(table_id)
	if not referrers.is_empty():
		_status.text = "移出失败：数据表 %s 仍被这些字段引用：%s。" % [table_id, ", ".join(referrers)]
		return
	var source := str(tables[table_index].get("source", ""))
	_removed_tables_by_source[source] = tables[table_index].duplicate(true)
	tables.remove_at(table_index)
	_schema["tables"] = tables
	_refresh_table_selector(mini(table_index, tables.size() - 1))
	_refresh_data_files()
	_status.text = "%s 已移出 Schema；CSV 文件仍保留在数据目录中。" % source


func _add_field() -> void:
	if _selected_table < 0:
		return
	var root := _fields.get_root()
	var item := _add_field_item(root, {"name": "field", "type": "string", "required": false})
	_select_field_row(item, 0)
	_selected_field_column = 0
	_refresh_primary_key_options(_field_option_values(), _selected_primary_key())


func _request_remove_field() -> void:
	var selected := _selected_field_item
	if selected == null:
		_status.text = "请先选择需要移除的字段。"
		return
	var field_error := _validate_field_inputs()
	if not field_error.is_empty():
		_status.text = "移除失败：%s" % field_error
		return
	_commit_current_table()
	var field_name := selected.get_text(0)
	var referrers := _foreign_key_referrers(_table_id.text, field_name)
	if not referrers.is_empty():
		_status.text = "移除失败：字段 %s.%s 仍被这些字段引用：%s。" % [
			_table_id.text,
			field_name,
			", ".join(referrers),
		]
		return
	_remove_confirmation.dialog_text = (
		"确认移除字段“%s”？\n保存 Schema 时会删除 CSV 中对应列。" % selected.get_text(0)
	)
	var callback := _remove_field
	_reset_confirmation(callback)
	_remove_confirmation.popup_centered(Vector2i(560, 180))


func _remove_field() -> void:
	var selected := _selected_field_item
	if selected == null:
		return
	var next_selection := selected.get_next()
	if next_selection == null:
		next_selection = selected.get_prev()
	var primary_key := _selected_primary_key()
	selected.free()
	_selected_field_item = null
	_refresh_primary_key_options(_field_option_values(), primary_key)
	if next_selection != null:
		_select_field_row(next_selection, 0)


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
	var csv_state := _build_csv_files()
	if not csv_state.valid:
		return
	var saved_schema := _schema.duplicate(true)
	_normalize_schema_numbers(saved_schema)
	_strip_editor_metadata(saved_schema)
	var files: Dictionary = csv_state.files
	files[_schema_path] = JSON.stringify(saved_schema, "\t") + "\n"
	if not _commit_text_files(files):
		return
	_schema = saved_schema
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
	var namespace_value := _namespace.text.strip_edges()
	if namespace_value.is_empty():
		return "C# 命名空间不能为空。"
	for part in namespace_value.split(".", true):
		if not _is_ascii_identifier(part):
			return "C# 命名空间包含无效标识符：%s。" % part
	for path in [_source_directory.text, _output_directory.text, _csharp_output.text]:
		if not _is_safe_relative_path(path):
			return "目录和输出文件必须是 Schema 目录内的安全相对路径。"
	var schema_root := _schema_path.get_base_dir()
	var source_root := schema_root.path_join(_source_directory.text).simplify_path()
	var output_root := schema_root.path_join(_output_directory.text).simplify_path()
	var csharp_path := schema_root.path_join(_csharp_output.text).simplify_path()
	if _is_same_or_child(source_root, output_root):
		return "运行数据目录不能包含原始表目录。"
	if _is_same_or_child(csharp_path, output_root):
		return "C# 输出必须位于运行数据目录之外。"
	var tables: Array = _schema.get("tables", [])
	if tables.is_empty():
		return "至少需要一张数据表。"
	var ids := {}
	var sources := {}
	var tables_by_id := {}
	for table in tables:
		var table_id := str(table.get("id", ""))
		if not _is_ascii_identifier(table_id) or ids.has(table_id):
			return "数据表 ID 必须是唯一的有效标识符：%s。" % table_id
		ids[table_id] = true
		tables_by_id[table_id] = table
		var source := str(table.get("source", ""))
		if not _is_safe_relative_path(source) or source.get_extension().to_lower() != "csv" or sources.has(source):
			return "CSV 文件必须是唯一的安全 .csv 相对路径：%s。" % source
		sources[source] = true
		if str(table.get("audience", "")) not in AUDIENCES:
			return "数据表 %s 的数据导出范围无效。" % table_id
		var fields_value = table.get("fields", null)
		if not fields_value is Array or fields_value.is_empty():
			return "数据表 %s 至少需要一个字段。" % table_id
		var names := {}
		for field in fields_value:
			if not field is Dictionary:
				return "数据表 %s 包含无效字段配置。" % table_id
			var name := str(field.get("name", ""))
			if not _is_ascii_identifier(name) or names.has(name):
				return "字段名必须是唯一的有效标识符：%s.%s。" % [table_id, name]
			names[name] = true
			if not SUPPORTED_TYPES.has(str(field.get("type", ""))):
				return "字段类型不受支持：%s.%s。" % [table_id, name]
			var constraint_error := _validate_field_constraints(table_id, field)
			if not constraint_error.is_empty():
				return constraint_error
		var primary_key := str(table.get("primary_key", ""))
		if not names.has(primary_key):
			return "主键字段不存在：%s.%s。" % [table_id, primary_key]
		for field in fields_value:
			if str(field.get("name", "")) == primary_key:
				if str(field.get("type", "")) != "string":
					return "主键字段必须使用 string：%s.%s。" % [table_id, primary_key]
				if not bool(field.get("required", false)):
					return "主键字段必须设为必填：%s.%s。" % [table_id, primary_key]
				break
	for table in tables:
		for field in table.get("fields", []):
			var foreign_key := str(field.get("foreign_key", ""))
			if foreign_key.is_empty():
				continue
			var parts := foreign_key.split(".", false)
			if parts.size() != 2 or not tables_by_id.has(parts[0]):
				return "外键目标无效：%s.%s -> %s。" % [table.id, field.name, foreign_key]
			var target: Dictionary = tables_by_id[parts[0]]
			if str(target.get("primary_key", "")) != parts[1]:
				return "外键必须引用目标表主键：%s.%s -> %s。" % [table.id, field.name, foreign_key]
			if str(field.get("type", "")) != "string":
				return "外键字段必须使用 string：%s.%s。" % [table.id, field.name]
	return ""


func _validate_field_constraints(table_id: String, field: Dictionary) -> String:
	var field_name := str(field.get("name", ""))
	var field_type := str(field.get("type", ""))
	if field_type == "enum":
		var values = field.get("values", null)
		if not values is Array or values.is_empty():
			return "enum 字段必须至少有一个枚举值：%s.%s。" % [table_id, field_name]
		var unique_values := {}
		for value in values:
			if not value is String or str(value).is_empty() or unique_values.has(value):
				return "enum 枚举值必须是唯一的非空字符串：%s.%s。" % [table_id, field_name]
			unique_values[value] = true
	for key in ["min", "max"]:
		if not field.has(key):
			continue
		if field_type not in ["int32", "float64"]:
			return "%s 只有数值字段才能设置 %s：%s.%s。" % [key.to_upper(), key, table_id, field_name]
		var number = field[key]
		if not number is int and not number is float:
			return "%s.%s 的 %s 必须是数字。" % [table_id, field_name, key]
		if not is_finite(float(number)):
			return "%s.%s 的 %s 必须是有限数字。" % [table_id, field_name, key]
		if field_type == "int32" and (int(number) < -2147483648 or int(number) > 2147483647):
			return "%s.%s 的 %s 超出 int32 范围。" % [table_id, field_name, key]
	if field.has("min") and field.has("max") and field.min > field.max:
		return "%s.%s 的最小值不能大于最大值。" % [table_id, field_name]
	for key in ["min_length", "max_length"]:
		if not field.has(key):
			continue
		if field_type != "string":
			return "只有 string 字段才能设置长度限制：%s.%s。" % [table_id, field_name]
		if not field[key] is int or int(field[key]) < 0:
			return "%s.%s 的 %s 必须是非负整数。" % [table_id, field_name, key]
	if field.has("min_length") and field.has("max_length") and field.min_length > field.max_length:
		return "%s.%s 的最短长度不能大于最长长度。" % [table_id, field_name]
	if not field.has("default"):
		return ""
	var default_value = field.default
	if field_type == "string" and not default_value is String:
		return "%s.%s 的默认值必须是字符串。" % [table_id, field_name]
	if field_type == "bool" and not default_value is bool:
		return "%s.%s 的默认值必须是 true 或 false。" % [table_id, field_name]
	if field_type == "int32" and (
		not default_value is int
		or int(default_value) < -2147483648
		or int(default_value) > 2147483647
	):
		return "%s.%s 的默认值超出 int32 范围。" % [table_id, field_name]
	if field_type == "float64" and (
		(not default_value is int and not default_value is float)
		or not is_finite(float(default_value))
	):
		return "%s.%s 的默认值必须是有限数字。" % [table_id, field_name]
	if field_type == "enum" and not field.get("values", []).has(default_value):
		return "%s.%s 的默认值不在枚举值中。" % [table_id, field_name]
	if field.has("min") and default_value < field.min:
		return "%s.%s 的默认值小于最小值。" % [table_id, field_name]
	if field.has("max") and default_value > field.max:
		return "%s.%s 的默认值大于最大值。" % [table_id, field_name]
	if default_value is String and field.has("min_length") and default_value.length() < field.min_length:
		return "%s.%s 的默认值短于最短长度。" % [table_id, field_name]
	if default_value is String and field.has("max_length") and default_value.length() > field.max_length:
		return "%s.%s 的默认值长于最长长度。" % [table_id, field_name]
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
		if type == "int32" and not default_value.is_empty():
			var parsed_default := _parse_integer(default_value)
			if parsed_default < -2147483648 or parsed_default > 2147483647:
				return "%s 的默认值超出 int32 范围。" % name
		if type == "float64" and not default_value.is_empty() and not default_value.is_valid_float():
			return "%s 的默认值必须是数字。" % name
		if type == "float64" and not default_value.is_empty() and not is_finite(float(default_value)):
			return "%s 的默认值必须是有限数字。" % name
		for column in [4, 5]:
			var number := item.get_text(column).strip_edges()
			if number.is_empty():
				continue
			if type not in ["int32", "float64"]:
				return "%s 只有数值类型才能设置 Min/Max。" % name
			if type == "int32" and not _is_integer_text(number):
				return "%s 的 Min/Max 必须是整数。" % name
			if type == "int32" and _is_integer_text(number):
				var parsed_number := _parse_integer(number)
				if parsed_number < -2147483648 or parsed_number > 2147483647:
					return "%s 的 Min/Max 超出 int32 范围。" % name
			if type == "float64" and not number.is_valid_float():
				return "%s 的 Min/Max 必须是数字。" % name
			if type == "float64" and number.is_valid_float() and not is_finite(float(number)):
				return "%s 的 Min/Max 必须是有限数字。" % name
		for column in [6, 7]:
			var length := item.get_text(column).strip_edges()
			if not length.is_empty() and not _is_integer_text(length):
				return "%s 的长度限制必须是整数。" % name
			if not length.is_empty() and _parse_integer(length) < 0:
				return "%s 的长度限制不能为负数。" % name
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
		old_structure.erase("source")
		new_structure.erase("source")
		if old_structure != new_structure:
			table["schema_version"] = int(old.get("schema_version", 1)) + 1
		else:
			table["schema_version"] = int(old.get("schema_version", 1))


func _build_csv_files() -> Dictionary:
	var schema_root := _schema_path.get_base_dir()
	var source_root := schema_root.path_join(str(_schema.source_directory)).simplify_path()
	var original_source_root := schema_root.path_join(str(_original_schema.get("source_directory", _schema.source_directory))).simplify_path()
	var files := {source_root.path_join(".gdignore"): "\n"}
	for table in _schema.get("tables", []):
		var path := source_root.path_join(str(table.source)).simplify_path()
		var original_source := str(table.get("_editor_original_source", table.source))
		var input_path := original_source_root.path_join(original_source).simplify_path()
		if not FileAccess.file_exists(input_path):
			input_path = path
		if table.has("_editor_original_id") and not FileAccess.file_exists(input_path):
			_status.text = "保存失败：Schema 引用的 CSV 已缺失，请恢复文件或移除数据表：%s" % table.source
			return {"valid": false}
		var csv_state := _build_csv_text(input_path, table)
		if not csv_state.valid:
			return {"valid": false}
		files[path] = csv_state.text
	return {"valid": true, "files": files}


func _build_csv_text(input_path: String, table: Dictionary) -> Dictionary:
	var rows: Array[PackedStringArray] = []
	var old_header := PackedStringArray()
	if FileAccess.file_exists(input_path):
		var input := FileAccess.open(input_path, FileAccess.READ)
		if input == null:
			_status.text = "保存失败：无法读取 CSV：%s" % input_path
			return {"valid": false}
		if input.get_position() < input.get_length():
			old_header = input.get_csv_line()
		while input.get_position() < input.get_length():
			rows.append(input.get_csv_line())
	var fields: Array = table.get("fields", [])
	var new_header := PackedStringArray()
	for field in fields:
		new_header.append(str(field.name))
	var text := _encode_csv_line(new_header)
	for old_row in rows:
		var new_row := PackedStringArray()
		for index in new_header.size():
			var field: Dictionary = fields[index]
			var old_name := str(field.get("_editor_original_name", new_header[index]))
			var old_index := old_header.find(old_name)
			new_row.append(old_row[old_index] if old_index >= 0 and old_index < old_row.size() else "")
		text += _encode_csv_line(new_row)
	return {"valid": true, "text": text}


func _encode_csv_line(values: PackedStringArray) -> String:
	var encoded := PackedStringArray()
	for raw_value in values:
		var value := str(raw_value)
		var must_quote := value.contains(",") or value.contains("\"") or value.contains("\n") or value.contains("\r")
		value = value.replace("\"", "\"\"")
		encoded.append("\"%s\"" % value if must_quote else value)
	return ",".join(encoded) + "\n"


func _commit_text_files(files: Dictionary) -> bool:
	var token := "%d_%d" % [Time.get_unix_time_from_system(), Time.get_ticks_usec()]
	var entries: Array[Dictionary] = []
	var index := 0
	for path in files:
		var destination := ProjectSettings.globalize_path(str(path))
		var directory_error := DirAccess.make_dir_recursive_absolute(destination.get_base_dir())
		if directory_error != OK:
			_cleanup_transaction_files(entries)
			_status.text = "保存失败：无法创建目录 %s。" % destination.get_base_dir()
			return false
		var temporary := "%s.godo-datatable-%s-%d.tmp" % [destination, token, index]
		var backup := "%s.godo-datatable-%s-%d.bak" % [destination, token, index]
		var output := FileAccess.open(temporary, FileAccess.WRITE)
		if output == null:
			_cleanup_transaction_files(entries)
			_status.text = "保存失败：无法暂存 %s。" % destination
			return false
		output.store_string(str(files[path]))
		var write_error := output.get_error()
		output.close()
		if write_error != OK:
			DirAccess.remove_absolute(temporary)
			_cleanup_transaction_files(entries)
			_status.text = "保存失败：暂存 %s 时发生错误：%s。" % [destination, error_string(write_error)]
			return false
		entries.append({
			"destination": destination,
			"temporary": temporary,
			"backup": backup,
			"had_original": FileAccess.file_exists(destination),
		})
		index += 1

	var committed := 0
	for entry in entries:
		if entry.had_original:
			var backup_error := DirAccess.rename_absolute(entry.destination, entry.backup)
			if backup_error != OK:
				_rollback_text_files(entries, committed)
				_status.text = "保存失败：无法备份 %s：%s。" % [entry.destination, error_string(backup_error)]
				return false
		var replace_error := DirAccess.rename_absolute(entry.temporary, entry.destination)
		if replace_error != OK:
			if entry.had_original and FileAccess.file_exists(entry.backup):
				DirAccess.rename_absolute(entry.backup, entry.destination)
			_rollback_text_files(entries, committed)
			_status.text = "保存失败：无法提交 %s：%s；原文件已恢复。" % [entry.destination, error_string(replace_error)]
			return false
		committed += 1

	for entry in entries:
		if FileAccess.file_exists(entry.backup):
			DirAccess.remove_absolute(entry.backup)
	return true


func _rollback_text_files(entries: Array[Dictionary], committed: int) -> void:
	for index in range(committed - 1, -1, -1):
		var entry: Dictionary = entries[index]
		if FileAccess.file_exists(entry.destination):
			DirAccess.remove_absolute(entry.destination)
		if entry.had_original and FileAccess.file_exists(entry.backup):
			DirAccess.rename_absolute(entry.backup, entry.destination)
	_cleanup_transaction_files(entries)


func _cleanup_transaction_files(entries: Array[Dictionary]) -> void:
	for entry in entries:
		if FileAccess.file_exists(entry.temporary):
			DirAccess.remove_absolute(entry.temporary)
		if FileAccess.file_exists(entry.backup) and FileAccess.file_exists(entry.destination):
			DirAccess.remove_absolute(entry.backup)


func _without_editor_metadata(value: Dictionary) -> Dictionary:
	var result := value.duplicate(true)
	result.erase("_editor_original_id")
	result.erase("_editor_original_source")
	for field in result.get("fields", []):
		field.erase("_editor_original_name")
		field.erase("_editor_reference_name")
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


func _normalize_schema_numbers(schema: Dictionary) -> void:
	for table in schema.get("tables", []):
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


func _strip_editor_metadata(schema: Dictionary) -> void:
	for table in schema.get("tables", []):
		table.erase("_editor_original_id")
		table.erase("_editor_original_source")
		for field in table.get("fields", []):
			field.erase("_editor_original_name")
			field.erase("_editor_reference_name")


func _is_safe_relative_path(value: String) -> bool:
	if value.strip_edges().is_empty() or value.begins_with("/") or value.contains("\\") or value.contains("://"):
		return false
	for part in value.split("/", true):
		if part == "..":
			return false
	return true


func _is_ascii_identifier(value: String) -> bool:
	if value.is_empty():
		return false
	for index in value.length():
		var code := value.unicode_at(index)
		var valid := (
			code == 95
			or (code >= 65 and code <= 90)
			or (code >= 97 and code <= 122)
			or (index > 0 and code >= 48 and code <= 57)
		)
		if not valid:
			return false
	return true


func _is_same_or_child(path: String, parent: String) -> bool:
	var normalized_parent := parent.trim_suffix("/")
	return path == normalized_parent or path.begins_with(normalized_parent + "/")
