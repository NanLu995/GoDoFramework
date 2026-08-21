@tool
extends RefCounted

signal config_paths_changed(preferred_path: String)

const ACTION_CREATE := "create"
const ACTION_MANAGE := "manage"
const ACTION_VALIDATE := "validate"
const UI_CONFIG_SCRIPT_PATH := "res://addons/godo_framework/Runtime/UI/UiConfig.cs"
const UI_CONFIG_ENTRY_SCRIPT_PATH := "res://addons/godo_framework/Runtime/UI/UiConfigEntry.cs"
const NORMAL_COLOR := Color("#8BD49C")
const WARNING_COLOR := Color("#F2C14E")
const ERROR_COLOR := Color("#FF6B6B")

var _plugin: EditorPlugin
var _config_file_dialog: EditorFileDialog
var _config_selector_dialog: ConfirmationDialog
var _config_selector_label: Label
var _config_selector_tree: Tree
var _config_selector_create_button: Button
var _config_selector_manual_button: Button
var _scene_file_dialog: EditorFileDialog
var _manage_dialog: AcceptDialog
var _managed_config_label: Label
var _entries_tree: Tree
var _search_input: LineEdit
var _add_button: Button
var _edit_button: Button
var _remove_button: Button
var _validate_button: Button
var _locate_config_button: Button
var _locate_scene_button: Button
var _entry_dialog: ConfirmationDialog
var _entry_id_input: LineEdit
var _entry_locator_input: LineEdit
var _entry_layer_input: OptionButton
var _entry_instance_mode_input: OptionButton
var _entry_reuse_input: CheckBox
var _remove_dialog: ConfirmationDialog
var _report_dialog: AcceptDialog
var _report_label: RichTextLabel
var _file_action := ""
var _selector_action := ""
var _managed_config_path := ""
var _managed_entry_index := -1
var _editing_entry_index := -1
var _csharp_resource_load_error := ""
var _config_persistence_error := ""
var _file_system_dock


func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
	var editor_root := _plugin.get_editor_interface().get_base_control()

	_config_file_dialog = EditorFileDialog.new()
	_config_file_dialog.name = "UiConfigFileDialog"
	_config_file_dialog.exclusive = true
	_config_file_dialog.transient_to_focused = true
	_config_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_config_file_dialog.mode_overrides_title = false
	_config_file_dialog.filters = PackedStringArray(["*.tres,*.res;UI config resources"])
	_config_file_dialog.file_selected.connect(_on_config_file_selected)
	_config_file_dialog.canceled.connect(_on_config_file_dialog_canceled)
	editor_root.add_child(_config_file_dialog)

	_scene_file_dialog = EditorFileDialog.new()
	_scene_file_dialog.name = "GoDoUiSceneFileDialog"
	_scene_file_dialog.exclusive = true
	_scene_file_dialog.transient_to_focused = true
	_scene_file_dialog.title = "选择 UI Scene"
	_scene_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_scene_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_scene_file_dialog.mode_overrides_title = false
	_scene_file_dialog.filters = PackedStringArray(["*.tscn;Godot scenes"])
	_scene_file_dialog.file_selected.connect(_on_scene_file_selected)
	editor_root.add_child(_scene_file_dialog)

	_create_config_selector_dialog(editor_root)
	_create_manage_dialog(editor_root)
	_create_entry_dialog()
	_create_remove_dialog()
	_create_report_dialog(editor_root)
	_file_system_dock = _plugin.get_editor_interface().get_file_system_dock()
	if is_instance_valid(_file_system_dock):
		_file_system_dock.file_removed.connect(_on_editor_file_removed)
		_file_system_dock.files_moved.connect(_on_editor_files_moved)


func dispose() -> void:
	if is_instance_valid(_file_system_dock):
		if _file_system_dock.file_removed.is_connected(_on_editor_file_removed):
			_file_system_dock.file_removed.disconnect(_on_editor_file_removed)
		if _file_system_dock.files_moved.is_connected(_on_editor_files_moved):
			_file_system_dock.files_moved.disconnect(_on_editor_files_moved)
	_file_system_dock = null
	for dialog in [
		_config_file_dialog,
		_config_selector_dialog,
		_scene_file_dialog,
		_manage_dialog,
		_entry_dialog,
		_remove_dialog,
		_report_dialog,
	]:
		if is_instance_valid(dialog):
			dialog.queue_free()


func _create_config_selector_dialog(editor_root: Control) -> void:
	_config_selector_dialog = ConfirmationDialog.new()
	_config_selector_dialog.name = "UiConfigSelectorDialog"
	_config_selector_dialog.exclusive = true
	_config_selector_dialog.transient_to_focused = true
	_config_selector_dialog.title = "选择 UI 配置"
	_config_selector_dialog.ok_button_text = "打开"
	_config_selector_dialog.cancel_button_text = "取消"
	_config_selector_dialog.min_size = Vector2i(760, 460)
	_config_selector_dialog.get_label().hide()

	var content := VBoxContainer.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -64
	content.add_theme_constant_override("separation", 8)
	_config_selector_dialog.add_child(content)

	_config_selector_label = Label.new()
	content.add_child(_config_selector_label)
	_config_selector_tree = Tree.new()
	_config_selector_tree.name = "UiConfigSelectorTree"
	_config_selector_tree.columns = 1
	_config_selector_tree.column_titles_visible = true
	_config_selector_tree.hide_root = true
	_config_selector_tree.select_mode = Tree.SELECT_ROW
	_config_selector_tree.set_column_title(0, "UiConfig Resource")
	_config_selector_tree.item_selected.connect(_on_config_selector_item_selected)
	_config_selector_tree.item_activated.connect(_on_config_selector_item_activated)
	_config_selector_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_config_selector_tree)

	var action_row := HBoxContainer.new()
	action_row.add_theme_constant_override("separation", 8)
	content.add_child(action_row)
	_config_selector_create_button = Button.new()
	_config_selector_create_button.name = "UiConfigSelectorCreateButton"
	_config_selector_create_button.text = "创建 UI 配置..."
	_config_selector_create_button.pressed.connect(_on_config_selector_create_pressed)
	action_row.add_child(_config_selector_create_button)
	_config_selector_manual_button = Button.new()
	_config_selector_manual_button.name = "UiConfigSelectorManualButton"
	_config_selector_manual_button.text = "手动选择其他配置..."
	_config_selector_manual_button.pressed.connect(_on_config_selector_manual_pressed)
	action_row.add_child(_config_selector_manual_button)
	_config_selector_dialog.confirmed.connect(_on_config_selector_confirmed)
	editor_root.add_child(_config_selector_dialog)


