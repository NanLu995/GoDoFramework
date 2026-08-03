@tool
extends RefCounted

const ACTION_CREATE := "create"
const ACTION_MANAGE := "manage"
const ACTION_VALIDATE := "validate"
const UI_CONFIG_SCRIPT_PATH := "res://addons/godo_framework/Runtime/UI/UiConfig.cs"
const UI_CONFIG_ENTRY_SCRIPT_PATH := "res://addons/godo_framework/Runtime/UI/UiConfigEntry.cs"
const NORMAL_COLOR := Color("#8BD49C")
const ERROR_COLOR := Color("#FF6B6B")

var _plugin: EditorPlugin
var _config_file_dialog: EditorFileDialog
var _scene_file_dialog: EditorFileDialog
var _manage_dialog: AcceptDialog
var _entries_tree: Tree
var _search_input: LineEdit
var _add_button: Button
var _edit_button: Button
var _remove_button: Button
var _validate_button: Button
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
var _managed_config_path := ""
var _managed_entry_index := -1
var _editing_entry_index := -1
var _csharp_resource_load_error := ""


func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
	var editor_root := _plugin.get_editor_interface().get_base_control()

	_config_file_dialog = EditorFileDialog.new()
	_config_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_config_file_dialog.mode_overrides_title = false
	_config_file_dialog.filters = PackedStringArray(["*.tres,*.res;UI config resources"])
	_config_file_dialog.file_selected.connect(_on_config_file_selected)
	editor_root.add_child(_config_file_dialog)

	_scene_file_dialog = EditorFileDialog.new()
	_scene_file_dialog.title = "选择 UI 场景"
	_scene_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_scene_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_scene_file_dialog.mode_overrides_title = false
	_scene_file_dialog.filters = PackedStringArray(["*.tscn;Godot scenes"])
	_scene_file_dialog.file_selected.connect(_on_scene_file_selected)
	editor_root.add_child(_scene_file_dialog)

	_create_manage_dialog(editor_root)
	_create_entry_dialog()
	_create_remove_dialog()
	_create_report_dialog(editor_root)


func dispose() -> void:
	for dialog in [
		_config_file_dialog,
		_scene_file_dialog,
		_manage_dialog,
		_entry_dialog,
		_remove_dialog,
		_report_dialog,
	]:
		if is_instance_valid(dialog):
			dialog.queue_free()


func open_create_dialog() -> void:
	_file_action = ACTION_CREATE
	_config_file_dialog.title = "创建 UI 配置"
	_config_file_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	_config_file_dialog.get_ok_button().text = "创建"
	_config_file_dialog.current_path = "res://UiConfig.tres"
	_config_file_dialog.popup_centered(Vector2i(720, 480))


func open_manage_dialog() -> void:
	_open_existing_config(ACTION_MANAGE, "选择要管理的 UI 配置")


func open_validate_dialog() -> void:
	_open_existing_config(ACTION_VALIDATE, "选择要校验的 UI 配置")


func _create_manage_dialog(editor_root: Control) -> void:
	_manage_dialog = AcceptDialog.new()
	_manage_dialog.title = "UI 配置管理"
	_manage_dialog.ok_button_text = "关闭"
	_manage_dialog.min_size = Vector2i(1080, 560)
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
	var search_label := Label.new()
	search_label.text = "Search"
	toolbar.add_child(search_label)
	_search_input = LineEdit.new()
	_search_input.placeholder_text = "Filter by Id or scene path"
	_search_input.clear_button_enabled = true
	_search_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_search_input.text_changed.connect(_on_search_changed)
	toolbar.add_child(_search_input)
	_add_button = Button.new()
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
	_validate_button.text = "校验"
	_validate_button.pressed.connect(_on_validate_pressed)
	toolbar.add_child(_validate_button)

	_entries_tree = Tree.new()
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
	_entry_id_input.placeholder_text = "例如：main_menu"
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
	choose_scene_button.text = "选择场景..."
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
	_remove_dialog.title = "删除 UI 配置条目"
	_remove_dialog.ok_button_text = "删除"
	_remove_dialog.cancel_button_text = "取消"
	_remove_dialog.confirmed.connect(_on_remove_confirmed)
	_manage_dialog.add_child(_remove_dialog)


