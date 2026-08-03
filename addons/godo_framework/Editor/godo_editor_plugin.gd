@tool
extends EditorPlugin

const EDITOR_EXTENSION_HOST_SCRIPT := preload("res://addons/godo_framework/Editor/godo_editor_extension_host.gd")
const RUNTIME_SETUP_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_runtime_setup_controller.gd")
const RESOURCE_MANIFEST_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_resource_manifest_controller.gd")
const UI_CONFIG_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_ui_config_controller.gd")
const MENU_SETUP_ID := 1
const MENU_VALIDATE_MANIFEST_ID := 100
const MENU_CREATE_MANIFEST_ID := 101
const MENU_ADD_SELECTED_RESOURCE_ID := 102
const MENU_MANAGE_MANIFEST_ID := 103
const MENU_CREATE_UI_CONFIG_ID := 200
const MENU_MANAGE_UI_CONFIG_ID := 201
const MENU_VALIDATE_UI_CONFIG_ID := 202

var _toolbar_menu_button: MenuButton
var _tool_menu: PopupMenu
var _editor_extension_host: RefCounted
var _runtime_setup_controller: RefCounted
var _resource_manifest_controller: RefCounted
var _ui_config_controller: RefCounted

func _enter_tree() -> void:
	_create_tool_menu()
	_runtime_setup_controller = RUNTIME_SETUP_CONTROLLER_SCRIPT.new()
	_runtime_setup_controller.initialize(self)
	_resource_manifest_controller = RESOURCE_MANIFEST_CONTROLLER_SCRIPT.new()
	_resource_manifest_controller.initialize(self)
	_ui_config_controller = UI_CONFIG_CONTROLLER_SCRIPT.new()
	_ui_config_controller.initialize(self)
	_editor_extension_host = EDITOR_EXTENSION_HOST_SCRIPT.new()
	_editor_extension_host.activate(self, _tool_menu)
	add_control_to_container(CONTAINER_TOOLBAR, _toolbar_menu_button)


func _exit_tree() -> void:
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
	_tool_menu.add_item("配置 (Setup)...", MENU_SETUP_ID)
	_tool_menu.add_separator("资源管理")
	_tool_menu.add_item("创建资源清单 (Create Resource Manifest)...", MENU_CREATE_MANIFEST_ID)
	_tool_menu.add_item("管理资源清单 (Manage Resource Manifest)...", MENU_MANAGE_MANIFEST_ID)
	_tool_menu.add_item("校验资源清单 (Validate Resource Manifest)...", MENU_VALIDATE_MANIFEST_ID)
	_tool_menu.add_separator()
	_tool_menu.add_item("选择资源并添加 (Select Resource to Add)...", MENU_ADD_SELECTED_RESOURCE_ID)
	_tool_menu.add_separator("UI 配置")
	_tool_menu.add_item("创建 UI 配置 (Create UI Config)...", MENU_CREATE_UI_CONFIG_ID)
	_tool_menu.add_item("管理 UI 配置 (Manage UI Config)...", MENU_MANAGE_UI_CONFIG_ID)
	_tool_menu.add_item("校验 UI 配置 (Validate UI Config)...", MENU_VALIDATE_UI_CONFIG_ID)
	_tool_menu.id_pressed.connect(_on_tool_menu_id_pressed)


func _on_tool_menu_id_pressed(id: int) -> void:
	match id:
		MENU_SETUP_ID:
			_runtime_setup_controller.open_dialog()
		MENU_CREATE_MANIFEST_ID:
			_resource_manifest_controller.open_create_dialog()
		MENU_ADD_SELECTED_RESOURCE_ID:
			_resource_manifest_controller.open_add_selected_resource_dialog()
		MENU_MANAGE_MANIFEST_ID:
			_resource_manifest_controller.open_manage_dialog()
		MENU_VALIDATE_MANIFEST_ID:
			_resource_manifest_controller.open_validate_dialog()
		MENU_CREATE_UI_CONFIG_ID:
			_ui_config_controller.open_create_dialog()
		MENU_MANAGE_UI_CONFIG_ID:
			_ui_config_controller.open_manage_dialog()
		MENU_VALIDATE_UI_CONFIG_ID:
			_ui_config_controller.open_validate_dialog()
