@tool
extends EditorPlugin

const EDITOR_EXTENSION_HOST_SCRIPT := preload("res://addons/godo_framework/Editor/godo_editor_extension_host.gd")
const RUNTIME_SETUP_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_runtime_setup_controller.gd")
const RESOURCE_MANIFEST_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_resource_manifest_controller.gd")
const UI_CONFIG_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_ui_config_controller.gd")
const EXPORT_FILTER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_export_filter.gd")
const FRAMEWORK_WINDOW_SCRIPT := preload("res://addons/godo_framework/Editor/Window/godo_framework_window.gd")
const MENU_SETUP_ID := 1

var _toolbar_menu_button: MenuButton
var _tool_menu: PopupMenu
var _editor_extension_host: RefCounted
var _runtime_setup_controller: RefCounted
var _resource_manifest_controller: RefCounted
var _ui_config_controller: RefCounted
var _export_filter: EditorExportPlugin
var _framework_window: RefCounted

func _enter_tree() -> void:
	_create_tool_menu()
	_export_filter = EXPORT_FILTER_SCRIPT.new()
	add_export_plugin(_export_filter)
	_runtime_setup_controller = RUNTIME_SETUP_CONTROLLER_SCRIPT.new()
	_runtime_setup_controller.initialize(self)
	_resource_manifest_controller = RESOURCE_MANIFEST_CONTROLLER_SCRIPT.new()
	_resource_manifest_controller.initialize(self)
	_ui_config_controller = UI_CONFIG_CONTROLLER_SCRIPT.new()
	_ui_config_controller.initialize(self)
	_editor_extension_host = EDITOR_EXTENSION_HOST_SCRIPT.new()
	_editor_extension_host.activate(self)
	_framework_window = FRAMEWORK_WINDOW_SCRIPT.new()
	_framework_window.initialize(
		self,
		_runtime_setup_controller,
		_resource_manifest_controller,
		_ui_config_controller,
		_editor_extension_host)
	add_control_to_container(CONTAINER_TOOLBAR, _toolbar_menu_button)


func _exit_tree() -> void:
	if is_instance_valid(_framework_window):
		_framework_window.dispose()
		_framework_window = null
	if is_instance_valid(_export_filter):
		remove_export_plugin(_export_filter)
		_export_filter = null
	if is_instance_valid(_editor_extension_host):
		_editor_extension_host.deactivate()
		_editor_extension_host = null
	if is_instance_valid(_runtime_setup_controller):
		_runtime_setup_controller.dispose()
		_runtime_setup_controller = null
	if is_instance_valid(_resource_manifest_controller):
		_resource_manifest_controller.dispose()
		_resource_manifest_controller = null
	if is_instance_valid(_ui_config_controller):
		_ui_config_controller.dispose()
		_ui_config_controller = null
	if is_instance_valid(_toolbar_menu_button):
		remove_control_from_container(CONTAINER_TOOLBAR, _toolbar_menu_button)
		_toolbar_menu_button.queue_free()


func _create_tool_menu() -> void:
	_toolbar_menu_button = MenuButton.new()
	_toolbar_menu_button.name = "GoDoFrameworkToolbarMenu"
	_toolbar_menu_button.text = "GoDo Framework"
	_toolbar_menu_button.tooltip_text = "GoDo Framework"
	_tool_menu = _toolbar_menu_button.get_popup()
	_tool_menu.add_item("打开 GoDo Framework...", MENU_SETUP_ID)
	_tool_menu.id_pressed.connect(_on_tool_menu_id_pressed)


func _on_tool_menu_id_pressed(id: int) -> void:
	match id:
		MENU_SETUP_ID:
			_framework_window.open()
