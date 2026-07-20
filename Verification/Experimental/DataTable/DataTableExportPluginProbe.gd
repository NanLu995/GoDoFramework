@tool
extends SceneTree

const EXPORT_PLUGIN_SCRIPT := preload("res://addons/godo_framework/Tools/DataTable/Editor/datatable_export_plugin.gd")
const CONFIG := "res://Verification/Experimental/DataTable/Artifacts/scratch/export-targets/items.datatable.schema.json"
const OUTPUT := "res://Verification/Experimental/DataTable/Artifacts/scratch/export-targets/output"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var plugin: EditorExportPlugin = EXPORT_PLUGIN_SCRIPT.new()
	var client_release: Dictionary = plugin.build_export_plan(CONFIG, "client", false)
	_assert_plan(client_release, "ClientSetting", "ServerSetting", "manifest.client.json", false)
	var client_debug: Dictionary = plugin.build_export_plan(CONFIG, "client", true)
	_assert_plan(client_debug, "ClientSetting", "ServerSetting", "manifest.client.json", true)
	var server_release: Dictionary = plugin.build_export_plan(CONFIG, "server", false)
	_assert_plan(server_release, "ServerSetting", "ClientSetting", "manifest.server.json", false)
	var discovered: PackedStringArray = plugin.discover_schemas(CONFIG.get_base_dir())
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


func _assert_plan(
	plan: Dictionary,
	included_table: String,
	excluded_table: String,
	manifest_source: String,
	expect_debug: bool
) -> void:
	if not plan.get("valid", false):
		_fail("导出规划失败：%s" % plan.get("error", "未知错误"))
		return
	var added: Dictionary = plan.added
	if not added.has(OUTPUT.path_join("%s.gdtb" % included_table)):
		_fail("导出规划缺少 %s。" % included_table)
		return
	if added.has(OUTPUT.path_join("%s.gdtb" % excluded_table)):
		_fail("导出规划泄漏 %s。" % excluded_table)
		return
	if str(added.get(OUTPUT.path_join("manifest.json"), "")).get_file() != manifest_source:
		_fail("导出规划选择了错误 Manifest。")
		return
	var has_debug := added.has(OUTPUT.path_join("debug.json"))
	if has_debug != expect_debug:
		_fail("Debug JSON 导出规划错误。")


func _fail(message: String) -> void:
	push_error("[DataTableExportPluginProbe] FAIL: %s" % message)
	quit(1)
