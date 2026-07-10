@tool
extends EditorPlugin

const TOOL_MENU_NAME := "GoDo Framework"
const MENU_SETUP_ID := 1
const MENU_VALIDATE_MANIFEST_ID := 100
const AUTOLOAD_NAME := "GoDoRuntime"
const AUTOLOAD_SETTING := "autoload/GoDoRuntime"
const RUNTIME_SCENE_PATH := "res://addons/godo_framework/Core/GoDoRuntime.tscn"
const RUNTIME_SCRIPT_PATH := "res://addons/godo_framework/Core/GoDoRuntime.cs"
const FRAMEWORK_PATH := "res://addons/godo_framework"
const NORMAL_COLOR := Color("#8BD49C")
const PENDING_COLOR := Color("#AEB6C2")
const WARNING_COLOR := Color("#FFD166")
const ERROR_COLOR := Color("#FF6B6B")

enum HealthLevel {
	NORMAL,
	PENDING,
	WARNING,
	ERROR,
}

var _setup_dialog: AcceptDialog
var _uninstall_dialog: ConfirmationDialog
var _manifest_file_dialog: EditorFileDialog
var _manifest_report_dialog: AcceptDialog
var _manifest_report_label: RichTextLabel
var _tool_menu: PopupMenu
var _resources_menu: PopupMenu
var _content: VBoxContainer
var _report_label: RichTextLabel
var _message_label: RichTextLabel
var _install_button: Button
var _uninstall_button: Button


func _enter_tree() -> void:
	_create_dialogs()
	_create_tool_menu()
	add_tool_submenu_item(TOOL_MENU_NAME, _tool_menu)


func _exit_tree() -> void:
	remove_tool_menu_item(TOOL_MENU_NAME)
	if is_instance_valid(_tool_menu):
		_tool_menu.queue_free()
	if is_instance_valid(_setup_dialog):
		_setup_dialog.queue_free()
	if is_instance_valid(_uninstall_dialog):
		_uninstall_dialog.queue_free()
	if is_instance_valid(_manifest_file_dialog):
		_manifest_file_dialog.queue_free()
	if is_instance_valid(_manifest_report_dialog):
		_manifest_report_dialog.queue_free()


func _create_tool_menu() -> void:
	_tool_menu = PopupMenu.new()
	_tool_menu.name = "GoDoFrameworkToolMenu"
	_tool_menu.add_item("设置 (Setup)...", MENU_SETUP_ID)
	_tool_menu.add_separator()
	_resources_menu = PopupMenu.new()
	_resources_menu.name = "Resources"
	_resources_menu.add_item("校验资源清单 (Validate Resource Manifest)...", MENU_VALIDATE_MANIFEST_ID)
	_resources_menu.id_pressed.connect(_on_resources_menu_id_pressed)
	_tool_menu.add_submenu_node_item("资源 (Resources)", _resources_menu)
	_tool_menu.id_pressed.connect(_on_tool_menu_id_pressed)


