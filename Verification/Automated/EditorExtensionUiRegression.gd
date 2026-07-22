@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	await process_frame
	var menu_button := root.find_child(MENU_BUTTON_NAME, true, false) as MenuButton
	if menu_button == null:
		_fail("未找到 GoDo 工具栏菜单。")
		return

	var menu := menu_button.get_popup()
	if not _verify_menu_layout(menu):
		return
	if not await _open_and_verify_datatable(menu):
		return
	if not await _open_and_verify(
		menu,
		"输入映射配置 (GUIDE Input Settings)...",
		"GoDo GUIDE Input 设置",
		"GuideInputReport",
		"GuideInputMessage",
		"GuideInputRepairButton"
	):
		return
	if not await _open_and_verify(
		menu,
		"幻影相机配置 (Phantom Camera Settings)...",
		"GoDo Phantom Camera 设置",
		"PhantomCameraReport",
		"PhantomCameraMessage",
		"PhantomCameraEnableButton"
	):
		return

	print("[EditorExtensionUiRegression] PASS (4/4)")
	quit(0)


func _verify_menu_layout(menu: PopupMenu) -> bool:
	var ordered_labels := PackedStringArray([
		"配置 (Setup)...",
		"资源管理",
		"创建资源清单 (Create Resource Manifest)...",
		"管理资源清单 (Manage Resource Manifest)...",
		"校验资源清单 (Validate Resource Manifest)...",
		"选择资源并添加 (Select Resource to Add)...",
		"数据表",
		"数据表配置 (DataTable Configuration)...",
		"编辑器扩展",
		"编辑器扩展状态 (Editor Extension Status)...",
		"输入映射配置 (GUIDE Input Settings)...",
		"幻影相机配置 (Phantom Camera Settings)...",
	])
	var previous_index := -1
	var add_resource_index := -1
	for label in ordered_labels:
		var index := _find_menu_index(menu, label)
		if index < 0:
			_fail("未找到菜单项：%s" % label)
			return false
		if index <= previous_index:
			_fail("菜单顺序错误：%s" % label)
			return false
		previous_index = index
		if label == "选择资源并添加 (Select Resource to Add)...":
			add_resource_index = index
	if add_resource_index <= 0 or not menu.is_item_separator(add_resource_index - 1):
		_fail("“选择资源并添加”前缺少分组分隔线。")
		return false
	return true


func _open_and_verify(
	menu: PopupMenu,
	menu_label: String,
	dialog_title: String,
	report_name: String,
	message_name: String,
	action_button_name: String
) -> bool:
	var menu_id := _find_menu_id(menu, menu_label)
	if menu_id < 0:
		_fail("未找到菜单项：%s" % menu_label)
		return false
	menu.id_pressed.emit(menu_id)
	await process_frame

	var dialog := _find_window(root, dialog_title)
	if dialog == null:
		_fail("未找到窗口：%s" % dialog_title)
		return false
	var report := dialog.find_child(report_name, true, false) as RichTextLabel
	if report == null or report.text.is_empty():
		_fail("%s 的状态报告为空。" % dialog_title)
		return false
	var message := dialog.find_child(message_name, true, false) as RichTextLabel
	if message == null or not message.text.contains("提示："):
		_fail("%s 缺少独立提示栏。" % dialog_title)
		return false
	var action_button := dialog.find_child(action_button_name, true, false) as Button
	if action_button == null or not action_button.disabled:
		_fail("%s 在健康状态下仍允许重复写入。" % dialog_title)
		return false
	dialog.hide()
	return true


