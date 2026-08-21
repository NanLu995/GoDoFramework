@tool
extends RefCounted

signal manifest_paths_changed(preferred_path: String)

const MANIFEST_ACTION_VALIDATE := "validate"
const MANIFEST_ACTION_CREATE := "create"
const MANIFEST_ACTION_ADD_SELECTED := "add_selected"
const MANIFEST_ACTION_MANAGE := "manage"
const RESOURCE_MANIFEST_SCRIPT_PATH := "res://addons/godo_framework/Runtime/Resources/ResourceManifest.cs"
const RESOURCE_MANIFEST_ENTRY_SCRIPT_PATH := "res://addons/godo_framework/Runtime/Resources/ResourceManifestEntry.cs"
const MANIFEST_ADD_PREVIEW_LIMIT := 8
const NORMAL_COLOR := Color("#8BD49C")
const PENDING_COLOR := Color("#AEB6C2")
const WARNING_COLOR := Color("#FFD166")
const ERROR_COLOR := Color("#FF6B6B")

enum HealthLevel {
	NORMAL,
	PENDING,
	WARNING,
	ERROR,
}

var _plugin: EditorPlugin
var _manifest_file_dialog: EditorFileDialog
var _manifest_selector_dialog: ConfirmationDialog
var _manifest_selector_label: Label
var _manifest_selector_tree: Tree
var _manifest_selector_create_button: Button
var _manifest_selector_manual_button: Button
var _resource_file_dialog: EditorFileDialog
var _manifest_add_confirm_dialog: ConfirmationDialog
var _manifest_manage_dialog: AcceptDialog
var _manifest_remove_confirm_dialog: ConfirmationDialog
var _manifest_edit_dialog: ConfirmationDialog
var _manifest_uid_confirm_dialog: ConfirmationDialog
var _manifest_report_dialog: AcceptDialog
var _manifest_report_label: RichTextLabel
var _managed_manifest_label: Label
var _manifest_entries_tree: Tree
var _manifest_search_input: LineEdit
var _manifest_edit_id_input: LineEdit
var _manifest_edit_locator_input: LineEdit
var _manifest_locate_button: Button
var _manifest_add_resource_button: Button
var _manifest_validate_button: Button
var _manifest_edit_button: Button
var _manifest_uid_button: Button
var _manifest_remove_button: Button
var _manifest_action := ""
var _manifest_return_action_after_create := ""
var _pending_resource_paths := PackedStringArray()
var _pending_manifest_path := ""
var _pending_add_target_manifest_path := ""
var _managed_manifest_path := ""
var _managed_entry_index := -1
var _csharp_resource_load_error := ""
var _uid_generation_error := ""
var _manifest_persistence_error := ""
var _file_system_dock