func _create_dialogs() -> void:
	_setup_dialog = AcceptDialog.new()
	_setup_dialog.title = "GoDo Framework"
	_setup_dialog.ok_button_text = "关闭"
	_setup_dialog.min_size = Vector2i(620, 360)
	_setup_dialog.get_label().hide()
	_create_content()
	var check_button := _setup_dialog.add_button("重新检查", true)
	_install_button = _setup_dialog.add_button("安装 Runtime", true)
	_uninstall_button = _setup_dialog.add_button("卸载 Runtime", true)
	check_button.pressed.connect(_on_check_pressed)
	_install_button.pressed.connect(_on_install_pressed)
	_uninstall_button.pressed.connect(_on_uninstall_pressed)
	get_editor_interface().get_base_control().add_child(_setup_dialog)

	_uninstall_dialog = ConfirmationDialog.new()
	_uninstall_dialog.title = "卸载 GoDoRuntime"
	_uninstall_dialog.dialog_text = "只会移除正确匹配的 GoDoRuntime Autoload，不会删除任何框架或业务文件。是否继续？"
	_uninstall_dialog.ok_button_text = "卸载"
	_uninstall_dialog.cancel_button_text = "取消"
	_uninstall_dialog.confirmed.connect(_on_uninstall_confirmed)
	_uninstall_dialog.canceled.connect(_on_uninstall_canceled)
	get_editor_interface().get_base_control().add_child(_uninstall_dialog)

	_manifest_file_dialog = EditorFileDialog.new()
	_manifest_file_dialog.title = "Validate Resource Manifest"
	_manifest_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	_manifest_file_dialog.access = FileDialog.ACCESS_RESOURCES
	_manifest_file_dialog.filters = PackedStringArray(["*.tres,*.res;Resource files"])
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.file_selected.connect(_on_manifest_file_selected)
	get_editor_interface().get_base_control().add_child(_manifest_file_dialog)

	_manifest_report_dialog = AcceptDialog.new()
	_manifest_report_dialog.title = "Resource Manifest Validation"
	_manifest_report_dialog.ok_button_text = "关闭"
	_manifest_report_dialog.min_size = Vector2i(720, 420)
	_manifest_report_dialog.get_label().hide()
	_manifest_report_label = RichTextLabel.new()
	_manifest_report_label.name = "ManifestReportLabel"
	_manifest_report_label.bbcode_enabled = false
	_manifest_report_label.selection_enabled = true
	_manifest_report_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_manifest_report_dialog.add_child(_manifest_report_label)
	_manifest_report_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_manifest_report_label.offset_left = 16
	_manifest_report_label.offset_top = 16
	_manifest_report_label.offset_right = -16
	_manifest_report_label.offset_bottom = -48
	get_editor_interface().get_base_control().add_child(_manifest_report_dialog)


func _create_content() -> void:
	_content = _setup_dialog.get_node_or_null("Content") as VBoxContainer
	if is_instance_valid(_content):
		_report_label = _content.get_node_or_null("ReportLabel") as RichTextLabel
		_message_label = _content.get_node_or_null("MessageLabel") as RichTextLabel
		return

	for child in _setup_dialog.get_children():
		if child is RichTextLabel:
			child.hide()

	_content = VBoxContainer.new()
	_content.name = "Content"
	_setup_dialog.add_child(_content)
	_content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_content.offset_left = 16
	_content.offset_top = 16
	_content.offset_right = -16
	_content.offset_bottom = -64
	_content.add_theme_constant_override("separation", 10)

	_report_label = RichTextLabel.new()
	_report_label.name = "ReportLabel"
	_report_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_report_label.bbcode_enabled = false
	_report_label.selection_enabled = true
	_content.add_child(_report_label)

	_message_label = RichTextLabel.new()
	_message_label.name = "MessageLabel"
	_message_label.custom_minimum_size.y = 44
	_message_label.bbcode_enabled = false
	_message_label.scroll_active = false
	_message_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_content.add_child(_message_label)


func _ensure_content() -> bool:
	if not is_instance_valid(_setup_dialog):
		return false
	if not is_instance_valid(_report_label) or not is_instance_valid(_message_label):
		_create_content()
	return is_instance_valid(_report_label) and is_instance_valid(_message_label)


func _on_tool_menu_id_pressed(id: int) -> void:
	if id == MENU_SETUP_ID:
		_open_setup_dialog()


func _on_resources_menu_id_pressed(id: int) -> void:
	if id == MENU_VALIDATE_MANIFEST_ID:
		_open_manifest_file_dialog()


func _open_setup_dialog() -> void:
	var report := _refresh_report()
	_show_status_advice(report)
	_setup_dialog.popup_centered(Vector2i(620, 360))


func _open_manifest_file_dialog() -> void:
	if not is_instance_valid(_manifest_file_dialog):
		return
	_manifest_file_dialog.current_path = "res://"
	_manifest_file_dialog.popup_centered(Vector2i(720, 480))


