@tool
extends RefCounted

const BACKUP_SUFFIX := ".godo-backup"

const GUIDE_PROPERTY := "  <PropertyGroup>\n    <GoDoIncludeGuideInput Condition=\"'$(GoDoIncludeGuideInput)' == '' and Exists('addons/guideCS/plugin.cfg') and Exists('addons/guideCS/guide/plugin.cfg')\">true</GoDoIncludeGuideInput>\n  </PropertyGroup>"
const PHANTOM_PROPERTY := "  <PropertyGroup>\n    <GoDoIncludePhantomCamera Condition=\"'$(GoDoIncludePhantomCamera)' == '' and Exists('addons/phantom_camera/plugin.cfg')\">true</GoDoIncludePhantomCamera>\n  </PropertyGroup>"
const FRIFLO_PROPERTY := "  <PropertyGroup>\n    <GoDoIncludeFrifloEcs Condition=\"'$(GoDoIncludeFrifloEcs)' == '' and Exists('addons/godo_framework/Integrations/FrifloEcs/Runtime/EcsWorldHost.cs')\">true</GoDoIncludeFrifloEcs>\n  </PropertyGroup>"

const GUIDE_GROUP := "  <ItemGroup Condition=\"'$(GoDoIncludeGuideInput)' != 'true'\">\n    <Compile Remove=\"addons/guideCS/**/*.cs\" />\n    <Compile Remove=\"addons/godo_framework/Integrations/GuideInput/**/*.cs\" />\n  </ItemGroup>"
const PHANTOM_GROUP := "  <ItemGroup Condition=\"'$(GoDoIncludePhantomCamera)' != 'true'\">\n    <Compile Remove=\"addons/phantom_camera/**/*.cs\" />\n    <Compile Remove=\"addons/godo_framework/Integrations/PhantomCamera/**/*.cs\" />\n  </ItemGroup>"
const FRIFLO_GROUP := "  <ItemGroup Condition=\"'$(GoDoIncludeFrifloEcs)' != 'true'\">\n    <Compile Remove=\"addons/godo_framework/Integrations/FrifloEcs/**/*.cs\" />\n  </ItemGroup>"
const RELEASE_GROUP := "  <ItemGroup Condition=\"'$(Configuration)' == 'Release' or '$(Configuration)' == 'ExportRelease'\">\n    <Compile Remove=\"addons/godo_framework/Debugger/DebuggerOverlay.cs\" />\n  </ItemGroup>"


