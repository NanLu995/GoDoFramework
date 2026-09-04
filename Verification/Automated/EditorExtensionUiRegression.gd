@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"
const SETUP_CONTROLLER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_runtime_setup_controller.gd")
const EXPORT_FILTER_SCRIPT := preload("res://addons/godo_framework/Editor/godo_export_filter.gd")


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	await process_frame
	if not _verify_export_filter():
		return
	var menu_button := root.find_child(MENU_BUTTON_NAME, true, false) as MenuButton
	if menu_button == null:
		_fail("未找到 GoDo 工具栏菜单。")
		return

	var menu := menu_button.get_popup()
	if not _verify_menu_layout(menu):
		return
	var framework_window := await _open_framework_window(menu)
	if framework_window == null:
		return
	if not _verify_precreated_window_layering(framework_window):
		return
	if not await _verify_management_page(
		framework_window,
		"manifest",
		"GoDoResourceManifestList",
		"GoDoResourceManifestRefreshButton",
		"GoDoResourceManifestCreateButton",
		"GoDoResourceManifestManageButton",
		"GoDoResourceManifestValidateButton",
		"ManifestFileDialog",
		"res://GeneratedEditorUiManifest.tres",
		"GoDoManifestManageDialog",
		"资源清单管理",
		"GoDoManagedManifestLabel",
		"GoDoManifestSearchInput",
		"GoDoManifestEntriesTree",
		"GoDoManifestValidateButton",
		"GoDoManifestReportDialog"):
		return
	if not await _verify_management_page(
		framework_window,
		"ui_config",
		"GoDoUiConfigList",
		"GoDoUiConfigRefreshButton",
		"GoDoUiConfigCreateButton",
		"GoDoUiConfigManageButton",
		"GoDoUiConfigValidateButton",
		"UiConfigFileDialog",
		"res://GeneratedEditorUiConfig.tres",
		"GoDoUiConfigManageDialog",
		"UI 配置管理",
		"GoDoManagedUiConfigLabel",
		"GoDoUiConfigSearchInput",
		"GoDoUiConfigEntriesTree",
		"GoDoUiConfigManageValidateButton",
		"GoDoUiConfigReportDialog"):
		return
	if not await _open_and_verify_setup(framework_window):
		return
	if not _verify_csproj_page(framework_window):
		return
	if not await _open_and_verify_datatable(framework_window):
		return
	if not await _open_and_verify(
		framework_window,
		"extension_godo_guide_input",
		"GoDo GUIDE Input 设置",
		"安装 / 修复 GUIDE Input",
		"GuideInputReport",
		"GuideInputMessage",
		"GuideInputRepairButton",
		"GuideInputOfficialSourceButton",
		"https://github.com/Phlegmlee/G.U.I.D.E-CSharp"
	):
		return
	if not await _open_and_verify(
		framework_window,
		"extension_godo_phantom_camera",
		"GoDo Phantom Camera 设置",
		"启用 Phantom Camera",
		"PhantomCameraReport",
		"PhantomCameraMessage",
		"PhantomCameraEnableButton",
		"PhantomCameraOfficialSourceButton",
		"https://github.com/ramokz/phantom-camera/releases/tag/v0.11.0.3"
	):
		return
	if not await _open_and_verify_friflo_dependency(framework_window):
		return
	if not _verify_project_config_dispose(framework_window):
		return

	print("[EditorExtensionUiRegression] PASS (10/10)")
	quit(0)


func _verify_export_filter() -> bool:
	var export_filter = EXPORT_FILTER_SCRIPT.new()
	if not export_filter.should_skip_path(
		"res://addons/godo_framework/Editor/godo_editor_plugin.gd", true):
		_fail("Debug 导出没有排除 Editor。")
		return false
	if not export_filter.should_skip_path(
		"res://addons/godo_framework/Tools/DataTable/godo_datatable.py", true):
		_fail("Debug 导出没有排除 Tools。")
		return false
	if export_filter.should_skip_path(
		"res://addons/godo_framework/Debugger/DebuggerOverlay.tscn", true):
		_fail("Debug 导出错误地排除了 Debugger。")
		return false
	if not export_filter.should_skip_path(
		"res://addons/godo_framework/Debugger/DebuggerOverlay.tscn", false):
		_fail("Release 导出没有排除 Debugger。")
		return false
	if export_filter.should_skip_path(
		"res://addons/godo_framework/Runtime/UI/UiService.cs", false):
		_fail("Release 导出错误地排除了 Runtime。")
		return false
	return true


func _verify_menu_layout(menu: PopupMenu) -> bool:
	return menu.item_count == 1 and menu.get_item_text(0) == "打开 GoDo Framework..."


func _verify_extension_navigation(navigation: Tree) -> bool:
	var root_item := navigation.get_root()
	var runtime := _find_tree_item_by_metadata(root_item, "runtime")
	var data_table := _find_tree_item_by_metadata(root_item, "extension_godo_datatable")
	var status := _find_tree_item_by_metadata(root_item, "extensions")
	var guide := _find_tree_item_by_metadata(root_item, "extension_godo_guide_input")
	var camera := _find_tree_item_by_metadata(root_item, "extension_godo_phantom_camera")
	var ecs := _find_tree_item_by_metadata(root_item, "extension_godo_friflo_ecs")
	if (
		runtime == null
		or data_table == null
		or guide == null
		or camera == null
		or ecs == null
	):
		_fail("统一窗口缺少数据表或可选编辑器扩展的独立导航条目。")
		return false
	var extension_group := guide.get_parent()
	if (
		status != null
		or extension_group == null
		or data_table.get_parent() != root_item
		or data_table.get_text(0) != "数据表"
		or data_table.get_index() != runtime.get_index() + 1
		or extension_group.get_text(0) != "编辑器扩展"
		or guide.get_parent() != extension_group
		or camera.get_parent() != extension_group
		or ecs.get_parent() != extension_group
	):
		_fail("数据表位置错误、扩展未正确分组，或仍保留多余的状态条目。")
		return false
	return true


