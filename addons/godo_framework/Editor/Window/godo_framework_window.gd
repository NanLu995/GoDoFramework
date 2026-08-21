@tool
extends RefCounted

const OVERVIEW_PAGE_SCRIPT := preload("res://addons/godo_framework/Editor/Pages/overview_page.gd")
const RUNTIME_PAGE_SCRIPT := preload("res://addons/godo_framework/Editor/Pages/runtime_setup_page.gd")
const MANIFEST_PAGE_SCRIPT := preload("res://addons/godo_framework/Editor/Pages/resource_manifest_page.gd")
const UI_CONFIG_PAGE_SCRIPT := preload("res://addons/godo_framework/Editor/Pages/ui_config_page.gd")
const EXTENSIONS_PAGE_SCRIPT := preload("res://addons/godo_framework/Editor/Pages/extension_status_page.gd")
const EMBEDDED_EXTENSION_PAGE_SCRIPT := preload(
	"res://addons/godo_framework/Editor/Pages/embedded_extension_page.gd"
)

const PREFERRED_WINDOW_SIZE := Vector2i(900, 600)
const MINIMUM_WINDOW_SIZE := Vector2i(560, 360)
const WINDOW_MARGIN := Vector2i(48, 96)

const STATIC_PAGE_DEFINITIONS := [
	{
		"id": "overview",
		"label": "概览",
		"description": "集中管理 GoDo Runtime、C# 项目配置、DataTable、资源配置和编辑器扩展。",
		"script": OVERVIEW_PAGE_SCRIPT,
	},
	{
		"id": "runtime",
		"label": "项目配置",
		"description": "检查框架版本、C# 项目配置、Runtime 场景和 Autoload 接入状态。",
		"script": RUNTIME_PAGE_SCRIPT,
	},
	{
		"id": "manifest",
		"label": "资源/资源清单",
		"description": "项目内现有的 ResourceManifest。选择后可直接管理或校验。",
		"script": MANIFEST_PAGE_SCRIPT,
	},
	{
		"id": "ui_config",
		"label": "资源/UI 配置",
		"description": "项目内现有的 UiConfig。选择后可直接管理或校验。",
		"script": UI_CONFIG_PAGE_SCRIPT,
	},
]

var _dialog: AcceptDialog
var _root: VBoxContainer
var _navigation: Tree
var _navigation_filter: LineEdit
var _page_title: Label
var _page_description: Label
var _page_host: MarginContainer
var _pages: Dictionary = {}
var _page_items: Dictionary = {}
var _page_descriptions: Dictionary = {}
var _group_items: Dictionary = {}
var _page_definitions: Array[Dictionary] = []
var _data_table_page_id := ""
var _editor_extension_page_id := ""


