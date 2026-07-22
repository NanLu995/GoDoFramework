extends SceneTree

const SCHEMA_EDITOR_SCRIPT := preload("res://addons/godo_framework/Tools/DataTable/Editor/datatable_schema_editor.gd")

var _root := ""


func _initialize() -> void:
	_root = ProjectSettings.globalize_path(
		"user://datatable-schema-editor-regression-%d" % Time.get_ticks_usec()
	)
	var error := DirAccess.make_dir_recursive_absolute(_root)
	if error != OK:
		_fail("无法创建隔离测试目录：%s。" % error_string(error))
		return
	var editor = SCHEMA_EDITOR_SCRIPT.new()
	editor._status = Label.new()
	if not _verify_transaction_rollback(editor):
		return
	if not _verify_nested_directory(editor):
		return
	if not _verify_schema_version_semantics(editor):
		return
	if not _verify_schema_validation(editor):
		return
	_cleanup()
	print("[DataTableSchemaEditorSaveRegression] PASS (4/4)")
	quit(0)


func _verify_transaction_rollback(editor) -> bool:
	var first := _root.path_join("first.csv")
	var blocked := _root.path_join("blocked.csv")
	if not _write(first, "before\n"):
		return false
	var directory_error := DirAccess.make_dir_recursive_absolute(blocked)
	if directory_error != OK:
		_fail("无法创建提交失败探针目录。")
		return false
	var committed: bool = editor._commit_text_files({
		first: "after\n",
		blocked: "blocked\n",
	})
	if committed:
		_fail("目标路径为目录时，事务提交不应成功。")
		return false
	if FileAccess.get_file_as_string(first) != "before\n":
		_fail("事务失败后，第一个文件未恢复。")
		return false
	for file_name in DirAccess.get_files_at(_root):
		if ".godo-datatable-" in file_name:
			_fail("事务失败后遗留临时文件：%s。" % file_name)
			return false
	return true


func _verify_nested_directory(editor) -> bool:
	var nested := _root.path_join("nested/tables/Items.csv")
	if not editor._commit_text_files({nested: "id\n"}):
		_fail("嵌套 CSV 路径提交失败：%s" % editor._status.text)
		return false
	if FileAccess.get_file_as_string(nested) != "id\n":
		_fail("嵌套 CSV 文件内容错误。")
		return false
	return true


func _verify_schema_version_semantics(editor) -> bool:
	var original_table := {
		"id": "Item",
		"source": "Items.csv",
		"schema_version": 4,
		"audience": "Shared",
		"primary_key": "id",
		"fields": [{"name": "id", "type": "string", "required": true}],
	}
	editor._original_schema = {"tables": [original_table.duplicate(true)]}
	var current_table: Dictionary = original_table.duplicate(true)
	current_table["source"] = "nested/Items.csv"
	current_table["_editor_original_id"] = "Item"
	current_table["_editor_original_source"] = "Items.csv"
	editor._schema = {"tables": [current_table]}
	editor._apply_schema_versions()
	if int(current_table.schema_version) != 4:
		_fail("只修改 CSV 来源错误递增了表结构版本。")
		return false
	current_table.fields[0]["min_length"] = 1
	editor._apply_schema_versions()
	if int(current_table.schema_version) != 5:
		_fail("字段结构变化未递增表结构版本。")
		return false
	return true


func _verify_schema_validation(editor) -> bool:
	editor._schema_path = _root.path_join(".datatable.schema.json")
	editor._data_set_id = LineEdit.new()
	editor._data_set_id.text = "game.test"
	editor._namespace = LineEdit.new()
	editor._namespace.text = "Game.DataTables.Test"
	editor._source_directory = LineEdit.new()
	editor._source_directory.text = ".datafiles"
	editor._output_directory = LineEdit.new()
	editor._output_directory.text = "Runtime"
	editor._csharp_output = LineEdit.new()
	editor._csharp_output.text = "TestDataTables.g.cs"
	editor._schema = {
		"tables": [{
			"id": "Item",
			"source": "Items.csv",
			"schema_version": 1,
			"audience": "Shared",
			"primary_key": "id",
			"fields": [{"name": "id", "type": "string", "required": true}],
		}],
	}
	if not editor._validate_schema().is_empty():
		_fail("有效 Schema 被编辑器拒绝：%s" % editor._validate_schema())
		return false
	editor._namespace.text = "Game.Invalid-Namespace"
	if "命名空间" not in editor._validate_schema():
		_fail("无效 C# 命名空间未被保存前校验拒绝。")
		return false
	editor._namespace.text = "Game.DataTables.Test"
	editor._schema.tables[0].id = "道具"
	if "数据表 ID" not in editor._validate_schema():
		_fail("非 ASCII 数据表 ID 未被保存前校验拒绝。")
		return false
	editor._schema.tables[0].id = "Item"
	editor._schema.tables[0].fields.append({
		"name": "rarity",
		"type": "enum",
		"required": false,
		"values": ["Common", "Common"],
	})
	if "枚举值" not in editor._validate_schema():
		_fail("重复枚举值未被保存前校验拒绝。")
		return false
	return true


func _write(path: String, text: String) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		_fail("无法写入隔离测试文件：%s。" % path)
		return false
	file.store_string(text)
	return true


func _cleanup() -> void:
	if _root.is_empty() or not _root.contains("datatable-schema-editor-regression-"):
		return
	_remove_tree(_root)


func _remove_tree(path: String) -> void:
	var directory := DirAccess.open(path)
	if directory == null:
		return
	for file_name in DirAccess.get_files_at(path):
		DirAccess.remove_absolute(path.path_join(file_name))
	for directory_name in DirAccess.get_directories_at(path):
		_remove_tree(path.path_join(directory_name))
	DirAccess.remove_absolute(path)


func _fail(message: String) -> void:
	_cleanup()
	push_error("[DataTableSchemaEditorSaveRegression] FAIL: %s" % message)
	quit(1)