func _open_framework_window(menu: PopupMenu) -> Window:
	menu.id_pressed.emit(menu.get_item_id(0))
	await process_frame
	await process_frame
	var dialog := root.find_child("GoDoFrameworkWindow", true, false) as Window
	if dialog == null or not dialog.visible:
		_fail("单一菜单入口没有打开 GoDo Framework 窗口。")
		return null
	var navigation := dialog.find_child("GoDoFrameworkNavigation", true, false) as Tree
	if navigation == null or navigation.get_root() == null:
		_fail("统一窗口缺少左侧导航。")
		return null
	if not _verify_extension_navigation(navigation):
		return null
	var navigation_panel := dialog.find_child(
		"GoDoFrameworkNavigationPanel", true, false) as PanelContainer
	var content_panel := dialog.find_child(
		"GoDoFrameworkContentPanel", true, false) as PanelContainer
	var outer_frame := dialog.find_child(
		"GoDoFrameworkOuterFrame", true, false) as PanelContainer
	if navigation_panel == null or content_panel == null or outer_frame == null:
		_fail("统一窗口缺少外框、导航区或内容区的主题面板。")
		return null
	var navigation_style := navigation_panel.get_theme_stylebox("panel") as StyleBoxFlat
	var content_style := content_panel.get_theme_stylebox("panel") as StyleBoxFlat
	var outer_style := outer_frame.get_theme_stylebox("panel") as StyleBoxFlat
	if (
		navigation_style == null
		or content_style == null
		or outer_style == null
		or navigation_style.bg_color == content_style.bg_color
		or navigation_style.border_width_left != 1
		or content_style.border_width_left != 1
		or outer_style.border_width_left != 1
	):
		_fail("统一窗口的三层边界或左右背景区分未生效。")
		return null
	var close_button := (dialog as AcceptDialog).get_ok_button() as Button
	if outer_frame.get_global_rect().end.y > close_button.get_global_rect().position.y:
		_fail("统一窗口首次打开时，内容外框覆盖了底部关闭按钮区：outer=%s, close=%s, dialog=%s。" % [
			outer_frame.get_global_rect(),
			close_button.get_global_rect(),
			dialog.size,
		])
		return null
	var navigation_filter := dialog.find_child(
		"GoDoFrameworkNavigationFilter", true, false) as LineEdit
	if navigation_filter == null or navigation_filter.placeholder_text != "筛选设置":
		_fail("统一窗口缺少项目设置式导航筛选框。")
		return null
	var page_scroll := dialog.find_child(
		"GoDoFrameworkPageScroll", true, false) as ScrollContainer
	if page_scroll == null or page_scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED:
		_fail("统一窗口的右侧页面缺少垂直滚动边界。")
		return null
	var page_host := dialog.find_child(
		"GoDoFrameworkPageHost", true, false) as MarginContainer
	if (
		page_host == null
		or page_scroll.size_flags_horizontal != Control.SIZE_EXPAND_FILL
		or page_host.size_flags_horizontal != Control.SIZE_EXPAND_FILL
		or page_host.size.x < page_scroll.size.x - 24.0
	):
		_fail("统一窗口的右侧页面未填满滚动区宽度。")
		return null
	if (
		not dialog.keep_title_visible
		or dialog.wrap_controls
		or dialog.min_size != Vector2i(560, 360)
		or dialog.size.x > 900
		or dialog.size.y > 600
	):
		_fail("统一窗口没有重置为可见的紧凑尺寸：size=%s, min_size=%s, mode=%s。" % [
			dialog.size,
			dialog.min_size,
			dialog.mode,
		])
		return null
	if not _verify_overview_layout(dialog):
		return null
	return dialog


func _verify_overview_layout(framework_window: Window) -> bool:
	var page := framework_window.find_child("OverviewPage", true, false) as VBoxContainer
	var navigation := framework_window.find_child("GoDoFrameworkNavigation", true, false) as Tree
	var page_title := framework_window.find_child("GoDoFrameworkPageTitle", true, false) as Label
	var page_description := framework_window.find_child("GoDoFrameworkPageDescription", true, false) as Label
	var page_separator := framework_window.find_child("GoDoFrameworkPageSeparator", true, false) as HSeparator
	var header := framework_window.find_child("GoDoOverviewRuntimeHeader", true, false) as HBoxContainer
	var title := framework_window.find_child("GoDoOverviewRuntimeTitle", true, false) as Label
	var detail := framework_window.find_child("GoDoOverviewRuntimeDetail", true, false) as Label
	var button := framework_window.find_child("GoDoOverviewOpenRuntimeButton", true, false) as Button
	var separator := framework_window.find_child("GoDoOverviewManifestSeparator", true, false) as HSeparator
	if (
		page == null
		or navigation == null
		or page_title == null
		or page_description == null
		or page_separator == null
		or page_description.get_parent() != page_title.get_parent()
		or page_separator.get_parent() != page_title.get_parent()
		or page_description.get_index() >= page_separator.get_index()
		or not page_description.text.contains("集中管理")
		or _find_tree_item_by_metadata(navigation.get_root(), "csproj") != null
		or header == null
		or title == null
		or detail == null
		or button == null
		or separator == null
		or separator.get_parent() != page
		or not is_equal_approx(separator.modulate.a, 0.7)
		or page.get_theme_constant("separation") != 2
		or title.get_parent() != header
		or button.get_parent() != header
		or detail.get_parent() != header.get_parent()
		or button.size_flags_vertical != Control.SIZE_SHRINK_CENTER
		or title.vertical_alignment != VERTICAL_ALIGNMENT_BOTTOM
		or title.get_theme_font_size("font_size") != 15
	):
		_fail("概览页没有保持紧凑条目、标题按钮同行或标准按钮高度。")
		return false
	button.pressed.emit()
	if (
		navigation.has_focus()
		or navigation.get_selected() == null
		or str(navigation.get_selected().get_metadata(0)) != "runtime"
	):
		_fail("概览页打开项目配置后，左侧导航没有仅同步选中状态，或仍显示焦点框。")
		return false
	return true


