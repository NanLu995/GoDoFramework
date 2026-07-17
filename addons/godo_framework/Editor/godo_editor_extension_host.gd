@tool
extends RefCounted

const HOST_API_VERSION := 1
const EXTENSION_CONTEXT_SCRIPT := preload("res://addons/godo_framework/Editor/godo_editor_extension_context.gd")
const MANIFEST_NAME := "godo_editor_extension.cfg"
const EXTENSION_SECTION := "extension"
const MENU_SEPARATOR_ID := 9000
const MENU_STATUS_ID := 9001
const FIRST_EXTENSION_MENU_ID := 10000
const EXTENSION_ID_CHARACTERS := "abcdefghijklmnopqrstuvwxyz0123456789._-"

var _owner: EditorPlugin
var _menu: PopupMenu
var _extensions: Array[Dictionary] = []
var _statuses: Array[Dictionary] = []
var _action_keys: Dictionary = {}
var _action_callbacks: Dictionary = {}
var _action_extensions: Dictionary = {}
var _next_menu_id := FIRST_EXTENSION_MENU_ID
var _status_dialog: AcceptDialog


func activate(owner: EditorPlugin, menu: PopupMenu) -> void:
	_owner = owner
	_menu = menu
	_menu.add_separator("编辑器扩展", MENU_SEPARATOR_ID)
	_discover_extensions()
	_menu.add_item("编辑器扩展状态...", MENU_STATUS_ID)
	_menu.id_pressed.connect(_on_menu_id_pressed)


func deactivate() -> void:
	if is_instance_valid(_menu) and _menu.id_pressed.is_connected(_on_menu_id_pressed):
		_menu.id_pressed.disconnect(_on_menu_id_pressed)

	for index in range(_extensions.size() - 1, -1, -1):
		var instance = _extensions[index].instance
		if instance != null and instance.has_method("deactivate"):
			instance.deactivate()

	if is_instance_valid(_menu):
		for menu_id in _action_callbacks.keys():
			_remove_menu_item(int(menu_id))
		_remove_menu_item(MENU_STATUS_ID)
		_remove_menu_item(MENU_SEPARATOR_ID)

	if is_instance_valid(_status_dialog):
		_status_dialog.queue_free()

	_extensions.clear()
	_statuses.clear()
	_action_keys.clear()
	_action_callbacks.clear()
	_action_extensions.clear()
	_owner = null
	_menu = null


func register_menu_action(
	extension_id: String,
	action_id: String,
	label: String,
	callback: Callable
) -> Error:
	var action_key := "%s:%s" % [extension_id, action_id]
	if action_id.is_empty() or label.is_empty() or not callback.is_valid():
		return ERR_INVALID_PARAMETER
	if _action_keys.has(action_key):
		return ERR_ALREADY_EXISTS

	var menu_id := _next_menu_id
	_next_menu_id += 1
	_action_keys[action_key] = menu_id
	_action_callbacks[menu_id] = callback
	_action_extensions[menu_id] = extension_id
	_menu.add_item(label, menu_id)
	return OK


func _discover_extensions() -> void:
	var descriptors: Array[Dictionary] = []
	_append_manifest_descriptors("res://addons", descriptors)
	_append_manifest_descriptors("res://addons/godo_framework/Integrations", descriptors)

	descriptors.sort_custom(_compare_descriptors)
	var loaded_ids: Dictionary = {}
	for descriptor in descriptors:
		var extension_id: String = descriptor.id
		if loaded_ids.has(extension_id):
			_record_failure(extension_id, descriptor.display_name, "扩展 ID 重复。")
			continue
		loaded_ids[extension_id] = true
		_load_extension(descriptor)


func _append_manifest_descriptors(root_path: String, descriptors: Array[Dictionary]) -> void:
	var package_names := Array(DirAccess.get_directories_at(root_path))
	package_names.sort()
	for package_name in package_names:
		var package_root := "%s/%s" % [root_path, package_name]
		var manifest_path := "%s/%s" % [package_root, MANIFEST_NAME]
		if not FileAccess.file_exists(manifest_path):
			continue
		var descriptor := _read_manifest(manifest_path, package_root)
		if descriptor.is_empty():
			continue
		descriptors.append(descriptor)


