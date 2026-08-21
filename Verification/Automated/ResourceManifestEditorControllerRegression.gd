extends SceneTree

const CONTROLLER_SCRIPT := preload(
	"res://addons/godo_framework/Editor/godo_resource_manifest_controller.gd")
const TEMP_MANIFEST_PATH := "user://godo_resource_manifest_editor_regression.tres"


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
	if not _verify_entry_persistence(controller):
		return

	print("[ResourceManifestEditorControllerRegression] PASS")
	_cleanup_temp_manifest()
	quit(0)


func _verify_entry_persistence(controller: RefCounted) -> bool:
	_cleanup_temp_manifest()
	var manifest: Resource = controller._create_manifest_instance()
	var entry: Resource = controller._create_manifest_entry_instance()
	if manifest == null or entry == null:
		_fail("无法实例化 ResourceManifest 持久化回归资源")
		return false
	entry.set("Id", "regression/original")
	entry.set("Locator", "res://Verification/Automated/Fixtures/UI/UiControlA.tscn")
	var entries = controller._copy_manifest_entries(controller._get_manifest_entries(manifest))
	entries.append(entry)
	manifest.set("Entries", entries)
	manifest = controller._save_manifest_and_reload(manifest, TEMP_MANIFEST_PATH)
	if manifest == null:
		_fail("新增条目保存后校验失败：%s" % controller._manifest_persistence_error)
		return false
	entries = controller._get_manifest_entries(manifest)
	if entries.size() != 1 or entries[0].get("Id") != "regression/original":
		_fail("新增条目重新加载后丢失")
		return false
	var report: Dictionary = controller._validate_manifest(TEMP_MANIFEST_PATH)
	if report.entry_count != 1 or report.level != 0:
		_fail("新增条目保存后校验没有读取到磁盘条目")
		return false

	var updated_entries = controller._copy_manifest_entries(entries)
	var updated_entry: Resource = entries[0].duplicate()
	updated_entry.set("Id", "regression/edited")
	updated_entries[0] = updated_entry
	manifest.set("Entries", updated_entries)
	manifest = controller._save_manifest_and_reload(manifest, TEMP_MANIFEST_PATH)
	if manifest == null or controller._get_manifest_entries(manifest)[0].get("Id") != "regression/edited":
		_fail("编辑条目重新加载后没有保留")
		return false

	updated_entries = controller._copy_manifest_entries(controller._get_manifest_entries(manifest))
	updated_entries.remove_at(0)
	manifest.set("Entries", updated_entries)
	manifest = controller._save_manifest_and_reload(manifest, TEMP_MANIFEST_PATH)
	if manifest == null or not controller._get_manifest_entries(manifest).is_empty():
		_fail("删除条目重新加载后没有保留")
		return false
	return true


func _cleanup_temp_manifest() -> void:
	var absolute_path := ProjectSettings.globalize_path(TEMP_MANIFEST_PATH)
	if FileAccess.file_exists(absolute_path):
		DirAccess.remove_absolute(absolute_path)


func _fail(message: String) -> void:
	_cleanup_temp_manifest()
	push_error("[ResourceManifestEditorControllerRegression] FAIL: %s" % message)
	quit(1)
