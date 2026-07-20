@tool
extends SceneTree

const EXPORT_PLUGIN_SCRIPT := preload("res://addons/godo_framework/Tools/DataTable/Editor/datatable_export_plugin.gd")
const CONFIG := "res://DataTables/Base/.datatable.schema.json"
const OUTPUT := "res://DataTables/Base/Runtime"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not await _wait_for_filesystem_scan():
		_fail("等待 Godot 文件系统扫描完成超时。")
		return
	var plugin: EditorExportPlugin = EXPORT_PLUGIN_SCRIPT.new()
	var client_release: Dictionary = plugin.build_export_plan(CONFIG, "client", false)
	_assert_plan(client_release)
	var client_debug: Dictionary = plugin.build_export_plan(CONFIG, "client", true)
	_assert_plan(client_debug)
	var server_release: Dictionary = plugin.build_export_plan(CONFIG, "server", false)
	_assert_plan(server_release)
	var discovered: PackedStringArray = plugin.discover_schemas("res://DataTables")
	if not discovered.has(CONFIG):
		_fail("未发现一级目录内的 Schema。")
		return
	var invalid: Dictionary = plugin.build_export_plan(CONFIG, "invalid", false)
	if invalid.get("valid", true):
		_fail("未知导出目标未被拒绝。")
		return
	var verify_error: String = plugin._verify_generated("python", CONFIG)
	if not verify_error.is_empty():
		_fail("导出前 verify-generated 失败：%s" % verify_error)
		return
	print("[DataTableExportPluginProbe] PASS (5/5)")
	quit(0)


func _wait_for_filesystem_scan() -> bool:
	var filesystem := EditorInterface.get_resource_filesystem()
	for _attempt in 400:
		if not filesystem.is_scanning():
			await process_frame
			return true
		await create_timer(0.05).timeout
	return false


func _assert_plan(
	plan: Dictionary
) -> void:
	if not plan.get("valid", false):
		_fail("导出规划失败：%s" % plan.get("error", "未知错误"))
		return
	var added: Dictionary = plan.added
	for table_id in ["ItemCategory", "Item", "Reward"]:
		if not added.has(OUTPUT.path_join("%s.gdtb" % table_id)):
			_fail("导出规划缺少 %s。" % table_id)
			return
	if str(added.get(OUTPUT.path_join("manifest.json"), "")).get_file() != "manifest.json":
		_fail("导出规划选择了错误 Manifest。")
		return
	if added.has(OUTPUT.path_join("debug.json")):
		_fail("导出规划不应包含默认未生成的 Debug JSON。")


func _fail(message: String) -> void:
	push_error("[DataTableExportPluginProbe] FAIL: %s" % message)
	quit(1)