func open_create_dialog() -> void:
	_file_action = ACTION_CREATE
	_config_file_dialog.title = "创建 UI 配置"
	_config_file_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	_config_file_dialog.get_ok_button().text = "创建"
	_config_file_dialog.current_path = "res://UiConfig.tres"
	_config_file_dialog.popup_centered(Vector2i(720, 480))


func open_manage_dialog() -> void:
	_open_existing_config(ACTION_MANAGE)


func open_validate_dialog() -> void:
	_open_existing_config(ACTION_VALIDATE)


func find_config_paths() -> PackedStringArray:
	return _prepare_config_paths(_find_ui_config_paths("res://"))


func open_manage_path(path: String) -> void:
	_show_manager(path)


func open_validate_path(path: String) -> void:
	_show_validation_report(path)


func _create_manage_dialog(editor_root: Control) -> void:
	_manage_dialog = AcceptDialog.new()
	_manage_dialog.name = "GoDoUiConfigManageDialog"
	_manage_dialog.title = "UI 配置管理"
	_manage_dialog.ok_button_text = "关闭"
	_manage_dialog.min_size = Vector2i(1080, 560)
	_manage_dialog.exclusive = true
	_manage_dialog.transient_to_focused = true
	_manage_dialog.get_label().hide()

	var content := VBoxContainer.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -56
	content.add_theme_constant_override("separation", 8)
	_manage_dialog.add_child(content)

	var toolbar := HBoxContainer.new()
	toolbar.add_theme_constant_override("separation", 8)
	content.add_child(toolbar)
	var config_toolbar := HBoxContainer.new()
	config_toolbar.add_theme_constant_override("separation", 8)
	content.add_child(config_toolbar)
	content.move_child(config_toolbar, 0)
	_managed_config_label = Label.new()
	_managed_config_label.name = "GoDoManagedUiConfigLabel"
	_managed_config_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	config_toolbar.add_child(_managed_config_label)
	_locate_config_button = Button.new()
	_locate_config_button.name = "GoDoUiConfigLocateButton"
	_locate_config_button.text = "定位配置"
	_locate_config_button.pressed.connect(_on_locate_config_pressed)
	config_toolbar.add_child(_locate_config_button)
	_locate_scene_button = Button.new()
	_locate_scene_button.text = "定位 Scene"
	_locate_scene_button.disabled = true
	_locate_scene_button.pressed.connect(_on_locate_scene_pressed)
	config_toolbar.add_child(_locate_scene_button)
	var search_label := Label.new()
	search_label.text = "Search"
	toolbar.add_child(search_label)
	_search_input = LineEdit.new()
	_search_input.name = "GoDoUiConfigSearchInput"
	_search_input.placeholder_text = "Filter by Id or scene path"
	_search_input.clear_button_enabled = true
	_search_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_search_input.text_changed.connect(_on_search_changed)
	toolbar.add_child(_search_input)
	_add_button = Button.new()
	_add_button.name = "GoDoUiConfigAddButton"
	_add_button.text = "添加"
	_add_button.pressed.connect(_on_add_pressed)
	toolbar.add_child(_add_button)
	_edit_button = Button.new()
	_edit_button.text = "编辑"
	_edit_button.disabled = true
	_edit_button.pressed.connect(_on_edit_pressed)
	toolbar.add_child(_edit_button)
	_remove_button = Button.new()
	_remove_button.text = "删除"
	_remove_button.disabled = true
	_remove_button.pressed.connect(_on_remove_pressed)
	toolbar.add_child(_remove_button)
	_validate_button = Button.new()
	_validate_button.name = "GoDoUiConfigManageValidateButton"
	_validate_button.text = "校验"
	_validate_button.pressed.connect(_on_validate_pressed)
	toolbar.add_child(_validate_button)

	_entries_tree = Tree.new()
	_entries_tree.name = "GoDoUiConfigEntriesTree"
	_entries_tree.columns = 6
	_entries_tree.column_titles_visible = true
	_entries_tree.hide_root = true
	_entries_tree.select_mode = Tree.SELECT_ROW
	_entries_tree.set_column_title(0, "Id")
	_entries_tree.set_column_title(1, "Scene")
	_entries_tree.set_column_title(2, "Layer")
	_entries_tree.set_column_title(3, "Instance Mode")
	_entries_tree.set_column_title(4, "Reuse")
	_entries_tree.set_column_title(5, "Status")
	for column in range(2, 6):
		_entries_tree.set_column_title_alignment(column, HORIZONTAL_ALIGNMENT_CENTER)
	_entries_tree.set_column_expand_ratio(0, 2)
	_entries_tree.set_column_expand_ratio(1, 5)
	_entries_tree.set_column_expand_ratio(2, 1)
	_entries_tree.set_column_expand_ratio(3, 1)
	_entries_tree.set_column_expand_ratio(4, 1)
	_entries_tree.set_column_expand_ratio(5, 1)
	_entries_tree.item_selected.connect(_on_entry_selected)
	_entries_tree.item_activated.connect(_on_entry_activated)
	_entries_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_entries_tree)
	editor_root.add_child(_manage_dialog)