func _on_manifest_file_selected(path: String) -> void:
	var report := _validate_manifest(path)
	_render_manifest_report(path, report)
	_manifest_report_dialog.popup_centered(Vector2i(720, 420))


func _validate_manifest(path: String) -> Dictionary:
	var report := {
		"items": [],
		"level": HealthLevel.NORMAL,
		"entry_count": 0,
	}
	var resource := ResourceLoader.load(path)
	if resource == null:
		_add_item(report, HealthLevel.ERROR, "清单加载", "无法加载 %s" % path)
		return report

	var entries = resource.get("Entries")
	if entries == null:
		entries = resource.get("entries")
	if entries == null or not (entries is Array):
		_add_item(report, HealthLevel.ERROR, "清单类型", "缺少 Entries 数组，请选择 ResourceManifest 资源")
		return report

	report.entry_count = entries.size()
	if entries.is_empty():
		_add_item(report, HealthLevel.WARNING, "清单内容", "Entries 为空")
		return report

	var seen_ids := {}
	for index in range(entries.size()):
		var entry = entries[index]
		if entry == null or not (entry is Object):
			_add_item(report, HealthLevel.ERROR, "Entry %d" % index, "条目为空或不是 ResourceManifestEntry")
			continue

		var entry_id := _get_exported_string(entry, "Id", "id").strip_edges()
		var locator := _get_exported_string(entry, "Locator", "locator").strip_edges()
		var label := "Entry %d" % index
		if not entry_id.is_empty():
			label = "%s (%s)" % [label, entry_id]

		if entry_id.is_empty():
			_add_item(report, HealthLevel.ERROR, label, "Id 为空")
		elif seen_ids.has(entry_id):
			_add_item(report, HealthLevel.ERROR, label, "Id 与 Entry %d 重复" % seen_ids[entry_id])
		else:
			seen_ids[entry_id] = index

		if locator.is_empty():
			_add_item(report, HealthLevel.ERROR, label, "Locator 为空")
		elif not (locator.begins_with("res://") or locator.begins_with("uid://")):
			_add_item(report, HealthLevel.ERROR, label, "Locator 必须以 res:// 或 uid:// 开头：%s" % locator)
		elif not ResourceLoader.exists(locator):
			_add_item(report, HealthLevel.WARNING, label, "Locator 当前无法解析到资源：%s" % locator)

	if report.level == HealthLevel.NORMAL:
		_add_item(report, HealthLevel.NORMAL, "清单内容", "%d 个条目通过校验" % report.entry_count)
	return report


func _get_exported_string(target: Object, primary_name: String, fallback_name: String) -> String:
	var value = target.get(primary_name)
	if value == null:
		value = target.get(fallback_name)
	return str(value) if value != null else ""


func _render_manifest_report(path: String, report: Dictionary) -> void:
	if not is_instance_valid(_manifest_report_label):
		return
	_manifest_report_label.clear()
	_manifest_report_label.push_font_size(18)
	_manifest_report_label.add_text("ResourceManifest 校验：")
	_manifest_report_label.push_color(_level_color(report.level))
	_manifest_report_label.add_text(_level_name(report.level))
	_manifest_report_label.pop()
	_manifest_report_label.pop()
	_manifest_report_label.add_text("\n\n文件：%s\n条目数：%d\n\n" % [path, report.entry_count])
	for item in report.items:
		_manifest_report_label.push_color(_level_color(item.level))
		_manifest_report_label.add_text("[%s]" % _level_name(item.level))
		_manifest_report_label.pop()
		_manifest_report_label.add_text(" %s：%s\n" % [item.name, item.message])


func _on_check_pressed() -> void:
	var report := _refresh_report()
	var status := _framework_status(report)
	_show_message("检查已刷新。%s" % status.advice, status.level)


