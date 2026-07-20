@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"
const SCHEMA := "res://Verification/Experimental/DataTable/prototype.datatable.schema.json"
const BASE_SCHEMA := "res://DataTables/Base/.datatable.schema.json"
const PROBE_CSV := "res://DataTables/Base/.datafiles/ProbeExcluded.csv"
const SETTINGS_SECTION := "godo_framework/datatable"
const CONFIG_METADATA_KEY := "build_config_path"
const PYTHON_SETTING := "godo_framework/datatable/python_executable"

var _settings: EditorSettings
var _previous_config := ""
var _had_python_setting := false
var _previous_python = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_settings = EditorInterface.get_editor_settings()
	_previous_config = str(_settings.get_project_metadata(
		SETTINGS_SECTION,
		CONFIG_METADATA_KEY,
		"res://DataTables/Base/.datatable.schema.json"
	))
	_had_python_setting = _settings.has_setting(PYTHON_SETTING)
	if _had_python_setting:
		_previous_python = _settings.get_setting(PYTHON_SETTING)

	await process_frame
	await process_frame
	var menu_button := root.find_child(MENU_BUTTON_NAME, true, false) as MenuButton
	if menu_button == null:
		_fail("未找到 GoDo 工具栏菜单。")
		return
	var menu := menu_button.get_popup()
	var menu_id := _find_menu_id(menu, "DataTable...")
	if menu_id < 0:
		_fail("未找到 DataTable 菜单。")
		return
	menu.id_pressed.emit(menu_id)
	await process_frame

	var dialog := _find_window(root, "GoDo DataTable")
	if dialog == null:
		_fail("未找到 DataTable 窗口。")
		return
	var config_input := dialog.find_child("DataTableBuildConfigInput", true, false) as LineEdit
	var check_button := dialog.find_child("DataTableCheckButton", true, false) as Button
	var generate_button := dialog.find_child("DataTableGenerateButton", true, false) as Button
	var table_selector := dialog.find_child("DataTableTableSelector", true, false) as OptionButton
	var generate_selected_button := dialog.find_child("DataTableGenerateSelectedButton", true, false) as Button
	var report := dialog.find_child("DataTableReport", true, false) as RichTextLabel
	var edit_schema_button := dialog.find_child("DataTableEditSchemaButton", true, false) as Button
	if (
		config_input == null
		or check_button == null
		or generate_button == null
		or table_selector == null
		or generate_selected_button == null
		or report == null
		or edit_schema_button == null
	):
		_fail("DataTable 窗口缺少必需控件。")
		return

	config_input.text = SCHEMA
	config_input.text_changed.emit(SCHEMA)
	await process_frame
	if check_button.disabled:
		_fail("有效 Schema 未启用检查按钮：%s" % report.text)
		return
	check_button.pressed.emit()
	if not await _wait_for_report(report, "CHECK PASS"):
		_fail("DataTable 检查未成功：%s" % report.text)
		return

	generate_button.pressed.emit()
	await process_frame
	var confirmation := _find_window(root, "生成全部 DataTable") as ConfirmationDialog
	if confirmation == null:
		_fail("生成操作未显示确认窗口。")
		return
	confirmation.confirmed.emit()
	if not await _wait_for_report(report, "GENERATE PASS"):
		_fail("DataTable 生成未成功：%s" % report.text)
		return
	if not FileAccess.file_exists(
		"res://Verification/Experimental/DataTable/Artifacts/editor-output/manifest.json"
	):
		_fail("DataTable 编辑器生成未产生 Manifest。")
		return

	config_input.text = BASE_SCHEMA
	config_input.text_changed.emit(BASE_SCHEMA)
	await process_frame
	var probe_csv := FileAccess.open(PROBE_CSV, FileAccess.WRITE)
	if probe_csv == null:
		_fail("无法创建排除状态探针 CSV。")
		return
	probe_csv.store_string("id,value\nprobe,test\n")
	probe_csv.close()
	edit_schema_button.pressed.emit()
	await process_frame
	var schema_dialog := _find_window(root, "DataTable Schema 编辑器")
	if schema_dialog == null:
		_fail("未找到可视化 Schema 编辑器。")
		return
	var fields := schema_dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var data_files := schema_dialog.find_child("DataTableSchemaDataFiles", true, false) as Tree
	var add_csv := schema_dialog.find_child("DataTableSchemaAddCsvButton", true, false) as Button
	var open_data_directory := schema_dialog.find_child("DataTableSchemaOpenDataDirectoryButton", true, false) as Button
	var save_schema := schema_dialog.find_child("DataTableSchemaSaveButton", true, false) as Button
	var schema_status := schema_dialog.find_child("DataTableSchemaStatus", true, false) as Label
	if fields == null or data_files == null or add_csv == null or open_data_directory == null or save_schema == null or schema_status == null:
		_fail("Schema 编辑器缺少数据文件、字段或保存控件。")
		return
	var configured_file_count := 0
	var excluded_file: TreeItem
	var data_file_item := data_files.get_root().get_first_child()
	while data_file_item != null:
		if data_file_item.get_text(0) == "ProbeExcluded.csv":
			excluded_file = data_file_item
		elif data_file_item.get_text(1).begins_with("已加入 Schema"):
			configured_file_count += 1
		else:
			_fail("Base 数据文件状态异常：%s。" % data_file_item.get_text(1))
			return
		data_file_item = data_file_item.get_next()
	if configured_file_count != 3 or excluded_file == null or excluded_file.get_text(1) != "未加入（已排除）":
		_fail("Schema 编辑器未正确区分三张已加入 CSV 和一张排除 CSV。")
		return
	excluded_file.select(0)
	add_csv.pressed.emit()
	var schema_table_selector := schema_dialog.find_child("DataTableSchemaTableSelector", true, false) as OptionButton
	if schema_table_selector == null or _find_option_index(schema_table_selector, "ProbeExcluded") < 0:
		_fail("加入选中 CSV 未根据表头创建数据表。")
		return
	schema_dialog.hide()
	_cleanup_probe_csv()
	edit_schema_button.pressed.emit()
	await process_frame
	save_schema.pressed.emit()
	if not await _wait_for_report(report, "CHECK PASS"):
		_fail("Schema 保存后未通过自动检查：%s\nSchema 状态：%s" % [report.text, schema_status.text])
		return
	var saved_schema := FileAccess.get_file_as_string(BASE_SCHEMA)
	if not '"format_version": 2,' in saved_schema or not '"schema_version": 1,' in saved_schema:
		_fail("原样保存改变了格式或表结构版本。")
		return
	schema_dialog.hide()
	config_input.text = SCHEMA
	config_input.text_changed.emit(SCHEMA)
	await process_frame

	var item_index := _find_option_index(table_selector, "Item")
	if item_index < 0 or generate_selected_button.disabled:
		_fail("单表选择控件未提供 Item 或生成按钮不可用。")
		return
	table_selector.select(item_index)
	generate_selected_button.pressed.emit()
	await process_frame
	var selected_confirmation := _find_window(root, "生成选中 DataTable") as ConfirmationDialog
	if selected_confirmation == null:
		_fail("单表生成未显示确认窗口。")
		return
	selected_confirmation.confirmed.emit()
	if not await _wait_for_report(report, "GENERATE PASS"):
		_fail("DataTable 单表生成未成功：%s" % report.text)
		return
	if not FileAccess.file_exists(
		"res://Verification/Experimental/DataTable/Artifacts/editor-output/Item.gdtb"
	):
		_fail("单表生成未保留 Item 运行时二进制。")
		return

	await _wait_for_editor_scan()
	await create_timer(2.0).timeout
	dialog.hide()
	await process_frame
	await process_frame
	_restore_settings()
	print("[DataTableEditorExtensionProbe] PASS (6/6)")
	quit(0)


