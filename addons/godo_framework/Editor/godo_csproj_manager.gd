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
	if content.is_empty() or not _is_valid_project(project_path):
		return _result("invalid_project", false, false, "无法解析 %s。" % project_path.get_file(), project_path)
	var closing_index := content.rfind("</Project>")
	if closing_index < 0:
		return _result("invalid_project", false, false, "%s 缺少标准结束标签。" % project_path.get_file(), project_path)

	var capabilities := installed if not installed.is_empty() else _detect_integrations(project_root)
	var missing := PackedStringArray()
	var conflicts := PackedStringArray()
	var additions := PackedStringArray()
	_check_sdk_and_target(content, conflicts)
	if capabilities.get("guide", false):
		_check_rule(content, "GUIDE 属性", "GoDoIncludeGuideInput", ["<GoDoIncludeGuideInput Condition=\"'$(GoDoIncludeGuideInput)' == '' and Exists('addons/guideCS/plugin.cfg') and Exists('addons/guideCS/guide/plugin.cfg')\">true</GoDoIncludeGuideInput>"], GUIDE_PROPERTY, missing, conflicts, additions)
		_check_rule(content, "GUIDE 排除规则", "addons/godo_framework/Integrations/GuideInput/**/*.cs", ["<ItemGroup Condition=\"'$(GoDoIncludeGuideInput)' != 'true'\">", "<Compile Remove=\"addons/guideCS/**/*.cs\" />", "<Compile Remove=\"addons/godo_framework/Integrations/GuideInput/**/*.cs\" />"], GUIDE_GROUP, missing, conflicts, additions)
	if capabilities.get("phantom", false):
		_check_rule(content, "Phantom Camera 属性", "GoDoIncludePhantomCamera", ["<GoDoIncludePhantomCamera Condition=\"'$(GoDoIncludePhantomCamera)' == '' and Exists('addons/phantom_camera/plugin.cfg')\">true</GoDoIncludePhantomCamera>"], PHANTOM_PROPERTY, missing, conflicts, additions)
		_check_rule(content, "Phantom Camera 排除规则", "addons/godo_framework/Integrations/PhantomCamera/**/*.cs", ["<ItemGroup Condition=\"'$(GoDoIncludePhantomCamera)' != 'true'\">", "<Compile Remove=\"addons/phantom_camera/**/*.cs\" />", "<Compile Remove=\"addons/godo_framework/Integrations/PhantomCamera/**/*.cs\" />"], PHANTOM_GROUP, missing, conflicts, additions)
	if capabilities.get("friflo", false):
		_check_rule(content, "Friflo ECS 属性", "GoDoIncludeFrifloEcs", ["<GoDoIncludeFrifloEcs Condition=\"'$(GoDoIncludeFrifloEcs)' == '' and Exists('addons/godo_framework/Integrations/FrifloEcs/Runtime/EcsWorldHost.cs')\">true</GoDoIncludeFrifloEcs>"], FRIFLO_PROPERTY, missing, conflicts, additions)
		_check_rule(content, "Friflo ECS 排除规则", "addons/godo_framework/Integrations/FrifloEcs/**/*.cs", ["<ItemGroup Condition=\"'$(GoDoIncludeFrifloEcs)' != 'true'\">", "<Compile Remove=\"addons/godo_framework/Integrations/FrifloEcs/**/*.cs\" />"], FRIFLO_GROUP, missing, conflicts, additions)
	if capabilities.get("debugger", false):
		_check_rule(content, "Release Debugger 裁剪", "addons/godo_framework/Debugger/DebuggerOverlay.cs", ["<ItemGroup Condition=\"'$(Configuration)' == 'Release' or '$(Configuration)' == 'ExportRelease'\">", "<Compile Remove=\"addons/godo_framework/Debugger/DebuggerOverlay.cs\" />"], RELEASE_GROUP, missing, conflicts, additions)

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


func _check_sdk_and_target(content: String, conflicts: PackedStringArray) -> void:
	if not content.contains("Godot.NET.Sdk/"):
		conflicts.append("SDK 不是可识别的 Godot.NET.Sdk（只检查不修改）")
	if not content.contains("<TargetFramework>"):
		conflicts.append("缺少 TargetFramework（只检查不修改）")


func _check_rule(content: String, label: String, marker: String, expected_fragments: Array, block: String, missing: PackedStringArray, conflicts: PackedStringArray, additions: PackedStringArray) -> void:
	if marker in content:
		var compact_content := _compact(content)
		for fragment in expected_fragments:
			if _compact(str(fragment)) not in compact_content:
				conflicts.append("%s 已存在但不是 GoDo 默认规则（保持只读）" % label)
				return
		return
	missing.append(label)
	additions.append(block)


func _compact(value: String) -> String:
	return value.replace(" ", "").replace("\t", "").replace("\r", "").replace("\n", "")


func _is_valid_project(path: String) -> bool:
	var parser := XMLParser.new()
	if parser.open(path) != OK:
		return false
	while true:
		var error := parser.read()
		if error == ERR_FILE_EOF:
			return true
		if error != OK:
			return false
	return false


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
