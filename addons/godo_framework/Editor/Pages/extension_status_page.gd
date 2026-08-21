@tool
extends VBoxContainer

var _host: RefCounted
var _content: VBoxContainer
var _extension_id := ""
var _show_actions := true
var _menu_section := ""


func setup(
	host: RefCounted,
	extension_id := "",
	show_actions := true,
	menu_section := ""
) -> void:
	_host = host
	_extension_id = extension_id
	_show_actions = show_actions
	_menu_section = menu_section
	add_theme_constant_override("separation", 10)
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(scroll)
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", 8)
	scroll.add_child(_content)


func refresh() -> void:
	for child in _content.get_children():
		_content.remove_child(child)
		child.queue_free()
	var all_statuses: Array[Dictionary] = _host.get_statuses()
	var statuses: Array[Dictionary] = []
	for status in all_statuses:
		if (
			(_extension_id.is_empty() or status.id == _extension_id)
			and (_menu_section.is_empty() or status.menu_section == _menu_section)
		):
			statuses.append(status)
	if statuses.is_empty():
		var empty := Label.new()
		empty.text = "未发现可选编辑器扩展。"
		_content.add_child(empty)
	else:
		for status in statuses:
			var label := Label.new()
			label.text = "[%s] %s：%s" % ["正常" if status.healthy else "错误", status.name, status.detail]
			label.modulate = Color("#8bd49c") if status.healthy else Color("#ff6b6b")
			_content.add_child(label)
	var actions: Array[Dictionary] = []
	if _show_actions:
		actions = _host.get_actions(_extension_id)
	if not actions.is_empty():
		_content.add_child(HSeparator.new())
	for action in actions:
		var button := Button.new()
		button.text = action.label
		button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		button.pressed.connect(_host.execute_action.bind(action.key))
		_content.add_child(button)