func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
	_manifest_file_dialog = EditorFileDialog.new()
	_manifest_file_dialog.name = "ManifestFileDialog"
	_manifest_file_dialog.exclusive = true
	_manifest_file_dialog.transient_to_focused = true
	_manifest_file_dialog.title = "校验资源清单"
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_manifest_file_dialog.mode_overrides_title = false
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.file_selected.connect(_on_manifest_file_selected)
	_manifest_file_dialog.canceled.connect(_on_manifest_file_dialog_canceled)
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_file_dialog)
	_create_manifest_selector_dialog(_plugin.get_editor_interface().get_base_control())

	_resource_file_dialog = EditorFileDialog.new()
	_resource_file_dialog.name = "GoDoResourceFileDialog"
	_resource_file_dialog.exclusive = true
	_resource_file_dialog.transient_to_focused = true
	_resource_file_dialog.title = "选择要添加的资源"
	_resource_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILES
	_resource_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_resource_file_dialog.mode_overrides_title = false
	_resource_file_dialog.filters = _addable_resource_filters()
	_resource_file_dialog.current_path = "res://"
	_resource_file_dialog.files_selected.connect(_on_resource_files_selected)
	_plugin.get_editor_interface().get_base_control().add_child(_resource_file_dialog)

	_manifest_add_confirm_dialog = ConfirmationDialog.new()
	_manifest_add_confirm_dialog.name = "GoDoManifestAddConfirmDialog"
	_manifest_add_confirm_dialog.exclusive = true
	_manifest_add_confirm_dialog.transient_to_focused = true
	_manifest_add_confirm_dialog.title = "确认添加资源"
	_manifest_add_confirm_dialog.ok_button_text = "确认添加"
	_manifest_add_confirm_dialog.cancel_button_text = "取消添加"
	_manifest_add_confirm_dialog.confirmed.connect(_on_manifest_add_confirmed)
	_manifest_add_confirm_dialog.canceled.connect(_on_manifest_add_canceled)
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_add_confirm_dialog)

	_manifest_manage_dialog = AcceptDialog.new()
	_manifest_manage_dialog.name = "GoDoManifestManageDialog"
	_manifest_manage_dialog.title = "资源清单管理"
	_manifest_manage_dialog.ok_button_text = "关闭"
	_manifest_manage_dialog.min_size = Vector2i(1100, 520)
	_manifest_manage_dialog.exclusive = true
	_manifest_manage_dialog.transient_to_focused = true
	_manifest_manage_dialog.get_label().hide()
	var manage_content := VBoxContainer.new()
	manage_content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	manage_content.offset_left = 16
	manage_content.offset_top = 16
	manage_content.offset_right = -16
	manage_content.offset_bottom = -56
	manage_content.add_theme_constant_override("separation", 8)
	_manifest_manage_dialog.add_child(manage_content)

	var manifest_toolbar := HBoxContainer.new()
	manifest_toolbar.add_theme_constant_override("separation", 8)
	manage_content.add_child(manifest_toolbar)
	_managed_manifest_label = Label.new()
	_managed_manifest_label.name = "GoDoManagedManifestLabel"
	_managed_manifest_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	manifest_toolbar.add_child(_managed_manifest_label)
	_manifest_locate_button = Button.new()
	_manifest_locate_button.name = "GoDoManifestLocateButton"
	_manifest_locate_button.text = "定位清单"
	_manifest_locate_button.pressed.connect(_on_manifest_locate_pressed)
	manifest_toolbar.add_child(_manifest_locate_button)

	var entry_toolbar := HBoxContainer.new()
	entry_toolbar.add_theme_constant_override("separation", 8)
	manage_content.add_child(entry_toolbar)
	var search_label := Label.new()
	search_label.text = "Search"
	entry_toolbar.add_child(search_label)
	_manifest_search_input = LineEdit.new()
	_manifest_search_input.name = "GoDoManifestSearchInput"
	_manifest_search_input.placeholder_text = "Filter by Id or resource path"
	_manifest_search_input.clear_button_enabled = true
	_manifest_search_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_manifest_search_input.text_changed.connect(_on_manifest_search_changed)
	entry_toolbar.add_child(_manifest_search_input)
	_manifest_add_resource_button = Button.new()
	_manifest_add_resource_button.name = "GoDoManifestAddResourceButton"
	_manifest_add_resource_button.text = "添加资源"
	_manifest_add_resource_button.pressed.connect(_on_manifest_add_resource_pressed)
	entry_toolbar.add_child(_manifest_add_resource_button)
	_manifest_edit_button = Button.new()
	_manifest_edit_button.text = "编辑"
	_manifest_edit_button.disabled = true
	_manifest_edit_button.pressed.connect(_on_manifest_edit_pressed)
	entry_toolbar.add_child(_manifest_edit_button)
	_manifest_uid_button = Button.new()
	_manifest_uid_button.text = "生成并使用 UID"
	_manifest_uid_button.disabled = true
	_manifest_uid_button.pressed.connect(_on_manifest_uid_pressed)
	entry_toolbar.add_child(_manifest_uid_button)
	_manifest_remove_button = Button.new()
	_manifest_remove_button.text = "删除"
	_manifest_remove_button.disabled = true
	_manifest_remove_button.pressed.connect(_on_manifest_remove_pressed)
	entry_toolbar.add_child(_manifest_remove_button)
	_manifest_validate_button = Button.new()
	_manifest_validate_button.name = "GoDoManifestValidateButton"
	_manifest_validate_button.text = "校验"
	_manifest_validate_button.pressed.connect(_on_manifest_validate_pressed)
	entry_toolbar.add_child(_manifest_validate_button)

	_manifest_entries_tree = Tree.new()
	_manifest_entries_tree.name = "GoDoManifestEntriesTree"
	_manifest_entries_tree.columns = 3
	_manifest_entries_tree.column_titles_visible = true
	_manifest_entries_tree.hide_root = true
	_manifest_entries_tree.select_mode = Tree.SELECT_ROW
	_manifest_entries_tree.set_column_title(0, "Id")
	_manifest_entries_tree.set_column_title(1, "Locator")
	_manifest_entries_tree.set_column_title(2, "UID Status")
	_manifest_entries_tree.set_column_expand(0, true)
	_manifest_entries_tree.set_column_expand_ratio(0, 2)
	_manifest_entries_tree.set_column_expand(1, true)
	_manifest_entries_tree.set_column_expand_ratio(1, 4)
	_manifest_entries_tree.set_column_expand(2, true)
	_manifest_entries_tree.set_column_expand_ratio(2, 1)
	_manifest_entries_tree.item_selected.connect(_on_manifest_entry_selected)
	_manifest_entries_tree.item_activated.connect(_on_manifest_entry_activated)
	_manifest_entries_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	manage_content.add_child(_manifest_entries_tree)
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_manage_dialog)

	_manifest_remove_confirm_dialog = ConfirmationDialog.new()
	_manifest_remove_confirm_dialog.exclusive = true
	_manifest_remove_confirm_dialog.transient_to_focused = true
	_manifest_remove_confirm_dialog.title = "删除资源清单条目"
	_manifest_remove_confirm_dialog.ok_button_text = "删除"
	_manifest_remove_confirm_dialog.cancel_button_text = "取消"
	_manifest_remove_confirm_dialog.confirmed.connect(_on_manifest_remove_confirmed)
	_manifest_manage_dialog.add_child(_manifest_remove_confirm_dialog)

	_manifest_edit_dialog = ConfirmationDialog.new()
	_manifest_edit_dialog.exclusive = true
	_manifest_edit_dialog.transient_to_focused = true
	_manifest_edit_dialog.title = "编辑资源清单条目"
	_manifest_edit_dialog.ok_button_text = "保存修改"
	_manifest_edit_dialog.cancel_button_text = "取消"
	_manifest_edit_dialog.min_size = Vector2i(680, 280)
	_manifest_edit_dialog.get_label().hide()
	var edit_content := VBoxContainer.new()
	edit_content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	edit_content.offset_left = 16
	edit_content.offset_top = 16
	edit_content.offset_right = -16
	edit_content.offset_bottom = -64
	edit_content.add_theme_constant_override("separation", 8)
	_manifest_edit_dialog.add_child(edit_content)
	var id_label := Label.new()
	id_label.text = "Id"
	edit_content.add_child(id_label)
	_manifest_edit_id_input = LineEdit.new()
	_manifest_edit_id_input.placeholder_text = "例如：ui/main_menu"
	_manifest_edit_id_input.select_all_on_focus = true
	edit_content.add_child(_manifest_edit_id_input)
	var locator_label := Label.new()
	locator_label.text = "Locator"
	edit_content.add_child(locator_label)
	_manifest_edit_locator_input = LineEdit.new()
	_manifest_edit_locator_input.placeholder_text = "res:// 或 uid://"
	_manifest_edit_locator_input.select_all_on_focus = true
	edit_content.add_child(_manifest_edit_locator_input)
	_manifest_edit_dialog.confirmed.connect(_on_manifest_edit_confirmed)
	_manifest_manage_dialog.add_child(_manifest_edit_dialog)

	_manifest_uid_confirm_dialog = ConfirmationDialog.new()
	_manifest_uid_confirm_dialog.exclusive = true
	_manifest_uid_confirm_dialog.transient_to_focused = true
	_manifest_uid_confirm_dialog.title = "生成并使用 UID"
	_manifest_uid_confirm_dialog.ok_button_text = "确认生成"
	_manifest_uid_confirm_dialog.cancel_button_text = "取消"
	_manifest_uid_confirm_dialog.confirmed.connect(_on_manifest_uid_confirmed)
	_manifest_manage_dialog.add_child(_manifest_uid_confirm_dialog)

	_manifest_report_dialog = AcceptDialog.new()
	_manifest_report_dialog.name = "GoDoManifestReportDialog"
	_manifest_report_dialog.title = "资源清单校验"
	_manifest_report_dialog.ok_button_text = "关闭"
	_manifest_report_dialog.min_size = Vector2i(720, 420)
	_manifest_report_dialog.exclusive = true
	_manifest_report_dialog.transient_to_focused = true
	_manifest_report_dialog.get_label().hide()
	_manifest_report_label = RichTextLabel.new()
	_manifest_report_label.name = "ManifestReportLabel"
	_manifest_report_label.bbcode_enabled = false
	_manifest_report_label.selection_enabled = true
	_manifest_report_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_manifest_report_dialog.add_child(_manifest_report_label)
	_manifest_report_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_manifest_report_label.offset_left = 16
	_manifest_report_label.offset_top = 16
	_manifest_report_label.offset_right = -16
	_manifest_report_label.offset_bottom = -48
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_report_dialog)
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
	for dialog in [_manifest_file_dialog, _manifest_selector_dialog, _resource_file_dialog, _manifest_add_confirm_dialog, _manifest_manage_dialog, _manifest_remove_confirm_dialog, _manifest_edit_dialog, _manifest_uid_confirm_dialog, _manifest_report_dialog]:
		if is_instance_valid(dialog):
			dialog.queue_free()

func open_validate_dialog() -> void:
	_open_existing_manifest(MANIFEST_ACTION_VALIDATE)

func open_create_dialog() -> void:
	_open_manifest_create_dialog()

func open_manage_dialog() -> void:
	_open_existing_manifest(MANIFEST_ACTION_MANAGE)

func open_add_selected_resource_dialog() -> void:
	_pending_add_target_manifest_path = ""
	_open_add_selected_resource_dialog()


func find_manifest_paths() -> PackedStringArray:
	return _prepare_manifest_paths(_find_resource_manifest_paths("res://"))


