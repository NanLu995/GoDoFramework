@tool
extends EditorExportPlugin

const CONFIG_ROOT := "res://DataTables"
const TOOL_PATH := "res://addons/godo_framework/Tools/DataTable/godo_datatable.py"
const PYTHON_SETTING := "godo_framework/datatable/python_executable"
const BUILD_CONFIG_FIELDS := ["format_version", "profile", "source", "output", "csharp"]
const MAX_VERIFY_OUTPUT := 4096

var _skip_exact: Dictionary = {}
var _skip_roots := PackedStringArray()
var _added_paths: Dictionary = {}


func _get_name() -> String:
	return "GoDoDataTable"


func _export_begin(features: PackedStringArray, is_debug: bool, _path: String, _flags: int) -> void:
	_skip_exact.clear()
	_skip_roots.clear()
	_added_paths.clear()
	var config_paths := discover_build_configs(CONFIG_ROOT)
	if config_paths.is_empty():
		return
	var target := "server" if features.has("dedicated_server") else "client"
	var plans: Array[Dictionary] = []
	for config_path in config_paths:
		var plan := build_export_plan(config_path, target, is_debug)
		if not plan.valid:
			_report_error("%s：%s" % [config_path, plan.error])
			continue
		_register_exclusions(plan)
		plans.append({"config": config_path, "plan": plan})
	if plans.is_empty():
		return
	var python := _detect_python()
	if python.is_empty():
		_report_error("发现 DataTable 导出配置，但未找到 Python 3.10+。")
		return
	for entry in plans:
		var config_path := str(entry.config)
		var plan: Dictionary = entry.plan
		var verify_error := _verify_generated(python, config_path)
		if not verify_error.is_empty():
			_report_error("%s：%s" % [config_path, verify_error])
			continue
		_register_files(plan)


func _export_file(path: String, _type: String, _features: PackedStringArray) -> void:
	if _skip_exact.has(path):
		skip()
		return
	for root in _skip_roots:
		if path.begins_with(root):
			skip()
			return


func discover_build_configs(root: String) -> PackedStringArray:
	var result := PackedStringArray()
	if not DirAccess.dir_exists_absolute(root):
		return result
	var files := Array(DirAccess.get_files_at(root))
	files.sort()
	for file_name in files:
		if str(file_name).ends_with(".build.json"):
			result.append(root.path_join(str(file_name)))
	return result


func build_export_plan(config_path: String, target: String, is_debug: bool) -> Dictionary:
	if target != "client" and target != "server":
		return {"valid": false, "error": "未知导出目标：%s。" % target}
	var state := _read_build_config(config_path)
	if not state.valid:
		return state
	var profile := _read_json_object(str(state.profile), "Profile")
	if not profile.valid:
		return profile
	if not profile.value.get("tables", null) is Array:
		return {"valid": false, "error": "Profile 缺少 tables 数组。"}
	var allowed := ["Shared", "ServerOnly"] if target == "server" else ["Shared", "ClientOnly"]
	var added: Dictionary = {}
	for table in profile.value.tables:
		if not table is Dictionary:
			return {"valid": false, "error": "Profile tables 包含非对象条目。"}
		var table_id := str(table.get("id", "")).strip_edges()
		var audience := str(table.get("audience", "")).strip_edges()
		if table_id.is_empty() or not allowed.has(audience):
			continue
		var artifact_path := str(state.output).path_join("%s.gdtb" % table_id)
		added[artifact_path] = artifact_path
	var target_manifest := str(state.output).path_join("manifest.%s.json" % target)
	added[str(state.output).path_join("manifest.json")] = target_manifest
	if is_debug:
		var target_debug := str(state.output).path_join("debug.%s.json" % target)
		added[str(state.output).path_join("debug.json")] = target_debug
	return {
		"valid": true,
		"config": config_path,
		"profile": state.profile,
		"source": state.source,
		"output": state.output,
		"added": added,
	}


