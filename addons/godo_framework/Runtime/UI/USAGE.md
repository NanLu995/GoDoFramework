# UiService 使用指南

## 定位与适用场景

UiService 统一管理基于 Godot `Control` 的屏幕空间游戏 UI。业务层负责具体界面的显示内容和交互逻辑，UiService 负责资源加载、实例化、显示层、关闭与返回顺序。

首版不负责主场景切换、游戏流程、自动暂停、输入映射、动画、数据绑定、窗口基类或 UI 池化。Node2D/Node3D 世界空间 UI 依赖业务坐标和摄像机，仍由业务场景直接管理；Debugger 是独立开发工具，不进入游戏 UI 层。

## 运行时结构

`GoDoRuntime` 是唯一 Autoload。它在运行时创建与自身平级的 `GoDoUI` 显示根，UiService 本身不承载业务 UI 节点：

```text
/root
├── GoDoRuntime
│   └── UiService
├── GoDoUI
│   ├── SceneLayer (CanvasLayer 10)
│   │   └── SceneRoot
│   ├── ViewLayer (CanvasLayer 20)
│   │   └── ViewRoot
│   └── ModalLayer (CanvasLayer 30)
│       └── ModalRoot
└── CurrentScene
```

UI PackedScene 的根节点必须继承 `Control`。业务 UI 资源和脚本仍放在业务目录，打开后才成为 `GoDoUI` 的子节点。

## 快速上手

```csharp
IUiService ui = Services.Get<IUiService>();

Control hud = ui.Open(
    ResourceKey.Create("res://UI/Hud.tscn"),
    UiLayer.Scene);

Control settings = ui.Open(
    ResourceKey.Create("res://UI/Settings.tscn"),
    UiLayer.View);

Control confirm = ui.Open(
    ResourceKey.Create("res://UI/ConfirmQuit.tscn"),
    UiLayer.Modal);
```

关闭已打开界面：

```csharp
ui.Close(settings);
```

业务输入边界决定何时返回：

```csharp
if (Input.IsActionJustPressed("ui_cancel"))
    ui.TryGoBack();
```

UiService 不自动监听 `ui_cancel` 或平台返回键，避免与具体游戏的输入优先级冲突。

## 层级与生命周期语义

### Scene

- HUD、准星、关卡提示等与当前主内容场景关联的屏幕空间 UI。
- 同一时间允许多个 Scene 界面共存，可以按实例分别关闭。
- SceneService 成功提交新主场景后，UiService 自动清空该层。
- Scene 界面不进入返回栈。

### View

- 设置、背包和菜单等需要前后导航的完整界面。
- 打开新 View 会隐藏当前 View 并压入返回栈。
- 关闭顶部 View 会恢复前一个 View。
- View 默认跨主内容场景保留，业务流程结束时应显式关闭。

### Modal

- 确认框等必须覆盖其他游戏 UI 的模态界面。
- 多个 Modal 按打开顺序叠放，只能关闭最上层 Modal。
- Modal Host 覆盖整个视口并使用 `MouseFilter.Stop`，阻止 GUI 指针事件落到下层 Control。
- Modal 不自动暂停场景树，也不阻止业务节点处理键盘、手柄或 `_UnhandledInput`。

`TryGoBack()` 优先关闭顶部 Modal，其次关闭顶部 View；没有可返回界面时返回 `false`。Scene 界面不受返回操作影响。

受管理界面必须通过 `Close` 或 `TryGoBack` 退出，不要直接 `QueueFree()` 或从父节点移除，否则会绕过 UiService 的集合与返回栈维护。

## 失败语义

- 未知 `UiLayer` 抛出 `ArgumentOutOfRangeException`。
- 资源加载、PackedScene 实例化、根节点类型检查或场景树挂载失败时抛出 `UiOpenException`，其 `Key` 保存目标 ResourceKey。
- UI 根节点不是 `Control` 时抛出 `UiOpenException`。
- 服务或 `GoDoUI` 尚未完成初始化时抛出 `InvalidOperationException`。
- `Close` 传入非托管界面、非顶部 View 或非顶部 Modal 时抛出 `InvalidOperationException`。
- `TryGoBack` 没有可返回界面时返回 `false`，属于正常分支。

打开失败不会隐藏当前 View，也不会修改任何层的管理集合。模块内部不先向 ErrorHub 上报再抛出；业务调用边界负责捕获异常并补充上下文。

## 生命周期与线程

- 所有 API 只能在 Godot 主线程调用。
- 必须启用 `GoDoRuntime.tscn` Autoload；GoDoRuntime 创建 `GoDoUI`，初始化并注册 UiService。
- GoDoUI 位于主内容场景之外，SceneService 替换 `SceneTree.CurrentScene` 时不会释放显示根。
- Scene 层通过框架内部场景变更事件自动清理；SceneService 不直接依赖 UiService。
- 被覆盖的 View 仅隐藏，节点和状态会保留；关闭界面时在帧末释放。
- UI 节点使用 Godot 自身 `_Ready()`、`_ExitTree()` 和 Signal 生命周期，不需要实现框架窗口基类。

## 性能与误用

首版同步通过 ResourceHub 加载并实例化 UI。打开操作不应放在 `_Process` / `_PhysicsProcess` 中；较大的 UI 可由业务层提前加载相关资源，出现真实异步需求后再设计取消和并发语义。

UiService 没有每帧更新，也不池化或维护第二套资源缓存。View 栈会保留被隐藏界面，避免返回时丢失状态，但深层返回栈会相应占用节点内存。

## 验证

- `dotnet build GoDoFramework.sln`：验证 C# API、Godot 绑定和场景资源引用可编译。
- `Verification/Automated/UiServiceRegression.tscn`：Headless 验证三层打开与关闭、View / Modal 栈约束、失败后的状态保持，以及主场景变更事件对 Scene 层的清理。
- `python Verification/Automated/run_all.py --suite all --godot <Godot 4.7 Mono Console>`：运行包含 UiService 在内的工作台永久回归套件。
- `Verification/UI/UiVerificationScene.tscn`：使用 F6 手动验证 View 恢复、嵌套 Modal、视觉层级和 GUI 指针阻挡。
- Scene 层清理：先打开 Scene 层标记和 View A，再在 View A 内点击“切换主场景（验证 Scene 清理）”；切换后 View A 必须继续显示，关闭 View A 后应看到只有说明 Label 的目标场景，且顶部 Scene 层标记必须不存在。
- 失败语义：在 View A 或 View B 点击“运行失败语义验证”，结果必须显示通过，当前 View 的计数与导航仍应正常工作。
- 已在 Godot 中完成层级重构后的 View 恢复、嵌套 Modal、视觉层级和 GUI 指针阻挡验证。
- 已在 Godot 中完成 Scene 层随主内容场景切换自动清理，以及 View 跨场景保留验证。
- 已在 Godot 中完成资源缺失、错误根类型和非法关闭的失败语义验证；异常后当前 View 的状态与导航保持正常。