func _verify_management_page(
	dialog: Window,
	page_id: String,
	tree_name: String,
	refresh_button_name: String,
	create_button_name: String,
	manage_button_name: String,
	page_validate_button_name: String,
	file_dialog_name: String,
	generated_path: String,
	manager_name: String,
	manager_title: String,
	managed_label_name: String,
	search_name: String,
	entries_tree_name: String,
	manage_validate_button_name: String,
	report_name: String
) -> bool:
	if not _select_framework_page(dialog, page_id):
		return false
	var tree := dialog.find_child(tree_name, true, false) as Tree
	if tree == null or tree.get_root() == null:
		_fail("统一管理页没有显示现有配置列表：%s" % page_id)
		return false
	var refresh_button := dialog.find_child(refresh_button_name, true, false) as Button
	if refresh_button == null:
		_fail("统一管理页缺少主动刷新入口：%s" % page_id)
		return false
	var create_button := dialog.find_child(create_button_name, true, false) as Button
	if create_button == null:
		_fail("统一管理页缺少创建入口：%s" % page_id)
		return false
	var manage_button := dialog.find_child(manage_button_name, true, false) as Button
	if manage_button == null:
		_fail("统一管理页缺少管理入口：%s" % page_id)
		return false
	var page_validate_button := dialog.find_child(
		page_validate_button_name, true, false) as Button
	if page_validate_button == null:
		_fail("统一管理页缺少校验入口：%s" % page_id)
		return false

	create_button.pressed.emit()
	await process_frame
	var file_dialog := dialog.find_child(file_dialog_name, true, false) as FileDialog
	if file_dialog == null:
		_fail("创建入口没有打开文件选择器：%s" % page_id)
		return false
	file_dialog.file_selected.emit(generated_path)
	file_dialog.hide()
	await process_frame
	await process_frame
	var generated_item := _find_tree_item_by_metadata(tree.get_root(), generated_path)
	if (
		generated_item == null
		or tree.get_selected() != generated_item
		or manage_button.disabled
	):
		_fail("创建资源后列表没有立即刷新、选中新资源并启用管理入口：%s" % page_id)
		return false

	var manager := dialog.find_child(manager_name, true, false) as Window
	if manager == null or not manager.visible:
		manage_button.pressed.emit()
		await process_frame
		manager = dialog.find_child(manager_name, true, false) as Window
	if (
		manager == null
		or not manager.visible
		or manager.title != manager_title
		or manager.find_child(managed_label_name, true, false) == null
		or manager.find_child(search_name, true, false) == null
		or manager.find_child(entries_tree_name, true, false) == null
	):
		_fail("管理弹窗没有使用统一工具栏布局或固定标题：%s" % page_id)
		return false
	if page_id == "ui_config":
		var entries_tree := manager.find_child(entries_tree_name, true, false) as Tree
		if entries_tree.get_column_title(4) != "Reuse":
			_fail("UI 配置表格的 Reuse 表头不正确。")
			return false
		for column in range(2, 6):
			if entries_tree.get_column_title_alignment(column) != HORIZONTAL_ALIGNMENT_CENTER:
				_fail("UI 配置表格后四列表头没有居中：%d" % column)
				return false
	var managed_label := manager.find_child(managed_label_name, true, false) as Label
	if managed_label.text.contains("项目发现"):
		_fail("管理弹窗仍显示重复的项目资源数量：%s" % page_id)
		return false
	var manage_validate_button := manager.find_child(
		manage_validate_button_name, true, false) as Button
	if manage_validate_button == null:
		_fail("管理弹窗缺少校验入口：%s" % page_id)
		return false
	if not await _verify_nested_window_stack(dialog, manager, page_id):
		return false

	manage_validate_button.pressed.emit()
	await process_frame
	var report := dialog.find_child(report_name, true, false) as Window
	if (
		report == null
		or not report.visible
		or not report.exclusive
		or not report.transient_to_focused
		or not manager.visible
	):
		_fail("校验提示没有显示在管理弹窗上层，或关闭了下层管理页：%s" % page_id)
		return false
	report.hide()
	await process_frame
	if not manager.visible:
		_fail("关闭校验提示后管理弹窗没有保留：%s" % page_id)
		return false

	manager.hide()
	page_validate_button.pressed.emit()
	await process_frame
	if report == null or not report.visible:
		_fail("主页校验没有显示结果：%s" % page_id)
		return false
	report.hide()
	await process_frame
	tree.item_activated.emit()
	await process_frame
	if not manager.visible:
		_fail("关闭主页校验结果后，双击选中项无法再次进入管理页：%s" % page_id)
		return false
	manager.hide()

	create_button.pressed.emit()
	await process_frame
	if DirAccess.remove_absolute(ProjectSettings.globalize_path(generated_path)) != OK:
		_fail("隔离验证无法删除新建资源：%s" % page_id)
		return false
	file_dialog.canceled.emit()
	file_dialog.hide()
	await process_frame
	await process_frame
	if _find_tree_item_by_metadata(tree.get_root(), generated_path) != null:
		_fail("创建窗口内删除资源并取消后，主页列表没有刷新：%s" % page_id)
		return false
	refresh_button.pressed.emit()
	return true


func _verify_nested_window_stack(dialog: Window, manager: Window, page_id: String) -> bool:
	if not manager.exclusive or not manager.transient_to_focused:
		_fail("管理弹窗没有作为根窗口之上的模态子窗口：%s" % page_id)
		return false
	if page_id == "manifest":
		var add_button := manager.find_child(
			"GoDoManifestAddResourceButton", true, false) as Button
		var file_dialog := dialog.find_child(
			"GoDoResourceFileDialog", true, false) as Window
		if add_button == null or file_dialog == null:
			_fail("资源清单管理缺少添加资源窗口链。")
			return false
		add_button.pressed.emit()
		await process_frame
		if (
			not file_dialog.visible
			or not file_dialog.exclusive
			or not file_dialog.transient_to_focused
			or not manager.visible
		):
			_fail("资源选择窗口没有保持在资源清单管理窗上层。")
			return false
		file_dialog.hide()
		await process_frame
		if not manager.visible:
			_fail("取消资源选择后资源清单管理窗没有保留。")
			return false
		return true

	var add_button := manager.find_child("GoDoUiConfigAddButton", true, false) as Button
	var entry_dialog := manager.find_child("GoDoUiConfigEntryDialog", true, false) as Window
	var choose_scene_button := manager.find_child(
		"GoDoUiConfigChooseSceneButton", true, false) as Button
	var scene_dialog := dialog.find_child("GoDoUiSceneFileDialog", true, false) as Window
	if (
		add_button == null
		or entry_dialog == null
		or choose_scene_button == null
		or scene_dialog == null
	):
		_fail("UI 配置管理缺少条目或场景选择窗口链。")
		return false
	add_button.pressed.emit()
	await process_frame
	if (
		not entry_dialog.visible
		or not entry_dialog.exclusive
		or not entry_dialog.transient_to_focused
		or not manager.visible
	):
		_fail("UI 条目编辑窗口没有保持在 UI 配置管理窗上层。")
		return false
	choose_scene_button.pressed.emit()
	await process_frame
	if (
		not scene_dialog.visible
		or not scene_dialog.exclusive
		or not scene_dialog.transient_to_focused
		or not entry_dialog.visible
		or not manager.visible
	):
		_fail("UI 场景选择窗口改变了根窗口或管理窗口层级。")
		return false
	scene_dialog.hide()
	await process_frame
	if not entry_dialog.visible or not manager.visible:
		_fail("取消 UI 场景选择后条目编辑与管理窗口没有保留。")
		return false
	entry_dialog.hide()
	await process_frame
	return manager.visible