func open_manage_path(path: String) -> void:
	_show_manifest_manager(path)


func open_validate_path(path: String) -> void:
	var report := _validate_manifest(path)
	_render_manifest_report(path, report)
	_manifest_report_dialog.popup_centered(Vector2i(720, 420))

func _create_manifest_selector_dialog(editor_root: Control) -> void:
	_manifest_selector_dialog = ConfirmationDialog.new()
	_manifest_selector_dialog.exclusive = true
	_manifest_selector_dialog.transient_to_focused = true
	_manifest_selector_dialog.name = "ManifestSelectorDialog"
	_manifest_selector_dialog.title = "选择资源清单"
	_manifest_selector_dialog.ok_button_text = "打开"
	_manifest_selector_dialog.cancel_button_text = "取消"
	_manifest_selector_dialog.min_size = Vector2i(760, 460)
	_manifest_selector_dialog.get_label().hide()

	var content := VBoxContainer.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	content.offset_left = 16
	content.offset_top = 16
	content.offset_right = -16
	content.offset_bottom = -64
	content.add_theme_constant_override("separation", 8)
	_manifest_selector_dialog.add_child(content)

	_manifest_selector_label = Label.new()
	content.add_child(_manifest_selector_label)
	_manifest_selector_tree = Tree.new()
	_manifest_selector_tree.name = "ManifestSelectorTree"
	_manifest_selector_tree.columns = 1
	_manifest_selector_tree.column_titles_visible = true
	_manifest_selector_tree.hide_root = true
	_manifest_selector_tree.select_mode = Tree.SELECT_ROW
	_manifest_selector_tree.set_column_title(0, "ResourceManifest Resource")
	_manifest_selector_tree.item_selected.connect(_on_manifest_selector_item_selected)
	_manifest_selector_tree.item_activated.connect(_on_manifest_selector_item_activated)
	_manifest_selector_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_child(_manifest_selector_tree)
	var action_row := HBoxContainer.new()
	action_row.add_theme_constant_override("separation", 8)
	content.add_child(action_row)
	_manifest_selector_create_button = Button.new()
	_manifest_selector_create_button.name = "ManifestSelectorCreateButton"
	_manifest_selector_create_button.text = "创建资源清单..."
	_manifest_selector_create_button.pressed.connect(_on_manifest_selector_create_pressed)
	action_row.add_child(_manifest_selector_create_button)
	_manifest_selector_manual_button = Button.new()
	_manifest_selector_manual_button.name = "ManifestSelectorManualButton"
	_manifest_selector_manual_button.text = "手动选择其他资源清单..."
	_manifest_selector_manual_button.pressed.connect(_on_manifest_selector_manual_pressed)
	action_row.add_child(_manifest_selector_manual_button)
	_manifest_selector_dialog.confirmed.connect(_on_manifest_selector_confirmed)
	editor_root.add_child(_manifest_selector_dialog)


func set_window_parent(window_parent: Window) -> void:
	_manifest_file_dialog.reparent(window_parent)
	_resource_file_dialog.reparent(window_parent)
	_manifest_add_confirm_dialog.reparent(window_parent)
	_manifest_manage_dialog.reparent(window_parent)
	_manifest_report_dialog.reparent(window_parent)
	_manifest_selector_dialog.reparent(window_parent)


func _open_existing_manifest(action: String) -> void:
	var manifest_paths := _prepare_manifest_paths(_find_resource_manifest_paths("res://"))
	_show_manifest_selector(action, manifest_paths)


func _open_manifest_file_selector(action: String, title: String) -> void:
	if not is_instance_valid(_manifest_file_dialog):
		return
	_manifest_action = action
	_manifest_file_dialog.title = title
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.get_ok_button().text = "选择"
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.popup_centered(Vector2i(720, 480))


func _prepare_manifest_paths(manifest_paths: PackedStringArray) -> PackedStringArray:
	var unique_paths := {}
	for path in manifest_paths:
		var normalized_path := path.strip_edges()
		if not normalized_path.is_empty():
			unique_paths[normalized_path] = true
	var prepared := PackedStringArray()
	for path in unique_paths:
		prepared.append(path)
	prepared.sort()
	return prepared


func _show_manifest_selector(action: String, manifest_paths: PackedStringArray) -> void:
	if not is_instance_valid(_manifest_selector_dialog):
		return
	_manifest_action = action
	_manifest_selector_dialog.title = "选择要管理的资源清单" if action == MANIFEST_ACTION_MANAGE else "选择要校验的资源清单" if action == MANIFEST_ACTION_VALIDATE else "选择目标资源清单"
	_manifest_selector_dialog.ok_button_text = "管理" if action == MANIFEST_ACTION_MANAGE else "校验" if action == MANIFEST_ACTION_VALIDATE else "添加到此清单"
	_manifest_selector_label.text = (
		"项目内没有 ResourceManifest，请创建清单或手动选择。"
		if manifest_paths.is_empty()
		else "项目内发现 %d 份 ResourceManifest，请明确选择目标。" % manifest_paths.size())
	_manifest_selector_tree.clear()
	var root := _manifest_selector_tree.create_item()
	if manifest_paths.is_empty():
		var empty_item := _manifest_selector_tree.create_item(root)
		empty_item.set_text(0, "当前没有资源清单")
		empty_item.set_selectable(0, false)
	else:
		for path in manifest_paths:
			var item := _manifest_selector_tree.create_item(root)
			item.set_text(0, path)
			item.set_tooltip_text(0, path)
			item.set_metadata(0, path)
	_manifest_selector_dialog.get_ok_button().disabled = true
	_manifest_selector_dialog.popup_centered(Vector2i(760, 460))


func _on_manifest_selector_item_selected() -> void:
	_manifest_selector_dialog.get_ok_button().disabled = _get_selected_manifest_path().is_empty()


func _on_manifest_selector_item_activated() -> void:
	var path := _get_selected_manifest_path()
	if path.is_empty():
		return
	_manifest_selector_dialog.hide()
	_dispatch_manifest_action(_manifest_action, path)


func _on_manifest_selector_confirmed() -> void:
	var path := _get_selected_manifest_path()
	if not path.is_empty():
		_dispatch_manifest_action(_manifest_action, path)


func _on_manifest_selector_manual_pressed() -> void:
	var action := _manifest_action
	_manifest_selector_dialog.hide()
	call_deferred("_open_manifest_file_selector", action, "手动选择资源清单")


func _on_manifest_selector_create_pressed() -> void:
	_manifest_return_action_after_create = _manifest_action
	_manifest_selector_dialog.hide()
	call_deferred("_open_manifest_create_dialog")


func _on_manifest_file_dialog_canceled() -> void:
	manifest_paths_changed.emit("")
	if _manifest_action != MANIFEST_ACTION_CREATE:
		return
	_manifest_return_action_after_create = ""


func _get_selected_manifest_path() -> String:
	var item := _manifest_selector_tree.get_selected()
	if item == null:
		return ""
	var path = item.get_metadata(0)
	return str(path) if path != null else ""


