@tool
extends RefCounted

const AUTOLOAD_NAME := "GoDoRuntime"
const AUTOLOAD_SETTING := "autoload/GoDoRuntime"
const RUNTIME_SCENE_PATH := "res://addons/godo_framework/Core/GoDoRuntime.tscn"
const RUNTIME_SCRIPT_PATH := "res://addons/godo_framework/Core/GoDoRuntime.cs"
const FRAMEWORK_PATH := "res://addons/godo_framework"
const PLUGIN_CONFIG_PATH := "res://addons/godo_framework/plugin.cfg"
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

var _plugin: EditorPlugin
var _setup_dialog: AcceptDialog
var _uninstall_dialog: ConfirmationDialog
var _content: VBoxContainer
var _report_label: RichTextLabel
var _message_label: RichTextLabel
var _install_button: Button
var _uninstall_button: Button

func initialize(plugin: EditorPlugin) -> void:
	_plugin = plugin
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
	_plugin.get_editor_interface().get_base_control().add_child(_setup_dialog)

	_uninstall_dialog = ConfirmationDialog.new()
	_uninstall_dialog.title = "卸载 GoDoRuntime"
	_uninstall_dialog.dialog_text = "只会移除正确匹配的 GoDoRuntime Autoload，不会删除任何框架或业务文件。是否继续？"
	_uninstall_dialog.ok_button_text = "卸载"
	_uninstall_dialog.cancel_button_text = "取消"
	_uninstall_dialog.confirmed.connect(_on_uninstall_confirmed)
	_uninstall_dialog.canceled.connect(_on_uninstall_canceled)
	_plugin.get_editor_interface().get_base_control().add_child(_uninstall_dialog)

func dispose() -> void:
	if is_instance_valid(_setup_dialog):
		_setup_dialog.queue_free()
	if is_instance_valid(_uninstall_dialog):
		_uninstall_dialog.queue_free()

func open_dialog() -> void:
	_open_setup_dialog()


func _open_setup_dialog() -> void:
	var report := _refresh_report()
	_show_status_advice(report)
	_setup_dialog.popup_centered(Vector2i(620, 360))


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

	_plugin.add_autoload_singleton(AUTOLOAD_NAME, RUNTIME_SCENE_PATH)
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

	_plugin.remove_autoload_singleton(AUTOLOAD_NAME)
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
		"version_tested": false,
		"framework_version": "",
		"min_godot_version": "",
		"tested_godot_version": "",
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
	var current_version := Vector3i(version.major, version.minor, version.patch)
	var config := ConfigFile.new()
	var load_error := config.load(PLUGIN_CONFIG_PATH)
	if load_error != OK:
		_add_item(report, HealthLevel.ERROR, "GoDoFramework 版本", "无法读取 plugin.cfg：%s" % error_string(load_error))
		_add_item(report, HealthLevel.ERROR, "Godot 兼容性", "缺少可用的兼容性声明")
		return

	report.framework_version = str(config.get_value("plugin", "version", "")).strip_edges()
	report.min_godot_version = str(config.get_value("plugin", "min_godot_version", "")).strip_edges()
	report.tested_godot_version = str(config.get_value("plugin", "tested_godot_version", "")).strip_edges()
	if report.framework_version.is_empty():
		_add_item(report, HealthLevel.ERROR, "GoDoFramework 版本", "plugin.cfg 未声明版本")
		_add_item(report, HealthLevel.ERROR, "Godot 兼容性", "缺少可用的兼容性声明")
		return
	_add_item(report, HealthLevel.NORMAL, "GoDoFramework 版本", report.framework_version)

	var minimum_version := _parse_version(report.min_godot_version)
	var tested_version := _parse_version(report.tested_godot_version)
	if (
		minimum_version.x < 0
		or tested_version.x < 0
		or minimum_version.x != tested_version.x
		or _compare_versions(minimum_version, tested_version) > 0
	):
		_add_item(report, HealthLevel.ERROR, "Godot 兼容性", "plugin.cfg 中的最低版本或已验证版本无效")
		return

	var compatibility := _evaluate_version(current_version, minimum_version, tested_version)
	report.version_supported = compatibility.supported
	report.version_tested = compatibility.tested
	var current_text := _format_version(current_version)
	_add_item(
		report,
		HealthLevel.NORMAL if report.version_tested else (HealthLevel.WARNING if report.version_supported else HealthLevel.ERROR),
		"Godot 兼容性",
		"当前 %s，已验证范围 %s～%s" % [current_text, report.min_godot_version, report.tested_godot_version]
		if report.version_tested
		else ("当前 %s，高于已验证版本 %s；可以继续使用，但应升级框架或完成项目回归" % [current_text, report.tested_godot_version]
		if report.version_supported
		else "当前 %s，不兼容；需要 %s～%s 所在的 Godot %d.x" % [current_text, report.min_godot_version, report.tested_godot_version, minimum_version.x])
	)


func _parse_version(value: String) -> Vector3i:
	var parts := value.split(".")
	if parts.size() != 3:
		return Vector3i(-1, -1, -1)
	for part in parts:
		if not part.is_valid_int() or int(part) < 0:
			return Vector3i(-1, -1, -1)
	return Vector3i(int(parts[0]), int(parts[1]), int(parts[2]))


func _compare_versions(left: Vector3i, right: Vector3i) -> int:
	if left.x != right.x:
		return -1 if left.x < right.x else 1
	if left.y != right.y:
		return -1 if left.y < right.y else 1
	if left.z != right.z:
		return -1 if left.z < right.z else 1
	return 0


func _evaluate_version(current: Vector3i, minimum: Vector3i, tested: Vector3i) -> Dictionary:
	var supported := current.x == minimum.x and _compare_versions(current, minimum) >= 0
	return {
		"supported": supported,
		"tested": supported and current.x == tested.x and _compare_versions(current, tested) <= 0,
	}


func _format_version(version: Vector3i) -> String:
	return "%d.%d.%d" % [version.x, version.y, version.z]


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
		return {"name": "Godot 版本不兼容", "level": HealthLevel.ERROR, "advice": "请使用兼容范围内的 Godot，或升级 GoDoFramework。"}
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
	if not report.version_tested:
		return {"name": "Godot 版本尚未验证", "level": HealthLevel.WARNING, "advice": "当前版本不会阻止使用；请升级 GoDoFramework，或完成项目自动测试和关键场景回归。"}
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