func _verify_csproj_page(framework_window: Window) -> bool:
	if not _select_framework_page(framework_window, "runtime"):
		return false
	var report := framework_window.find_child("GoDoProjectConfigReport", true, false) as RichTextLabel
	var refresh := framework_window.find_child("GoDoProjectConfigCheckButton", true, false) as Button
	var repair := framework_window.find_child("GoDoCsprojRepairButton", true, false) as Button
	var confirmation := _find_window(root, "修复 GoDo C# 项目配置") as ConfirmationDialog
	if (
		report == null
		or not report.get_parsed_text().contains("当前状态：")
		or not report.get_parsed_text().contains("GoDoFramework 版本")
		or not report.get_parsed_text().contains("C# 项目配置")
		or framework_window.find_child("GoDoRuntimeSectionTitle", true, false) != null
		or framework_window.find_child("GoDoCsprojSectionTitle", true, false) != null
		or framework_window.find_child("GoDoProjectConfigSectionSeparator", true, false) != null
		or framework_window.find_child("GoDoCsprojReport", true, false) != null
		or framework_window.find_child("GoDoCsprojMessage", true, false) != null
		or framework_window.find_child("GoDoCsprojActions", true, false) != null
	):
		_fail("项目配置没有合并为单一报告、提示和操作区。报告：%s" % (
			"<missing>" if report == null else report.get_parsed_text()
		))
		return false
	if refresh == null:
		_fail("项目配置缺少统一的重新检查按钮。")
		return false
	refresh.pressed.emit()
	if (
		not report.get_parsed_text().contains("GoDoFramework 版本")
		or not report.get_parsed_text().contains("C# 项目配置")
	):
		_fail("统一重新检查没有同时刷新 Runtime 与 C# 项目配置。")
		return false
	if not _verify_status_page_layout(
		framework_window,
		"RuntimePage",
		"GoDoProjectConfigReport",
		"GoDoProjectConfigMessage",
		"GoDoProjectConfigActions"
	):
		return false
	if (
		repair == null
		or repair.text != "修复 C# 项目配置..."
		or not repair.tooltip_text.contains(".csproj")
		or not repair.disabled
	):
		_fail("C# 项目已就绪时修复按钮没有禁用。")
		return false
	if confirmation == null or not confirmation.dialog_text.is_empty():
		_fail("C# 项目确认窗口没有保持按需精确预览。")
		return false
	var page := framework_window.find_child("RuntimePage", true, false) as VBoxContainer
	var coordinator: RefCounted = page._controller if page != null else null
	if coordinator == null:
		_fail("项目配置页缺少统一控制器。")
		return false
	coordinator._csproj_controller.request_repair()
	if (
		not report.get_parsed_text().contains("GoDoFramework 版本")
		or not report.get_parsed_text().contains("C# 项目配置")
	):
		_fail("C# 状态变化分支覆盖了统一项目配置报告。")
		return false
	var runtime_state: Dictionary = coordinator._runtime_controller.inspect()
	runtime_state.autoload_healthy = false
	runtime_state.autoload_missing = true
	var combined: Dictionary = coordinator._combined_status(
		runtime_state,
		{"healthy": false, "can_repair": true, "detail": "测试缺少规则"}
	)
	if not combined.advice.contains("Runtime：") or not combined.advice.contains("C# 项目配置："):
		_fail("综合状态没有同时说明 Runtime 与 C# 项目配置行动。")
		return false
	return true


func _verify_project_config_dispose(framework_window: Window) -> bool:
	var page := framework_window.find_child("RuntimePage", true, false) as VBoxContainer
	var coordinator: RefCounted = page._controller if page != null else null
	if coordinator == null:
		_fail("释放检查缺少统一项目配置控制器。")
		return false
	var runtime_controller: RefCounted = coordinator._runtime_controller
	var csproj_controller: RefCounted = coordinator._csproj_controller
	if (
		not runtime_controller.state_changed.is_connected(coordinator._on_runtime_state_changed)
		or not csproj_controller.state_changed.is_connected(coordinator._on_csproj_state_changed)
	):
		_fail("统一项目配置控制器没有建立状态连接。")
		return false
	page.dispose()
	if (
		runtime_controller.state_changed.is_connected(coordinator._on_runtime_state_changed)
		or csproj_controller.state_changed.is_connected(coordinator._on_csproj_state_changed)
	):
		_fail("统一项目配置控制器释放后仍保留状态连接。")
		return false
	return true


func _verify_precreated_window_layering(framework_window: Window) -> bool:
	var dialog_names := PackedStringArray([
		"GoDoRuntimeUninstallDialog",
		"GoDoCsprojRepairConfirmation",
		"ManifestFileDialog",
		"GoDoResourceFileDialog",
		"GoDoManifestAddConfirmDialog",
		"GoDoManifestManageDialog",
		"GoDoManifestReportDialog",
		"ManifestSelectorDialog",
		"UiConfigFileDialog",
		"GoDoUiSceneFileDialog",
		"UiConfigSelectorDialog",
		"GoDoUiConfigManageDialog",
		"GoDoUiConfigReportDialog",
	])
	for dialog_name in dialog_names:
		var dialog := root.find_child(dialog_name, true, false) as Window
		if (
			dialog == null
			or dialog.get_parent() != framework_window
			or not dialog.exclusive
			or not dialog.transient_to_focused
		):
			_fail("顶层工具窗口没有挂到 GoDo Framework 根窗口：%s" % dialog_name)
			return false
	return true


func _open_and_verify_management_selector(
	menu: PopupMenu,
	menu_label: String,
	dialog_name: String,
	tree_name: String,
	create_button_name: String,
	manual_button_name: String,
	file_dialog_name: String
) -> bool:
	var menu_id := _find_menu_id(menu, menu_label)
	if menu_id < 0:
		_fail("未找到菜单项：%s" % menu_label)
		return false
	menu.id_pressed.emit(menu_id)
	await process_frame
	await process_frame

	var dialog := root.find_child(dialog_name, true, false) as Window
	if dialog == null or not dialog.visible:
		_fail("菜单没有打开管理选择弹窗：%s" % menu_label)
		return false
	var selector_tree := dialog.find_child(tree_name, true, false) as Tree
	if selector_tree == null or selector_tree.get_root() == null:
		_fail("管理选择弹窗没有显示配置列表：%s" % menu_label)
		return false
	if dialog.find_child(create_button_name, true, false) == null:
		_fail("管理选择弹窗缺少创建入口：%s" % menu_label)
		return false
	var manual_button := dialog.find_child(manual_button_name, true, false) as Button
	if manual_button == null:
		_fail("管理选择弹窗缺少手动选择入口：%s" % menu_label)
		return false
	manual_button.pressed.emit()
	await process_frame
	await process_frame
	var file_dialog := root.find_child(file_dialog_name, true, false) as Window
	if file_dialog == null or not file_dialog.visible:
		_fail("手动选择入口没有打开文件弹窗：%s" % menu_label)
		return false
	file_dialog.hide()
	return true


func _open_and_verify(
	framework_window: Window,
	page_id: String,
	legacy_dialog_title: String,
	confirmation_title: String,
	report_name: String,
	message_name: String,
	action_button_name: String,
	source_button_name: String,
	official_url: String
) -> bool:
	if not _select_framework_page(framework_window, page_id):
		return false
	await process_frame
	var page_name := "%sPage" % page_id.to_pascal_case()
	var page := framework_window.find_child(page_name, true, false) as Control
	if page == null or not page.is_visible_in_tree():
		_fail("扩展内容没有嵌入右侧页面：%s" % page_id)
		return false
	if _find_window(root, legacy_dialog_title) != null:
		_fail("扩展仍创建了旧的主设置窗口：%s" % legacy_dialog_title)
		return false
	var report := page.find_child(report_name, true, false) as RichTextLabel
	if report == null or report.text.is_empty():
		_fail("%s 的状态报告为空。" % page_id)
		return false
	var message := page.find_child(message_name, true, false) as RichTextLabel
	if message == null or not message.text.contains("提示："):
		_fail("%s 缺少独立提示栏。" % page_id)
		return false
	var target_button := page.find_child(action_button_name, true, false) as Button
	if target_button == null or not target_button.disabled:
		_fail("%s 在健康状态下仍允许重复写入。" % page_id)
		return false
	var source_button := page.find_child(source_button_name, true, false) as Button
	if source_button == null or source_button.disabled or source_button.tooltip_text != official_url:
		_fail("%s 缺少可用且地址明确的官方来源按钮。" % page_id)
		return false
	var refresh_button := _find_button(page, "重新检查")
	if refresh_button == null:
		_fail("%s 缺少重新检查按钮。" % page_id)
		return false
	refresh_button.pressed.emit()
	await process_frame
	if not message.get_parsed_text().contains("重新检查完成"):
		_fail("%s 点击重新检查后没有可见反馈。" % page_id)
		return false
	var confirmation := _find_window(page, confirmation_title) as ConfirmationDialog
	if confirmation == null or not confirmation.exclusive or not confirmation.transient_to_focused:
		_fail("%s 的写入确认没有保留统一模态层级。" % page_id)
		return false
	var actions := target_button.get_parent() as HBoxContainer
	var content := report.get_parent() as VBoxContainer
	if (
		actions == null
		or content == null
		or message.get_parent() != content
		or actions.get_parent() != content
		or content.get_theme_constant("separation") != 10
		or actions.get_theme_constant("separation") != 8
		or report.size_flags_vertical != Control.SIZE_EXPAND_FILL
		or message.custom_minimum_size.y != 48
		or message.scroll_active
		or message.vertical_alignment != VERTICAL_ALIGNMENT_CENTER
	):
		_fail("%s 没有采用项目配置页的报告、提示和操作栏布局。" % page_id)
		return false
	return true


