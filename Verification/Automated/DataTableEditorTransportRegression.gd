@tool
extends SceneTree

const TOOL_PATH := "res://addons/godo_framework/Tools/DataTable/godo_datatable.py"
const SCHEMA_PATH := "res://Docs/coverage.json"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	await process_frame
	await create_timer(1.0).timeout
	var output: Array = []
	var arguments := PackedStringArray([
		"-X",
		"utf8",
		ProjectSettings.globalize_path(TOOL_PATH),
		"--editor-output-base64",
		"check",
		"--schema",
		ProjectSettings.globalize_path(SCHEMA_PATH),
	])
	var exit_code := OS.execute("python", arguments, output, true)
	if output.is_empty():
		_fail("Godot 未捕获到编辑器诊断载荷；退出码 %d。" % exit_code)
		return
	var decoded := Marshalls.base64_to_utf8("\n".join(output).strip_edges())
	if decoded.is_empty() or "鏃" in decoded or "澶" in decoded or "�" in decoded:
		_fail("编辑器诊断载荷未还原为可读 UTF-8：%s" % decoded)
		return
	if exit_code == 0 or "[DataTableCompiler] FAIL" not in decoded or "缺少字段" not in decoded:
		_fail("编辑器未返回预期的可读中文失败诊断：%s" % decoded)
		return
	print("[DataTableEditorTransportRegression] PASS (exit=%d)" % exit_code)
	quit(0)


func _fail(message: String) -> void:
	push_error("[DataTableEditorTransportRegression] FAIL: %s" % message)
	quit(1)