func _open_and_verify_datatable(menu: PopupMenu) -> bool:
	var menu_id := _find_menu_id(menu, "数据表配置 (DataTable Configuration)...")
	if menu_id < 0:
		_fail("未找到 DataTable 配置菜单项。")
		return false
	menu.id_pressed.emit(menu_id)
	await process_frame

	var dialog := _find_window(root, "GoDo DataTable")
	if dialog == null:
		_fail("未找到 GoDo DataTable 窗口。")
		return false
	var selector := dialog.find_child("DataTableTableSelector", true, false) as OptionButton
	if selector == null:
		_fail("未找到 DataTable 单表选择器。")
		return false
	var generate_button := dialog.find_child("DataTableGenerateSelectedButton", true, false) as Button
	var generate_all_button := dialog.find_child("DataTableGenerateButton", true, false) as Button
	if (
		generate_button == null
		or generate_all_button == null
		or generate_button.get_parent() != selector.get_parent()
		or generate_all_button.get_parent() != selector.get_parent()
	):
		_fail("数据表生成按钮未与表选择器排列在同一行。")
		return false
	if generate_button.text != "生成当前表..." or generate_all_button.text != "生成全部表...":
		_fail("数据表生成按钮文本不准确。")
		return false
	var python_input := dialog.find_child("DataTablePythonInput", true, false) as LineEdit
	if python_input == null or python_input.placeholder_text != "可留空，将自动检测 python3 / python":
		_fail("Python 自动检测提示未放入输入框。")
		return false
	var schema_input := dialog.find_child("DataTableBuildConfigInput", true, false) as LineEdit
	if schema_input == null or not schema_input.placeholder_text.begins_with("例如：res://"):
		_fail("Schema 路径提示未使用示例风格。")
		return false
	var check_button := dialog.find_child("DataTableCheckButton", true, false) as Button
	var create_button := dialog.find_child("DataTableCreateSchemaButton", true, false) as Button
	var edit_button := dialog.find_child("DataTableEditSchemaButton", true, false) as Button
	if check_button == null or create_button == null or edit_button == null:
		_fail("DataTable 底部功能按钮不完整。")
		return false
	if check_button.position.x >= create_button.position.x or check_button.position.x >= edit_button.position.x:
		_fail("校验按钮和 Schema 功能按钮未左右分组。")
		return false
	var report := dialog.find_child("DataTableReport", true, false) as RichTextLabel
	var message := dialog.find_child("DataTableMessage", true, false) as RichTextLabel
	if report == null or message == null or not message.text.contains("提示："):
		_fail("DataTable 缺少状态报告或独立提示栏。")
		return false
	var expected := PackedStringArray()
	var schema_text := FileAccess.get_file_as_string(schema_input.text)
	var parsed_schema = JSON.parse_string(schema_text)
	if parsed_schema is Dictionary:
		for table in parsed_schema.get("tables", []):
			expected.append(str(table.get("id", "")))
	if selector.item_count != expected.size():
		_fail("DataTable 单表数量错误：%d。" % selector.item_count)
		return false
	for index in expected.size():
		if selector.get_item_text(index) != expected[index] or selector.get_item_text(index) == "项目":
			_fail(
				"DataTable 表 ID 被改写：期望 %s，实际 %s。" % [
					expected[index],
					selector.get_item_text(index),
				]
			)
			return false
	check_button.pressed.emit()
	for attempt in 100:
		if not check_button.disabled:
			break
		await create_timer(0.1).timeout
	if check_button.disabled:
		_fail("DataTable 校验操作未在预期时间内完成。")
		return false
	if message.text.contains("全部数据校验通过"):
		if report.text.contains("[DataTableCompiler] CHECK PASS") or not report.text.contains("当前状态："):
			_fail("DataTable 校验成功后未恢复为正常状态信息。")
			return false
	else:
		var parsed_report := report.get_parsed_text()
		if (
			not parsed_report.contains("[DataTableCompiler] FAIL")
			or parsed_report.contains("鏃")
			or parsed_report.contains("澶")
			or parsed_report.contains("�")
		):
			_fail("DataTable 失败诊断未以可读 UTF-8 文本显示：%s" % parsed_report)
			return false
	if not await _verify_datatable_schema_editor(dialog):
		return false
	dialog.hide()
	return true