func _open_and_verify_friflo_dependency(framework_window: Window) -> bool:
	if not _select_framework_page(framework_window, "extension_godo_friflo_ecs"):
		return false
	await process_frame
	var page := framework_window.find_child(
		"ExtensionGodoFrifloEcsPage", true, false) as Control
	if page == null or not page.is_visible_in_tree():
		_fail("Friflo ECS 内容没有嵌入右侧页面。")
		return false
	if _find_window(root, "GoDo Friflo ECS 依赖检查") != null:
		_fail("Friflo ECS 仍创建了旧的主依赖窗口。")
		return false
	var report := page.find_child("FrifloEcsDependencyReport", true, false) as RichTextLabel
	var note := page.find_child("FrifloEcsInstallBoundaryNote", true, false) as Label
	var message := page.find_child("FrifloEcsMessage", true, false) as RichTextLabel
	var refresh := page.find_child("FrifloEcsRefreshButton", true, false) as Button
	var install := page.find_child("FrifloEcsInstallButton", true, false) as Button
	var source := page.find_child("FrifloEcsOfficialSourceButton", true, false) as Button
	if report == null or not report.get_parsed_text().contains("已就绪"):
		_fail("Friflo ECS 没有识别当前项目的已验证依赖。")
		return false
	if note != null or message == null or not message.get_parsed_text().contains("中央包管理始终只读"):
		_fail("Friflo ECS 检查没有明确自动写入边界。")
		return false
	if refresh == null or install == null or not install.disabled:
		_fail("Friflo ECS 已就绪状态没有禁用安装，或缺少检查操作。")
		return false
	refresh.pressed.emit()
	await process_frame
	if not message.get_parsed_text().contains("重新检查完成"):
		_fail("Friflo ECS 点击重新检查后没有可见反馈。")
		return false
	if source == null or source.disabled or source.tooltip_text != "https://www.nuget.org/packages/Friflo.Engine.ECS/3.6.0":
		_fail("Friflo ECS 检查缺少精确版本的 NuGet 官方来源。")
		return false
	var confirmation := _find_window(page, "添加 Friflo ECS 依赖") as ConfirmationDialog
	if (
		confirmation == null
		or not confirmation.dialog_text.contains(".godo-backup")
		or not confirmation.exclusive
		or not confirmation.transient_to_focused
	):
		_fail("Friflo ECS 安装确认缺少精确变更或备份说明。")
		return false
	var actions := install.get_parent() as HBoxContainer
	var content := report.get_parent() as VBoxContainer
	if (
		actions == null
		or content == null
		or message.get_parent() != content
		or actions.get_parent() != content
		or content.get_theme_constant("separation") != 10
		or actions.get_theme_constant("separation") != 8
		or message.custom_minimum_size.y != 48
		or message.scroll_active
		or message.vertical_alignment != VERTICAL_ALIGNMENT_CENTER
	):
		_fail("Friflo ECS 没有采用项目配置页的统一布局。")
		return false
	return true


func _open_and_verify_setup(framework_window: Window) -> bool:
	var controller = SETUP_CONTROLLER_SCRIPT.new()
	if controller._parse_version("4.7") != Vector3i(-1, -1, -1):
		_fail("两段式 Godot 兼容版本没有按元数据契约拒绝。")
		return false
	if controller._parse_version("4.7.0") != Vector3i(4, 7, 0):
		_fail("最低支持版本 4.7.0 无法解析。")
		return false
	var minimum := Vector3i(4, 7, 1)
	var tested := Vector3i(4, 7, 2)
	if controller._evaluate_version(Vector3i(4, 7, 0), minimum, tested).supported:
		_fail("低于最低版本的 Godot 未被拒绝。")
		return false
	var newer: Dictionary = controller._evaluate_version(Vector3i(4, 7, 3), minimum, tested)
	if not newer.supported or newer.tested:
		_fail("高于已验证版本的同 major Godot 未进入兼容但未验证状态。")
		return false
	var engine_version := Engine.get_version_info()
	var current := Vector3i(engine_version.major, engine_version.minor, engine_version.patch)
	var current_compatibility: Dictionary = controller._evaluate_version(current, minimum, tested)
	if not current_compatibility.supported:
		_fail("当前测试引擎不在 Setup 声明的兼容 major 范围内。")
		return false
	var current_text := "%d.%d.%d" % [current.x, current.y, current.z]
	var expected_compatibility_text := (
		"当前 %s，已验证范围 4.7.0～4.7.2" % current_text
		if current_compatibility.tested
		else "当前 %s，高于已验证版本 4.7.2" % current_text
	)

	if not _select_framework_page(framework_window, "runtime"):
		return false
	await process_frame
	var report := framework_window.find_child("GoDoProjectConfigReport", true, false) as RichTextLabel
	if (
		report == null
		or not report.get_parsed_text().contains("GoDoFramework 版本")
		or not report.get_parsed_text().contains("Godot 兼容性")
		or not report.get_parsed_text().contains(expected_compatibility_text)
	):
		_fail("Setup 未显示框架版本和 Godot 兼容性：%s" % ("<missing>" if report == null else report.get_parsed_text()))
		return false
	if (
		framework_window.find_child("GoDoProjectConfigCheckButton", true, false) == null
		or framework_window.find_child("GoDoRuntimeInstallButton", true, false) == null
		or framework_window.find_child("GoDoRuntimeUninstallButton", true, false) == null
	):
		_fail("Runtime 页面缺少嵌入式检查、安装或卸载操作。")
		return false
	if not _verify_status_page_layout(
		framework_window,
		"RuntimePage",
		"GoDoProjectConfigReport",
		"GoDoProjectConfigMessage",
		"GoDoProjectConfigActions"
	):
		return false
	return true