func _read_build_config(path: String) -> Dictionary:
	if not path.begins_with("res://") or not FileAccess.file_exists(path):
		return {"valid": false, "error": "Build Config 必须是存在的 res:// 文件。"}
	var parsed := _read_json_object(path, "Build Config")
	if not parsed.valid:
		return parsed
	var value: Dictionary = parsed.value
	if int(value.get("format_version", 0)) != 1:
		return {"valid": false, "error": "Build Config format_version 必须为 1。"}
	for key in value.keys():
		if not BUILD_CONFIG_FIELDS.has(str(key)):
			return {"valid": false, "error": "Build Config 包含未知字段：%s。" % key}
	for field in BUILD_CONFIG_FIELDS:
		if field == "format_version":
			continue
		if not value.get(field, null) is String or str(value[field]).strip_edges().is_empty():
			return {"valid": false, "error": "Build Config 字段 %s 必须是非空字符串。" % field}
		if not _is_safe_relative_path(str(value[field])):
			return {"valid": false, "error": "Build Config 字段 %s 不是安全相对路径。" % field}
	var root := path.get_base_dir()
	var profile := root.path_join(str(value.profile)).simplify_path()
	var source := root.path_join(str(value.source)).simplify_path()
	var output := root.path_join(str(value.output)).simplify_path()
	var csharp := root.path_join(str(value.csharp)).simplify_path()
	if not FileAccess.file_exists(profile):
		return {"valid": false, "error": "Profile 不存在：%s。" % profile}
	if not DirAccess.dir_exists_absolute(source):
		return {"valid": false, "error": "CSV 源目录不存在：%s。" % source}
	if _is_same_or_child(profile, output) or _is_same_or_child(source, output):
		return {"valid": false, "error": "输出目录不能包含 Profile 或 CSV 源目录。"}
	if _is_same_or_child(csharp, output):
		return {"valid": false, "error": "C# 输出必须位于数据输出目录外。"}
	return {
		"valid": true,
		"profile": profile,
		"source": source,
		"output": output,
		"csharp": csharp,
	}


func _read_json_object(path: String, description: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {"valid": false, "error": "无法读取 %s：%s。" % [description, path]}
	var parsed = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		return {"valid": false, "error": "%s 根节点必须是 JSON 对象。" % description}
	return {"valid": true, "value": parsed}


func _register_exclusions(plan: Dictionary) -> void:
	var config_path := str(plan.config)
	var source_root := str(plan.source).trim_suffix("/") + "/"
	var output_root := str(plan.output).trim_suffix("/") + "/"
	_skip_exact[config_path] = true
	_skip_exact[str(plan.profile)] = true
	if not _skip_roots.has(source_root):
		_skip_roots.append(source_root)
	if not _skip_roots.has(output_root):
		_skip_roots.append(output_root)


func _register_files(plan: Dictionary) -> void:
	var config_path := str(plan.config)
	for virtual_path in plan.added:
		if _added_paths.has(virtual_path):
			_report_error("多个 DataTable 配置写入同一导出路径：%s。" % virtual_path)
			continue
		var source_path := str(plan.added[virtual_path])
		var bytes := FileAccess.get_file_as_bytes(source_path)
		if bytes.is_empty() and FileAccess.get_open_error() != OK:
			_report_error("无法读取 DataTable 导出产物：%s。" % source_path)
			continue
		add_file(str(virtual_path), bytes, false)
		_added_paths[virtual_path] = config_path


func _verify_generated(python: String, config_path: String) -> String:
	var output: Array = []
	var exit_code := OS.execute(
		python,
		PackedStringArray([
			"-X",
			"utf8",
			ProjectSettings.globalize_path(TOOL_PATH),
			"verify-generated",
			"--build-config",
			ProjectSettings.globalize_path(config_path),
		]),
		output,
		true
	)
	if exit_code == 0:
		return ""
	var text := "\n".join(output).strip_edges()
	if text.length() > MAX_VERIFY_OUTPUT:
		text = text.left(MAX_VERIFY_OUTPUT) + "..."
	return "生成产物校验失败（退出码 %d）：%s" % [exit_code, text]


func _detect_python() -> String:
	var candidates := PackedStringArray()
	var settings := EditorInterface.get_editor_settings()
	if settings.has_setting(PYTHON_SETTING):
		var configured := str(settings.get_setting(PYTHON_SETTING)).strip_edges()
		if not configured.is_empty():
			candidates.append(configured)
	candidates.append("python3")
	candidates.append("python")
	for executable in candidates:
		var output: Array = []
		if OS.execute(executable, PackedStringArray(["--version"]), output, true) != 0:
			continue
		if _is_supported_python("\n".join(output)):
			return executable
	return ""


func _is_supported_python(version_text: String) -> bool:
	var marker := version_text.find("Python ")
	if marker < 0:
		return false
	var version := version_text.substr(marker + 7).strip_edges().split(" ", false, 1)[0]
	var parts := version.split(".")
	if parts.size() < 2 or not parts[0].is_valid_int() or not parts[1].is_valid_int():
		return false
	return int(parts[0]) > 3 or (int(parts[0]) == 3 and int(parts[1]) >= 10)


func _report_error(message: String) -> void:
	var platform := get_export_platform()
	if platform != null:
		platform.add_message(EditorExportPlatform.EXPORT_MESSAGE_ERROR, "GoDo DataTable", message)
	else:
		push_error("[GoDo DataTable Export] %s" % message)


func _is_safe_relative_path(value: String) -> bool:
	if value.begins_with("/") or value.contains("\\") or value.contains("://"):
		return false
	for part in value.split("/", true):
		if part == "..":
			return false
	return true


func _is_same_or_child(path: String, parent: String) -> bool:
	var normalized_parent := parent.trim_suffix("/")
	return path == normalized_parent or path.begins_with(normalized_parent + "/")
