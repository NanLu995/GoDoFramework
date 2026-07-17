@tool
extends RefCounted

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
var _resource_file_dialog: EditorFileDialog
var _manifest_add_confirm_dialog: ConfirmationDialog
var _manifest_manage_dialog: AcceptDialog
var _manifest_remove_confirm_dialog: ConfirmationDialog
var _manifest_edit_dialog: ConfirmationDialog
var _manifest_uid_confirm_dialog: ConfirmationDialog
var _manifest_report_dialog: AcceptDialog
var _manifest_report_label: RichTextLabel
var _manifest_entries_tree: Tree
var _manifest_edit_id_input: LineEdit
var _manifest_edit_locator_input: LineEdit
var _manifest_edit_button: Button
var _manifest_uid_button: Button
var _manifest_remove_button: Button
var _manifest_action := ""
var _pending_resource_paths := PackedStringArray()
var _pending_manifest_path := ""
var _managed_manifest_path := ""
var _managed_entry_index := -1
var _csharp_resource_load_error := ""
var _uid_generation_error := ""

func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
	_manifest_file_dialog = EditorFileDialog.new()
	_manifest_file_dialog.title = "校验资源清单"
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_manifest_file_dialog.mode_overrides_title = false
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.file_selected.connect(_on_manifest_file_selected)
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_file_dialog)

	_resource_file_dialog = EditorFileDialog.new()
	_resource_file_dialog.title = "选择要添加的资源"
	_resource_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILES
	_resource_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_resource_file_dialog.mode_overrides_title = false
	_resource_file_dialog.filters = _addable_resource_filters()
	_resource_file_dialog.current_path = "res://"
	_resource_file_dialog.files_selected.connect(_on_resource_files_selected)
	_plugin.get_editor_interface().get_base_control().add_child(_resource_file_dialog)

	_manifest_add_confirm_dialog = ConfirmationDialog.new()
	_manifest_add_confirm_dialog.title = "确认添加资源"
	_manifest_add_confirm_dialog.ok_button_text = "确认添加"
	_manifest_add_confirm_dialog.cancel_button_text = "取消添加"
	_manifest_add_confirm_dialog.confirmed.connect(_on_manifest_add_confirmed)
	_manifest_add_confirm_dialog.canceled.connect(_on_manifest_add_canceled)
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_add_confirm_dialog)

	_manifest_manage_dialog = AcceptDialog.new()
	_manifest_manage_dialog.title = "资源清单管理"
	_manifest_manage_dialog.ok_button_text = "关闭"
	_manifest_manage_dialog.min_size = Vector2i(960, 520)
	_manifest_manage_dialog.get_label().hide()
	_manifest_entries_tree = Tree.new()
	_manifest_entries_tree.columns = 3
	_manifest_entries_tree.column_titles_visible = true
	_manifest_entries_tree.hide_root = true
	_manifest_entries_tree.select_mode = Tree.SELECT_ROW
	_manifest_entries_tree.set_column_title(0, "Id")
	_manifest_entries_tree.set_column_title(1, "定位")
	_manifest_entries_tree.set_column_title(2, "UID 状态")
	_manifest_entries_tree.set_column_expand(0, true)
	_manifest_entries_tree.set_column_expand_ratio(0, 2)
	_manifest_entries_tree.set_column_expand(1, true)
	_manifest_entries_tree.set_column_expand_ratio(1, 4)
	_manifest_entries_tree.set_column_expand(2, true)
	_manifest_entries_tree.set_column_expand_ratio(2, 1)
	_manifest_entries_tree.item_selected.connect(_on_manifest_entry_selected)
	_manifest_entries_tree.item_activated.connect(_on_manifest_entry_activated)
	_manifest_manage_dialog.add_child(_manifest_entries_tree)
	_manifest_entries_tree.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_manifest_entries_tree.offset_left = 16
	_manifest_entries_tree.offset_top = 16
	_manifest_entries_tree.offset_right = -16
	_manifest_entries_tree.offset_bottom = -56
	_manifest_edit_button = _manifest_manage_dialog.add_button("编辑选中项", true)
	_manifest_edit_button.disabled = true
	_manifest_edit_button.pressed.connect(_on_manifest_edit_pressed)
	_manifest_uid_button = _manifest_manage_dialog.add_button("生成并使用 UID", true)
	_manifest_uid_button.disabled = true
	_manifest_uid_button.pressed.connect(_on_manifest_uid_pressed)
	_manifest_remove_button = _manifest_manage_dialog.add_button("删除选中项", true)
	_manifest_remove_button.disabled = true
	_manifest_remove_button.pressed.connect(_on_manifest_remove_pressed)
	_plugin.get_editor_interface().get_base_control().add_child(_manifest_manage_dialog)

	_manifest_remove_confirm_dialog = ConfirmationDialog.new()
	_manifest_remove_confirm_dialog.title = "删除资源清单条目"
	_manifest_remove_confirm_dialog.ok_button_text = "删除"
	_manifest_remove_confirm_dialog.cancel_button_text = "取消"
	_manifest_remove_confirm_dialog.confirmed.connect(_on_manifest_remove_confirmed)
	_manifest_manage_dialog.add_child(_manifest_remove_confirm_dialog)

	_manifest_edit_dialog = ConfirmationDialog.new()
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
	locator_label.text = "定位"
	edit_content.add_child(locator_label)
	_manifest_edit_locator_input = LineEdit.new()
	_manifest_edit_locator_input.placeholder_text = "res:// 或 uid://"
	_manifest_edit_locator_input.select_all_on_focus = true
	edit_content.add_child(_manifest_edit_locator_input)
	_manifest_edit_dialog.confirmed.connect(_on_manifest_edit_confirmed)
	_manifest_manage_dialog.add_child(_manifest_edit_dialog)

	_manifest_uid_confirm_dialog = ConfirmationDialog.new()
	_manifest_uid_confirm_dialog.title = "生成并使用 UID"
	_manifest_uid_confirm_dialog.ok_button_text = "确认生成"
	_manifest_uid_confirm_dialog.cancel_button_text = "取消"
	_manifest_uid_confirm_dialog.confirmed.connect(_on_manifest_uid_confirmed)
	_manifest_manage_dialog.add_child(_manifest_uid_confirm_dialog)

	_manifest_report_dialog = AcceptDialog.new()
	_manifest_report_dialog.title = "资源清单校验"
	_manifest_report_dialog.ok_button_text = "关闭"
	_manifest_report_dialog.min_size = Vector2i(720, 420)
	_manifest_report_dialog.exclusive = false
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