func _wait_for_report(report: RichTextLabel, marker: String) -> bool:
	for _attempt in 200:
		if marker in report.text:
			return true
		if "[失败]" in report.text:
			return false
		await create_timer(0.05).timeout
	return false


func _wait_for_editor_scan() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	for _attempt in 400:
		if not filesystem.is_scanning():
			return
		await create_timer(0.05).timeout


func _find_menu_id(menu: PopupMenu, label: String) -> int:
	for index in menu.item_count:
		if menu.get_item_text(index) == label:
			return menu.get_item_id(index)
	return -1


func _find_option_index(options: OptionButton, label: String) -> int:
	for index in options.item_count:
		if options.get_item_text(index) == label:
			return index
	return -1


func _read_json(path: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {}
	var parsed = JSON.parse_string(file.get_as_text())
	return parsed if parsed is Dictionary else {}


func _find_window(node: Node, title: String) -> Window:
	for child in node.get_children():
		if child is Window and child.title == title:
			return child
		var nested := _find_window(child, title)
		if nested != null:
			return nested
	return null


func _restore_settings() -> void:
	_cleanup_probe_csv()
	if _settings == null:
		return
	_settings.set_project_metadata(SETTINGS_SECTION, CONFIG_METADATA_KEY, _previous_config)
	if _had_python_setting:
		_settings.set_setting(PYTHON_SETTING, _previous_python)
	elif _settings.has_setting(PYTHON_SETTING):
		_settings.erase(PYTHON_SETTING)


func _cleanup_probe_csv() -> void:
	if FileAccess.file_exists(PROBE_CSV):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(PROBE_CSV))


func _fail(message: String) -> void:
	_restore_settings()
	push_error("[DataTableEditorExtensionProbe] FAIL: %s" % message)
	quit(1)