func _on_install_pressed() -> void:
	var report := _check_health()
	if report.autoload_healthy and not report.has_duplicate:
		_show_message("GoDoRuntime 已正确安装，无需重复操作。", HealthLevel.NORMAL)
		return
	if not _can_install(report):
		_show_message("当前状态不能安全安装，请先处理上方检查项。", HealthLevel.ERROR)
		return

	add_autoload_singleton(AUTOLOAD_NAME, RUNTIME_SCENE_PATH)
	var result := _refresh_report()
	_show_message(
		"GoDoRuntime 安装成功。" if result.autoload_healthy
		else "安装调用已完成，但复查未通过，请查看上方检查结果和编辑器输出。",
		HealthLevel.NORMAL if result.autoload_healthy else HealthLevel.ERROR
	)


func _on_uninstall_pressed() -> void:
	var report := _check_health()
	if not report.autoload_healthy:
		_refresh_report()
		_show_message("当前 GoDoRuntime Autoload 不存在或路径不匹配，插件不会执行卸载。", HealthLevel.ERROR)
		return
	if not report.project_config_readable:
		_show_message("无法读取 project.godot，插件不会执行卸载。", HealthLevel.ERROR)
		return
	_setup_dialog.hide()
	_show_uninstall_dialog.call_deferred()


func _show_uninstall_dialog() -> void:
	_uninstall_dialog.popup_centered()


func _on_uninstall_canceled() -> void:
	_open_setup_dialog.call_deferred()


func _on_uninstall_confirmed() -> void:
	var report := _check_health()
	if not report.project_config_readable or not report.autoload_healthy:
		_show_message("项目配置或 Autoload 状态已变化，已取消卸载。", HealthLevel.ERROR)
		_show_setup_dialog.call_deferred()
		return

	remove_autoload_singleton(AUTOLOAD_NAME)
	var result := _refresh_report()
	_show_message(
		"GoDoRuntime Autoload 已卸载，框架文件未删除。" if result.autoload_missing
		else "卸载调用已完成，但复查未通过，请查看上方检查结果和编辑器输出。",
		HealthLevel.NORMAL if result.autoload_missing else HealthLevel.ERROR
	)
	_show_setup_dialog.call_deferred()


func _show_setup_dialog() -> void:
	_setup_dialog.popup_centered(Vector2i(620, 360))


func _refresh_report() -> Dictionary:
	var report := _check_health()
	_render_report(report)
	_install_button.disabled = not _can_install(report)
	_uninstall_button.disabled = not report.autoload_healthy or not report.project_config_readable
	return report


func _check_health() -> Dictionary:
	var report := {
		"items": [],
		"level": HealthLevel.NORMAL,
		"csharp_ready": false,
		"runtime_scene_valid": false,
		"version_supported": false,
		"autoload_missing": false,
		"autoload_healthy": false,
		"has_name_conflict": false,
		"has_duplicate": false,
		"project_config_readable": false,
	}
	_check_version(report)
	_check_runtime_scene(report)
	_check_csharp_environment(report)
	_check_autoload(report)
	return report


func _check_version(report: Dictionary) -> void:
	var version := Engine.get_version_info()
	var major: int = version.major
	var minor: int = version.minor
	report.version_supported = major == 4 and minor >= 7
	_add_item(
		report,
		HealthLevel.NORMAL if report.version_supported else HealthLevel.ERROR,
		"Godot 版本",
		"%d.%d" % [major, minor] if report.version_supported
		else "当前为 %d.%d，需要 Godot 4.7 或更高的 4.x 版本" % [major, minor]
	)