func dispose() -> void:
	for dialog in [_manifest_file_dialog, _resource_file_dialog, _manifest_add_confirm_dialog, _manifest_manage_dialog, _manifest_remove_confirm_dialog, _manifest_edit_dialog, _manifest_uid_confirm_dialog, _manifest_report_dialog]:
		if is_instance_valid(dialog):
			dialog.queue_free()

func open_validate_dialog() -> void:
	_open_manifest_validate_dialog()

func open_create_dialog() -> void:
	_open_manifest_create_dialog()

func open_manage_dialog() -> void:
	_open_manifest_manage_dialog()

func open_add_selected_resource_dialog() -> void:
	_open_add_selected_resource_dialog()

func _open_manifest_validate_dialog() -> void:
	if not is_instance_valid(_manifest_file_dialog):
		return
	_manifest_action = MANIFEST_ACTION_VALIDATE
	_manifest_file_dialog.title = "校验资源清单"
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.get_ok_button().text = "打开"
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.popup_centered(Vector2i(720, 480))


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


func _open_manifest_manage_dialog() -> void:
	var manifest_paths := _find_resource_manifest_paths("res://")
	if manifest_paths.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "打开失败", "项目内没有 ResourceManifest，请先创建资源清单")
		return
	if manifest_paths.size() == 1:
		call_deferred("_show_manifest_manager", manifest_paths[0])
		return
	if not is_instance_valid(_manifest_file_dialog):
		return
	_manifest_action = MANIFEST_ACTION_MANAGE
	_manifest_file_dialog.title = "选择要管理的资源清单（发现 %d 份）" % manifest_paths.size()
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.get_ok_button().text = "选择"
	_manifest_file_dialog.current_path = "res://"
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
	_select_target_manifest()


