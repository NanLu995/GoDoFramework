@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"
const SETUP_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_runtime_setup_controller.gd")


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
	if not await _open_and_verify_setup(menu):
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

	print("[EditorExtensionUiRegression] PASS (5/5)")
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


func _open_and_verify_setup(menu: PopupMenu) -> bool:
	var controller = SETUP_CONTROLLER_SCRIPT.new()
	var minimum := Vector3i(4, 7, 1)
	var tested := Vector3i(4, 7, 1)
	if controller._evaluate_version(Vector3i(4, 7, 0), minimum, tested).supported:
		_fail("低于最低版本的 Godot 未被拒绝。")
		return false
	var newer: Dictionary = controller._evaluate_version(Vector3i(4, 7, 2), minimum, tested)
	if not newer.supported or newer.tested:
		_fail("高于已验证版本的同 major Godot 未进入兼容但未验证状态。")
		return false

	var menu_id := _find_menu_id(menu, "配置 (Setup)...")
	if menu_id < 0:
		_fail("未找到 Setup 菜单项。")
		return false
	menu.id_pressed.emit(menu_id)
	await process_frame
	var dialog := _find_window(root, "GoDo Framework")
	if dialog == null:
		_fail("未找到 GoDo Framework 配置窗口。")
		return false
	var report := dialog.find_child("ReportLabel", true, false) as RichTextLabel
	if (
		report == null
		or not report.get_parsed_text().contains("GoDoFramework 版本")
		or not report.get_parsed_text().contains("Godot 兼容性")
	):
		_fail("Setup 未显示框架版本和 Godot 兼容性：%s" % ("<missing>" if report == null else report.get_parsed_text()))
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
		_fail("数据表导出按钮未与表选择器排列在同一行。")
		return false
	if generate_button.text != "导出当前表..." or generate_all_button.text != "导出全部表...":
		_fail("数据表导出按钮文本不准确。")
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
	var dataset_grid := dialog.find_child("DataTableSchemaDatasetGrid", true, false) as GridContainer
	var dataset_options := dialog.find_child("DataTableSchemaDatasetOptions", true, false) as HBoxContainer
	var data_set_id := dialog.find_child("DataTableSchemaDataSetId", true, false) as LineEdit
	if (
		dataset_grid == null
		or dataset_options == null
		or data_set_id == null
		or dataset_grid.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or data_set_id.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or advanced_button.size_flags_horizontal == Control.SIZE_EXPAND_FILL
	):
		_fail("Schema 数据集配置区未使用紧凑布局。")
		return false
	var data_files_top_separator := dialog.find_child("DataTableSchemaDataFilesTopSeparator", true, false) as HSeparator
	var data_files_bottom_separator := dialog.find_child("DataTableSchemaDataFilesBottomSeparator", true, false) as HSeparator
	if data_files_top_separator == null or data_files_bottom_separator == null:
		_fail("Schema 数据文件区域缺少上下分隔线。")
		return false
	var data_files := dialog.find_child("DataTableSchemaDataFiles", true, false) as Tree
	if data_files == null or data_files.columns != 3 or data_files.get_column_title(2) != "数据表 ID":
		_fail("Schema 数据文件未按文件、状态、数据表 ID 三列显示。")
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
	var schema_version := dialog.find_child("DataTableSchemaVersion", true, false) as LineEdit
	var schema_version_hint := dialog.find_child("DataTableSchemaVersionHint", true, false) as Label
	var table_selector := dialog.find_child("DataTableSchemaTableSelector", true, false) as OptionButton
	var table_details := dialog.find_child("DataTableSchemaTableDetails", true, false) as HBoxContainer
	var table_id_label := dialog.find_child("DataTableSchemaTableIdLabel", true, false) as Label
	var export_scope_label := dialog.find_child("DataTableSchemaExportScopeLabel", true, false) as Label
	var protocol_version_hint := dialog.find_child("DataTableSchemaProtocolVersionHint", true, false) as Label
	if (
		table_id == null
		or table_source == null
		or primary_key == null
		or schema_version == null
		or schema_version_hint == null
		or table_selector == null
		or table_details == null
		or table_id_label == null
		or export_scope_label == null
		or protocol_version_hint == null
		or table_id.editable
		or table_source.editable
		or schema_version.editable
		or table_id.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or table_source.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or schema_version.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or table_selector.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or table_details.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or table_id.text != "ItemCategory"
		or primary_key.item_count == 0
		or not schema_version.text.is_valid_int()
		or schema_version_hint.text != "保存结构变更时由工具自动递增"
		or schema_version_hint.get_parent() != schema_version.get_parent()
		or table_id_label.text != "数据表 ID"
		or export_scope_label.text != "数据导出范围"
		or protocol_version_hint.text != "客户端与服务器共享数据结构不兼容时手动递增"
	):
		_fail("Schema 表级安全编辑控件状态错误。")
		return false
	var rename_table_id := dialog.find_child("DataTableSchemaRenameTableIdButton", true, false) as Button
	var table_value_input := dialog.find_child("DataTableSchemaTableValueInput", true, false) as LineEdit
	if rename_table_id == null or table_value_input == null:
		_fail("Schema 缺少表 ID 重命名控件。")
		return false
	var rename_connections := rename_table_id.pressed.get_connections()
	var schema_editor: Object = (
		rename_connections[0].callable.get_object() if not rename_connections.is_empty() else null
	)
	if schema_editor == null:
		_fail("Schema 表 ID 重命名操作未连接到编辑器。")
		return false
	var original_file_name := data_file.get_text(0)
	var original_state := data_file.get_text(1)
	var original_table_id := data_file.get_text(2)
	schema_editor.set("_table_value_mode", "table_id")
	table_value_input.text = "%sUiRegression" % original_table_id
	schema_editor.call("_apply_table_value_change")
	await process_frame
	var renamed_data_file := data_files.get_root().get_first_child()
	while renamed_data_file != null and renamed_data_file.get_text(0) != original_file_name:
		renamed_data_file = renamed_data_file.get_next()
	if (
		renamed_data_file == null
		or renamed_data_file.get_text(1) != original_state
		or renamed_data_file.get_text(2) != "%sUiRegression" % original_table_id
	):
		_fail("Schema 表 ID 重命名后，数据文件列表未立即同步。")
		return false
	var renamed_table_id := "%sUiRegression" % original_table_id
	var item_index := _find_option_index(table_selector, "Item")
	if item_index < 0:
		_fail("Schema 外键联动回归缺少 Item 数据表。")
		return false
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	var relation_fields := dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var category_field := _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.id" % renamed_table_id:
		_fail("重命名数据表 ID 后，引用它的外键未同步更新。")
		return false
	var renamed_index := _find_option_index(table_selector, renamed_table_id)
	if renamed_index < 0:
		_fail("重命名后的数据表未保留在选择器中。")
		return false
	table_selector.select(renamed_index)
	table_selector.item_selected.emit(renamed_index)
	await process_frame
	schema_editor.set("_table_value_mode", "table_id")
	table_value_input.text = original_table_id
	schema_editor.call("_apply_table_value_change")
	await process_frame
	item_index = _find_option_index(table_selector, "Item")
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	relation_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_field = _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.id" % original_table_id:
		_fail("恢复数据表 ID 后，引用它的外键未同步恢复。")
		return false
	var category_index := _find_option_index(table_selector, original_table_id)
	table_selector.select(category_index)
	table_selector.item_selected.emit(category_index)
	await process_frame
	var category_fields := dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var category_id_field := _find_tree_item(category_fields, "id")
	if category_id_field == null:
		_fail("Schema 外键联动回归缺少 ItemCategory.id。")
		return false
	await _edit_tree_text_cell(category_fields, category_id_field, 0, "category_key")
	item_index = _find_option_index(table_selector, "Item")
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	relation_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_field = _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.category_key" % original_table_id:
		_fail("重命名主键字段后，引用它的外键未同步更新：%s。" % (
			"<missing>" if category_field == null else category_field.get_text(9)
		))
		return false
	table_selector.select(category_index)
	table_selector.item_selected.emit(category_index)
	await process_frame
	category_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_id_field = _find_tree_item(category_fields, "category_key")
	await _edit_tree_text_cell(category_fields, category_id_field, 0, "id")
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	relation_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_field = _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.id" % original_table_id:
		_fail("恢复主键字段后，引用它的外键未同步恢复。")
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


func _find_option_index(options: OptionButton, text: String) -> int:
	for index in options.item_count:
		if options.get_item_text(index) == text:
			return index
	return -1


func _find_tree_item(tree: Tree, text: String) -> TreeItem:
	if tree == null or tree.get_root() == null:
		return null
	var item := tree.get_root().get_first_child()
	while item != null:
		if item.get_text(0) == text:
			return item
		item = item.get_next()
	return null


func _edit_tree_text_cell(tree: Tree, item: TreeItem, column: int, text: String) -> void:
	var position := tree.get_item_area_rect(item, column).get_center()
	tree.item_mouse_selected.emit(position, MOUSE_BUTTON_LEFT)
	tree.item_activated.emit()
	await process_frame
	item.set_text(column, text)
	tree.item_edited.emit()
	await process_frame


func _fail(message: String) -> void:
	push_error("[EditorExtensionUiRegression] FAIL: %s" % message)
	quit(1)