func _check_csharp_environment(report: Dictionary) -> void:
	var project_files := PackedStringArray()
	for file_name in DirAccess.get_files_at("res://"):
		if file_name.get_extension().to_lower() == "csproj":
			project_files.append(file_name)

	if project_files.is_empty():
		_add_item(report, HealthLevel.ERROR, "C# 环境", "尚未创建 C# 项目，请先创建 C# 解决方案并编译一次")
		return
	if project_files.size() > 1:
		_add_item(report, HealthLevel.ERROR, "C# 环境", "根目录存在多个 .csproj，无法确定当前项目程序集")
		return
	var project_file := project_files[0]

	var assembly_name := str(ProjectSettings.get_setting("dotnet/project/assembly_name", ""))
	if assembly_name.is_empty():
		_add_item(report, HealthLevel.ERROR, "C# 环境", "缺少程序集名称，请重新创建 C# 解决方案并编译一次")
		return

	var assembly_path := "res://.godot/mono/temp/bin/Debug/%s.dll" % assembly_name
	if not FileAccess.file_exists(assembly_path):
		_add_item(report, HealthLevel.ERROR, "C# 环境", "已找到 %s，但尚未生成程序集，请先编译一次" % project_file)
		return
	if not FileAccess.file_exists(RUNTIME_SCRIPT_PATH):
		_add_item(report, HealthLevel.ERROR, "C# 环境", "缺少 %s" % RUNTIME_SCRIPT_PATH)
		return
	if FileAccess.get_modified_time(assembly_path) < _get_latest_csharp_modified_time(FRAMEWORK_PATH):
		_add_item(report, HealthLevel.ERROR, "C# 环境", "框架源码比程序集更新，请重新编译")
		return

	report.csharp_ready = true
	_add_item(report, HealthLevel.NORMAL, "C# 环境", "%s，程序集已生成且不早于框架源码" % project_file)


func _get_latest_csharp_modified_time(path: String) -> int:
	var latest_time := 0
	for file_name in DirAccess.get_files_at(path):
		if file_name.get_extension().to_lower() == "cs":
			latest_time = maxi(latest_time, FileAccess.get_modified_time(path.path_join(file_name)))
	for directory_name in DirAccess.get_directories_at(path):
		latest_time = maxi(
			latest_time,
			_get_latest_csharp_modified_time(path.path_join(directory_name))
		)
	return latest_time


func _check_runtime_scene(report: Dictionary) -> void:
	if not ResourceLoader.exists(RUNTIME_SCENE_PATH):
		_add_item(report, HealthLevel.ERROR, "Runtime 场景", "缺少 %s" % RUNTIME_SCENE_PATH)
		return
	report.runtime_scene_valid = true
	_add_item(report, HealthLevel.NORMAL, "Runtime 场景", "资源存在")


func _check_autoload(report: Dictionary) -> void:
	report.autoload_missing = not ProjectSettings.has_setting(AUTOLOAD_SETTING)
	if not report.autoload_missing:
		var actual_path := _normalize_autoload_path(
			str(ProjectSettings.get_setting(AUTOLOAD_SETTING))
		)
		report.autoload_healthy = actual_path == RUNTIME_SCENE_PATH
		report.has_name_conflict = not report.autoload_healthy

	var project_config := ConfigFile.new()
	var load_error := project_config.load("res://project.godot")
	if load_error != OK:
		_add_item(report, HealthLevel.ERROR, "框架唯一性", "无法读取 project.godot：%s" % error_string(load_error))
	else:
		report.project_config_readable = true
		if project_config.has_section("autoload"):
			for autoload_name in project_config.get_section_keys("autoload"):
				if autoload_name == AUTOLOAD_NAME:
					continue
				var path := _normalize_autoload_path(str(project_config.get_value("autoload", autoload_name)))
				if path != RUNTIME_SCENE_PATH:
					continue
				report.has_duplicate = true
				_add_item(report, HealthLevel.ERROR, "框架唯一性", "%s 也指向 %s" % [autoload_name, RUNTIME_SCENE_PATH])

		if not report.has_duplicate:
			if report.autoload_healthy:
				_add_item(report, HealthLevel.NORMAL, "框架唯一性", "已确认唯一注册")
			else:
				_add_item(report, HealthLevel.PENDING, "框架唯一性", "预检查通过，安装后再次确认")

	_add_autoload_status(report)


func _add_autoload_status(report: Dictionary) -> void:
	if report.autoload_missing:
		_add_item(report, HealthLevel.WARNING, "Autoload", "尚未安装 GoDoRuntime")
		return
	var actual_path := _normalize_autoload_path(
		str(ProjectSettings.get_setting(AUTOLOAD_SETTING))
	)
	_add_item(
		report,
		HealthLevel.NORMAL if report.autoload_healthy else HealthLevel.ERROR,
		"Autoload",
		"%s → %s" % [AUTOLOAD_NAME, RUNTIME_SCENE_PATH] if report.autoload_healthy
		else "名称 %s 已指向其他路径：%s" % [AUTOLOAD_NAME, actual_path]
	)