func _dispatch_manifest_action(action: String, path: String) -> void:
	match action:
		MANIFEST_ACTION_MANAGE:
			call_deferred("_show_manifest_manager", path)
		MANIFEST_ACTION_ADD_SELECTED:
			_preview_manifest_add(path)
		_:
			var report := _validate_manifest(path)
			_render_manifest_report(path, report)
			_manifest_report_dialog.popup_centered(Vector2i(720, 420))


func _open_manifest_create_dialog() -> void:
	if not is_instance_valid(_manifest_file_dialog):
		return
	_manifest_action = MANIFEST_ACTION_CREATE
	_manifest_file_dialog.title = "创建资源清单"
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.get_ok_button().text = "保存"
	_manifest_file_dialog.current_path = "res://ResourceManifest.tres"
	_manifest_file_dialog.popup_centered(Vector2i(720, 480))


func _open_add_selected_resource_dialog() -> void:
	if not is_instance_valid(_resource_file_dialog):
		return
	_pending_resource_paths = PackedStringArray()
	_resource_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILES
	_resource_file_dialog.title = "选择要添加的资源"
	_resource_file_dialog.get_ok_button().text = "添加"
	_resource_file_dialog.current_path = "res://"
	_resource_file_dialog.popup_centered(Vector2i(720, 480))


func _on_resource_files_selected(resource_paths: PackedStringArray) -> void:
	var rejection_reasons := PackedStringArray()
	for resource_path in resource_paths:
		var rejection_reason := _get_resource_add_rejection_reason(resource_path)
		if not rejection_reason.is_empty():
			rejection_reasons.append(rejection_reason)
	if not rejection_reasons.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "添加资源失败", "\n".join(rejection_reasons))
		return

	_pending_resource_paths = resource_paths
	var target_manifest_path := _pending_add_target_manifest_path
	_pending_add_target_manifest_path = ""
	if target_manifest_path.is_empty():
		_select_target_manifest()
	else:
		_preview_manifest_add(target_manifest_path)


func _select_target_manifest() -> void:
	var manifest_paths := _prepare_manifest_paths(_find_resource_manifest_paths("res://"))
	if manifest_paths.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "项目内没有 ResourceManifest，请先创建资源清单")
		return
	if manifest_paths.size() == 1:
		_preview_manifest_add(manifest_paths[0])
		return
	_show_manifest_selector(MANIFEST_ACTION_ADD_SELECTED, manifest_paths)


func _on_manifest_file_selected(path: String) -> void:
	match _manifest_action:
		MANIFEST_ACTION_CREATE:
			_create_manifest(path)
		MANIFEST_ACTION_ADD_SELECTED:
			_preview_manifest_add(path)
		MANIFEST_ACTION_MANAGE:
			call_deferred("_show_manifest_manager", path)
		_:
			var report := _validate_manifest(path)
			_render_manifest_report(path, report)
			_manifest_report_dialog.popup_centered(Vector2i(720, 420))


func _create_manifest(path: String) -> void:
	var return_action := _manifest_return_action_after_create
	_manifest_return_action_after_create = ""
	var save_path := _normalize_manifest_save_path(path)
	if FileAccess.file_exists(save_path):
		_show_manifest_message(
			HealthLevel.ERROR,
			"创建失败",
			"目标文件已经存在，不会覆盖：\n%s" % save_path)
		return
	var manifest := _create_manifest_instance()
	if manifest == null:
		_show_manifest_message(HealthLevel.ERROR, "创建失败", "ResourceManifest 脚本无法加载或实例化。\n%s" % _csharp_resource_load_error)
		return

	var save_error := ResourceSaver.save(manifest, save_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_manifest_message(HealthLevel.ERROR, "创建失败", "%s：%s" % [save_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	manifest_paths_changed.emit(save_path)
	if return_action.is_empty() or return_action == MANIFEST_ACTION_MANAGE:
		call_deferred("_show_manifest_manager", save_path)
	else:
		call_deferred("_dispatch_manifest_action", return_action, save_path)


func _add_selected_resources_to_manifest(manifest_path: String) -> void:
	if _pending_resource_paths.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "没有待添加的资源路径")
		return

	var manifest := _load_manifest_from_disk(manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "请选择 ResourceManifest 资源：%s" % manifest_path)
		return
	var entries = _copy_manifest_entries(_get_manifest_entries(manifest))
	if entries == null:
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "ResourceManifest 缺少有效的 Entries 数组")
		return
	var entry_ids := _get_pending_entry_ids(entries)
	if entry_ids.is_empty():
		return
	var locators := PackedStringArray()
	for resource_path in _pending_resource_paths:
		var locator := _ensure_resource_uid(resource_path)
		if locator.is_empty():
			_show_manifest_message(HealthLevel.ERROR, "添加失败", "无法为资源生成 UID，未写入资源清单。\n%s" % _uid_generation_error)
			return
		locators.append(locator)

	var new_entries: Array[Resource] = []
	for index in range(_pending_resource_paths.size()):
		var entry := _create_manifest_entry_instance()
		if entry == null:
			_show_manifest_message(HealthLevel.ERROR, "添加失败", "ResourceManifestEntry 脚本无法加载或实例化。\n%s" % _csharp_resource_load_error)
			return
		entry.set("Id", entry_ids[index])
		entry.set("Locator", locators[index])
		new_entries.append(entry)

	for entry in new_entries:
		entries.append(entry)
	manifest.set("Entries", entries)

	var saved_manifest := _save_manifest_and_reload(manifest, manifest_path)
	if saved_manifest == null:
		_show_manifest_message(HealthLevel.ERROR, "添加失败", _manifest_persistence_error)
		return

	_refresh_editor_filesystem()
	var success_message := "已添加 %d 个资源到：%s\n\n%s" % [
		_pending_resource_paths.size(),
		manifest_path,
		"\n".join(_pending_resource_paths),
	]
	if _managed_manifest_path == manifest_path:
		_render_manifest_entries(saved_manifest)
	_show_manifest_message(HealthLevel.NORMAL, "添加成功", success_message)
	_pending_resource_paths = PackedStringArray()
	_pending_manifest_path = ""


func _preview_manifest_add(manifest_path: String) -> void:
	if _pending_resource_paths.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "没有待添加的资源路径")
		return
	if _pending_resource_paths.has(manifest_path):
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "不能将目标 ResourceManifest 添加到自身")
		return

	var manifest := _load_manifest_from_disk(manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "请选择 ResourceManifest 资源：%s" % manifest_path)
		return
	var entry_ids := _get_pending_entry_ids(_get_manifest_entries(manifest))
	if entry_ids.is_empty():
		return

	_pending_manifest_path = manifest_path
	var preview_lines := PackedStringArray()
	for index in range(min(_pending_resource_paths.size(), MANIFEST_ADD_PREVIEW_LIMIT)):
		preview_lines.append("%s\nId：%s\nLocator：%s" % [
			_pending_resource_paths[index],
			entry_ids[index],
			_locator_for_resource(_pending_resource_paths[index]),
		])
	if _pending_resource_paths.size() > MANIFEST_ADD_PREVIEW_LIMIT:
		preview_lines.append("还有 %d 个资源将在确认后添加。" % [_pending_resource_paths.size() - MANIFEST_ADD_PREVIEW_LIMIT])
	var missing_uid_paths := _get_resources_without_uid(_pending_resource_paths)
	var uid_notice := ""
	if not missing_uid_paths.is_empty():
		uid_notice = "\n\n以下 %d 个资源当前没有 UID。确认后会生成 UID 并更新 Godot 的 UID 记录：\n%s" % [
			missing_uid_paths.size(),
			"\n".join(missing_uid_paths),
		]
	_manifest_add_confirm_dialog.dialog_text = "以下内容尚未写入。确认“确认添加”后才会保存。\n\n将添加 %d 个资源到：%s\n\n%s%s" % [
		_pending_resource_paths.size(),
		manifest_path,
		"\n\n".join(preview_lines),
		uid_notice,
	]
	_manifest_add_confirm_dialog.popup_centered(Vector2i(720, 420))


