# UiService 使用指南

## 定位与适用场景

UiService 管理基于 Godot `Control` 的页面、模态界面与返回顺序。它适合设置页、背包页、暂停面板和确认弹窗等叠加在主内容场景之上的 UI，并通过 `IUiService` 注册到 Services。

首版不负责主场景切换、游戏流程、自动暂停、输入映射、动画、数据绑定、窗口基类或 UI 池化。菜单、关卡和结算等流程仍由业务层决定；主内容场景切换继续使用 SceneService。

## 快速上手

UI PackedScene 的根节点必须继承 `Control`：

```csharp
private static readonly ResourceKey SettingsKey =
    ResourceKey.Create("res://UI/Settings.tscn");

IUiService ui = Services.Get<IUiService>();
Control settings = ui.OpenPage(SettingsKey);

// 页面自身的关闭按钮可以保存返回值，然后显式关闭。
ui.Close(settings);
```

打开模态：

```csharp
Control confirm = ui.OpenModal(
    ResourceKey.Create("res://UI/ConfirmQuit.tscn"));
```

业务输入边界决定何时返回：

```csharp
if (Input.IsActionJustPressed("ui_cancel"))
    Services.Get<IUiService>().TryGoBack();
```

UiService 不自动监听 `ui_cancel` 或平台返回键，避免与具体游戏的输入优先级冲突。

## 页面、模态与返回语义

- 打开页面会隐藏当前顶部页面，并把新页面压入页面栈。
- 关闭顶部页面会 `QueueFree()` 该页面，并恢复前一页面。
- 打开模态不会隐藏页面；多个模态按打开顺序叠放。
- 返回优先关闭顶部模态，其次关闭顶部页面；没有受管理 UI 时返回 `false`。
- `Close(view)` 只接受全局最上层界面。存在模态时不能跨过模态关闭页面。
- 首版不限制同一资源重复打开，是否允许重复由业务层决定。
- 受管理界面必须通过 `Close` 或 `TryGoBack` 退出，不要直接 `QueueFree()` 或从父节点移除，否则会绕过返回栈维护。

模态 Host 覆盖整个视口并使用 `MouseFilter.Stop`，阻止 Godot GUI 指针事件落到下层 Control。它不会暂停场景树，也不会阻止业务节点自行处理键盘、手柄或 `_UnhandledInput`；这些策略属于业务层。

## 失败语义

- 资源加载、PackedScene 实例化、根节点类型检查或场景树挂载失败时抛出 `UiOpenException`，其 `Key` 保存目标 ResourceKey。
- UI 根节点不是 `Control` 时抛出 `UiOpenException`。
- 服务尚未完成场景树初始化时抛出 `InvalidOperationException`。
- `Close` 传入非托管界面或非顶部界面时抛出 `InvalidOperationException`。
- `TryGoBack` 在空栈上返回 `false`，属于正常分支。

打开失败不会隐藏当前页面，也不会修改返回栈。模块内部不先向 ErrorHub 上报再抛出；业务调用边界负责捕获异常并补充上下文。

## 生命周期与线程

- 所有 API 只能在 Godot 主线程调用。
- 必须启用 `GoDoRuntime.tscn` Autoload；UiService 由其长期持有和注册。
- UiService 位于主内容场景之外，SceneService 替换 `SceneTree.CurrentScene` 时不会释放 UI 栈。
- 页面被覆盖时仅隐藏，节点和状态会保留；弹出栈时在帧末释放。
- UI 节点使用 Godot 自身 `_Ready()`、`_ExitTree()` 和 Signal 生命周期，不需要实现框架窗口基类。

## 性能与误用

首版同步通过 ResourceHub 加载并实例化 UI。打开操作不应放在 `_Process` / `_PhysicsProcess` 中；较大的 UI 可由业务层提前加载相关资源，出现真实异步需求后再设计取消和并发语义。

UiService 没有每帧更新，也不池化或维护第二套资源缓存。页面栈会保留被隐藏页面，避免返回时丢失状态，但深层页面栈会相应占用节点内存，业务层应在流程结束时显式返回或关闭。

## 验证

- `dotnet build GoDoFramework.sln`：验证 C# API、Godot 绑定和场景资源引用可编译。
- `TestScene.tscn`：运行页面隐藏与恢复、模态优先、跨层关闭拒绝、空栈和非法根节点回归。
- `Verification/UI/UiVerificationScene.tscn`：使用 F6 手动验证页面恢复、嵌套模态、视觉层级和 GUI 指针阻挡。
- Godot 手动验证：页面隐藏与恢复、嵌套模态顺序、GUI 指针阻挡、`QueueFree()` 时序、主场景切换后 UI 服务仍存在。
- 已运行 `TestScene.tscn`，并完成交互验证场景中的页面恢复、嵌套模态、视觉层级和 GUI 指针阻挡验证。
- 主内容场景切换期间保留 UI 的行为尚未单独手动验证。