func _add_item(report: Dictionary, level: HealthLevel, name: String, message: String) -> void:
	report.items.append({"level": level, "name": name, "message": message})
	report.level = max(report.level, level)


func _can_install(report: Dictionary) -> bool:
	return (
		report.project_config_readable
		and report.csharp_ready
		and report.runtime_scene_valid
		and report.version_supported
		and report.autoload_missing
		and not report.has_name_conflict
		and not report.has_duplicate
	)


func _normalize_autoload_path(path: String) -> String:
	return path.trim_prefix("*")


func _render_report(report: Dictionary) -> void:
	if not _ensure_content():
		return
	_report_label.clear()
	var status := _framework_status(report)
	_report_label.push_font_size(18)
	_report_label.add_text("当前状态：")
	_report_label.push_color(_level_color(status.level))
	_report_label.add_text(status.name)
	_report_label.pop()
	_report_label.pop()
	_report_label.add_text("\n\n")
	for item in report.items:
		_report_label.push_color(_level_color(item.level))
		_report_label.add_text("[%s]" % _level_name(item.level))
		_report_label.pop()
		_report_label.add_text(" %s：%s\n" % [item.name, item.message])


func _framework_status(report: Dictionary) -> Dictionary:
	if not report.version_supported:
		return {"name": "Godot 版本不兼容", "level": HealthLevel.ERROR, "advice": "请使用 Godot 4.7 或更高的 4.x 版本。"}
	if not report.runtime_scene_valid:
		return {"name": "框架资源缺失", "level": HealthLevel.ERROR, "advice": "请重新复制完整的框架文件。"}
	if not report.csharp_ready:
		return {"name": "等待 C# 编译", "level": HealthLevel.WARNING, "advice": "请根据 C# 环境检查结果完成编译。"}
	if not report.project_config_readable:
		return {"name": "项目配置不可读", "level": HealthLevel.ERROR, "advice": "请先修复 project.godot。"}
	if report.has_name_conflict:
		return {"name": "Autoload 名称冲突", "level": HealthLevel.ERROR, "advice": "请先处理已有的 GoDoRuntime Autoload。"}
	if report.has_duplicate:
		return {"name": "框架被重复注册", "level": HealthLevel.ERROR, "advice": "请手动处理指向同一 Runtime 场景的其他 Autoload。"}
	if report.autoload_healthy:
		return {"name": "已正确安装", "level": HealthLevel.NORMAL, "advice": "框架已经可以使用，无需重复安装。"}
	return {"name": "可以安装", "level": HealthLevel.NORMAL, "advice": "检查已通过，可以安装 Runtime。"}


func _level_color(level: HealthLevel) -> Color:
	match level:
		HealthLevel.NORMAL:
			return NORMAL_COLOR
		HealthLevel.PENDING:
			return PENDING_COLOR
		HealthLevel.WARNING:
			return WARNING_COLOR
		_:
			return ERROR_COLOR


func _level_name(level: HealthLevel) -> String:
	match level:
		HealthLevel.NORMAL:
			return "正常"
		HealthLevel.PENDING:
			return "待检查"
		HealthLevel.WARNING:
			return "警告"
		_:
			return "错误"


func _show_status_advice(report: Dictionary) -> void:
	var status := _framework_status(report)
	_show_message(status.advice, status.level)


func _show_message(message: String, level: HealthLevel) -> void:
	if not _ensure_content():
		return
	_message_label.clear()
	_message_label.push_paragraph(HORIZONTAL_ALIGNMENT_CENTER)
	_message_label.push_color(_level_color(level))
	_message_label.add_text("提示：")
	_message_label.pop()
	_message_label.add_text(message)
	_message_label.pop()
