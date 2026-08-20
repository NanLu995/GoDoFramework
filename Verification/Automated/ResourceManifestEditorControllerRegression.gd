extends SceneTree

const CONTROLLER_SCRIPT := preload(
	"res://addons/godo_framework/Editor/godo_resource_manifest_controller.gd")


func _initialize() -> void:
	var controller: RefCounted = CONTROLLER_SCRIPT.new()
	if not controller._should_skip_resource_manifest_directory(
		"res://Verification/Automated/Fixtures",
		"NestedGodotProject"):
		_fail("资源清单发现没有识别嵌套 Godot 项目边界")
		return
	if controller._should_skip_resource_manifest_directory(
		"res://Verification/Automated/Fixtures",
		"UI"):
		_fail("资源清单发现错误跳过了普通资源目录")
		return
	var root_manifests: PackedStringArray = controller._find_resource_manifest_paths("res://")
	for manifest_path in root_manifests:
		if manifest_path.begins_with("res://Verification/Automated/Fixtures/NestedGodotProject/"):
			_fail("资源清单发现进入了嵌套 Godot 项目")
			return

	print("[ResourceManifestEditorControllerRegression] PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("[ResourceManifestEditorControllerRegression] FAIL: %s" % message)
	quit(1)