func _verify_datatable_schema_editor(datatable_dialog: Window) -> bool:
	var edit_button := datatable_dialog.find_child("DataTableEditSchemaButton", true, false) as Button
	if edit_button == null:
		_fail("未找到编辑 Schema 按钮。")
		return false
	edit_button.pressed.emit()
	await process_frame
	var dialog := _find_window(root, "DataTable Schema 编辑器")
	if dialog == null:
		_fail("未找到 DataTable Schema 编辑器。")
		return false
	var advanced := dialog.find_child("DataTableSchemaAdvancedSettings", true, false) as GridContainer
	var advanced_button := dialog.find_child("DataTableSchemaAdvancedSettingsButton", true, false) as Button
	if advanced == null or advanced_button == null or advanced.visible:
		_fail("Schema 高级路径设置未默认折叠。")
		return false
	var data_files := dialog.find_child("DataTableSchemaDataFiles", true, false) as Tree
	if data_files == null or data_files.columns != 3 or data_files.get_column_title(2) != "表 ID":
		_fail("Schema 数据文件未按文件、状态、表 ID 三列显示。")
		return false
	var data_file := data_files.get_root().get_first_child()
	if data_file == null or data_file.get_text(1) != "已加入" or data_file.get_text(2).is_empty():
		_fail("Schema 数据文件状态或表 ID 显示错误。")
		return false
	var add_csv := dialog.find_child("DataTableSchemaAddCsvButton", true, false) as Button
	var remove_csv := dialog.find_child("DataTableSchemaRemoveCsvButton", true, false) as Button
	data_files.set_selected(data_file, 0)
	data_files.item_selected.emit()
	if (
		data_files.select_mode != Tree.SELECT_ROW
		or add_csv == null
		or remove_csv == null
		or not add_csv.disabled
		or remove_csv.disabled
	):
		_fail("Schema 数据文件整行选择或加入/移出操作状态错误。")
		return false
	var table_id := dialog.find_child("DataTableSchemaTableId", true, false) as LineEdit
	var table_source := dialog.find_child("DataTableSchemaTableSource", true, false) as LineEdit
	var primary_key := dialog.find_child("DataTableSchemaPrimaryKey", true, false) as OptionButton
	var schema_version := dialog.find_child("DataTableSchemaVersion", true, false) as Label
	if (
		table_id == null
		or table_source == null
		or primary_key == null
		or schema_version == null
		or table_id.editable
		or table_source.editable
		or table_id.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or table_source.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or table_id.text != "ItemCategory"
		or primary_key.item_count == 0
		or not schema_version.text.contains("自动递增")
	):
		_fail("Schema 表级安全编辑控件状态错误。")
		return false
	var create_table := dialog.find_child("DataTableSchemaCreateTableButton", true, false) as Button
	if create_table == null:
		_fail("Schema 缺少新建数据表操作。")
		return false
	var fields := dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var field := fields.get_root().get_first_child() if fields != null and fields.get_root() != null else null
	if (
		fields == null
		or field == null
		or fields.select_mode != Tree.SELECT_SINGLE
		or fields.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or fields.scroll_horizontal_enabled
		or field.is_editable(0)
		or not field.is_editable(1)
		or not field.is_editable(2)
		or field.get_text_alignment(2) != HORIZONTAL_ALIGNMENT_CENTER
	):
		_fail("Schema 字段表格选择、滚动或复选框布局错误。")
		return false
	fields.set_selected(field, 0)
	var target_column := 4
	var target_position := fields.get_item_area_rect(field, target_column).get_center()
	fields.item_mouse_selected.emit(target_position, MOUSE_BUTTON_LEFT)
	fields.item_activated.emit()
	await process_frame
	if (
		fields.get_selected_column() != target_column
		or not field.is_editable(target_column)
		or field.is_editable(0)
	):
		_fail("Schema 字段双击未进入目标单元格编辑。")
		return false
	fields.item_edited.emit()
	await process_frame
	var first_background := field.get_custom_bg_color(0)
	if (
		fields.get_selected_column() != target_column
		or not field.is_selected(target_column)
		or first_background.a <= 0.0
		or field.get_custom_bg_color(fields.columns - 1) != first_background
		or field.is_editable(target_column)
	):
		_fail("Schema 字段编辑结束后未恢复整行选择。")
		return false
	dialog.hide()
	return true


func _find_menu_id(menu: PopupMenu, label: String) -> int:
	var index := _find_menu_index(menu, label)
	return menu.get_item_id(index) if index >= 0 else -1


func _find_menu_index(menu: PopupMenu, label: String) -> int:
	for index in menu.item_count:
		if menu.get_item_text(index) == label:
			return index
	return -1


func _find_window(node: Node, title: String) -> Window:
	for child in node.get_children():
		if child is Window and child.title == title:
			return child
		var nested := _find_window(child, title)
		if nested != null:
			return nested
	return null


func _fail(message: String) -> void:
	push_error("[EditorExtensionUiRegression] FAIL: %s" % message)
	quit(1)