func inspect(project_root: String = "res://", installed: Dictionary = {}) -> Dictionary:
	var project_files := _find_project_files(project_root)
	if project_files.is_empty():
		return _result("missing_project", false, false, "项目根目录没有 .csproj。")
	if project_files.size() > 1:
		return _result("multiple_projects", false, false, "项目根目录存在多个 .csproj。")
	if FileAccess.file_exists(project_root.path_join("Directory.Packages.props")):
		return _result("central_management", false, false, "检测到 Directory.Packages.props，项目配置保持只读。", project_files[0])

	var project_path: String = project_files[0]
	var content := FileAccess.get_file_as_string(project_path)
	var project_xml := _read_project_xml(project_path)
	if content.is_empty() or not project_xml.readable:
		return _result("invalid_project", false, false, "无法解析 %s。" % project_path.get_file(), project_path)
	var closing_index := content.rfind("</Project>")
	if closing_index < 0:
		return _result("invalid_project", false, false, "%s 缺少标准结束标签。" % project_path.get_file(), project_path)

	var capabilities := installed if not installed.is_empty() else _detect_integrations(project_root)
	var missing := PackedStringArray()
	var conflicts := PackedStringArray()
	var additions := PackedStringArray()
	_check_sdk_and_target(project_xml, conflicts)
	if capabilities.get("guide", false):
		_check_property_rule(project_xml, "GUIDE 属性", "GoDoIncludeGuideInput", "'$(GoDoIncludeGuideInput)' == '' and Exists('addons/guideCS/plugin.cfg') and Exists('addons/guideCS/guide/plugin.cfg')", GUIDE_PROPERTY, missing, conflicts, additions)
		_check_compile_rule(project_xml, "GUIDE 排除规则", "'$(GoDoIncludeGuideInput)' != 'true'", ["addons/guideCS/**/*.cs", "addons/godo_framework/Integrations/GuideInput/**/*.cs"], "addons/godo_framework/Integrations/GuideInput/**/*.cs", GUIDE_GROUP, missing, conflicts, additions)
	if capabilities.get("phantom", false):
		_check_property_rule(project_xml, "Phantom Camera 属性", "GoDoIncludePhantomCamera", "'$(GoDoIncludePhantomCamera)' == '' and Exists('addons/phantom_camera/plugin.cfg')", PHANTOM_PROPERTY, missing, conflicts, additions)
		_check_compile_rule(project_xml, "Phantom Camera 排除规则", "'$(GoDoIncludePhantomCamera)' != 'true'", ["addons/phantom_camera/**/*.cs", "addons/godo_framework/Integrations/PhantomCamera/**/*.cs"], "addons/godo_framework/Integrations/PhantomCamera/**/*.cs", PHANTOM_GROUP, missing, conflicts, additions)
	if capabilities.get("friflo", false):
		_check_property_rule(project_xml, "Friflo ECS 属性", "GoDoIncludeFrifloEcs", "'$(GoDoIncludeFrifloEcs)' == '' and Exists('addons/godo_framework/Integrations/FrifloEcs/Runtime/EcsWorldHost.cs')", FRIFLO_PROPERTY, missing, conflicts, additions)
		_check_compile_rule(project_xml, "Friflo ECS 排除规则", "'$(GoDoIncludeFrifloEcs)' != 'true'", ["addons/godo_framework/Integrations/FrifloEcs/**/*.cs"], "addons/godo_framework/Integrations/FrifloEcs/**/*.cs", FRIFLO_GROUP, missing, conflicts, additions)
	if capabilities.get("debugger", false):
		_check_compile_rule(project_xml, "Release Debugger 裁剪", "'$(Configuration)' == 'Release' or '$(Configuration)' == 'ExportRelease'", ["addons/godo_framework/Debugger/DebuggerOverlay.cs"], "addons/godo_framework/Debugger/DebuggerOverlay.cs", RELEASE_GROUP, missing, conflicts, additions)

	if not conflicts.is_empty():
		return _result("conflict", false, false, "需要人工处理：%s" % "；".join(conflicts), project_path, missing, additions)
	if missing.is_empty():
		return _result("ready", true, false, "GoDo 维护的项目配置已就绪。", project_path)
	return _result("missing_rules", false, true, "缺少：%s" % "、".join(missing), project_path, missing, additions)


func repair(project_root: String = "res://", installed: Dictionary = {}) -> Dictionary:
	var state := inspect(project_root, installed)
	if not state["can_repair"]:
		return _repair_result(false, "当前项目状态不允许自动修复。")
	var project_path: String = state["project_path"]
	var content := FileAccess.get_file_as_string(project_path)
	var closing_index := content.rfind("</Project>")
	var newline := "\r\n" if "\r\n" in content else "\n"
	var blocks: PackedStringArray = state["additions"]
	var normalized_blocks := PackedStringArray()
	for block in blocks:
		normalized_blocks.append(block.replace("\n", newline))
	var updated := content.substr(0, closing_index).rstrip("\r\n") + newline + newline
	updated += (newline + newline).join(normalized_blocks) + newline
	updated += content.substr(closing_index)

	var backup_path := _next_backup_path(project_path)
	var copy_error := DirAccess.copy_absolute(project_path, backup_path)
	if copy_error != OK:
		return _repair_result(false, "无法创建备份：%s" % error_string(copy_error))
	var file := FileAccess.open(project_path, FileAccess.WRITE)
	if file == null:
		DirAccess.copy_absolute(backup_path, project_path)
		return _repair_result(false, "无法写入项目文件；已保留备份。", backup_path)
	file.store_string(updated)
	file.close()

	var verified := inspect(project_root, installed)
	if verified["code"] != "ready":
		var restore_error := DirAccess.copy_absolute(backup_path, project_path)
		return _repair_result(false, "写入后复查失败，%s" % ("已恢复备份。" if restore_error == OK else "自动恢复失败，请使用备份。"), backup_path)
	return _repair_result(true, "GoDo 项目配置已补齐；请重新执行 build。", backup_path)


