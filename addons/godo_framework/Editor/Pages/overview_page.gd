@tool
extends VBoxContainer

var _window: RefCounted


func setup(window: RefCounted) -> void:
	_window = window
	add_theme_constant_override("separation", 14)
	var description := Label.new()
	description.text = "集中管理 GoDo Runtime、资源清单、UI 配置和编辑器扩展。"
	description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(description)
	_add_entry("项目配置", "检查框架状态，并安装或卸载 GoDoRuntime。", "runtime")
	_add_entry("资源清单", "查看项目内已有 ResourceManifest，再进行维护和校验。", "manifest")
	_add_entry("UI 配置", "查看项目内已有 UiConfig，再进行界面配置维护。", "ui_config")
	_add_entry("编辑器扩展", "查看扩展加载状态，打开 DataTable 和可选集成工具。", "extensions")


func _add_entry(title: String, detail: String, page_id: String) -> void:
	var panel := PanelContainer.new()
	add_child(panel)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	panel.add_child(row)
	var text := VBoxContainer.new()
	text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(text)
	var title_label := Label.new()
	title_label.text = title
	title_label.add_theme_font_size_override("font_size", 16)
	text.add_child(title_label)
	var detail_label := Label.new()
	detail_label.text = detail
	detail_label.modulate.a = 0.75
	detail_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	text.add_child(detail_label)
	var button := Button.new()
	button.text = "打开"
	button.pressed.connect(_window._show_page.bind(page_id))
	row.add_child(button)