func _read_manifest(manifest_path: String, package_root: String) -> Dictionary:
	var config := ConfigFile.new()
	var load_error := config.load(manifest_path)
	if load_error != OK:
		_record_failure(manifest_path, package_root.get_file(), "清单读取失败：%s" % error_string(load_error))
		return {}
	if not config.has_section(EXTENSION_SECTION):
		_record_failure(manifest_path, package_root.get_file(), "缺少 [extension]。")
		return {}

	var extension_id := str(config.get_value(EXTENSION_SECTION, "id", "")).strip_edges()
	var display_name := str(config.get_value(EXTENSION_SECTION, "display_name", "")).strip_edges()
	var api_version := int(config.get_value(EXTENSION_SECTION, "api_version", 0))
	var script_path := str(config.get_value(EXTENSION_SECTION, "script", "")).strip_edges()
	var menu_order := int(config.get_value(EXTENSION_SECTION, "menu_order", 0))

	if extension_id.is_empty() or display_name.is_empty() or script_path.is_empty():
		_record_failure(manifest_path, display_name if not display_name.is_empty() else package_root.get_file(), "清单必填字段为空。")
		return {}
	if not _is_valid_extension_id(extension_id):
		_record_failure(extension_id, display_name, "扩展 ID 只能使用小写字母、数字、点、下划线和连字符。")
		return {}
	if api_version != HOST_API_VERSION:
		_record_failure(extension_id, display_name, "API 版本 %d 不兼容，宿主版本为 %d。" % [api_version, HOST_API_VERSION])
		return {}
	if not script_path.begins_with(package_root + "/") or "/../" in script_path or not script_path.ends_with(".gd"):
		_record_failure(extension_id, display_name, "扩展脚本必须位于自己的适配包目录。")
		return {}
	if not FileAccess.file_exists(script_path):
		_record_failure(extension_id, display_name, "扩展脚本不存在：%s" % script_path)
		return {}

	return {
		"id": extension_id,
		"display_name": display_name,
		"script": script_path,
		"menu_order": menu_order,
	}


func _load_extension(descriptor: Dictionary) -> void:
	var extension_script := load(descriptor.script) as Script
	if extension_script == null or not extension_script.can_instantiate():
		_record_failure(descriptor.id, descriptor.display_name, "扩展脚本加载失败。")
		return

	var instance = extension_script.new()
	if instance == null or not instance.has_method("activate") or not instance.has_method("deactivate"):
		_record_failure(descriptor.id, descriptor.display_name, "扩展必须实现 activate(context) 与 deactivate()。")
		return

	var context = EXTENSION_CONTEXT_SCRIPT.new(self, _owner, descriptor.id)
	var activate_error = instance.activate(context)
	if activate_error != OK:
		_remove_extension_actions(descriptor.id)
		instance.deactivate()
		_record_failure(descriptor.id, descriptor.display_name, "激活失败：%s" % error_string(int(activate_error)))
		return

	_extensions.append({"id": descriptor.id, "instance": instance, "context": context})
	_statuses.append({"name": descriptor.display_name, "healthy": true, "detail": "已加载"})


func _record_failure(extension_id: String, display_name: String, detail: String) -> void:
	_statuses.append({"name": display_name, "healthy": false, "detail": detail})
	push_error("[GoDo Editor Extension] %s (%s): %s" % [display_name, extension_id, detail])


func _remove_extension_actions(extension_id: String) -> void:
	var menu_ids: Array[int] = []
	for menu_id in _action_extensions:
		if _action_extensions[menu_id] == extension_id:
			menu_ids.append(int(menu_id))
	for menu_id in menu_ids:
		_remove_menu_item(menu_id)
		_action_callbacks.erase(menu_id)
		_action_extensions.erase(menu_id)
	for action_key in _action_keys.keys():
		if str(action_key).begins_with(extension_id + ":"):
			_action_keys.erase(action_key)


func _remove_menu_item(menu_id: int) -> void:
	var item_index := _menu.get_item_index(menu_id)
	if item_index >= 0:
		_menu.remove_item(item_index)


func _on_menu_id_pressed(menu_id: int) -> void:
	if menu_id == MENU_STATUS_ID:
		_show_status_dialog()
		return
	var callback: Callable = _action_callbacks.get(menu_id, Callable())
	if callback.is_valid():
		callback.call()


func _show_status_dialog() -> void:
	if not is_instance_valid(_status_dialog):
		_status_dialog = AcceptDialog.new()
		_status_dialog.title = "GoDo 编辑器扩展状态"
		_status_dialog.ok_button_text = "关闭"
		_status_dialog.min_size = Vector2i(620, 320)
		_owner.get_editor_interface().get_base_control().add_child(_status_dialog)

	var lines := PackedStringArray()
	if _statuses.is_empty():
		lines.append("未发现可选编辑器扩展。")
	else:
		for status in _statuses:
			var marker := "[正常]" if status.healthy else "[错误]"
			lines.append("%s %s：%s" % [marker, status.name, status.detail])
	_status_dialog.dialog_text = "\n".join(lines)
	_status_dialog.popup_centered(Vector2i(620, 320))


func _compare_descriptors(left: Dictionary, right: Dictionary) -> bool:
	if left.menu_order != right.menu_order:
		return left.menu_order < right.menu_order
	return left.id < right.id


func _is_valid_extension_id(extension_id: String) -> bool:
	if extension_id.is_empty() or extension_id[0] in "._-" or extension_id[-1] in "._-":
		return false
	for character in extension_id:
		if not EXTENSION_ID_CHARACTERS.contains(character):
			return false
	return true