func _create_entry_dialog() -> void:
	_entry_dialog = ConfirmationDialog.new()
	_entry_dialog.name = "GoDoUiConfigEntryDialog"
	_entry_dialog.exclusive = true
	_entry_dialog.transient_to_focused = true
	_entry_dialog.title = "UI 配置条目"
	_entry_dialog.ok_button_text = "保存"
	_entry_dialog.cancel_button_text = "取消"
	_entry_dialog.min_size = Vector2i(760, 430)
	_entry_dialog.get_label().hide()

	var content := GridContainer.new()
	content.columns = 2
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -64
	content.add_theme_constant_override("h_separation", 12)
	content.add_theme_constant_override("v_separation", 10)
	_entry_dialog.add_child(content)

	content.add_child(_create_label("Id"))
	_entry_id_input = LineEdit.new()
	_entry_id_input.placeholder_text = "例如：ui/main_menu"
	_entry_id_input.select_all_on_focus = true
	_entry_id_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_child(_entry_id_input)

	content.add_child(_create_label("UI Scene"))
	var locator_row := HBoxContainer.new()
	_entry_locator_input = LineEdit.new()
	_entry_locator_input.placeholder_text = "res:// 或 uid://"
	_entry_locator_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_entry_locator_input.select_all_on_focus = true
	locator_row.add_child(_entry_locator_input)
	var choose_scene_button := Button.new()
	choose_scene_button.name = "GoDoUiConfigChooseSceneButton"
	choose_scene_button.text = "选择 Scene..."
	choose_scene_button.pressed.connect(_on_choose_scene_pressed)
	locator_row.add_child(choose_scene_button)
	content.add_child(locator_row)

	content.add_child(_create_label("Layer"))
	_entry_layer_input = OptionButton.new()
	_entry_layer_input.add_item("Scene", 0)
	_entry_layer_input.add_item("View", 1)
	_entry_layer_input.add_item("Modal", 2)
	_entry_layer_input.add_item("Overlay", 3)
	_entry_layer_input.set_item_tooltip(0, "随主场景切换清理的场景级 UI")
	_entry_layer_input.set_item_tooltip(1, "参与返回栈的主要页面")
	_entry_layer_input.set_item_tooltip(2, "阻止下层交互的模态界面")
	_entry_layer_input.set_item_tooltip(3, "独立显示且默认不参与返回的覆盖层")
	content.add_child(_entry_layer_input)

	content.add_child(_create_label("Instance Mode"))
	_entry_instance_mode_input = OptionButton.new()
	_entry_instance_mode_input.add_item("Single", 0)
	_entry_instance_mode_input.add_item("Multiple", 1)
	_entry_instance_mode_input.set_item_tooltip(0, "同一 Id 同时只允许一个打开或加载中实例")
	_entry_instance_mode_input.set_item_tooltip(1, "同一 Id 允许同时打开多个独立实例")
	content.add_child(_entry_instance_mode_input)

	content.add_child(_create_label("Reuse Instance"))
	_entry_reuse_input = CheckBox.new()
	_entry_reuse_input.text = "关闭后保留节点实例"
	_entry_reuse_input.tooltip_text = "仅支持 Single；复用 UI 可通过 IPoolable 重置状态。"
	content.add_child(_entry_reuse_input)

	_entry_dialog.confirmed.connect(_on_entry_confirmed)
	_manage_dialog.add_child(_entry_dialog)


func _create_remove_dialog() -> void:
	_remove_dialog = ConfirmationDialog.new()
	_remove_dialog.exclusive = true
	_remove_dialog.transient_to_focused = true
	_remove_dialog.title = "删除 UI 配置条目"
	_remove_dialog.ok_button_text = "删除"
	_remove_dialog.cancel_button_text = "取消"
	_remove_dialog.confirmed.connect(_on_remove_confirmed)
	_manage_dialog.add_child(_remove_dialog)


func _create_report_dialog(editor_root: Control) -> void:
	_report_dialog = AcceptDialog.new()
	_report_dialog.name = "GoDoUiConfigReportDialog"
	_report_dialog.title = "UI 配置"
	_report_dialog.ok_button_text = "关闭"
	_report_dialog.min_size = Vector2i(720, 420)
	_report_dialog.exclusive = true
	_report_dialog.transient_to_focused = true
	_report_dialog.get_label().hide()
	_report_label = RichTextLabel.new()
	_report_label.bbcode_enabled = false
	_report_label.selection_enabled = true
	_report_dialog.add_child(_report_label)
	_report_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_report_label.offset_left = 16
	_report_label.offset_top = 16
	_report_label.offset_right = -16
	_report_label.offset_bottom = -48
	editor_root.add_child(_report_dialog)


func set_window_parent(window_parent: Window) -> void:
	_config_file_dialog.reparent(window_parent)
	_scene_file_dialog.reparent(window_parent)
	_config_selector_dialog.reparent(window_parent)
	_manage_dialog.reparent(window_parent)
	_report_dialog.reparent(window_parent)


func _create_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label


func _open_config_selector(action: String, title: String) -> void:
	_file_action = action
	_config_file_dialog.title = title
	_config_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_config_file_dialog.get_ok_button().text = "选择"
	_config_file_dialog.current_path = "res://"
	_config_file_dialog.popup_centered(Vector2i(720, 480))


func _open_existing_config(action: String) -> void:
	var config_paths := _prepare_config_paths(_find_ui_config_paths("res://"))
	_show_config_selector(action, config_paths)


func _prepare_config_paths(config_paths: PackedStringArray) -> PackedStringArray:
	var unique_paths := {}
	for path in config_paths:
		var normalized_path := path.strip_edges()
		if not normalized_path.is_empty():
			unique_paths[normalized_path] = true
	var prepared := PackedStringArray()
	for path in unique_paths:
		prepared.append(path)
	prepared.sort()
	return prepared