func _select_target_manifest() -> void:
	var manifest_paths := _find_resource_manifest_paths("res://")
	if manifest_paths.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "项目内没有 ResourceManifest，请先创建资源清单")
		return
	if manifest_paths.size() == 1:
		_preview_manifest_add(manifest_paths[0])
		return

	_manifest_action = MANIFEST_ACTION_ADD_SELECTED
	_manifest_file_dialog.title = "选择目标资源清单（发现 %d 份）" % manifest_paths.size()
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.filters = _manifest_file_filters()
	_manifest_file_dialog.get_ok_button().text = "确认选择"
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.popup_centered(Vector2i(720, 480))


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
	var save_path := _normalize_manifest_save_path(path)
	var manifest := _create_manifest_instance()
	if manifest == null:
		_show_manifest_message(HealthLevel.ERROR, "创建失败", "ResourceManifest 脚本无法加载或实例化。\n%s" % _csharp_resource_load_error)
		return

	var save_error := ResourceSaver.save(manifest, save_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_manifest_message(HealthLevel.ERROR, "创建失败", "%s：%s" % [save_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	_show_manifest_message(HealthLevel.NORMAL, "创建成功", "已创建空 ResourceManifest：%s" % save_path)


func _add_selected_resources_to_manifest(manifest_path: String) -> void:
	if _pending_resource_paths.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "没有待添加的资源路径")
		return

	var manifest := ResourceLoader.load(manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "请选择 ResourceManifest 资源：%s" % manifest_path)
		return
	var entries = _get_manifest_entries(manifest)
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

	var save_error := ResourceSaver.save(manifest, manifest_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_manifest_message(HealthLevel.ERROR, "添加失败", "%s：%s" % [manifest_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	var success_message := "已添加 %d 个资源到：%s\n\n%s" % [
		_pending_resource_paths.size(),
		manifest_path,
		"\n".join(_pending_resource_paths),
	]
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

	var manifest := ResourceLoader.load(manifest_path)
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
	var manifest := ResourceLoader.load(manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "打开失败", "请选择 ResourceManifest 资源：%s" % manifest_path)
		return

	_managed_manifest_path = manifest_path
	_render_manifest_entries(manifest)
	_manifest_manage_dialog.popup_centered(Vector2i(960, 520))


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
	for index in range(entries.size()):
		var entry = entries[index]
		var entry_id := _get_exported_string(entry, "Id", "id")
		var locator := _get_exported_string(entry, "Locator", "locator")
		var display_locator := _display_locator(locator)
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

	var manifest := ResourceLoader.load(_managed_manifest_path)
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

	var manifest := ResourceLoader.load(_managed_manifest_path)
	var entries = _get_manifest_entries(manifest)
	var entry = entries[_managed_entry_index]
	var entry_id := _get_exported_string(entry, "Id", "id")
	var locator := _get_exported_string(entry, "Locator", "locator")
	_manifest_uid_confirm_dialog.dialog_text = "将为以下资源生成并使用 UID。确认后会更新 Godot 的 UID 记录，并将此清单条目的 Locator 改为 uid://。\n\nId：%s\n资源：%s" % [entry_id, locator]
	_manifest_uid_confirm_dialog.popup_centered(Vector2i(680, 260))


func _on_manifest_uid_confirmed() -> void:
	if not _selected_entry_uses_path_locator():
		return

	var manifest := ResourceLoader.load(_managed_manifest_path)
	var entries = _get_manifest_entries(manifest)
	var entry = entries[_managed_entry_index]
	var resource_path := _get_exported_string(entry, "Locator", "locator")
	var uid_locator := _ensure_resource_uid(resource_path)
	if uid_locator.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "生成 UID 失败", "%s\n\n资源清单未修改。" % _uid_generation_error)
		return

	entry.set("Locator", uid_locator)
	var save_error := ResourceSaver.save(manifest, _managed_manifest_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_manifest_message(HealthLevel.ERROR, "生成 UID 失败", "%s：%s\n\n资源清单未修改。" % [_managed_manifest_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	_render_manifest_entries(manifest)


func _on_manifest_edit_confirmed() -> void:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return

	var entry_id := _manifest_edit_id_input.text.strip_edges()
	var locator := _manifest_edit_locator_input.text.strip_edges()
	if entry_id.is_empty():
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "Id 不能为空")
		return
	if not (locator.begins_with("res://") or locator.begins_with("uid://")):
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "定位必须以 res:// 或 uid:// 开头：%s" % locator)
		return
	if not ResourceLoader.exists(locator):
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "当前定位无法解析到资源：%s" % locator)
		return

	var manifest := ResourceLoader.load(_managed_manifest_path)
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

	var entry = entries[_managed_entry_index]
	entry.set("Id", entry_id)
	entry.set("Locator", locator)
	var save_error := ResourceSaver.save(manifest, _managed_manifest_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_manifest_message(HealthLevel.ERROR, "编辑失败", "%s：%s" % [_managed_manifest_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	_render_manifest_entries(manifest)


func _on_manifest_remove_pressed() -> void:
	if _managed_entry_index < 0 or _managed_manifest_path.is_empty():
		return

	var manifest := ResourceLoader.load(_managed_manifest_path)
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

	var manifest := ResourceLoader.load(_managed_manifest_path)
	if not _is_resource_manifest(manifest):
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "当前 ResourceManifest 无法重新加载：%s" % _managed_manifest_path)
		return
	var entries = _get_manifest_entries(manifest)
	if _managed_entry_index >= entries.size():
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "当前选中条目已不存在")
		return

	entries.remove_at(_managed_entry_index)
	manifest.set("Entries", entries)
	var save_error := ResourceSaver.save(manifest, _managed_manifest_path, ResourceSaver.FLAG_CHANGE_PATH)
	if save_error != OK:
		_show_manifest_message(HealthLevel.ERROR, "删除失败", "%s：%s" % [_managed_manifest_path, error_string(save_error)])
		return

	_refresh_editor_filesystem()
	_render_manifest_entries(manifest)


func _validate_manifest(path: String) -> Dictionary:
	var report := {
		"items": [],
		"level": HealthLevel.NORMAL,
		"entry_count": 0,
	}
	var resource := ResourceLoader.load(path)
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
		if directory_name.begins_with("."):
			continue
		manifest_paths.append_array(_find_resource_manifest_paths(path.path_join(directory_name)))
	return manifest_paths


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
	var loaded_resource := ResourceLoader.load(script_path, "", ResourceLoader.CACHE_MODE_IGNORE)
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
	var manifest := ResourceLoader.load(_managed_manifest_path)
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
				"text": "已使用 UID",
				"color": NORMAL_COLOR,
				"tooltip": "清单当前通过 uid:// 定位该资源。",
			}
		return {
			"text": "UID 无效",
			"color": ERROR_COLOR,
			"tooltip": "清单使用 uid://，但当前无法解析该资源。",
		}
	if locator.begins_with("res://") and ResourceLoader.exists(locator):
		if ResourceLoader.get_resource_uid(locator) != ResourceUID.INVALID_ID:
			return {
				"text": "可转换为 UID",
				"color": WARNING_COLOR,
				"tooltip": "资源已有 UID；点击“生成并使用 UID”可更新清单定位。",
			}
		return {
			"text": "缺少 UID",
			"color": PENDING_COLOR,
			"tooltip": "资源当前没有可用 UID；点击“生成并使用 UID”可创建。",
		}
	return {
		"text": "无法判断",
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