func _verify_status_page_layout(
	framework_window: Window,
	page_name: String,
	report_name: String,
	message_name: String,
	actions_name: String
) -> bool:
	var page := framework_window.find_child(page_name, true, false) as VBoxContainer
	var report := framework_window.find_child(report_name, true, false) as RichTextLabel
	var message := framework_window.find_child(message_name, true, false) as RichTextLabel
	var actions := framework_window.find_child(actions_name, true, false) as HBoxContainer
	if (
		page == null
		or report == null
		or message == null
		or actions == null
		or page.get_theme_constant("separation") != 10
		or report.size_flags_vertical != Control.SIZE_EXPAND_FILL
		or message.custom_minimum_size.y != 48.0
		or message.scroll_active
		or message.vertical_alignment != VERTICAL_ALIGNMENT_CENTER
		or actions.get_theme_constant("separation") != 8
	):
		_fail("项目配置页没有保持统一的报告、提示或操作区布局：%s" % page_name)
		return false
	return true


func _open_and_verify_datatable(framework_window: Window) -> bool:
	if not _select_framework_page(framework_window, "extension_godo_datatable"):
		return false
	var action_button := _find_button(framework_window, "数据表配置 (DataTable Configuration)...")
	if action_button == null:
		_fail("编辑器扩展页缺少 DataTable 配置入口。当前按钮：%s" % ", ".join(_button_texts(framework_window)))
		return false
	action_button.pressed.emit()
	await process_frame

	var dialog := _find_window(root, "GoDo DataTable")
	if dialog == null:
		_fail("未找到 GoDo DataTable 窗口。")
		return false
	if not _verify_open_child_window_layering(framework_window, dialog):
		return false
	var selector := dialog.find_child("DataTableTableSelector", true, false) as OptionButton
	if selector == null:
		_fail("未找到 DataTable 单表选择器。")
		return false
	var generate_button := dialog.find_child("DataTableGenerateSelectedButton", true, false) as Button
	var generate_all_button := dialog.find_child("DataTableGenerateButton", true, false) as Button
	var export_spacer := dialog.find_child("DataTableExportSpacer", true, false) as Control
	if (
		generate_button == null
		or generate_all_button == null
		or export_spacer == null
		or generate_button.get_parent() != selector.get_parent()
		or generate_all_button.get_parent() != selector.get_parent()
	):
		_fail("数据表导出按钮未与表选择器排列在同一行。")
		return false
	if generate_button.text != "导出当前表..." or generate_all_button.text != "导出全部表...":
		_fail("数据表导出按钮文本不准确。")
		return false
	if selector.size.x > 390.0 or generate_button.position.x - (selector.position.x + selector.size.x) > 12.0:
		_fail("数据表选择器过宽，或“导出当前表”未紧贴选择器。")
		return false
	if (
		export_spacer.size_flags_horizontal != Control.SIZE_EXPAND_FILL
		or export_spacer.position.x <= generate_button.position.x
		or generate_all_button.get_theme_color("font_color") != Color("#8BD49C")
	):
		_fail("“导出全部表”未保持靠右或缺少主操作提示色。")
		return false
	var normal_style := generate_all_button.get_theme_stylebox("normal") as StyleBoxFlat
	var hover_style := generate_all_button.get_theme_stylebox("hover") as StyleBoxFlat
	var pressed_style := generate_all_button.get_theme_stylebox("pressed") as StyleBoxFlat
	if (
		normal_style == null
		or hover_style == null
		or pressed_style == null
		or normal_style.border_width_left != 1
		or hover_style.bg_color.a <= normal_style.bg_color.a
		or pressed_style.bg_color.a <= hover_style.bg_color.a
	):
		_fail("“导出全部表”缺少描边、悬停或按下状态样式。")
		return false
	var python_input := dialog.find_child("DataTablePythonInput", true, false) as LineEdit
	if python_input == null or python_input.placeholder_text != "可留空，将自动检测 python3 / python":
		_fail("Python 自动检测提示未放入输入框。")
		return false
	var schema_input := dialog.find_child("DataTableBuildConfigInput", true, false) as LineEdit
	if schema_input == null or not schema_input.placeholder_text.begins_with("例如：res://"):
		_fail("Schema 路径提示未使用示例风格。")
		return false
	var check_button := dialog.find_child("DataTableCheckButton", true, false) as Button
	var create_button := dialog.find_child("DataTableCreateSchemaButton", true, false) as Button
	var edit_button := dialog.find_child("DataTableEditSchemaButton", true, false) as Button
	if check_button == null or create_button == null or edit_button == null:
		_fail("DataTable Schema 首行功能按钮不完整。")
		return false
	var config_row := dialog.find_child("DataTableConfigRow", true, false) as HBoxContainer
	if (
		config_row == null
		or schema_input.get_parent() != config_row
		or check_button.get_parent() != config_row
		or create_button.get_parent() != config_row
		or edit_button.get_parent() != config_row
		or check_button.text != "校验"
		or create_button.text != "新建"
		or edit_button.text != "编辑"
		or dialog.min_size.x < 960
	):
		_fail("DataTable 三个短操作按钮未排在 Schema 首行，或窗口宽度不足。")
		return false
	var report := dialog.find_child("DataTableReport", true, false) as RichTextLabel
	var message := dialog.find_child("DataTableMessage", true, false) as RichTextLabel
	if report == null or message == null or not message.text.contains("提示："):
		_fail("DataTable 缺少状态报告或独立提示栏。")
		return false
	var expected := PackedStringArray()
	var schema_text := FileAccess.get_file_as_string(schema_input.text)
	var parsed_schema = JSON.parse_string(schema_text)
	if parsed_schema is Dictionary:
		for table in parsed_schema.get("tables", []):
			expected.append(str(table.get("id", "")))
	if selector.item_count != expected.size():
		_fail("DataTable 单表数量错误：%d。" % selector.item_count)
		return false
	for index in expected.size():
		if selector.get_item_text(index) != expected[index] or selector.get_item_text(index) == "项目":
			_fail(
				"DataTable 表 ID 被改写：期望 %s，实际 %s。" % [
					expected[index],
					selector.get_item_text(index),
				]
			)
			return false
	check_button.pressed.emit()
	for attempt in 100:
		if not check_button.disabled:
			break
		await create_timer(0.1).timeout
	if check_button.disabled:
		_fail("DataTable 校验操作未在预期时间内完成。")
		return false
	if message.text.contains("全部数据校验通过"):
		if report.text.contains("[DataTableCompiler] CHECK PASS") or not report.text.contains("当前状态："):
			_fail("DataTable 校验成功后未恢复为正常状态信息。")
			return false
	else:
		var parsed_report := report.get_parsed_text()
		if (
			not parsed_report.contains("[DataTableCompiler] FAIL")
			or parsed_report.contains("鏃")
			or parsed_report.contains("澶")
			or parsed_report.contains("�")
		):
			_fail("DataTable 失败诊断未以可读 UTF-8 文本显示：%s" % parsed_report)
			return false
	if not await _verify_datatable_schema_editor(dialog):
		return false
	dialog.hide()
	if not framework_window.visible:
		_fail("关闭 DataTable 子窗口后 GoDo Framework 根窗口不可见。")
		return false
	return true