func _check_sdk_and_target(project_xml: Dictionary, conflicts: PackedStringArray) -> void:
	var sdk: String = project_xml.sdk
	if not sdk.begins_with("Godot.NET.Sdk/") or sdk.trim_prefix("Godot.NET.Sdk/").is_empty():
		conflicts.append("SDK 不是可识别的 Godot.NET.Sdk（只检查不修改）")
	if project_xml.target_frameworks.is_empty():
		conflicts.append("缺少 TargetFramework（只检查不修改）")


func _check_property_rule(project_xml: Dictionary, label: String, property_name: String, expected_condition: String, block: String, missing: PackedStringArray, conflicts: PackedStringArray, additions: PackedStringArray) -> void:
	var entries: Array = project_xml.properties.get(property_name.to_lower(), [])
	if entries.is_empty():
		missing.append(label)
		additions.append(block)
		return
	if entries.size() != 1 or _compact(entries[0].condition) != _compact(expected_condition) or entries[0].value.strip_edges().to_lower() != "true":
		conflicts.append("%s 已存在但不是 GoDo 默认规则（保持只读）" % label)


func _check_compile_rule(project_xml: Dictionary, label: String, expected_condition: String, expected_removes: Array, marker: String, block: String, missing: PackedStringArray, conflicts: PackedStringArray, additions: PackedStringArray) -> void:
	var marker_groups: Array = []
	for group in project_xml.item_groups:
		if marker in group.compile_removes:
			marker_groups.append(group)
	if marker_groups.is_empty():
		missing.append(label)
		additions.append(block)
		return
	var valid := marker_groups.size() == 1 and _compact(marker_groups[0].condition) == _compact(expected_condition)
	if valid:
		for remove_path in expected_removes:
			if remove_path not in marker_groups[0].compile_removes:
				valid = false
				break
	if not valid:
		conflicts.append("%s 已存在但不是 GoDo 默认规则（保持只读）" % label)


func _compact(value: String) -> String:
	return value.replace(" ", "").replace("\t", "").replace("\r", "").replace("\n", "")


