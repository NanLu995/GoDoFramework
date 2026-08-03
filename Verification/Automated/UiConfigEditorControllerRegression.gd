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
	if not controller._get_direct_config_path(PackedStringArray()).is_empty():
		_fail("零配置时错误地选择了直接打开目标")
		return
	if controller._get_direct_config_path(
		PackedStringArray([VALID_CONFIG_PATH])) != VALID_CONFIG_PATH:
		_fail("单配置时没有直接返回目标")
		return
	if not controller._get_direct_config_path(
		PackedStringArray([VALID_CONFIG_PATH, "res://OtherUiConfig.tres"])).is_empty():
		_fail("多配置时错误地选择了直接打开目标")
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
		"res://UI/MainMenu.tscn") != "MainMenu":
		_fail("选择场景后的默认 Id 不正确")
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

	print("[UiConfigEditorControllerRegression] PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("[UiConfigEditorControllerRegression] FAIL: %s" % message)
	quit(1)