func _verify_open_child_window_layering(parent_window: Window, dialog: Window) -> bool:
	if not parent_window.visible:
		_fail("打开子窗口时父工具窗口被隐藏：%s" % dialog.title)
		return false
	if not dialog.visible:
		_fail("子窗口没有保持可见：%s" % dialog.title)
		return false
	if dialog.get_parent() != parent_window:
		_fail("子窗口没有挂到实际父工具窗口：%s" % dialog.title)
		return false
	if not dialog.exclusive or not dialog.transient_to_focused:
		_fail("子窗口没有使用统一模态层级策略：%s" % dialog.title)
		return false
	return true


func _verify_datatable_schema_editor(datatable_dialog: Window) -> bool:
	var edit_button := datatable_dialog.find_child("DataTableEditSchemaButton", true, false) as Button
	if edit_button == null:
		_fail("未找到编辑 Schema 按钮。")
		return false
	edit_button.pressed.emit()
	await process_frame
	var dialog := _find_window(root, "DataTable Schema 编辑器")
	if dialog == null:
		_fail("未找到 DataTable Schema 编辑器。")
		return false
	if not _verify_open_child_window_layering(datatable_dialog, dialog):
		return false
	var advanced := dialog.find_child("DataTableSchemaAdvancedSettings", true, false) as GridContainer
	var advanced_button := dialog.find_child("DataTableSchemaAdvancedSettingsButton", true, false) as Button
	if advanced == null or advanced_button == null or advanced.visible:
		_fail("Schema 高级路径设置未默认折叠。")
		return false
	var dataset_grid := dialog.find_child("DataTableSchemaDatasetGrid", true, false) as GridContainer
	var dataset_options := dialog.find_child("DataTableSchemaDatasetOptions", true, false) as HBoxContainer
	var data_set_id := dialog.find_child("DataTableSchemaDataSetId", true, false) as LineEdit
	if (
		dataset_grid == null
		or dataset_options == null
		or data_set_id == null
		or dataset_grid.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or data_set_id.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or advanced_button.size_flags_horizontal == Control.SIZE_EXPAND_FILL
	):
		_fail("Schema 数据集配置区未使用紧凑布局。")
		return false
	var data_files_top_separator := dialog.find_child("DataTableSchemaDataFilesTopSeparator", true, false) as HSeparator
	var data_files_bottom_separator := dialog.find_child("DataTableSchemaDataFilesBottomSeparator", true, false) as HSeparator
	if data_files_top_separator == null or data_files_bottom_separator == null:
		_fail("Schema 数据文件区域缺少上下分隔线。")
		return false
	var data_files := dialog.find_child("DataTableSchemaDataFiles", true, false) as Tree
	if data_files == null or data_files.columns != 3 or data_files.get_column_title(2) != "数据表 ID":
		_fail("Schema 数据文件未按文件、状态、数据表 ID 三列显示。")
		return false
	var data_file := data_files.get_root().get_first_child()
	if data_file == null or data_file.get_text(1) != "已加入" or data_file.get_text(2).is_empty():
		_fail("Schema 数据文件状态或表 ID 显示错误。")
		return false
	var add_csv := dialog.find_child("DataTableSchemaAddCsvButton", true, false) as Button
	var remove_csv := dialog.find_child("DataTableSchemaRemoveCsvButton", true, false) as Button
	data_files.set_selected(data_file, 0)
	data_files.item_selected.emit()
	if (
		data_files.select_mode != Tree.SELECT_ROW
		or add_csv == null
		or remove_csv == null
		or not add_csv.disabled
		or remove_csv.disabled
	):
		_fail("Schema 数据文件整行选择或加入/移出操作状态错误。")
		return false
	var table_id := dialog.find_child("DataTableSchemaTableId", true, false) as LineEdit
	var table_source := dialog.find_child("DataTableSchemaTableSource", true, false) as LineEdit
	var primary_key := dialog.find_child("DataTableSchemaPrimaryKey", true, false) as OptionButton
	var schema_version := dialog.find_child("DataTableSchemaVersion", true, false) as LineEdit
	var schema_version_hint := dialog.find_child("DataTableSchemaVersionHint", true, false) as Label
	var table_selector := dialog.find_child("DataTableSchemaTableSelector", true, false) as OptionButton
	var table_details := dialog.find_child("DataTableSchemaTableDetails", true, false) as HBoxContainer
	var table_id_label := dialog.find_child("DataTableSchemaTableIdLabel", true, false) as Label
	var export_scope_label := dialog.find_child("DataTableSchemaExportScopeLabel", true, false) as Label
	var protocol_version_hint := dialog.find_child("DataTableSchemaProtocolVersionHint", true, false) as Label
	if (
		table_id == null
		or table_source == null
		or primary_key == null
		or schema_version == null
		or schema_version_hint == null
		or table_selector == null
		or table_details == null
		or table_id_label == null
		or export_scope_label == null
		or protocol_version_hint == null
		or table_id.editable
		or table_source.editable
		or schema_version.editable
		or table_id.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or table_source.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or schema_version.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or table_selector.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or table_details.size_flags_horizontal != Control.SIZE_SHRINK_BEGIN
		or table_id.text != "ItemCategory"
		or primary_key.item_count == 0
		or not schema_version.text.is_valid_int()
		or schema_version_hint.text != "保存结构变更时由工具自动递增"
		or schema_version_hint.get_parent() != schema_version.get_parent()
		or table_id_label.text != "数据表 ID"
		or export_scope_label.text != "数据导出范围"
		or protocol_version_hint.text != "客户端与服务器共享数据结构不兼容时手动递增"
	):
		_fail("Schema 表级安全编辑控件状态错误。")
		return false
	var rename_table_id := dialog.find_child("DataTableSchemaRenameTableIdButton", true, false) as Button
	var table_value_input := dialog.find_child("DataTableSchemaTableValueInput", true, false) as LineEdit
	if rename_table_id == null or table_value_input == null:
		_fail("Schema 缺少表 ID 重命名控件。")
		return false
	var rename_connections := rename_table_id.pressed.get_connections()
	var schema_editor: Object = (
		rename_connections[0].callable.get_object() if not rename_connections.is_empty() else null
	)
	if schema_editor == null:
		_fail("Schema 表 ID 重命名操作未连接到编辑器。")
		return false
	var original_file_name := data_file.get_text(0)
	var original_state := data_file.get_text(1)
	var original_table_id := data_file.get_text(2)
	schema_editor.set("_table_value_mode", "table_id")
	table_value_input.text = "%sUiRegression" % original_table_id
	schema_editor.call("_apply_table_value_change")
	await process_frame
	var renamed_data_file := data_files.get_root().get_first_child()
	while renamed_data_file != null and renamed_data_file.get_text(0) != original_file_name:
		renamed_data_file = renamed_data_file.get_next()
	if (
		renamed_data_file == null
		or renamed_data_file.get_text(1) != original_state
		or renamed_data_file.get_text(2) != "%sUiRegression" % original_table_id
	):
		_fail("Schema 表 ID 重命名后，数据文件列表未立即同步。")
		return false
	var renamed_table_id := "%sUiRegression" % original_table_id
	var item_index := _find_option_index(table_selector, "Item")
	if item_index < 0:
		_fail("Schema 外键联动回归缺少 Item 数据表。")
		return false
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	var relation_fields := dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var category_field := _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.id" % renamed_table_id:
		_fail("重命名数据表 ID 后，引用它的外键未同步更新。")
		return false
	var renamed_index := _find_option_index(table_selector, renamed_table_id)
	if renamed_index < 0:
		_fail("重命名后的数据表未保留在选择器中。")
		return false
	table_selector.select(renamed_index)
	table_selector.item_selected.emit(renamed_index)
	await process_frame
	schema_editor.set("_table_value_mode", "table_id")
	table_value_input.text = original_table_id
	schema_editor.call("_apply_table_value_change")
	await process_frame
	item_index = _find_option_index(table_selector, "Item")
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	relation_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_field = _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.id" % original_table_id:
		_fail("恢复数据表 ID 后，引用它的外键未同步恢复。")
		return false
	var category_index := _find_option_index(table_selector, original_table_id)
	table_selector.select(category_index)
	table_selector.item_selected.emit(category_index)
	await process_frame
	var category_fields := dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var category_id_field := _find_tree_item(category_fields, "id")
	if category_id_field == null:
		_fail("Schema 外键联动回归缺少 ItemCategory.id。")
		return false
	await _edit_tree_text_cell(category_fields, category_id_field, 0, "category_key")
	item_index = _find_option_index(table_selector, "Item")
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	relation_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_field = _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.category_key" % original_table_id:
		_fail("重命名主键字段后，引用它的外键未同步更新：%s。" % (
			"<missing>" if category_field == null else category_field.get_text(9)
		))
		return false
	table_selector.select(category_index)
	table_selector.item_selected.emit(category_index)
	await process_frame
	category_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_id_field = _find_tree_item(category_fields, "category_key")
	await _edit_tree_text_cell(category_fields, category_id_field, 0, "id")
	table_selector.select(item_index)
	table_selector.item_selected.emit(item_index)
	await process_frame
	relation_fields = dialog.find_child("DataTableSchemaFields", true, false) as Tree
	category_field = _find_tree_item(relation_fields, "category_id")
	if category_field == null or category_field.get_text(9) != "%s.id" % original_table_id:
		_fail("恢复主键字段后，引用它的外键未同步恢复。")
		return false
	var create_table := dialog.find_child("DataTableSchemaCreateTableButton", true, false) as Button
	if create_table == null:
		_fail("Schema 缺少新建数据表操作。")
		return false
	var fields := dialog.find_child("DataTableSchemaFields", true, false) as Tree
	var field := fields.get_root().get_first_child() if fields != null and fields.get_root() != null else null
	if (
		fields == null
		or field == null
		or fields.select_mode != Tree.SELECT_SINGLE
		or fields.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED
		or fields.scroll_horizontal_enabled
		or field.is_editable(0)
		or not field.is_editable(1)
		or not field.is_editable(2)
		or field.get_text_alignment(2) != HORIZONTAL_ALIGNMENT_CENTER
	):
		_fail("Schema 字段表格选择、滚动或复选框布局错误。")
		return false
	fields.set_selected(field, 0)
	var target_column := 4
	var target_position := fields.get_item_area_rect(field, target_column).get_center()
	fields.item_mouse_selected.emit(target_position, MOUSE_BUTTON_LEFT)
	fields.item_activated.emit()
	await process_frame
	if (
		fields.get_selected_column() != target_column
		or not field.is_editable(target_column)
		or field.is_editable(0)
	):
		_fail("Schema 字段双击未进入目标单元格编辑。")
		return false
	fields.item_edited.emit()
	await process_frame
	var first_background := field.get_custom_bg_color(0)
	if (
		fields.get_selected_column() != target_column
		or not field.is_selected(target_column)
		or first_background.a <= 0.0
		or field.get_custom_bg_color(fields.columns - 1) != first_background
		or field.is_editable(target_column)
	):
		_fail("Schema 字段编辑结束后未恢复整行选择。")
		return false
	dialog.hide()
	if not datatable_dialog.visible:
		_fail("关闭 Schema 编辑器后 DataTable 父窗口不可见。")
		return false
	return true


