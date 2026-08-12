@tool
extends EditorExportPlugin

const EDITOR_ROOT := "res://addons/godo_framework/Editor/"
const TOOLS_ROOT := "res://addons/godo_framework/Tools/"
const DEBUGGER_ROOT := "res://addons/godo_framework/Debugger/"

var _is_debug_export := false


func _get_name() -> String:
	return "GoDoFrameworkExportFilter"


func _export_begin(
	_features: PackedStringArray,
	is_debug: bool,
	_path: String,
	_flags: int
) -> void:
	_is_debug_export = is_debug


func _export_file(path: String, _type: String, _features: PackedStringArray) -> void:
	if should_skip_path(path, _is_debug_export):
		skip()


func should_skip_path(path: String, is_debug: bool) -> bool:
	if path.begins_with(EDITOR_ROOT) or path.begins_with(TOOLS_ROOT):
		return true
	return not is_debug and path.begins_with(DEBUGGER_ROOT)