func _create_report_dialog(editor_root: Control) -> void:
	_report_dialog = AcceptDialog.new()
	_report_dialog.title = "UI 配置"
	_report_dialog.ok_button_text = "关闭"
	_report_dialog.min_size = Vector2i(720, 420)
	_report_dialog.exclusive = false
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


func _open_existing_config(action: String, title: String) -> void:
	var config_paths := _find_ui_config_paths("res://")
	var direct_path := _get_direct_config_path(config_paths)
	if not direct_path.is_empty():
		if action == ACTION_MANAGE:
			call_deferred("_show_manager", direct_path)
		else:
			call_deferred("_show_validation_report", direct_path)
		return

	var selector_title := title
	if config_paths.is_empty():
		selector_title = "%s（未自动发现，可手动选择）" % title
	else:
		selector_title = "%s（发现 %d 份）" % [title, config_paths.size()]
	_open_config_selector(action, selector_title)


func _get_direct_config_path(config_paths: PackedStringArray) -> String:
	return config_paths[0] if config_paths.size() == 1 else ""


func _entry_matches_filter(entry_id: String, locator: String, filter_text: String) -> bool:
	var normalized_filter := filter_text.strip_edges().to_lower()
	if normalized_filter.is_empty():
		return true
	return (
		entry_id.to_lower().contains(normalized_filter)
		or locator.to_lower().contains(normalized_filter)
	)


func _default_id_from_scene_path(path: String) -> String:
	return path.get_file().get_basename()


func _display_scene_path(locator: String) -> String:
	return locator.trim_prefix("res://") if locator.begins_with("res://") else locator


func _on_config_file_selected(path: String) -> void:
	match _file_action:
		ACTION_CREATE:
			_create_config(path)
		ACTION_MANAGE:
			call_deferred("_show_manager", path)
		ACTION_VALIDATE:
			call_deferred("_show_validation_report", path)


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
	call_deferred("_show_manager", save_path)


func _show_manager(path: String) -> void:
	if not _ensure_manage_dialog_ready():
		_show_message(false, "打开失败", "UI 配置管理弹窗初始化失败，请重新启用 GoDo Framework 插件。")
		return

	var config := ResourceLoader.load(path)
	if not _is_ui_config(config):
		_show_message(false, "打开失败", "请选择 UiConfig 资源：\n%s" % path)
		return

	_managed_config_path = path
	_manage_dialog.title = "UI 配置管理 — %s" % path
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
		and is_instance_valid(_entry_reuse_input)
	)


func _render_entries(config: Resource) -> void:
	_entries_tree.clear()
	_managed_entry_index = -1
	_edit_button.disabled = true
	_remove_button.disabled = true
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
			item.set_text(4, "Yes" if reuse_instance else "No")
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
				item.set_text(5, "Valid")
				item.set_custom_color(5, NORMAL_COLOR)
				item.set_tooltip_text(5, "配置有效")
			else:
				item.set_text(5, "Invalid")
				item.set_custom_color(5, ERROR_COLOR)
				item.set_tooltip_text(5, rejection)
		item.set_metadata(0, index)
	if visible_count == 0:
		var empty_item := _entries_tree.create_item(root)
		empty_item.set_text(0, "没有匹配的 UI 配置条目")
		for column in range(6):
			empty_item.set_selectable(column, false)


func _on_search_changed(_text: String) -> void:
	if _managed_config_path.is_empty():
		return
	var config := ResourceLoader.load(_managed_config_path)
	if _is_ui_config(config):
		_render_entries(config)


func _on_entry_selected() -> void:
	var item := _entries_tree.get_selected()
	if item == null or item.get_metadata(0) == null:
		return
	_managed_entry_index = int(item.get_metadata(0))
	_edit_button.disabled = false
	_remove_button.disabled = false


func _on_entry_activated() -> void:
	_on_entry_selected()
	_on_edit_pressed()


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
	var entries = _get_entries(config)
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
		var entry = entries[_editing_entry_index]
		entry.set("Id", entry_id)
		entry.set("Locator", locator)
		entry.set("Layer", layer)
		entry.set("InstanceMode", instance_mode)
		entry.set("ReuseInstance", reuse_instance)

	config.set("Entries", entries)
	if _save_managed_config(config, "保存失败"):
		_render_entries(config)


