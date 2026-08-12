@tool
extends VBoxContainer

var _controller: RefCounted
var _tree: Tree
var _manage_button: Button
var _validate_button: Button


func setup(controller: RefCounted) -> void:
	_controller = controller
	add_theme_constant_override("separation", 10)
	var description := Label.new()
	description.text = "项目内现有的 ResourceManifest。选择后可直接管理或校验。"
	add_child(description)
	_tree = Tree.new()
	_tree.name = "GoDoResourceManifestList"
	_tree.hide_root = true
	_tree.columns = 1
	_tree.column_titles_visible = true
	_tree.set_column_title(0, "ResourceManifest")
	_tree.select_mode = Tree.SELECT_ROW
	_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_tree.item_selected.connect(_on_selected)
	_tree.item_activated.connect(_on_manage)
	add_child(_tree)
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 8)
	add_child(actions)
	var create_button := Button.new()
	create_button.name = "GoDoResourceManifestCreateButton"
	create_button.text = "创建清单..."
	create_button.pressed.connect(_controller.open_create_dialog)
	actions.add_child(create_button)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(spacer)
	_validate_button = Button.new()
	_validate_button.text = "校验选中项"
	_validate_button.disabled = true
	_validate_button.pressed.connect(_on_validate)
	actions.add_child(_validate_button)
	_manage_button = Button.new()
	_manage_button.name = "GoDoResourceManifestManageButton"
	_manage_button.text = "管理选中项..."
	_manage_button.disabled = true
	_manage_button.pressed.connect(_on_manage)
	actions.add_child(_manage_button)


func refresh() -> void:
	_tree.clear()
	var root := _tree.create_item()
	var paths: PackedStringArray = _controller.find_manifest_paths()
	if paths.is_empty():
		var empty := _tree.create_item(root)
		empty.set_text(0, "当前项目没有资源清单")
		empty.set_selectable(0, false)
	else:
		for path in paths:
			var item := _tree.create_item(root)
			item.set_text(0, path)
			item.set_metadata(0, path)
	_manage_button.disabled = true
	_validate_button.disabled = true


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
