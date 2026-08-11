extends SceneTree

const CONTROLLER_SCRIPT := preload(
	"res://addons/godo_framework/Editor/godo_ui_config_controller.gd")
const VALID_CONFIG_PATH := "res://Verification/Automated/Fixtures/UI/UiConfigValid.tres"
const INVALID_ROOT_PATH := "res://Verification/Automated/Fixtures/UI/UiInvalidRoot.tscn"


func _initialize() -> void:
	var controller: RefCounted = CONTROLLER_SCRIPT.new()
	if controller._ensure_manage_dialog_ready():
		_fail("未初始化 EditorPlugin 时错误地报告管理弹窗可用")
		return
	var config := ResourceLoader.load(VALID_CONFIG_PATH)
	if config == null:
		_fail("无法加载有效 UiConfig fixture")
		return
	var entries = controller._get_entries(config)
	var errors: PackedStringArray = controller._validate_config(config)
	if not errors.is_empty():
		_fail("有效 UiConfig 被拒绝：%s" % "\n".join(errors))
		return
	var discovered_configs: PackedStringArray = controller._find_ui_config_paths(
		"res://Verification/Automated/Fixtures/UI")
	if not discovered_configs.has(VALID_CONFIG_PATH):
		_fail("UiConfig 资源发现没有返回有效配置")
		return
	for discovered_path in discovered_configs:
		if not discovered_path.ends_with(".tres") and not discovered_path.ends_with(".res"):
			_fail("UiConfig 资源发现返回了非 Resource 文件")
			return
	if not controller._should_skip_ui_config_directory(
		"res://Templates",
		"GoDoTemplate"):
		_fail("UiConfig 资源发现没有识别嵌套 Godot 项目边界")
		return
	if controller._should_skip_ui_config_directory(
		"res://Verification/Automated/Fixtures",
		"UI"):
		_fail("UiConfig 资源发现错误跳过了普通资源目录")
		return
	var root_configs: PackedStringArray = controller._find_ui_config_paths("res://")
	if root_configs.has("res://Templates/GoDoTemplate/Ui/UiConfig.tres"):
		_fail("UiConfig 资源发现进入了嵌套 GoDoTemplate 项目")
		return
	var prepared_paths: PackedStringArray = controller._prepare_config_paths(
		PackedStringArray([
			"res://ZUiConfig.tres",
			VALID_CONFIG_PATH,
			"res://ZUiConfig.tres",
		]))
	if prepared_paths != PackedStringArray([VALID_CONFIG_PATH, "res://ZUiConfig.tres"]):
		_fail("多配置选择列表没有稳定排序并去重")
		return
	if not controller._entry_matches_filter(
		"settings",
		"res://UI/Settings.tscn",
		"SETT"):
		_fail("搜索没有按 Id 忽略大小写匹配")
		return
	if not controller._entry_matches_filter(
		"settings",
		"res://UI/Settings.tscn",
		"ui/settings"):
		_fail("搜索没有按场景路径匹配")
		return
	if controller._entry_matches_filter(
		"settings",
		"res://UI/Settings.tscn",
		"main_menu"):
		_fail("搜索错误地匹配了无关条目")
		return
	if controller._default_id_from_scene_path(
		"res://UI/MainMenu.tscn") != "ui/main_menu":
		_fail("选择场景后的默认 Id 不正确")
		return
	if controller._default_id_from_scene_path(
		"res://UI/confirm-dialog.tscn") != "ui/confirm_dialog":
		_fail("连字符场景名没有转换为 snake_case Id")
		return
	if controller._resolve_locator_path(
		"res://Verification/Automated/Fixtures/UI/UiControlA.tscn") != (
			"res://Verification/Automated/Fixtures/UI/UiControlA.tscn"):
		_fail("有效 res:// Locator 没有解析为可定位路径")
		return
	if not controller._resolve_locator_path("res://UI/Missing.tscn").is_empty():
		_fail("不存在的 Locator 错误返回了可定位路径")
		return

	var duplicate_reason: String = controller._get_entry_rejection_reason(
		"settings",
		"res://Verification/Automated/Fixtures/UI/UiControlA.tscn",
		1,
		0,
		false,
		entries,
		-1)
	if not duplicate_reason.contains("已经存在 Id"):
		_fail("重复 Id 没有被拒绝")
		return

	var normalized_duplicate_reason: String = controller._get_entry_rejection_reason(
		" settings ",
		"res://Verification/Automated/Fixtures/UI/UiControlA.tscn",
		1,
		0,
		false,
		entries,
		-1)
	if not normalized_duplicate_reason.contains("已经存在 Id"):
		_fail("带首尾空白的重复 Id 没有被拒绝")
		return

	var locator_reason: String = controller._get_entry_rejection_reason(
		"missing",
		"UI/Missing.tscn",
		1,
		0,
		false,
		entries,
		-1)
	if not locator_reason.contains("必须以 res:// 或 uid:// 开头"):
		_fail("非法场景定位没有被拒绝")
		return

	var root_reason: String = controller._get_entry_rejection_reason(
		"invalid_root",
		INVALID_ROOT_PATH,
		1,
		0,
		false,
		entries,
		-1)
	if not root_reason.contains("必须继承 Control"):
		_fail("非 Control 根节点场景没有被拒绝")
		return

	var multiple_reuse_reason: String = controller._get_entry_rejection_reason(
		"multiple_reuse",
		"res://Verification/Automated/Fixtures/UI/UiControlA.tscn",
		1,
		1,
		true,
		entries,
		-1)
	if not multiple_reuse_reason.contains("只有 Single UI"):
		_fail("Multiple UI 启用实例复用没有被编辑器拒绝")
		return

	var duplicate_locator_reason: String = controller._get_entry_rejection_reason(
		"settings_alias",
		"res://Verification/Automated/Fixtures/UI/UiControlA.tscn",
		1,
		0,
		false,
		entries,
		-1)
	if not duplicate_locator_reason.is_empty():
		_fail("重复 Locator 被错误升级为保存错误：%s" % duplicate_locator_reason)
		return
	var duplicate_locator_warning: String = controller._get_entry_warning_reason(
		"res://Verification/Automated/Fixtures/UI/UiControlA.tscn",
		entries,
		-1)
	if not duplicate_locator_warning.contains("多个 Id"):
		_fail("重复 Locator 没有产生明确警告")
		return
	if not controller._get_entry_warning_reason(
		"res://Verification/Automated/Fixtures/UI/UiControlB.tscn",
		entries,
		1).is_empty():
		_fail("唯一 Locator 被错误标记为 Warning")
		return
	var config_warnings: PackedStringArray = controller._validate_config_warnings(config)
	if config_warnings.is_empty():
		_fail("包含重复 Locator 的配置没有返回 Warning")
		return

	print("[UiConfigEditorControllerRegression] PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("[UiConfigEditorControllerRegression] FAIL: %s" % message)
	quit(1)