func _show_config_selector(action: String, config_paths: PackedStringArray) -> void:
	_selector_action = action
	_config_selector_dialog.title = (
		"选择要管理的 UI 配置"
		if action == ACTION_MANAGE
		else "选择要校验的 UI 配置")
	_config_selector_dialog.ok_button_text = "管理" if action == ACTION_MANAGE else "校验"
	_config_selector_label.text = (
		"项目内没有 UiConfig，请创建配置或手动选择。"
		if config_paths.is_empty()
		else "项目内发现 %d 份 UiConfig，请明确选择目标。" % config_paths.size())
	_config_selector_tree.clear()
	var root := _config_selector_tree.create_item()
	if config_paths.is_empty():
		var empty_item := _config_selector_tree.create_item(root)
		empty_item.set_text(0, "当前没有 UI 配置")
		empty_item.set_selectable(0, false)
	else:
		for path in config_paths:
			var item := _config_selector_tree.create_item(root)
			item.set_text(0, path)
			item.set_tooltip_text(0, path)
			item.set_metadata(0, path)
	_config_selector_dialog.get_ok_button().disabled = true
	_config_selector_dialog.popup_centered(Vector2i(760, 460))


func _on_config_selector_item_selected() -> void:
	_config_selector_dialog.get_ok_button().disabled = (
		_get_selected_config_path().is_empty())


func _on_config_selector_item_activated() -> void:
	var path := _get_selected_config_path()
	if path.is_empty():
		return
	_config_selector_dialog.hide()
	_dispatch_config_action(_selector_action, path)


func _on_config_selector_confirmed() -> void:
	var path := _get_selected_config_path()
	if not path.is_empty():
		_dispatch_config_action(_selector_action, path)


func _on_config_selector_manual_pressed() -> void:
	var action := _selector_action
	_config_selector_dialog.hide()
	call_deferred("_open_config_selector", action, "手动选择 UI 配置")


func _on_config_selector_create_pressed() -> void:
	_config_selector_dialog.hide()
	call_deferred("open_create_dialog")


func _get_selected_config_path() -> String:
	var item := _config_selector_tree.get_selected()
	if item == null:
		return ""
	var path = item.get_metadata(0)
	return str(path) if path != null else ""


func _dispatch_config_action(action: String, path: String) -> void:
	if action == ACTION_MANAGE:
		call_deferred("_show_manager", path)
	else:
		call_deferred("_show_validation_report", path)


func _entry_matches_filter(entry_id: String, locator: String, filter_text: String) -> bool:
	var normalized_filter := filter_text.strip_edges().to_lower()
	if normalized_filter.is_empty():
		return true
	return (
		entry_id.to_lower().contains(normalized_filter)
		or locator.to_lower().contains(normalized_filter)
	)


func _default_id_from_scene_path(path: String) -> String:
	return "ui/%s" % path.get_file().get_basename().to_snake_case()


func _display_scene_path(locator: String) -> String:
	return locator.trim_prefix("res://") if locator.begins_with("res://") else locator


func _on_config_file_selected(path: String) -> void:
	match _file_action:
		ACTION_CREATE:
			_create_config(path)
		_:
			_dispatch_config_action(_file_action, path)


func _on_config_file_dialog_canceled() -> void:
	config_paths_changed.emit("")


