@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"
const BUILD_CONFIG := "res://Verification/Experimental/DataTable/datatable.build.json"
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
		"res://DataTables/datatable.build.json"
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
	if (
		config_input == null
		or check_button == null
		or generate_button == null
		or table_selector == null
		or generate_selected_button == null
		or report == null
	):
		_fail("DataTable 窗口缺少必需控件。")
		return

	config_input.text = BUILD_CONFIG
	config_input.text_changed.emit(BUILD_CONFIG)
	await process_frame
	if check_button.disabled:
		_fail("有效 Build Config 未启用检查按钮：%s" % report.text)
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
	var build_report := _read_json(
		"res://Verification/Experimental/DataTable/Artifacts/editor-output/build-report.json"
	)
	if build_report.get("scope", "") != "single" or build_report.get("selected_table", "") != "Item":
		_fail("单表生成报告未记录 Item：%s" % build_report)
		return

	await _wait_for_editor_scan()
	await create_timer(2.0).timeout
	dialog.hide()
	await process_frame
	await process_frame
	_restore_settings()
	print("[DataTableEditorExtensionProbe] PASS (5/5)")
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
	if _settings == null:
		return
	_settings.set_project_metadata(SETTINGS_SECTION, CONFIG_METADATA_KEY, _previous_config)
	if _had_python_setting:
		_settings.set_setting(PYTHON_SETTING, _previous_python)
	elif _settings.has_setting(PYTHON_SETTING):
		_settings.erase(PYTHON_SETTING)


func _fail(message: String) -> void:
	_restore_settings()
	push_error("[DataTableEditorExtensionProbe] FAIL: %s" % message)
	quit(1)