func initialize(
	plugin: EditorPlugin,
	runtime_controller: RefCounted,
	csproj_controller: RefCounted,
	manifest_controller: RefCounted,
	ui_config_controller: RefCounted,
	extension_host: RefCounted
) -> void:
	_dialog = AcceptDialog.new()
	_dialog.name = "GoDoFrameworkWindow"
	_dialog.title = "GoDo Framework"
	_dialog.ok_button_text = "关闭"
	_dialog.min_size = MINIMUM_WINDOW_SIZE
	_dialog.size = PREFERRED_WINDOW_SIZE
	_dialog.keep_title_visible = true
	_dialog.wrap_controls = false
	_dialog.exclusive = false
	_dialog.get_label().hide()

	_root = VBoxContainer.new()
	_apply_root_rect()
	_root.add_theme_constant_override("separation", 8)
	_dialog.add_child(_root)

	var editor_base := plugin.get_editor_interface().get_base_control()
	var panel_style := editor_base.get_theme_stylebox("panel", "Panel") as StyleBoxFlat
	var content_color := panel_style.bg_color if panel_style != null else Color("#282828")
	var navigation_color := content_color.darkened(0.08)
	var border_color := (
		content_color.lightened(0.22)
		if content_color.get_luminance() < 0.5
		else content_color.darkened(0.22)
	)

	var outer_frame := PanelContainer.new()
	outer_frame.name = "GoDoFrameworkOuterFrame"
	outer_frame.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	outer_frame.size_flags_vertical = Control.SIZE_EXPAND_FILL
	outer_frame.add_theme_stylebox_override(
		"panel", _create_panel_style(content_color, border_color, 1))
	_root.add_child(outer_frame)

	var outer_margin := MarginContainer.new()
	outer_margin.add_theme_constant_override("margin_left", 4)
	outer_margin.add_theme_constant_override("margin_top", 4)
	outer_margin.add_theme_constant_override("margin_right", 4)
	outer_margin.add_theme_constant_override("margin_bottom", 4)
	outer_frame.add_child(outer_margin)

	var body := HSplitContainer.new()
	body.name = "GoDoFrameworkBody"
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.split_offset = 230
	outer_margin.add_child(body)

	var navigation_background := PanelContainer.new()
	navigation_background.name = "GoDoFrameworkNavigationPanel"
	navigation_background.custom_minimum_size.x = 210
	navigation_background.add_theme_stylebox_override(
		"panel", _create_panel_style(navigation_color, border_color, 1))
	body.add_child(navigation_background)

	var navigation_margin := MarginContainer.new()
	navigation_margin.add_theme_constant_override("margin_left", 8)
	navigation_margin.add_theme_constant_override("margin_top", 8)
	navigation_margin.add_theme_constant_override("margin_right", 8)
	navigation_margin.add_theme_constant_override("margin_bottom", 8)
	navigation_background.add_child(navigation_margin)

	var navigation_panel := VBoxContainer.new()
	navigation_panel.add_theme_constant_override("separation", 6)
	navigation_margin.add_child(navigation_panel)

	_navigation_filter = LineEdit.new()
	_navigation_filter.name = "GoDoFrameworkNavigationFilter"
	_navigation_filter.placeholder_text = "筛选设置"
	_navigation_filter.clear_button_enabled = true
	_navigation_filter.text_changed.connect(_on_navigation_filter_changed)
	navigation_panel.add_child(_navigation_filter)

	_navigation = Tree.new()
	_navigation.name = "GoDoFrameworkNavigation"
	_navigation.hide_root = true
	_navigation.select_mode = Tree.SELECT_SINGLE
	_navigation.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_navigation.item_selected.connect(_on_navigation_selected)
	navigation_panel.add_child(_navigation)

	var content_background := PanelContainer.new()
	content_background.name = "GoDoFrameworkContentPanel"
	content_background.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content_background.add_theme_stylebox_override(
		"panel", _create_panel_style(content_color, border_color, 1))
	body.add_child(content_background)

	var content_margin := MarginContainer.new()
	content_margin.add_theme_constant_override("margin_left", 10)
	content_margin.add_theme_constant_override("margin_top", 8)
	content_margin.add_theme_constant_override("margin_right", 10)
	content_margin.add_theme_constant_override("margin_bottom", 8)
	content_background.add_child(content_margin)

	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right.add_theme_constant_override("separation", 8)
	content_margin.add_child(right)

	_page_title = Label.new()
	_page_title.name = "GoDoFrameworkPageTitle"
	_page_title.add_theme_font_size_override("font_size", 16)
	right.add_child(_page_title)
	_page_description = Label.new()
	_page_description.name = "GoDoFrameworkPageDescription"
	_page_description.modulate.a = 0.8
	_page_description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	right.add_child(_page_description)
	var page_separator := HSeparator.new()
	page_separator.name = "GoDoFrameworkPageSeparator"
	right.add_child(page_separator)

	_page_host = MarginContainer.new()
	_page_host.name = "GoDoFrameworkPageHost"
	_page_host.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_page_host.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var page_scroll := ScrollContainer.new()
	page_scroll.name = "GoDoFrameworkPageScroll"
	page_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	page_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	page_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	right.add_child(page_scroll)
	page_scroll.add_child(_page_host)

	_create_pages(runtime_controller, csproj_controller, manifest_controller, ui_config_controller, extension_host)
	plugin.get_editor_interface().get_base_control().add_child(_dialog)


