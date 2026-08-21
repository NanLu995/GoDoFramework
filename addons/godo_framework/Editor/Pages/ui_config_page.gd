@tool
extends VBoxContainer

var _controller: RefCounted
var _tree: Tree
var _manage_button: Button
var _validate_button: Button


func setup(controller: RefCounted) -> void:
	_controller = controller
	_controller.config_paths_changed.connect(_on_config_paths_changed)
	add_theme_constant_override("separation", 10)
	_tree = Tree.new()
	_tree.name = "GoDoUiConfigList"
	_tree.hide_root = true
	_tree.columns = 1
	_tree.column_titles_visible = true
	_tree.set_column_title(0, "UiConfig")
	_tree.select_mode = Tree.SELECT_ROW
	_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_tree.item_selected.connect(_on_selected)
	_tree.item_activated.connect(_on_manage)
	add_child(_tree)
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 8)
	add_child(actions)
	var refresh_button := Button.new()
	refresh_button.name = "GoDoUiConfigRefreshButton"
	refresh_button.text = "刷新"
	refresh_button.pressed.connect(_on_refresh_pressed)
	actions.add_child(refresh_button)
	var create_button := Button.new()
	create_button.name = "GoDoUiConfigCreateButton"
	create_button.text = "创建 UI 配置..."
	create_button.pressed.connect(_controller.open_create_dialog)
	actions.add_child(create_button)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	_validate_button = Button.new()
	_validate_button.name = "GoDoUiConfigValidateButton"
	_validate_button.text = "校验选中项"
	_validate_button.disabled = true
	_validate_button.pressed.connect(_on_validate)
	actions.add_child(_validate_button)
	_manage_button = Button.new()
	_manage_button.name = "GoDoUiConfigManageButton"
	_manage_button.text = "管理选中项..."
	_manage_button.disabled = true
	_manage_button.pressed.connect(_on_manage)
	actions.add_child(_manage_button)


func dispose() -> void:
	if (
		is_instance_valid(_controller)
		and _controller.config_paths_changed.is_connected(_on_config_paths_changed)
	):
		_controller.config_paths_changed.disconnect(_on_config_paths_changed)


func refresh(preferred_path := "") -> void:
	var path_to_select := preferred_path if not preferred_path.is_empty() else _selected_path()
	_tree.clear()
	var root := _tree.create_item()
	var paths: PackedStringArray = _controller.find_config_paths()
	var selected_item: TreeItem
	if paths.is_empty():
		var empty := _tree.create_item(root)
		empty.set_text(0, "当前项目没有 UI 配置")
		empty.set_selectable(0, false)
	else:
		for path in paths:
			var item := _tree.create_item(root)
			item.set_text(0, path)
			item.set_metadata(0, path)
			if path == path_to_select:
				selected_item = item
	if selected_item != null:
		_tree.set_selected(selected_item, 0)
		_tree.scroll_to_item(selected_item)
	_on_selected()


func _on_config_paths_changed(preferred_path: String) -> void:
	refresh(preferred_path)


func _on_refresh_pressed() -> void:
	refresh()


func _on_selected() -> void:
	var has_selection := not _selected_path().is_empty()
	_manage_button.disabled = not has_selection
	_validate_button.disabled = not has_selection


func _on_manage() -> void:
	var path := _selected_path()
	if not path.is_empty():
		_controller.open_manage_path(path)


func _on_validate() -> void:
	var path := _selected_path()
	if not path.is_empty():
		_controller.open_validate_path(path)


func _selected_path() -> String:
	var item := _tree.get_selected()
	return "" if item == null else str(item.get_metadata(0))
