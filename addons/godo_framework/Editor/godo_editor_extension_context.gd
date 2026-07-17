@tool
extends RefCounted

var _host
var _owner
var _extension_id: String
var _editor_interface


func _init(host, owner, extension_id: String) -> void:
	_host = host
	_owner = owner
	_extension_id = extension_id
	_editor_interface = owner.get_editor_interface()


func add_menu_action(action_id: String, label: String, callback: Callable) -> Error:
	return _host.register_menu_action(_extension_id, action_id, label, callback)


func get_editor_interface():
	return _editor_interface


func add_export_plugin(plugin: EditorExportPlugin) -> void:
	_owner.add_export_plugin(plugin)


func remove_export_plugin(plugin: EditorExportPlugin) -> void:
	_owner.remove_export_plugin(plugin)


func add_autoload_singleton(name: String, path: String) -> void:
	_owner.add_autoload_singleton(name, path)


func remove_autoload_singleton(name: String) -> void:
	_owner.remove_autoload_singleton(name)
