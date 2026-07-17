@tool
extends SceneTree

const MENU_BUTTON_NAME := "GoDoFrameworkToolbarMenu"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	await process_frame
	var menu_button := root.find_child(MENU_BUTTON_NAME, true, false) as MenuButton
	if menu_button == null:
		_fail("未找到 GoDo 工具栏菜单。")
		return

	var menu := menu_button.get_popup()
	if not await _open_and_verify(
		menu,
		"GUIDE Input 设置...",
		"GoDo GUIDE Input 设置",
		"GuideInputReport",
		"GuideInputRepairButton"
	):
		return
	if not await _open_and_verify(
		menu,
		"Phantom Camera 设置...",
		"GoDo Phantom Camera 设置",
		"PhantomCameraReport",
		"PhantomCameraEnableButton"
	):
		return

	print("[EditorExtensionUiRegression] PASS (2/2)")
	quit(0)


func _open_and_verify(
	menu: PopupMenu,
	menu_label: String,
	dialog_title: String,
	report_name: String,
	action_button_name: String
) -> bool:
	var menu_id := _find_menu_id(menu, menu_label)
	if menu_id < 0:
		_fail("未找到菜单项：%s" % menu_label)
		return false
	menu.id_pressed.emit(menu_id)
	await process_frame

	var dialog := _find_window(root, dialog_title)
	if dialog == null:
		_fail("未找到窗口：%s" % dialog_title)
		return false
	var report := dialog.find_child(report_name, true, false) as RichTextLabel
	if report == null or report.text.is_empty():
		_fail("%s 的状态报告为空。" % dialog_title)
		return false
	var action_button := dialog.find_child(action_button_name, true, false) as Button
	if action_button == null or not action_button.disabled:
		_fail("%s 在健康状态下仍允许重复写入。" % dialog_title)
		return false
	dialog.hide()
	return true


func _find_menu_id(menu: PopupMenu, label: String) -> int:
	for index in menu.item_count:
		if menu.get_item_text(index) == label:
			return menu.get_item_id(index)
	return -1


func _find_window(node: Node, title: String) -> Window:
	for child in node.get_children():
		if child is Window and child.title == title:
			return child
		var nested := _find_window(child, title)
		if nested != null:
			return nested
	return null


func _fail(message: String) -> void:
	push_error("[EditorExtensionUiRegression] FAIL: %s" % message)
	quit(1)
