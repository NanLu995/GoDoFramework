# 可选 GUIDE Input 接入

GoDoTemplate 不分发 GUIDE / GuideCs，也不预设其 Autoload。需要输入 Context 时，先安装 `addons/guideCS/`，等待 Godot 扫描完成，再从 `GoDo -> GUIDE Input 设置...` 完成插件和 Autoload 安装。

随后创建一个 `GuideInputProfile`，并至少映射以下模板 ID：

```text
Action:  ui.back
Context: menu
Context: gameplay
Context: pause
```

在 `Boot.tscn` 的 `Boot` 节点下添加 `GuideInputBackendInstaller`，设置该 Profile 和独立的 `PersistenceSlot`。它必须先于 `Boot._Ready()` 安装完成；作为子节点时 Godot 会先调用该安装器的 `_Ready()`。

模板只使用 `IInputService`。未安装后端时不会读取 `InputFrame` 或切换 Context；安装成功后会自动使用 `menu`、`gameplay` 和 `pause` Context，并把 `ui.back` 转换为返回、关闭确认框或打开暂停菜单的业务意图。
