@tool
extends VBoxContainer

var _window: RefCounted


func setup(
	window: RefCounted,
	data_table_page_id := "",
	editor_extension_page_id := ""
) -> void:
	_window = window
	add_theme_constant_override("separation", 2)
	_add_entry("项目配置", "检查框架、C# 项目配置和 Runtime 安装状态。", "runtime")
	if not data_table_page_id.is_empty():
		_add_entry("数据表", "校验、维护和导出 DataTable 数据。", data_table_page_id)
	_add_entry("资源清单", "查看项目内已有 ResourceManifest，再进行维护和校验。", "manifest")
	_add_entry("UI 配置", "查看项目内已有 UiConfig，再进行界面配置维护。", "ui_config")
	if not editor_extension_page_id.is_empty():
		_add_entry("编辑器扩展", "配置输入、相机和 ECS 等可选集成工具。", editor_extension_page_id)


func _add_entry(title: String, detail: String, page_id: String) -> void:
	if get_child_count() > 0:
		var separator := HSeparator.new()
		separator.name = "GoDoOverview%sSeparator" % page_id.to_pascal_case()
		separator.modulate.a = 0.7
		add_child(separator)
	var panel := PanelContainer.new()
	panel.name = "GoDoOverview%sPanel" % page_id.to_pascal_case()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(panel)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 4)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 4)
	panel.add_child(margin)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 0)
	margin.add_child(content)
	var header := HBoxContainer.new()
	header.name = "GoDoOverview%sHeader" % page_id.to_pascal_case()
	header.add_theme_constant_override("separation", 8)
	content.add_child(header)
	var title_label := Label.new()
	title_label.name = "GoDoOverview%sTitle" % page_id.to_pascal_case()
	title_label.text = title
	title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_label.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	title_label.add_theme_font_size_override("font_size", 15)
	header.add_child(title_label)
	var button := Button.new()
	button.name = "GoDoOverviewOpen%sButton" % page_id.to_pascal_case()
	button.text = "打开"
	button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	button.pressed.connect(_window._show_page.bind(page_id))
	header.add_child(button)
	var detail_label := Label.new()
	detail_label.name = "GoDoOverview%sDetail" % page_id.to_pascal_case()
	detail_label.text = detail
	detail_label.modulate.a = 0.75
	detail_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(detail_label)