func _on_manifest_add_confirmed() -> void:
	_add_selected_resources_to_manifest(_pending_manifest_path)


func _on_manifest_add_canceled() -> void:
	var manifest_path := _pending_manifest_path
	_pending_resource_paths = PackedStringArray()
	_pending_manifest_path = ""
	call_deferred(
		"_show_manifest_message",
		HealthLevel.NORMAL,
		"已取消添加",
		"未向 %s 写入任何资源。" % manifest_path
	)


func _show_manifest_manager(manifest_path: String) -> void:
	var manifest := ResourceLoader.load(
		manifest_path,
		"",
		ResourceLoader.CACHE_MODE_REPLACE)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "打开失败", "请选择 ResourceManifest 资源：%s" % manifest_path)
		return

	_managed_manifest_path = manifest_path
	_manifest_manage_dialog.title = "资源清单管理"
	_managed_manifest_label.text = "当前：%s" % manifest_path
	_manifest_locate_button.disabled = false
	_manifest_add_resource_button.disabled = false
	_manifest_validate_button.disabled = false
	_manifest_search_input.clear()
	_render_manifest_entries(manifest)
	_manifest_manage_dialog.popup_centered(Vector2i(1100, 520))


func _on_manifest_add_resource_pressed() -> void:
	if _managed_manifest_path.is_empty():
		return
	_pending_add_target_manifest_path = _managed_manifest_path
	_open_add_selected_resource_dialog()


func _on_manifest_validate_pressed() -> void:
	if _managed_manifest_path.is_empty():
		return
	var report := _validate_manifest(_managed_manifest_path)
	_render_manifest_report(_managed_manifest_path, report)
	_manifest_report_dialog.popup_centered(Vector2i(720, 420))


func _on_manifest_locate_pressed() -> void:
	_locate_in_file_system(_managed_manifest_path)


func _on_manifest_search_changed(_text: String) -> void:
	if _managed_manifest_path.is_empty():
		return
	if not ResourceLoader.exists(_managed_manifest_path):
		_render_missing_managed_manifest(_managed_manifest_path)
		return
	var manifest := ResourceLoader.load(
		_managed_manifest_path,
		"",
		ResourceLoader.CACHE_MODE_REPLACE)
	if _is_resource_manifest(manifest):
		_render_manifest_entries(manifest)


func _render_manifest_entries(manifest: Resource) -> void:
	_manifest_entries_tree.clear()
	_managed_entry_index = -1
	_manifest_edit_button.disabled = true
	_manifest_uid_button.disabled = true
	_manifest_remove_button.disabled = true

	var entries = _get_manifest_entries(manifest)
	var root := _manifest_entries_tree.create_item()
	if entries.is_empty():
		var empty_item := _manifest_entries_tree.create_item(root)
		empty_item.set_text(0, "当前资源清单没有条目")
		empty_item.set_selectable(0, false)
		empty_item.set_selectable(1, false)
		empty_item.set_selectable(2, false)
		return
	var filter_text := (
		_manifest_search_input.text.strip_edges().to_lower()
		if is_instance_valid(_manifest_search_input)
		else "")
	var visible_count := 0
	for index in range(entries.size()):
		var entry = entries[index]
		var entry_id := _get_exported_string(entry, "Id", "id")
		var locator := _get_exported_string(entry, "Locator", "locator")
		var display_locator := _display_locator(locator)
		if (
			not filter_text.is_empty()
			and not entry_id.to_lower().contains(filter_text)
			and not locator.to_lower().contains(filter_text)
			and not display_locator.to_lower().contains(filter_text)
		):
			continue
		visible_count += 1
		var uid_status := _get_uid_status(locator)
		var item := _manifest_entries_tree.create_item(root)
		item.set_text(0, entry_id)
		item.set_text(1, display_locator)
		item.set_text(2, uid_status.text)
		item.set_custom_color(2, uid_status.color)
		item.set_tooltip_text(0, entry_id)
		item.set_tooltip_text(1, "显示路径：%s\n实际定位：%s" % [display_locator, locator])
		item.set_tooltip_text(2, uid_status.tooltip)
		item.set_metadata(0, index)
	if visible_count == 0:
		var empty_item := _manifest_entries_tree.create_item(root)
		empty_item.set_text(0, "没有匹配的资源清单条目")
		for column in range(3):
			empty_item.set_selectable(column, false)


func _on_manifest_entry_selected() -> void:
	var item := _manifest_entries_tree.get_selected()
	if item == null:
		return
	_managed_entry_index = int(item.get_metadata(0))
	_manifest_edit_button.disabled = false
	_manifest_uid_button.disabled = not _selected_entry_uses_path_locator()
	_manifest_remove_button.disabled = false


func _on_manifest_entry_activated() -> void:
	var item := _manifest_entries_tree.get_selected()
	if item == null:
		return
	_managed_entry_index = int(item.get_metadata(0))
	_on_manifest_edit_pressed()


func _on_manifest_edit_pressed() -> void:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return

	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "当前 ResourceManifest 无法重新加载：%s" % _managed_manifest_path)
		return
	var entries = _get_manifest_entries(manifest)
	if _managed_entry_index >= entries.size():
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "当前选中条目已不存在")
		return

	var entry = entries[_managed_entry_index]
	_manifest_edit_id_input.text = _get_exported_string(entry, "Id", "id")
	_manifest_edit_locator_input.text = _get_exported_string(entry, "Locator", "locator")
	_manifest_edit_dialog.popup_centered(Vector2i(680, 280))
	_manifest_edit_id_input.grab_focus()
	_manifest_edit_id_input.select_all()


func _on_manifest_uid_pressed() -> void:
	if not _selected_entry_uses_path_locator():
		return

	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	var entries = _get_manifest_entries(manifest)
	var entry = entries[_managed_entry_index]
	var entry_id := _get_exported_string(entry, "Id", "id")
	var locator := _get_exported_string(entry, "Locator", "locator")
	_manifest_uid_confirm_dialog.dialog_text = "将为以下资源生成并使用 UID。确认后会更新 Godot 的 UID 记录，并将此清单条目的 Locator 改为 uid://。\n\nId：%s\n资源：%s" % [entry_id, locator]
	_manifest_uid_confirm_dialog.popup_centered(Vector2i(680, 260))