func _find_menu_id(menu: PopupMenu, label: String) -> int:
	var index := _find_menu_index(menu, label)
	return menu.get_item_id(index) if index >= 0 else -1


func _find_menu_index(menu: PopupMenu, label: String) -> int:
	for index in menu.item_count:
		if menu.get_item_text(index) == label:
			return index
	return -1


func _select_framework_page(dialog: Window, page_id: String) -> bool:
	var navigation := dialog.find_child("GoDoFrameworkNavigation", true, false) as Tree
	if navigation == null or navigation.get_root() == null:
		_fail("统一窗口缺少导航树。")
		return false
	var item := _find_tree_item_by_metadata(navigation.get_root(), page_id)
	if item == null:
		_fail("统一窗口缺少页面：%s" % page_id)
		return false
	navigation.set_selected(item, 0)
	navigation.item_selected.emit()
	return true


func _find_tree_item_by_metadata(item: TreeItem, value: String) -> TreeItem:
	var child := item.get_first_child()
	while child != null:
		if str(child.get_metadata(0)) == value:
			return child
		var nested := _find_tree_item_by_metadata(child, value)
		if nested != null:
			return nested
		child = child.get_next()
	return null


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text and child.visible:
			return child
		var nested := _find_button(child, text)
		if nested != null:
			return nested
	return null


func _button_texts(node: Node) -> PackedStringArray:
	var result := PackedStringArray()
	for child in node.get_children():
		if child is Button and child.is_visible_in_tree():
			result.append(child.text)
		result.append_array(_button_texts(child))
	return result


func _find_window(node: Node, title: String) -> Window:
	for child in node.get_children():
		if child is Window and child.title == title:
			return child
		var nested := _find_window(child, title)
		if nested != null:
			return nested
	return null


func _find_option_index(options: OptionButton, text: String) -> int:
	for index in options.item_count:
		if options.get_item_text(index) == text:
			return index
	return -1


func _find_tree_item(tree: Tree, text: String) -> TreeItem:
	if tree == null or tree.get_root() == null:
		return null
	var item := tree.get_root().get_first_child()
	while item != null:
		if item.get_text(0) == text:
			return item
		item = item.get_next()
	return null


func _edit_tree_text_cell(tree: Tree, item: TreeItem, column: int, text: String) -> void:
	var position := tree.get_item_area_rect(item, column).get_center()
	tree.item_mouse_selected.emit(position, MOUSE_BUTTON_LEFT)
	tree.item_activated.emit()
	await process_frame
	item.set_text(column, text)
	tree.item_edited.emit()
	await process_frame


func _fail(message: String) -> void:
	push_error("[EditorExtensionUiRegression] FAIL: %s" % message)
	quit(1)
