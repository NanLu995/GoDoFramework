@tool
extends RefCounted

const PACKAGE_ID := "Friflo.Engine.ECS"
const REQUIRED_VERSION := "3.6.0"
const CENTRAL_PACKAGES_FILE := "Directory.Packages.props"


func inspect(project_root: String = "res://") -> Dictionary:
	var project_files := _find_project_files(project_root)
	if project_files.is_empty():
		return _result("missing_project", false, "项目根目录没有 .csproj。")
	if project_files.size() > 1:
		return _result(
			"multiple_projects",
			false,
			"项目根目录存在多个 .csproj，无法确定 Godot 使用的目标项目。"
		)

	var project_path: String = project_files[0]
	var central_path := project_root.path_join(CENTRAL_PACKAGES_FILE)
	var has_central_file := FileAccess.file_exists(central_path)
	var project_result := _find_item_version(project_path, "PackageReference", PACKAGE_ID)
	if not project_result["readable"]:
		return _result("invalid_project", false, "无法解析 %s。" % project_path.get_file(), project_path)
	if not project_result["found"]:
		if has_central_file:
			return _result(
				"missing_reference_central",
				false,
				"项目使用 %s；请按中央包管理约定手工添加依赖。" % CENTRAL_PACKAGES_FILE,
				project_path
			)
		return _result(
			"missing_reference",
			false,
			"%s 未引用 %s。" % [project_path.get_file(), PACKAGE_ID],
			project_path,
			"",
			true
		)

	var version: String = project_result["version"]
	var source := project_path.get_file()
	if version.is_empty():
		if has_central_file:
			var central_result := _find_item_version(central_path, "PackageVersion", PACKAGE_ID)
			if not central_result["readable"]:
				return _result(
					"invalid_central_file",
					false,
					"无法解析 %s。" % CENTRAL_PACKAGES_FILE,
					project_path
				)
			if central_result["found"]:
				version = central_result["version"]
				source = CENTRAL_PACKAGES_FILE

	if version.is_empty() or "$" in version:
		return _result(
			"unknown_version",
			false,
			"已找到 %s，但版本为空或使用 MSBuild 变量，需人工确认。" % PACKAGE_ID,
			project_path,
			version
		)
	if version != REQUIRED_VERSION:
		return _result(
			"version_mismatch",
			false,
			"%s 声明版本 %s；GoDo 当前验证版本为 %s。" % [source, version, REQUIRED_VERSION],
			project_path,
			version
		)

	var condition: String = project_result["condition"]
	var condition_detail := ""
	if not condition.is_empty():
		condition_detail = " 引用带有 Condition，请确保目标构建会启用该条件。"
	return _result(
		"ready",
		true,
		"%s 已声明 %s %s。%s" % [source, PACKAGE_ID, REQUIRED_VERSION, condition_detail],
		project_path,
		version
	)


func _find_project_files(project_root: String) -> PackedStringArray:
	var result := PackedStringArray()
	for file_name in DirAccess.get_files_at(project_root):
		if file_name.to_lower().ends_with(".csproj"):
			result.append(project_root.path_join(file_name))
	result.sort()
	return result


func _find_item_version(path: String, item_name: String, package_id: String) -> Dictionary:
	var parser := XMLParser.new()
	if parser.open(path) != OK:
		return {"readable": false, "found": false, "version": "", "condition": ""}

	var depth := 0
	var item_group_depth := -1
	var item_group_condition := ""
	var item_depth := -1
	var version_depth := -1
	var found := false
	var version := ""
	var condition := ""
	while true:
		var read_error := parser.read()
		if read_error == ERR_FILE_EOF:
			break
		if read_error != OK:
			return {"readable": false, "found": false, "version": "", "condition": ""}

		match parser.get_node_type():
			XMLParser.NODE_ELEMENT:
				depth += 1
				var node_name := parser.get_node_name()
				if node_name.to_lower() == "itemgroup":
					item_group_depth = depth
					item_group_condition = _attribute(parser, "Condition").strip_edges()
				if item_depth < 0 and node_name.to_lower() == item_name.to_lower():
					var include := _attribute(parser, "Include")
					if include.is_empty():
						include = _attribute(parser, "Update")
					if include.to_lower() == package_id.to_lower():
						found = true
						item_depth = depth
						version = _attribute(parser, "Version").strip_edges()
						condition = _attribute(parser, "Condition").strip_edges()
						if condition.is_empty():
							condition = item_group_condition
				elif item_depth >= 0 and depth == item_depth + 1 and node_name.to_lower() == "version":
					version_depth = depth

				if parser.is_empty():
					if depth == item_depth:
						item_depth = -1
					if depth == version_depth:
						version_depth = -1
					if depth == item_group_depth:
						item_group_depth = -1
						item_group_condition = ""
					depth -= 1
			XMLParser.NODE_TEXT:
				if version_depth >= 0:
					version += parser.get_node_data().strip_edges()
			XMLParser.NODE_ELEMENT_END:
				if depth == version_depth:
					version_depth = -1
				if depth == item_depth:
					item_depth = -1
				if depth == item_group_depth:
					item_group_depth = -1
					item_group_condition = ""
				depth -= 1

	if depth != 0:
		return {"readable": false, "found": false, "version": "", "condition": ""}

	return {
		"readable": true,
		"found": found,
		"version": version.strip_edges(),
		"condition": condition,
	}


func _attribute(parser: XMLParser, name: String) -> String:
	for index in parser.get_attribute_count():
		if parser.get_attribute_name(index).to_lower() == name.to_lower():
			return parser.get_attribute_value(index)
	return ""


func _result(
	code: String,
	healthy: bool,
	detail: String,
	project_path: String = "",
	version: String = "",
	can_install: bool = false
) -> Dictionary:
	return {
		"code": code,
		"healthy": healthy,
		"detail": detail,
		"project_path": project_path,
		"version": version,
		"can_install": can_install,
	}