func _on_manifest_uid_confirmed() -> void:
	if not _selected_entry_uses_path_locator():
		return

	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	var entries = _get_manifest_entries(manifest)
	var entry = entries[_managed_entry_index]
	var resource_path := _get_exported_string(entry, "Locator", "locator")
	var uid_locator := _ensure_resource_uid(resource_path)
	if uid_locator.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "生成 UID 失败", "%s\n\n资源清单未修改。" % _uid_generation_error)
		return

	var updated_entries = _copy_manifest_entries(entries)
	var updated_entry: Resource = entry.duplicate()
	updated_entry.set("Locator", uid_locator)
	updated_entries[_managed_entry_index] = updated_entry
	manifest.set("Entries", updated_entries)
	var saved_manifest := _save_manifest_and_reload(manifest, _managed_manifest_path)
	if saved_manifest == null:
		_show_manifest_message(HealthLevel.ERROR, "生成 UID 失败", _manifest_persistence_error)
		return

	_refresh_editor_filesystem()
	_render_manifest_entries(saved_manifest)


func _on_manifest_edit_confirmed() -> void:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return

	var entry_id := _manifest_edit_id_input.text.strip_edges()
	var locator := _manifest_edit_locator_input.text.strip_edges()
	if entry_id.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "Id 不能为空")
		return
	if not (locator.begins_with("res://") or locator.begins_with("uid://")):
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "Locator 必须以 res:// 或 uid:// 开头：%s" % locator)
		return
	if not ResourceLoader.exists(locator):
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "当前 Locator 无法解析到资源：%s" % locator)
		return

	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "当前 ResourceManifest 无法重新加载：%s" % _managed_manifest_path)
		return
	var entries = _get_manifest_entries(manifest)
	if _managed_entry_index >= entries.size():
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "当前选中条目已不存在")
		return
	for index in range(entries.size()):
		if index == _managed_entry_index:
			continue
		if _get_exported_string(entries[index], "Id", "id") == entry_id:
			_show_manifest_message(HealthLevel.ERROR, "编辑失败", "清单中已存在 Id：%s" % entry_id)
			return

	var updated_entries = _copy_manifest_entries(entries)
	var updated_entry: Resource = entries[_managed_entry_index].duplicate()
	updated_entry.set("Id", entry_id)
	updated_entry.set("Locator", locator)
	updated_entries[_managed_entry_index] = updated_entry
	manifest.set("Entries", updated_entries)
	var saved_manifest := _save_manifest_and_reload(manifest, _managed_manifest_path)
	if saved_manifest == null:
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", _manifest_persistence_error)
		return

	_refresh_editor_filesystem()
	_render_manifest_entries(saved_manifest)


func _on_manifest_remove_pressed() -> void:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return

	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "当前 ResourceManifest 无法重新加载：%s" % _managed_manifest_path)
		return
	var entries = _get_manifest_entries(manifest)
	if _managed_entry_index >= entries.size():
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "当前选中条目已不存在")
		return

	var entry = entries[_managed_entry_index]
	var entry_id := _get_exported_string(entry, "Id", "id")
	var locator := _get_exported_string(entry, "Locator", "locator")
	_manifest_remove_confirm_dialog.dialog_text = "仅从清单移除以下映射，不会删除资源文件。\n\nId：%s\nLocator：%s" % [entry_id, locator]
	_manifest_remove_confirm_dialog.popup_centered(Vector2i(640, 260))


func _on_manifest_remove_confirmed() -> void:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return

	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "当前 ResourceManifest 无法重新加载：%s" % _managed_manifest_path)
		return
	var entries = _get_manifest_entries(manifest)
	if _managed_entry_index >= entries.size():
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "当前选中条目已不存在")
		return

	var updated_entries = _copy_manifest_entries(entries)
	updated_entries.remove_at(_managed_entry_index)
	manifest.set("Entries", updated_entries)
	var saved_manifest := _save_manifest_and_reload(manifest, _managed_manifest_path)
	if saved_manifest == null:
		_show_manifest_message(HealthLevel.ERROR, "删除失败", _manifest_persistence_error)
		return

	_refresh_editor_filesystem()
	_render_manifest_entries(saved_manifest)


func _validate_manifest(path: String) -> Dictionary:
	var report := {
		"items": [],
		"level": HealthLevel.NORMAL,
		"entry_count": 0,
	}
	var resource := _load_manifest_from_disk(path)
	if resource == null:
		_add_item(report, HealthLevel.ERROR, "清单加载", "无法加载 %s" % path)
		return report

	var entries = _get_manifest_entries(resource)
	if entries == null or not (entries is Array):
		_add_item(report, HealthLevel.ERROR, "清单类型", "缺少 Entries 数组，请选择 ResourceManifest 资源")
		return report

	report.entry_count = entries.size()
	if entries.is_empty():
		_add_item(report, HealthLevel.WARNING, "清单内容", "Entries 为空")
		return report

	var seen_ids := {}
	for index in range(entries.size()):
		var entry = entries[index]
		if entry == null or not (entry is Object):
			_add_item(report, HealthLevel.ERROR, "Entry %d" % index, "条目为空或不是 ResourceManifestEntry")
			continue

		var entry_id := _get_exported_string(entry, "Id", "id").strip_edges()
		var locator := _get_exported_string(entry, "Locator", "locator").strip_edges()
		var label := "Entry %d" % index
		if not entry_id.is_empty():
			label = "%s (%s)" % [label, entry_id]

		if entry_id.is_empty():
			_add_item(report, HealthLevel.ERROR, label, "Id 为空")
		elif seen_ids.has(entry_id):
			_add_item(report, HealthLevel.ERROR, label, "Id 与 Entry %d 重复" % seen_ids[entry_id])
		else:
			seen_ids[entry_id] = index

		if locator.is_empty():
			_add_item(report, HealthLevel.ERROR, label, "Locator 为空")
		elif not (locator.begins_with("res://") or locator.begins_with("uid://")):
			_add_item(report, HealthLevel.ERROR, label, "Locator 必须以 res:// 或 uid:// 开头：%s" % locator)
		elif not ResourceLoader.exists(locator):
			_add_item(report, HealthLevel.WARNING, label, "Locator 当前无法解析到资源：%s" % locator)

	if report.level == HealthLevel.NORMAL:
		_add_item(report, HealthLevel.NORMAL, "清单内容", "%d 个条目通过校验" % report.entry_count)
	return report


func _get_exported_string(target: Object, primary_name: String, fallback_name: String) -> String:
	var value = target.get(primary_name)
	if value == null:
		value = target.get(fallback_name)
	return str(value) if value != null else ""


func _get_manifest_entries(manifest: Resource):
	if manifest == null:
		return null
	var entries = manifest.get("Entries")
	if entries == null:
		entries = manifest.get("entries")
	return entries


func _copy_manifest_entries(entries):
	if entries == null or not (entries is Array):
		return null
	return entries.duplicate()


func _load_manifest_from_disk(path: String) -> Resource:
	return ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)