func _read_project_xml(path: String) -> Dictionary:
	var result := {
		"readable": false,
		"sdk": "",
		"target_frameworks": PackedStringArray(),
		"properties": {},
		"item_groups": [],
	}
	var parser := XMLParser.new()
	if parser.open(path) != OK:
		return result
	var depth := 0
	var property_group_depth := -1
	var property_depth := -1
	var property := {}
	var item_group_depth := -1
	var item_group := {}
	while true:
		var read_error := parser.read()
		if read_error == ERR_FILE_EOF:
			break
		if read_error != OK:
			return result
		match parser.get_node_type():
			XMLParser.NODE_ELEMENT:
				depth += 1
				var node_name := parser.get_node_name().to_lower()
				if depth == 1:
					if node_name != "project":
						return result
					result.sdk = _attribute(parser, "Sdk").strip_edges()
				elif node_name == "propertygroup":
					property_group_depth = depth
				elif property_group_depth >= 0 and depth == property_group_depth + 1:
					property_depth = depth
					property = {
						"name": node_name,
						"condition": _attribute(parser, "Condition").strip_edges(),
						"value": "",
					}
				elif node_name == "itemgroup":
					item_group_depth = depth
					item_group = {
						"condition": _attribute(parser, "Condition").strip_edges(),
						"compile_removes": PackedStringArray(),
					}
				elif item_group_depth >= 0 and depth == item_group_depth + 1 and node_name == "compile":
					var remove_path := _attribute(parser, "Remove").strip_edges()
					if not remove_path.is_empty():
						item_group.compile_removes.append(remove_path)
				if parser.is_empty():
					if depth == property_depth:
						_append_property(result, property)
						property_depth = -1
						property = {}
					if depth == property_group_depth:
						property_group_depth = -1
					if depth == item_group_depth:
						result.item_groups.append(item_group)
						item_group_depth = -1
						item_group = {}
					depth -= 1
			XMLParser.NODE_TEXT:
				if property_depth >= 0:
					property.value += parser.get_node_data()
			XMLParser.NODE_ELEMENT_END:
				if depth == property_depth:
					_append_property(result, property)
					property_depth = -1
					property = {}
				if depth == property_group_depth:
					property_group_depth = -1
				if depth == item_group_depth:
					result.item_groups.append(item_group)
					item_group_depth = -1
					item_group = {}
				depth -= 1
	if depth != 0:
		return result
	result.readable = true
	return result


func _append_property(project_xml: Dictionary, property: Dictionary) -> void:
	if property.is_empty():
		return
	var property_name: String = property.name
	var entries: Array = project_xml.properties.get(property_name, [])
	entries.append({"condition": property.condition, "value": property.value.strip_edges()})
	project_xml.properties[property_name] = entries
	if property_name == "targetframework" and not property.value.strip_edges().is_empty():
		project_xml.target_frameworks.append(property.value.strip_edges())


func _attribute(parser: XMLParser, name: String) -> String:
	for index in parser.get_attribute_count():
		if parser.get_attribute_name(index).to_lower() == name.to_lower():
			return parser.get_attribute_value(index)
	return ""


func _detect_integrations(project_root: String) -> Dictionary:
	return {
		"guide": FileAccess.file_exists(project_root.path_join("addons/godo_framework/Integrations/GuideInput/Runtime/GuideInputBackend.cs")),
		"phantom": FileAccess.file_exists(project_root.path_join("addons/godo_framework/Integrations/PhantomCamera/Runtime/PhantomCameraRig.cs")),
		"friflo": FileAccess.file_exists(project_root.path_join("addons/godo_framework/Integrations/FrifloEcs/Runtime/EcsWorldHost.cs")),
		"debugger": FileAccess.file_exists(project_root.path_join("addons/godo_framework/Debugger/DebuggerOverlay.cs")),
	}


func _find_project_files(project_root: String) -> PackedStringArray:
	var result := PackedStringArray()
	for file_name in DirAccess.get_files_at(project_root):
		if file_name.to_lower().ends_with(".csproj"):
			result.append(project_root.path_join(file_name))
	result.sort()
	return result


func _next_backup_path(project_path: String) -> String:
	var base_path := project_path + BACKUP_SUFFIX
	if not FileAccess.file_exists(base_path):
		return base_path
	var index := 1
	while FileAccess.file_exists("%s.%d" % [base_path, index]):
		index += 1
	return "%s.%d" % [base_path, index]


func _result(code: String, healthy: bool, can_repair: bool, detail: String, project_path: String = "", missing: PackedStringArray = PackedStringArray(), additions: PackedStringArray = PackedStringArray()) -> Dictionary:
	return {"code": code, "healthy": healthy, "can_repair": can_repair, "detail": detail, "project_path": project_path, "missing": missing, "additions": additions}


func _repair_result(success: bool, detail: String, backup_path: String = "") -> Dictionary:
	return {"success": success, "detail": detail, "backup_path": backup_path}
