# GoDo EditorPlugin 使用指南

## 定位

本插件是 GoDo 的项目安装助手与健康检查工具，只负责检查、安装和卸载 `GoDoRuntime` Autoload。它不创建业务场景或 UI，不修改 C# 项目文件，也不参与导出后的游戏运行。

EditorPlugin 只依赖 Godot Editor API，不依赖 Services、ErrorHub 或其他运行时模块；Runtime 不反向依赖插件。

## 启用与检查

1. 在“项目设置 → 插件”中启用 `GoDo Framework`。
2. 打开“项目 → 工具 → GoDo Framework...”窗口。
3. 查看 Godot 版本、Runtime 场景、Autoload 和重复注册检查结果。

启用插件只增加工具菜单，不修改 Autoload。禁用插件只移除菜单和对话框，不卸载 Runtime。

## 安装

只有同时满足以下条件时“安装 Runtime”按钮才可用：

- 当前引擎为 Godot 4.7 或更高的 4.x 版本；
- `res://addons/godo_framework/Core/GoDoRuntime.tscn` 存在且可实例化；
- `GoDoRuntime` 名称尚未被占用；
- 没有其他 Autoload 名称指向同一个 Runtime 场景。

安装使用 Godot EditorPlugin 的 Autoload API，并在调用后重新检查实际状态。已正确安装时不会重复写入；名称冲突和重复注册只报告，不自动覆盖或删除。

## 卸载

卸载前必须经过确认。插件只会移除名称为 `GoDoRuntime` 且精确指向框架 Runtime 场景的 Autoload；路径不匹配时拒绝操作。

卸载不会删除框架文件、业务文件或其他 Autoload。禁用插件也不会触发卸载。

## 失败语义

- 健康检查是只读操作，问题以正常、警告、错误检查项展示。
- 预期冲突不会抛出到编辑器，而是禁用安装或拒绝卸载并显示原因。
- 非预期 Editor API 异常显示简短消息，同时通过 `GD.PushError` 输出完整异常。
- 所有写操作完成后重新检查，不能仅凭 Editor API 调用返回判断成功。

## 验证

- `dotnet build GoDoFramework.sln`：验证 C# EditorPlugin API 和运行时代码可编译。
- 当前项目：启用插件后检查结果应为健康；禁用插件后菜单消失且 Autoload 保持不变。
- 第二个小项目：验证未安装、安装、重复安装、名称冲突、重复路径和安全卸载。

当前项目不自动执行安装或卸载测试，避免修改现有 `project.godot`。