func _create_config(path: String) -> void:
	var save_path := path if not path.get_extension().is_empty() else "%s.tres" % path
	if FileAccess.file_exists(save_path):
		_show_message(false, "创建失败", "目标文件已经存在，不会覆盖：\n%s" % save_path)
		return

	var config := _instantiate_csharp_resource(UI_CONFIG_SCRIPT_PATH)
	if config == null:
		_show_message(false, "创建失败", "UiConfig 脚本无法实例化。\n%s" % _csharp_resource_load_error)
		return

	var save_error := ResourceSaver.save(config, save_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_message(false, "创建失败", "%s：%s" % [save_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	config_paths_changed.emit(save_path)
	call_deferred("_show_manager", save_path)


func _show_manager(path: String) -> void:
	if not _ensure_manage_dialog_ready():
		_show_message(false, "打开失败", "UI 配置管理弹窗初始化失败，请重新启用 GoDo Framework 插件。")
		return

	if not ResourceLoader.exists(path):
		_show_message(false, "打开失败", "UiConfig 资源已经删除或移动：\n%s" % path)
		return
	var config := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	if not _is_ui_config(config):
		_show_message(false, "打开失败", "请选择 UiConfig 资源：\n%s" % path)
		return

	_managed_config_path = path
	_manage_dialog.title = "UI 配置管理"
	_managed_config_label.text = "当前：%s" % path
	_locate_config_button.disabled = false
	_add_button.disabled = false
	_validate_button.disabled = false
	_search_input.clear()
	_render_entries(config)
	_manage_dialog.popup_centered(Vector2i(1080, 560))


func _ensure_manage_dialog_ready() -> bool:
	if (
		is_instance_valid(_manage_dialog)
		and is_instance_valid(_search_input)
		and is_instance_valid(_entries_tree)
		and is_instance_valid(_edit_button)
		and is_instance_valid(_remove_button)
		and is_instance_valid(_managed_config_label)
		and is_instance_valid(_locate_config_button)
		and is_instance_valid(_locate_scene_button)
		and is_instance_valid(_entry_reuse_input)
	):
		return true
	if not is_instance_valid(_plugin):
		return false

	if is_instance_valid(_manage_dialog):
		_manage_dialog.queue_free()
	var editor_root := _plugin.get_editor_interface().get_base_control()
	_create_manage_dialog(editor_root)
	_create_entry_dialog()
	_create_remove_dialog()
	return (
		is_instance_valid(_manage_dialog)
		and is_instance_valid(_search_input)
		and is_instance_valid(_entries_tree)
		and is_instance_valid(_managed_config_label)
		and is_instance_valid(_entry_reuse_input)
	)


func _render_entries(config: Resource) -> void:
	_entries_tree.clear()
	_managed_entry_index = -1
	_edit_button.disabled = true
	_remove_button.disabled = true
	_locate_scene_button.disabled = true
	var entries = _get_entries(config)
	var root := _entries_tree.create_item()
	if entries == null or entries.is_empty():
		var empty_item := _entries_tree.create_item(root)
		empty_item.set_text(0, "当前配置没有条目")
		for column in range(6):
			empty_item.set_selectable(column, false)
		return

	var filter_text := (
		_search_input.text.strip_edges()
		if is_instance_valid(_search_input)
		else "")
	var visible_count := 0
	for index in range(entries.size()):
		var entry = entries[index]
		var entry_id := "" if entry == null else _get_string(entry, "Id", "id")
		var locator := "" if entry == null else _get_string(entry, "Locator", "locator")
		if not _entry_matches_filter(entry_id, locator, filter_text):
			continue

		visible_count += 1
		var item := _entries_tree.create_item(root)
		if entry == null:
			item.set_text(0, "<null>")
			item.set_custom_color(0, ERROR_COLOR)
			item.set_text(5, "Invalid")
			item.set_custom_color(5, ERROR_COLOR)
			item.set_tooltip_text(5, "条目不能为 null")
		else:
			item.set_text(0, entry_id)
			item.set_text(1, _display_scene_path(locator))
			item.set_text(2, _layer_name(_get_int(entry, "Layer", "layer")))
			item.set_text(3, _instance_mode_name(_get_int(entry, "InstanceMode", "instance_mode")))
			var reuse_instance := _get_bool(entry, "ReuseInstance", "reuse_instance")
			item.set_text(4, "True" if reuse_instance else "False")
			item.set_tooltip_text(1, locator)
			var rejection := _get_entry_rejection_reason(
				entry_id,
				locator,
				_get_int(entry, "Layer", "layer"),
				_get_int(entry, "InstanceMode", "instance_mode"),
				reuse_instance,
				entries,
				index)
			if rejection.is_empty():
				var warning := _get_entry_warning_reason(locator, entries, index)
				if warning.is_empty():
					item.set_text(5, "Valid")
					item.set_custom_color(5, NORMAL_COLOR)
					item.set_tooltip_text(5, "配置有效")
				else:
					item.set_text(5, "Warning")
					item.set_custom_color(5, WARNING_COLOR)
					item.set_tooltip_text(5, warning)
			else:
				item.set_text(5, "Invalid")
				item.set_custom_color(5, ERROR_COLOR)
				item.set_tooltip_text(5, rejection)
		item.set_metadata(0, index)
		item.set_metadata(1, locator)
		for column in range(2, 6):
			item.set_text_alignment(column, HORIZONTAL_ALIGNMENT_CENTER)
	if visible_count == 0:
		var empty_item := _entries_tree.create_item(root)
		empty_item.set_text(0, "没有匹配的 UI 配置条目")
		for column in range(6):
			empty_item.set_selectable(column, false)


func _on_search_changed(_text: String) -> void:
	if _managed_config_path.is_empty():
		return
	if not ResourceLoader.exists(_managed_config_path):
		_render_missing_managed_config(_managed_config_path)
		return
	var config := _load_config_from_disk(_managed_config_path)
	if _is_ui_config(config):
		_render_entries(config)


func _on_entry_selected() -> void:
	var item := _entries_tree.get_selected()
	if item == null or item.get_metadata(0) == null:
		return
	_managed_entry_index = int(item.get_metadata(0))
	_edit_button.disabled = false
	_remove_button.disabled = false
	_locate_scene_button.disabled = _resolve_locator_path(str(item.get_metadata(1))).is_empty()


func _on_entry_activated() -> void:
	_on_entry_selected()
	_on_edit_pressed()


func _on_locate_config_pressed() -> void:
	_locate_in_file_system(_managed_config_path)


func _on_locate_scene_pressed() -> void:
	var item := _entries_tree.get_selected()
	if item == null:
		return
	_locate_in_file_system(_resolve_locator_path(str(item.get_metadata(1))))


func _resolve_locator_path(locator: String) -> String:
	var normalized_locator := locator.strip_edges()
	if normalized_locator.is_empty() or not ResourceLoader.exists(normalized_locator):
		return ""
	if normalized_locator.begins_with("res://"):
		return normalized_locator
	var resource := ResourceLoader.load(normalized_locator)
	return resource.resource_path if resource != null else ""


func _locate_in_file_system(path: String) -> void:
	if path.is_empty() or not ResourceLoader.exists(path):
		_show_message(false, "定位失败", "资源已经删除或移动：\n%s" % path)
		return
	if not is_instance_valid(_file_system_dock):
		_show_message(false, "定位失败", "Godot FileSystem Dock 当前不可用。")
		return
	_file_system_dock.navigate_to_path(path)


func _on_editor_file_removed(path: String) -> void:
	config_paths_changed.emit("")
	if not is_instance_valid(_manage_dialog) or not _manage_dialog.visible:
		return
	if path == _managed_config_path:
		_render_missing_managed_config(path)
		return
	_refresh_managed_config_after_filesystem_change()


func _on_editor_files_moved(old_path: String, new_path: String) -> void:
	var managed_config_moved := old_path == _managed_config_path
	if managed_config_moved:
		_managed_config_path = new_path
	config_paths_changed.emit(new_path if managed_config_moved else "")
	if not is_instance_valid(_manage_dialog) or not _manage_dialog.visible:
		return
	_refresh_managed_config_after_filesystem_change()


func _refresh_managed_config_after_filesystem_change() -> void:
	if _managed_config_path.is_empty() or not is_instance_valid(_manage_dialog):
		return
	if not ResourceLoader.exists(_managed_config_path):
		_render_missing_managed_config(_managed_config_path)
		return
	var config := ResourceLoader.load(
		_managed_config_path,
		"",
		ResourceLoader.CACHE_MODE_REPLACE)
	if not _is_ui_config(config):
		_render_missing_managed_config(_managed_config_path)
		return
	_manage_dialog.title = "UI 配置管理"
	_managed_config_label.text = "当前：%s" % _managed_config_path
	_locate_config_button.disabled = false
	_add_button.disabled = false
	_validate_button.disabled = false
	_render_entries(config)


func _render_missing_managed_config(path: String) -> void:
	_managed_config_label.text = "配置已删除或移动：%s" % path
	_entries_tree.clear()
	var root := _entries_tree.create_item()
	var item := _entries_tree.create_item(root)
	item.set_text(0, "配置资源已经不存在，请重新打开管理菜单选择配置。")
	for column in range(6):
		item.set_selectable(column, false)
	_edit_button.disabled = true
	_remove_button.disabled = true
	_add_button.disabled = true
	_validate_button.disabled = true
	_locate_config_button.disabled = true
	_locate_scene_button.disabled = true


func _on_add_pressed() -> void:
	_editing_entry_index = -1
	_entry_dialog.title = "添加 UI 配置条目"
	_entry_id_input.clear()
	_entry_locator_input.clear()
	_entry_layer_input.select(1)
	_entry_instance_mode_input.select(0)
	_entry_reuse_input.button_pressed = false
	_show_entry_dialog()


func _on_edit_pressed() -> void:
	var config := _load_managed_config("编辑失败")
	if config == null:
		return
	var entries = _get_entries(config)
	if entries == null:
		_show_message(false, "编辑失败", "UiConfig 缺少有效的 Entries 数组。")
		return
	if _managed_entry_index < 0 or _managed_entry_index >= entries.size():
		_show_message(false, "编辑失败", "当前选中条目已经不存在。")
		return
	var entry = entries[_managed_entry_index]
	if entry == null:
		_show_message(false, "编辑失败", "当前选中条目为 null，请删除后重新添加。")
		return

	_editing_entry_index = _managed_entry_index
	_entry_dialog.title = "编辑 UI 配置条目"
	_entry_id_input.text = _get_string(entry, "Id", "id")
	_entry_locator_input.text = _get_string(entry, "Locator", "locator")
	_select_option_id(_entry_layer_input, _get_int(entry, "Layer", "layer"))
	_select_option_id(
		_entry_instance_mode_input,
		_get_int(entry, "InstanceMode", "instance_mode"))
	_entry_reuse_input.button_pressed = _get_bool(
		entry,
		"ReuseInstance",
		"reuse_instance")
	_show_entry_dialog()


func _show_entry_dialog() -> void:
	_entry_dialog.popup_centered(Vector2i(760, 430))
	_entry_id_input.grab_focus()
	_entry_id_input.select_all()


func _on_choose_scene_pressed() -> void:
	_scene_file_dialog.current_path = (
		_entry_locator_input.text
		if _entry_locator_input.text.begins_with("res://")
		else "res://")
	_scene_file_dialog.popup_centered(Vector2i(720, 480))


func _on_scene_file_selected(path: String) -> void:
	_entry_locator_input.text = path
	if _entry_id_input.text.strip_edges().is_empty():
		_entry_id_input.text = _default_id_from_scene_path(path)


func _on_entry_confirmed() -> void:
	var config := _load_managed_config("保存失败")
	if config == null:
		return
	var entries = _copy_entries(_get_entries(config))
	if entries == null:
		_show_message(false, "保存失败", "UiConfig 缺少有效的 Entries 数组。")
		return
	var entry_id := _entry_id_input.text.strip_edges()
	var locator := _entry_locator_input.text.strip_edges()
	var layer := _entry_layer_input.get_selected_id()
	var instance_mode := _entry_instance_mode_input.get_selected_id()
	var reuse_instance := _entry_reuse_input.button_pressed
	var rejection := _get_entry_rejection_reason(
		entry_id,
		locator,
		layer,
		instance_mode,
		reuse_instance,
		entries,
		_editing_entry_index)
	if not rejection.is_empty():
		_show_message(false, "保存失败", rejection)
		return

	if _editing_entry_index < 0:
		var entry := _instantiate_csharp_resource(UI_CONFIG_ENTRY_SCRIPT_PATH)
		if entry == null:
			_show_message(false, "保存失败", "UiConfigEntry 脚本无法实例化。\n%s" % _csharp_resource_load_error)
			return
		entry.set("Id", entry_id)
		entry.set("Locator", locator)
		entry.set("Layer", layer)
		entry.set("InstanceMode", instance_mode)
		entry.set("ReuseInstance", reuse_instance)
		entries.append(entry)
	else:
		var entry: Resource = entries[_editing_entry_index].duplicate()
		entry.set("Id", entry_id)
		entry.set("Locator", locator)
		entry.set("Layer", layer)
		entry.set("InstanceMode", instance_mode)
		entry.set("ReuseInstance", reuse_instance)
		entries[_editing_entry_index] = entry

	config.set("Entries", entries)
	var saved_config := _save_managed_config(config, "保存失败")
	if saved_config != null:
		_render_entries(saved_config)


func _on_remove_pressed() -> void:
	var config := _load_managed_config("删除失败")
	if config == null:
		return
	var entries = _get_entries(config)
	if _managed_entry_index < 0 or _managed_entry_index >= entries.size():
		return
	var entry = entries[_managed_entry_index]
	var entry_id := "<null>" if entry == null else _get_string(entry, "Id", "id")
	_remove_dialog.dialog_text = "仅移除配置条目，不会删除 UI Scene。\n\nId：%s" % entry_id
	_remove_dialog.popup_centered(Vector2i(600, 240))


func _on_remove_confirmed() -> void:
	var config := _load_managed_config("删除失败")
	if config == null:
		return
	var entries = _copy_entries(_get_entries(config))
	if entries == null:
		_show_message(false, "删除失败", "UiConfig 缺少有效的 Entries 数组。")
		return
	if _managed_entry_index < 0 or _managed_entry_index >= entries.size():
		return
	entries.remove_at(_managed_entry_index)
	config.set("Entries", entries)
	var saved_config := _save_managed_config(config, "删除失败")
	if saved_config != null:
		_render_entries(saved_config)


func _on_validate_pressed() -> void:
	_show_validation_report(_managed_config_path)


func _show_validation_report(path: String) -> void:
	if not ResourceLoader.exists(path):
		_show_message(false, "校验失败", "UiConfig 资源已经删除或移动：\n%s" % path)
		return
	var config := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	if not _is_ui_config(config):
		_show_message(false, "校验失败", "请选择 UiConfig 资源：\n%s" % path)
		return
	var errors := _validate_config(config)
	var warnings := _validate_config_warnings(config)
	if errors.is_empty() and warnings.is_empty():
		_show_message(true, "校验通过", "配置包含 %d 个有效 UI 条目。\n%s" % [
			_get_entries(config).size(),
			path,
		])
	elif errors.is_empty():
		_show_message(
			true,
			"校验通过（有警告）",
			"配置包含 %d 个有效 UI 条目。\n%s\n\nWarning：\n%s" % [
				_get_entries(config).size(),
				path,
				"\n".join(warnings),
			],
			WARNING_COLOR)
	else:
		var warning_text := (
			"\n\nWarning：\n%s" % "\n".join(warnings)
			if not warnings.is_empty()
			else "")
		_show_message(false, "校验失败", "%s\n\n%s%s" % [
			path,
			"\n".join(errors),
			warning_text,
		])


func _validate_config(config: Resource) -> PackedStringArray:
	var errors := PackedStringArray()
	var entries = _get_entries(config)
	if entries == null or entries.is_empty():
		errors.append("UiConfig 至少需要一个配置条目。")
		return errors
	for index in range(entries.size()):
		var entry = entries[index]
		if entry == null:
			errors.append("条目 %d 不能为 null。" % index)
			continue
		var rejection := _get_entry_rejection_reason(
			_get_string(entry, "Id", "id"),
			_get_string(entry, "Locator", "locator"),
			_get_int(entry, "Layer", "layer"),
			_get_int(entry, "InstanceMode", "instance_mode"),
			_get_bool(entry, "ReuseInstance", "reuse_instance"),
			entries,
			index)
		if not rejection.is_empty():
			errors.append("条目 %d：%s" % [index, rejection])
	return errors


func _validate_config_warnings(config: Resource) -> PackedStringArray:
	var warnings := PackedStringArray()
	var entries = _get_entries(config)
	if entries == null:
		return warnings
	for index in range(entries.size()):
		var entry = entries[index]
		if entry == null:
			continue
		var warning := _get_entry_warning_reason(
			_get_string(entry, "Locator", "locator"),
			entries,
			index)
		if not warning.is_empty():
			warnings.append("条目 %d：%s" % [index, warning])
	return warnings


func _get_entry_rejection_reason(
	entry_id: String,
	locator: String,
	layer: int,
	instance_mode: int,
	reuse_instance: bool,
	entries: Array,
	skip_index: int) -> String:
	var normalized_id := entry_id.strip_edges()
	if normalized_id.is_empty():
		return "Id 不能为空。"
	if not (locator.begins_with("res://") or locator.begins_with("uid://")):
		return "Scene Locator 必须以 res:// 或 uid:// 开头：%s" % locator
	if not ResourceLoader.exists(locator):
		return "Scene Locator 无法解析：%s" % locator
	var scene := ResourceLoader.load(locator) as PackedScene
	if scene == null:
		return "资源不是 PackedScene：%s" % locator
	var state := scene.get_state()
	if state == null or state.get_node_count() == 0:
		return "Scene 不包含根节点：%s" % locator
	var root_type := str(state.get_node_type(0))
	if not ClassDB.is_parent_class(root_type, "Control"):
		return "UI Scene 根节点必须继承 Control，当前为 %s：%s" % [root_type, locator]
	if layer < 0 or layer > 3:
		return "未知 UI 层：%d" % layer
	if instance_mode < 0 or instance_mode > 1:
		return "未知实例模式：%d" % instance_mode
	if reuse_instance and instance_mode != 0:
		return "只有 Single UI 可以启用实例复用。"
	for index in range(entries.size()):
		if index == skip_index:
			continue
		var other = entries[index]
		if (
			other != null
			and _get_string(other, "Id", "id").strip_edges() == normalized_id
		):
			return "配置中已经存在 Id：%s" % normalized_id
	return ""


func _get_entry_warning_reason(locator: String, entries: Array, skip_index: int) -> String:
	var normalized_locator := locator.strip_edges()
	if normalized_locator.is_empty():
		return ""
	var other_ids := PackedStringArray()
	for index in range(entries.size()):
		if index == skip_index:
			continue
		var other = entries[index]
		if (
			other != null
			and _get_string(other, "Locator", "locator").strip_edges() == normalized_locator
		):
			other_ids.append(_get_string(other, "Id", "id").strip_edges())
	if other_ids.is_empty():
		return ""
	return "同一 Locator 被多个 Id 使用（%s）；这是允许的，请确认它们确实共享 Scene：%s" % [
		", ".join(other_ids),
		normalized_locator,
	]


func _load_managed_config(title: String) -> Resource:
	if _managed_config_path.is_empty():
		_show_message(false, title, "尚未选择 UiConfig。")
		return null
	if not ResourceLoader.exists(_managed_config_path):
		_render_missing_managed_config(_managed_config_path)
		_show_message(false, title, "当前 UiConfig 已经删除或移动：\n%s" % _managed_config_path)
		return null
	var config := _load_config_from_disk(_managed_config_path)
	if not _is_ui_config(config):
		_show_message(false, title, "当前 UiConfig 无法重新加载：\n%s" % _managed_config_path)
		return null
	return config


func _save_managed_config(config: Resource, title: String) -> Resource:
	var reloaded := _save_config_and_reload(config, _managed_config_path)
	if reloaded == null:
		_show_message(false, title, _config_persistence_error)
		return null
	_refresh_editor_filesystem()
	_plugin.get_editor_interface().edit_resource(reloaded)
	return reloaded


func _save_config_and_reload(config: Resource, path: String) -> Resource:
	_config_persistence_error = ""
	var expected_signature := _config_entries_signature(_get_entries(config))
	config.emit_changed()
	var save_error := ResourceSaver.save(
		config,
		path,
		ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_config_persistence_error = "%s：%s" % [
			path,
			error_string(save_error),
		]
		return null
	var reloaded := _load_config_from_disk(path)
	if not _is_ui_config(reloaded):
		_config_persistence_error = "保存后无法从磁盘重新加载 UiConfig：%s" % path
		return null
	if _config_entries_signature(_get_entries(reloaded)) != expected_signature:
		_config_persistence_error = "保存后校验失败，磁盘中的 Entries 与待保存内容不一致：%s" % path
		return null
	return reloaded


func _instantiate_csharp_resource(script_path: String) -> Resource:
	_csharp_resource_load_error = ""
	var loaded_resource := ResourceLoader.load(
		script_path,
		"",
		ResourceLoader.CACHE_MODE_REUSE)
	var script := loaded_resource as Script
	if script == null:
		_csharp_resource_load_error = "无法将 %s 加载为 Script。" % script_path
		return null
	var instance: Variant = script.new()
	if not (instance is Resource):
		_csharp_resource_load_error = "Script.new() 未返回 Resource。"
		return null
	return instance as Resource


func _is_ui_config(resource: Resource) -> bool:
	if resource == null:
		return false
	var script := resource.get_script() as Script
	return script != null and script.resource_path == UI_CONFIG_SCRIPT_PATH


func _find_ui_config_paths(path: String) -> PackedStringArray:
	var config_paths := PackedStringArray()
	for file_name in DirAccess.get_files_at(path):
		var extension := file_name.get_extension().to_lower()
		if extension != "tres" and extension != "res":
			continue
		var resource_path := path.path_join(file_name)
		if _is_ui_config(ResourceLoader.load(resource_path)):
			config_paths.append(resource_path)
	for directory_name in DirAccess.get_directories_at(path):
		if _should_skip_ui_config_directory(path, directory_name):
			continue
		config_paths.append_array(
			_find_ui_config_paths(path.path_join(directory_name)))
	return config_paths


func _should_skip_ui_config_directory(path: String, directory_name: String) -> bool:
	if directory_name.begins_with("."):
		return true
	var directory_path := path.path_join(directory_name)
	return FileAccess.file_exists(directory_path.path_join("project.godot"))


func _get_entries(config: Resource):
	if config == null:
		return null
	var entries = config.get("Entries")
	if entries == null:
		entries = config.get("entries")
	return entries


func _copy_entries(entries):
	if entries == null or not (entries is Array):
		return null
	return entries.duplicate()


func _load_config_from_disk(path: String) -> Resource:
	return ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)


func _config_entries_signature(entries) -> PackedStringArray:
	var signature := PackedStringArray()
	if entries == null or not (entries is Array):
		return signature
	for entry in entries:
		if entry == null or not (entry is Object):
			signature.append("<null>")
			continue
		signature.append("%s\u001f%s\u001f%d\u001f%d\u001f%s" % [
			_get_string(entry, "Id", "id"),
			_get_string(entry, "Locator", "locator"),
			_get_int(entry, "Layer", "layer"),
			_get_int(entry, "InstanceMode", "instance_mode"),
			str(_get_bool(entry, "ReuseInstance", "reuse_instance")),
		])
	return signature


func _get_string(target: Object, primary_name: String, fallback_name: String) -> String:
	var value = target.get(primary_name)
	if value == null:
		value = target.get(fallback_name)
	return str(value) if value != null else ""


func _get_int(target: Object, primary_name: String, fallback_name: String) -> int:
	var value = target.get(primary_name)
	if value == null:
		value = target.get(fallback_name)
	return int(value) if value != null else -1


func _get_bool(target: Object, primary_name: String, fallback_name: String) -> bool:
	var value = target.get(primary_name)
	if value == null:
		value = target.get(fallback_name)
	return bool(value) if value != null else false


func _select_option_id(option: OptionButton, id: int) -> void:
	for index in range(option.item_count):
		if option.get_item_id(index) == id:
			option.select(index)
			return
	option.select(0)


func _layer_name(value: int) -> String:
	match value:
		0:
			return "Scene"
		1:
			return "View"
		2:
			return "Modal"
		3:
			return "Overlay"
		_:
			return "未知 (%d)" % value


func _instance_mode_name(value: int) -> String:
	match value:
		0:
			return "Single"
		1:
			return "Multiple"
		_:
			return "未知 (%d)" % value


func _refresh_editor_filesystem() -> void:
	if not is_instance_valid(_plugin):
		return
	var filesystem := _plugin.get_editor_interface().get_resource_filesystem()
	if filesystem != null and not filesystem.is_scanning():
		filesystem.scan()


func _show_message(
	success: bool,
	title: String,
	message: String,
	override_color: Color = Color(0, 0, 0, 0)) -> void:
	if not is_instance_valid(_report_label):
		return
	_report_dialog.title = title
	_report_label.clear()
	_report_label.push_font_size(18)
	var title_color := (
		override_color
		if override_color.a > 0.0
		else NORMAL_COLOR if success else ERROR_COLOR)
	_report_label.push_color(title_color)
	_report_label.add_text(title)
	_report_label.pop()
	_report_label.pop()
	_report_label.add_text("\n\n%s" % message)
	_report_dialog.popup_centered(Vector2i(720, 420))