func _create_panel_style(background_color: Color, border_color: Color, border_width: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = background_color
	style.border_color = border_color
	style.set_border_width_all(border_width)
	style.set_corner_radius_all(3)
	return style


func dispose() -> void:
	for page in _pages.values():
		if page.has_method("dispose"):
			page.dispose()
	if is_instance_valid(_dialog):
		_dialog.queue_free()
	_pages.clear()
	_page_items.clear()
	_page_descriptions.clear()
	_group_items.clear()
	_page_definitions.clear()
	_data_table_page_id = ""
	_editor_extension_page_id = ""


func get_window() -> Window:
	return _dialog


func open(page_id := "overview") -> void:
	if not is_instance_valid(_dialog):
		return
	_show_page(page_id)
	_root.update_minimum_size()
	var parent_window := _dialog.get_parent().get_window()
	var target_size := _calculate_window_size(parent_window.size)
	_open_at_size.call_deferred(target_size)


func _open_at_size(target_size: Vector2i) -> void:
	if not is_instance_valid(_dialog):
		return
	_dialog.mode = Window.MODE_WINDOWED
	_dialog.size = target_size
	_dialog.popup_centered(target_size)
	_apply_root_rect.call_deferred()


func _apply_root_rect() -> void:
	if not is_instance_valid(_root):
		return
	_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.offset_left = 8
	_root.offset_top = 8
	_root.offset_right = -8
	_root.offset_bottom = -56
	_root.queue_sort()


func _calculate_window_size(parent_size: Vector2i) -> Vector2i:
	var available_size := Vector2i(
		maxi(parent_size.x - WINDOW_MARGIN.x, 1),
		maxi(parent_size.y - WINDOW_MARGIN.y, 1)
	)
	return Vector2i(
		maxi(mini(PREFERRED_WINDOW_SIZE.x, available_size.x), mini(MINIMUM_WINDOW_SIZE.x, available_size.x)),
		maxi(mini(PREFERRED_WINDOW_SIZE.y, available_size.y), mini(MINIMUM_WINDOW_SIZE.y, available_size.y))
	)


func _create_pages(
	runtime_controller: RefCounted,
	csproj_controller: RefCounted,
	manifest_controller: RefCounted,
	ui_config_controller: RefCounted,
	extension_host: RefCounted
) -> void:
	_page_definitions = _build_page_definitions(extension_host)
	var navigation_root := _navigation.create_item()
	for definition in _page_definitions:
		var page_id: String = definition.id
		var label_path: String = definition.label
		var parts := label_path.split("/", false)
		var parent := navigation_root
		if parts.size() > 1:
			var group_name: String = parts[0]
			if not _group_items.has(group_name):
				var group_item := _navigation.create_item(navigation_root)
				group_item.set_text(0, group_name)
				group_item.set_selectable(0, false)
				_group_items[group_name] = group_item
			parent = _group_items[group_name]
		var item := _navigation.create_item(parent)
		item.set_text(0, parts[-1])
		item.set_metadata(0, page_id)
		_page_items[page_id] = item
		_page_descriptions[page_id] = definition.description

		var page: Control = definition.script.new()
		page.name = "%sPage" % page_id.to_pascal_case()
		page.visible = false
		page.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		page.size_flags_vertical = Control.SIZE_EXPAND_FILL
		_page_host.add_child(page)
		_pages[page_id] = page

	_pages.overview.setup(self, _data_table_page_id, _editor_extension_page_id)
	_pages.runtime.setup(runtime_controller, csproj_controller)
	_pages.manifest.setup(manifest_controller)
	_pages.ui_config.setup(ui_config_controller)
	for definition in _page_definitions:
		var extension_id: String = definition.get("extension_id", "")
		if extension_id.is_empty():
			continue
		var page: Control = _pages[definition.id]
		if page.has_method("setup"):
			page.setup(extension_host, extension_id)


func _build_page_definitions(extension_host: RefCounted) -> Array[Dictionary]:
	var definitions: Array[Dictionary] = []
	definitions.append(STATIC_PAGE_DEFINITIONS[0])
	definitions.append(STATIC_PAGE_DEFINITIONS[1])
	var extension_pages: Array[Dictionary] = extension_host.get_extension_pages()
	for extension in extension_pages:
		if extension.menu_section != "data_tables":
			continue
		var page_id := _extension_page_id(extension.id)
		if _data_table_page_id.is_empty():
			_data_table_page_id = page_id
		definitions.append(_extension_page_definition(extension, page_id, "数据表"))
	definitions.append(STATIC_PAGE_DEFINITIONS[2])
	definitions.append(STATIC_PAGE_DEFINITIONS[3])
	for extension in extension_pages:
		if extension.menu_section == "data_tables":
			continue
		var page_id := _extension_page_id(extension.id)
		if _editor_extension_page_id.is_empty():
			_editor_extension_page_id = page_id
		definitions.append(_extension_page_definition(
			extension,
			page_id,
			"编辑器扩展/%s" % extension.name))
	return definitions


func _extension_page_definition(
	extension: Dictionary,
	page_id: String,
	label: String
) -> Dictionary:
	return {
		"id": page_id,
		"label": label,
		"description": "检查并管理 %s 的项目配置。" % extension.name,
		"script": (
			EMBEDDED_EXTENSION_PAGE_SCRIPT
			if extension.get("has_embedded_page", false)
			else EXTENSIONS_PAGE_SCRIPT
		),
		"extension_id": extension.id,
	}


func _extension_page_id(extension_id: String) -> String:
	return "extension_%s" % extension_id.replace(".", "_").replace("-", "_")


func _on_navigation_selected() -> void:
	var item := _navigation.get_selected()
	if item != null:
		_show_page(str(item.get_metadata(0)))


func _on_navigation_filter_changed(value: String) -> void:
	var filter := value.strip_edges().to_lower()
	for definition in _page_definitions:
		var page_id: String = definition.id
		var label: String = definition.label
		_page_items[page_id].visible = filter.is_empty() or label.to_lower().contains(filter)
	for group_name in _group_items:
		var group_item: TreeItem = _group_items[group_name]
		var child := group_item.get_first_child()
		var has_visible_child := false
		while child != null:
			has_visible_child = has_visible_child or child.visible
			child = child.get_next()
		group_item.visible = has_visible_child


func _show_page(page_id: String) -> void:
	if page_id == "csproj":
		page_id = "runtime"
	if not _pages.has(page_id):
		page_id = "overview"
	for candidate_id in _pages:
		_pages[candidate_id].visible = candidate_id == page_id
	var item: TreeItem = _page_items[page_id]
	_navigation.set_selected(item, 0)
	_navigation.scroll_to_item(item)
	_page_title.text = item.get_text(0)
	_page_description.text = _page_descriptions[page_id]
	var page: Control = _pages[page_id]
	if page.has_method("refresh"):
		page.refresh()
