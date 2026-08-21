@tool
extends VBoxContainer

var _host: RefCounted
var _extension_id := ""


func setup(host: RefCounted, extension_id: String) -> void:
	_host = host
	_extension_id = extension_id
	var content: Control = _host.create_embedded_page(_extension_id)
	if content == null:
		var error_label := Label.new()
		error_label.text = "扩展管理页面创建失败。"
		add_child(error_label)
		return
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(content)


func refresh() -> void:
	if _host != null and not _extension_id.is_empty():
		_host.refresh_embedded_page(_extension_id)