func _on_remove_pressed() -> void:
	var config := _load_managed_config("删除失败")
	if config == null:
		return
	var entries = _get_entries(config)
	if _managed_entry_index < 0 or _managed_entry_index >= entries.size():
		return
	var entry = entries[_managed_entry_index]
	var entry_id := "<null>" if entry == null else _get_string(entry, "Id", "id")
	_remove_dialog.dialog_text = "仅移除配置条目，不会删除 UI 场景。\n\nId：%s" % entry_id
	_remove_dialog.popup_centered(Vector2i(600, 240))


func _on_remove_confirmed() -> void:
	var config := _load_managed_config("删除失败")
	if config == null:
		return
	var entries = _get_entries(config)
	if _managed_entry_index < 0 or _managed_entry_index >= entries.size():
		return
	entries.remove_at(_managed_entry_index)
	config.set("Entries", entries)
	if _save_managed_config(config, "删除失败"):
		_render_entries(config)


func _on_validate_pressed() -> void:
	_show_validation_report(_managed_config_path)


func _show_validation_report(path: String) -> void:
	var config := ResourceLoader.load(path)
	if not _is_ui_config(config):
		_show_message(false, "校验失败", "请选择 UiConfig 资源：\n%s" % path)
		return
	var errors := _validate_config(config)
	if errors.is_empty():
		_show_message(true, "校验通过", "配置包含 %d 个有效 UI 条目。\n%s" % [
			_get_entries(config).size(),
			path,
		])
	else:
		_show_message(false, "校验失败", "%s\n\n%s" % [path, "\n".join(errors)])


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
		return "场景定位必须以 res:// 或 uid:// 开头：%s" % locator
	if not ResourceLoader.exists(locator):
		return "场景定位无法解析：%s" % locator
	var scene := ResourceLoader.load(locator) as PackedScene
	if scene == null:
		return "资源不是 PackedScene：%s" % locator
	var state := scene.get_state()
	if state == null or state.get_node_count() == 0:
		return "场景不包含根节点：%s" % locator
	var root_type := str(state.get_node_type(0))
	if not ClassDB.is_parent_class(root_type, "Control"):
		return "UI 场景根节点必须继承 Control，当前为 %s：%s" % [root_type, locator]
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


func _load_managed_config(title: String) -> Resource:
	if _managed_config_path.is_empty():
		_show_message(false, title, "尚未选择 UiConfig。")
		return null
	var config := ResourceLoader.load(_managed_config_path)
	if not _is_ui_config(config):
		_show_message(false, title, "当前 UiConfig 无法重新加载：\n%s" % _managed_config_path)
		return null
	return config


func _save_managed_config(config: Resource, title: String) -> bool:
	var save_error := ResourceSaver.save(
		config,
		_managed_config_path,
		ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_message(false, title, "%s：%s" % [
			_managed_config_path,
			error_string(save_error),
		])
		return false
	_refresh_editor_filesystem()
	return true


func _instantiate_csharp_resource(script_path: String) -> Resource:
	_csharp_resource_load_error = ""
	var loaded_resource := ResourceLoader.load(
		script_path,
		"",
		ResourceLoader.CACHE_MODE_IGNORE)
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
		if directory_name.begins_with("."):
			continue
		config_paths.append_array(
			_find_ui_config_paths(path.path_join(directory_name)))
	return config_paths


func _get_entries(config: Resource):
	if config == null:
		return null
	var entries = config.get("Entries")
	if entries == null:
		entries = config.get("entries")
	return entries


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
	var filesystem := _plugin.get_editor_interface().get_resource_filesystem()
	if filesystem != null and not filesystem.is_scanning():
		filesystem.scan()


func _show_message(success: bool, title: String, message: String) -> void:
	if not is_instance_valid(_report_label):
		return
	_report_dialog.title = title
	_report_label.clear()
	_report_label.push_font_size(18)
	_report_label.push_color(NORMAL_COLOR if success else ERROR_COLOR)
	_report_label.add_text(title)
	_report_label.pop()
	_report_label.pop()
	_report_label.add_text("\n\n%s" % message)
	_report_dialog.popup_centered(Vector2i(720, 420))