func _save_manifest_and_reload(manifest: Resource, path: String) -> Resource:
	_manifest_persistence_error = ""
	var expected_signature := _manifest_entries_signature(_get_manifest_entries(manifest))
	manifest.emit_changed()
	var save_error := ResourceSaver.save(manifest, path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_manifest_persistence_error = "%s：%s" % [path, error_string(save_error)]
		return null
	var reloaded := _load_manifest_from_disk(path)
	if not _is_resource_manifest(reloaded):
		_manifest_persistence_error = "保存后无法从磁盘重新加载 ResourceManifest：%s" % path
		return null
	if _manifest_entries_signature(_get_manifest_entries(reloaded)) != expected_signature:
		_manifest_persistence_error = "保存后校验失败，磁盘中的 Entries 与待保存内容不一致：%s" % path
		return null
	return reloaded


func _manifest_entries_signature(entries) -> PackedStringArray:
	var signature := PackedStringArray()
	if entries == null or not (entries is Array):
		return signature
	for entry in entries:
		if entry == null or not (entry is Object):
			signature.append("<null>")
			continue
		signature.append("%s\u001f%s" % [
			_get_exported_string(entry, "Id", "id"),
			_get_exported_string(entry, "Locator", "locator"),
		])
	return signature


func _manifest_file_filters() -> PackedStringArray:
	return PackedStringArray(["*.tres,*.res;Resource files"])


func _addable_resource_filters() -> PackedStringArray:
	return PackedStringArray([
		"*.tscn;Scenes",
		"*.tres,*.res;Resources",
		"*.png,*.jpg,*.jpeg,*.webp,*.svg;Textures",
		"*.wav,*.ogg,*.mp3;Audio",
		"*.ttf,*.otf;Fonts",
		"*.glb,*.gltf;3D scenes",
	])


func _get_resource_add_rejection_reason(resource_path: String) -> String:
	if not _is_project_file(resource_path):
		return "只能添加 res:// 项目目录内的资源：%s" % resource_path
	if not ResourceLoader.exists(resource_path):
		return "当前路径无法被 ResourceLoader 解析：%s" % resource_path

	var resource := ResourceLoader.load(resource_path)
	if resource == null:
		return "无法加载资源：%s" % resource_path
	if resource is Script:
		return "不能将脚本资源添加到 ResourceManifest：%s" % resource_path
	if _is_resource_manifest(resource):
		return "不能将 ResourceManifest 添加到其他清单：%s" % resource_path
	return ""


func _is_resource_manifest(resource: Resource) -> bool:
	if resource == null:
		return false
	var script := resource.get_script() as Script
	return script != null and script.resource_path == RESOURCE_MANIFEST_SCRIPT_PATH


func _find_resource_manifest_paths(path: String) -> PackedStringArray:
	var manifest_paths := PackedStringArray()
	for file_name in DirAccess.get_files_at(path):
		var extension := file_name.get_extension().to_lower()
		if extension != "tres" and extension != "res":
			continue
		var resource_path := path.path_join(file_name)
		if _is_resource_manifest(ResourceLoader.load(resource_path)):
			manifest_paths.append(resource_path)
	for directory_name in DirAccess.get_directories_at(path):
		if _should_skip_resource_manifest_directory(path, directory_name):
			continue
		manifest_paths.append_array(_find_resource_manifest_paths(path.path_join(directory_name)))
	return manifest_paths


func _should_skip_resource_manifest_directory(path: String, directory_name: String) -> bool:
	if directory_name.begins_with("."):
		return true
	var directory_path := path.path_join(directory_name)
	return FileAccess.file_exists(directory_path.path_join("project.godot"))


func _get_pending_entry_ids(entries: Array) -> PackedStringArray:
	var entry_ids := PackedStringArray()
	for resource_path in _pending_resource_paths:
		var rejection_reason := _get_resource_add_rejection_reason(resource_path)
		if not rejection_reason.is_empty():
			_show_manifest_message(HealthLevel.ERROR, "添加失败", rejection_reason)
			return PackedStringArray()

		var entry_id := _default_resource_id(resource_path)
		if _manifest_contains_id(entries, entry_id) or entry_ids.has(entry_id):
			_show_manifest_message(HealthLevel.ERROR, "添加失败", "清单或本次选择中已存在 Id：%s" % entry_id)
			return PackedStringArray()
		entry_ids.append(entry_id)
	return entry_ids


func _create_manifest_instance() -> Resource:
	return _instantiate_csharp_resource(RESOURCE_MANIFEST_SCRIPT_PATH)


func _create_manifest_entry_instance() -> Resource:
	return _instantiate_csharp_resource(RESOURCE_MANIFEST_ENTRY_SCRIPT_PATH)


func _instantiate_csharp_resource(script_path: String) -> Resource:
	_csharp_resource_load_error = ""
	var loaded_resource := ResourceLoader.load(script_path)
	var script := loaded_resource as Script
	if script == null:
		_csharp_resource_load_error = "无法将 %s 加载为 Script：%s" % [script_path, loaded_resource]
		return null

	var instance: Variant = script.new()
	if not (instance is Resource):
		_csharp_resource_load_error = "Script.new() 未返回 Resource：%s" % instance
		return null
	return instance as Resource


func _manifest_contains_id(entries: Array, entry_id: String) -> bool:
	for entry in entries:
		if entry == null or not (entry is Object):
			continue
		if _get_exported_string(entry, "Id", "id") == entry_id:
			return true
	return false


func _selected_entry_uses_path_locator() -> bool:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return false
	var manifest := _load_manifest_from_disk(_managed_manifest_path)
	if not _is_resource_manifest(manifest):
		return false
	var entries = _get_manifest_entries(manifest)
	if entries == null or _managed_entry_index >= entries.size():
		return false
	var locator := _get_exported_string(entries[_managed_entry_index], "Locator", "locator")
	return locator.begins_with("res://") and ResourceLoader.exists(locator)


func _default_resource_id(resource_path: String) -> String:
	return resource_path.trim_prefix("res://").trim_suffix(".%s" % resource_path.get_extension())


func _locator_for_resource(resource_path: String) -> String:
	var uid := ResourceLoader.get_resource_uid(resource_path)
	if uid != ResourceUID.INVALID_ID:
		return ResourceUID.id_to_text(uid)
	return resource_path


func _get_uid_status(locator: String) -> Dictionary:
	if locator.begins_with("uid://"):
		if ResourceLoader.exists(locator):
			return {
				"text": "Using UID",
				"color": NORMAL_COLOR,
				"tooltip": "清单当前通过 uid:// 定位该资源。",
			}
		return {
			"text": "Invalid UID",
			"color": ERROR_COLOR,
			"tooltip": "清单使用 uid://，但当前无法解析该资源。",
		}
	if locator.begins_with("res://") and ResourceLoader.exists(locator):
		if ResourceLoader.get_resource_uid(locator) != ResourceUID.INVALID_ID:
			return {
				"text": "Convertible",
				"color": WARNING_COLOR,
				"tooltip": "资源已有 UID；点击“生成并使用 UID”可更新清单定位。",
			}
		return {
			"text": "Missing UID",
			"color": PENDING_COLOR,
			"tooltip": "资源当前没有可用 UID；点击“生成并使用 UID”可创建。",
		}
	return {
		"text": "Unresolved",
		"color": ERROR_COLOR,
		"tooltip": "当前定位无法解析资源。",
	}


func _get_resources_without_uid(resource_paths: PackedStringArray) -> PackedStringArray:
	var missing_uid_paths := PackedStringArray()
	for resource_path in resource_paths:
		if ResourceLoader.get_resource_uid(resource_path) == ResourceUID.INVALID_ID:
			missing_uid_paths.append(resource_path)
	return missing_uid_paths


func _ensure_resource_uid(resource_path: String) -> String:
	_uid_generation_error = ""
	if not _is_project_file(resource_path) or not ResourceLoader.exists(resource_path):
		_uid_generation_error = "资源路径无效：%s" % resource_path
		return ""

	var uid := ResourceLoader.get_resource_uid(resource_path)
	if uid == ResourceUID.INVALID_ID:
		uid = ResourceSaver.get_resource_id_for_path(resource_path, true)
		if uid == ResourceUID.INVALID_ID:
			_uid_generation_error = "Godot 未能为资源分配 UID：%s" % resource_path
			return ""
		var set_uid_error := ResourceSaver.set_uid(resource_path, uid)
		if set_uid_error != OK:
			_uid_generation_error = "无法写入资源 UID：%s（%s）" % [resource_path, error_string(set_uid_error)]
			return ""

	if ResourceUID.has_id(uid):
		ResourceUID.set_id(uid, resource_path)
	else:
		ResourceUID.add_id(uid, resource_path)

	var locator := ResourceUID.id_to_text(uid)
	if locator.is_empty() or not ResourceLoader.exists(locator):
		_uid_generation_error = "生成后的 UID 无法解析资源：%s" % resource_path
		return ""
	return locator


func _display_locator(locator: String) -> String:
	if locator.begins_with("uid://"):
		var resource_path := ResourceUID.uid_to_path(locator)
		if not resource_path.is_empty():
			return resource_path
	return locator


func _locate_in_file_system(path: String) -> void:
	if path.is_empty() or not ResourceLoader.exists(path):
		_show_manifest_message(
			HealthLevel.ERROR,
			"定位失败",
			"资源已经删除或移动：\n%s" % path)
		return
	if not is_instance_valid(_file_system_dock):
		_show_manifest_message(
			HealthLevel.ERROR,
			"定位失败",
			"Godot FileSystem Dock 当前不可用。")
		return
	_file_system_dock.navigate_to_path(path)


func _on_editor_file_removed(path: String) -> void:
	manifest_paths_changed.emit("")
	if not is_instance_valid(_manifest_manage_dialog) or not _manifest_manage_dialog.visible:
		return
	if path == _managed_manifest_path:
		_render_missing_managed_manifest(path)
		return
	_refresh_managed_manifest_after_filesystem_change()


func _on_editor_files_moved(old_path: String, new_path: String) -> void:
	var managed_manifest_moved := old_path == _managed_manifest_path
	if managed_manifest_moved:
		_managed_manifest_path = new_path
	manifest_paths_changed.emit(new_path if managed_manifest_moved else "")
	if not is_instance_valid(_manifest_manage_dialog) or not _manifest_manage_dialog.visible:
		return
	_refresh_managed_manifest_after_filesystem_change()


func _refresh_managed_manifest_after_filesystem_change() -> void:
	if _managed_manifest_path.is_empty() or not is_instance_valid(_manifest_manage_dialog):
		return
	if not ResourceLoader.exists(_managed_manifest_path):
		_render_missing_managed_manifest(_managed_manifest_path)
		return
	var manifest := ResourceLoader.load(
		_managed_manifest_path,
		"",
		ResourceLoader.CACHE_MODE_REPLACE)
	if not _is_resource_manifest(manifest):
		_render_missing_managed_manifest(_managed_manifest_path)
		return
	_manifest_manage_dialog.title = "资源清单管理"
	_managed_manifest_label.text = "当前：%s" % _managed_manifest_path
	_manifest_locate_button.disabled = false
	_manifest_add_resource_button.disabled = false
	_manifest_validate_button.disabled = false
	_render_manifest_entries(manifest)


func _render_missing_managed_manifest(path: String) -> void:
	_manifest_manage_dialog.title = "资源清单管理"
	_managed_manifest_label.text = "清单已删除或移动：%s" % path
	_manifest_entries_tree.clear()
	var root := _manifest_entries_tree.create_item()
	var item := _manifest_entries_tree.create_item(root)
	item.set_text(0, "资源清单已经不存在，请返回资源清单页面重新选择。")
	for column in range(3):
		item.set_selectable(column, false)
	_managed_entry_index = -1
	_manifest_edit_button.disabled = true
	_manifest_uid_button.disabled = true
	_manifest_remove_button.disabled = true
	_manifest_add_resource_button.disabled = true
	_manifest_validate_button.disabled = true
	_manifest_locate_button.disabled = true


func _normalize_manifest_save_path(path: String) -> String:
	if path.get_extension().is_empty():
		return "%s.tres" % path
	return path


func _is_project_file(path: String) -> bool:
	return path.begins_with("res://") and not path.ends_with("/") and path.get_file() != ""


func _refresh_editor_filesystem() -> void:
	var filesystem := _plugin.get_editor_interface().get_resource_filesystem()
	if filesystem != null and not filesystem.is_scanning():
		filesystem.scan()


func _show_manifest_message(level: HealthLevel, title: String, message: String) -> void:
	if not is_instance_valid(_manifest_report_label):
		return
	_manifest_report_label.clear()
	_manifest_report_label.push_font_size(18)
	_manifest_report_label.push_color(_level_color(level))
	_manifest_report_label.add_text(title)
	_manifest_report_label.pop()
	_manifest_report_label.pop()
	_manifest_report_label.add_text("\n\n%s" % message)
	_manifest_report_dialog.popup_centered(Vector2i(720, 420))


func _add_item(report: Dictionary, level: HealthLevel, name: String, message: String) -> void:
	report.items.append({"level": level, "name": name, "message": message})
	report.level = max(report.level, level)


func _level_color(level: HealthLevel) -> Color:
	match level:
		HealthLevel.NORMAL:
			return NORMAL_COLOR
		HealthLevel.PENDING:
			return PENDING_COLOR
		HealthLevel.WARNING:
			return WARNING_COLOR
		_:
			return ERROR_COLOR


func _level_name(level: HealthLevel) -> String:
	match level:
		HealthLevel.NORMAL:
			return "正常"
		HealthLevel.PENDING:
			return "待检查"
		HealthLevel.WARNING:
			return "警告"
		_:
			return "错误"


func _render_manifest_report(path: String, report: Dictionary) -> void:
	if not is_instance_valid(_manifest_report_label):
		return
	_manifest_report_label.clear()
	_manifest_report_label.push_font_size(18)
	_manifest_report_label.add_text("ResourceManifest 校验：")
	_manifest_report_label.push_color(_level_color(report.level))
	_manifest_report_label.add_text(_level_name(report.level))
	_manifest_report_label.pop()
	_manifest_report_label.pop()
	_manifest_report_label.add_text("\n\n文件：%s\n条目数：%d\n\n" % [path, report.entry_count])
	for item in report.items:
		_manifest_report_label.push_color(_level_color(item.level))
		_manifest_report_label.add_text("[%s]" % _level_name(item.level))
		_manifest_report_label.pop()
		_manifest_report_label.add_text(" %s：%s\n" % [item.name, item.message])
