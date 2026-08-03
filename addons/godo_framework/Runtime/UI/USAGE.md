# UiService 使用指南

## 定位与适用场景

UiService 统一管理根节点为 Godot `Control` 的屏幕空间游戏 UI，负责资源加载、实例化、显示层、焦点、查询、关闭、返回顺序和可选实例复用。业务层仍负责界面内容、交互、动画、数据绑定、暂停策略和输入映射。

Node2D/Node3D 世界空间 UI 依赖业务坐标和摄像机，由业务场景直接管理；Debugger 是独立开发工具，不进入游戏 UI 层。

## 运行时结构

`GoDoRuntime` 创建与自身平级的 `GoDoUI`，UiService 只管理引用和生命周期：

```text
/root
├── GoDoRuntime
│   └── UiService
├── GoDoUI
│   ├── SceneLayer   (CanvasLayer 10)
│   ├── ViewLayer    (CanvasLayer 20)
│   ├── ModalLayer   (CanvasLayer 30)
│   └── OverlayLayer (CanvasLayer 40)
└── CurrentScene
```

四个 CanvasLayer 都包含一个全屏 `Control` 根节点。UI PackedScene 的根节点必须继承 `Control`。

## 配置与打开

在 Godot Inspector 中创建 `UiConfig` Resource；也可通过 GoDo 菜单的 UI 配置管理弹窗创建、定位和校验当前项目中的配置。每个 `UiConfigEntry` 包含：

- `Id`：业务使用的区分大小写语义标识；
- `Locator`：UI PackedScene 的 `res://` 路径或 `uid://` 定位；
- `Layer`：默认显示层；
- `InstanceMode`：`Single` 或 `Multiple`；
- `ReuseInstance`：关闭后保留一个 `Single` 实例，供下次打开复用。

业务启动流程在打开 UI 前加载一次配置：

```csharp
private static readonly ResourceKey UiConfigKey =
    ResourceKey.Create("res://Config/UiConfig.tres");
private static readonly UiId SettingsId = UiId.Create("settings");

IUiService ui = Services.Get<IUiService>();
ui.LoadUiConfig(UiConfigKey);

SettingsView settings = ui.Open<SettingsView>(
    SettingsId,
    view => view.Initialize(model));
```

`configure` 在加入场景树前执行，适合注入首帧所需数据。无语义配置的临时入口可直接指定资源和层：

```csharp
Control toast = ui.Open(
    ResourceKey.Create("res://UI/Toast.tscn"),
    UiLayer.Overlay);
```

较重界面可异步打开：

```csharp
SettingsView settings = await ui.OpenAsync<SettingsView>(
    SettingsId,
    view => view.Initialize(model),
    progress => loadingBar.Value = progress * 100f,
    cancellationToken);
```

ResourceHub 负责线程化资源加载；实例化、配置和挂载仍在 Godot 主线程进行。取消只阻止该请求继续实例化和挂载，不会中止可能被其他调用共享的底层资源加载。可使用 `IsOpening`、`GetOpeningCount` 和 `CancelOpenRequests` 按 UiId 或层查询、取消请求。主场景变更会立即取消 Scene 层请求。

## 层级与返回语义

### Scene

- HUD、准星和关卡提示等与当前主内容场景关联的界面；
- 可同时存在多个实例，不进入返回栈；
- 主场景成功变更时自动关闭，并清理 Scene 层缓存和未完成打开请求。

### View

- 设置、背包和菜单等前后导航页面；
- 打开新 View 会隐藏当前 View；关闭顶部 View 会恢复前一个 View；
- 默认跨主场景保留，拥有它的业务流程应负责关闭。

### Modal

- 确认框等需要覆盖其他游戏 UI 的模态界面；
- 多个 Modal 按打开顺序叠放，只允许按顶部顺序关闭；
- 每个 Modal Host 覆盖视口并使用 `MouseFilter.Stop`，阻止 GUI 指针事件落到下层；
- 不自动暂停 SceneTree，也不阻止业务节点处理键盘、手柄或 `_UnhandledInput`。

### Overlay

- Toast、加载提示、引导遮罩等位于 Modal 之上的短期界面；
- 不进入返回栈，默认不阻止 GUI 指针输入；需要遮罩时由该 UI 自己提供全屏 `Control` 并设置鼠标过滤；
- 可同时存在多个实例，关闭时不受 Modal/View 顶部约束。

`TryGoBack()` 优先关闭顶部 Modal，其次关闭顶部 View；Scene 与 Overlay 不参与返回操作。打开 View 或 Modal 时会隔离当前受管理焦点；关闭顶部界面或重新打开缓存实例时，会尽力恢复仍有效、可见且可聚焦的最后焦点。首次打开后的默认焦点仍由业务设置。

## 查询与关闭

```csharp
if (ui.IsOpen(SettingsId) && ui.TryGetTop<SettingsView>(SettingsId, out var settings))
    settings.Refresh();

ui.TryClose(SettingsId);        // 关闭该 Id 最上层实例
ui.CloseAll(UiLayer.Overlay);   // 批量关闭一层
ui.CloseAll(ToastId);           // 批量关闭一个 Id
ui.CloseTo(SettingsId);         // 保留目标，关闭其显示顺序之上的所有 UI
```

`GetOpenCount` 返回指定 Id 的实例数；`TryGetTop(UiLayer, ...)` 返回指定层的顶部或最后打开实例。`Close(Control)` 用于必须成功的所有权路径，目标无效或顺序非法时抛出异常；`TryClose(Control)` 适合允许目标已关闭的清理路径。

Procedure 或其他明确生命周期的所有者可使用 `OpenScoped<TView>` 获得 `UiScope<TView>`，并把 Scope 登记到 `ProcedureContext.RegisterCleanup`。Scope 只能在 Godot 主线程释放，释放时通过 `TryClose` 幂等关闭该实例；它不提供终结器，也不改变 View/Modal 的顶部关闭约束。

```csharp
UiScope<GameplayHud> hud = ui.OpenScoped<GameplayHud>(GameplayHudId);
context.RegisterCleanup(hud);
hud.View.SetScore(score);
```

View 与 Modal 必须按顶部顺序逐个关闭。`CloseAll` 和 `CloseTo` 会在服务内部按安全顺序处理。受管理界面不应直接 `QueueFree()`、`RemoveChild()` 或重挂载；外部释放会在下一次服务操作时清理失效记录，但直接改变节点所有权仍会绕过正常顺序。

## 实例复用

仅 `Single` 配置可启用 `ReuseInstance`。关闭时节点会从显示层移到 UiService 的隐藏缓存根，并在下次按同一 UiId 打开时重新配置和挂载。它不建立第二套资源缓存，也不是多实例对象池。

使用 `HasCachedInstance` 查询，使用 `ClearCachedInstance` 或 `ClearCachedInstances` 主动释放。重载 UiConfig、关闭服务和主场景变更都会清理相应缓存。只对实测创建成本较高且状态可可靠重置的界面启用复用；缓存节点仍占用内存并保留业务状态和信号连接。

## 失败语义

- `LoadUiConfig` 加载失败抛出 `ResourceLoadException`，内容无效抛出 `ConfigValidationException`；存在打开或打开中的受管理 UI 时拒绝替换配置；
- 未初始化 UiId 抛出 `ArgumentException`，未注册 Id 抛出 `KeyNotFoundException`，配置未加载或违反 `Single` 约束抛出 `InvalidOperationException`；
- 未知 `UiLayer` 抛出 `ArgumentOutOfRangeException`；
- 资源加载、实例化、根类型转换、配置或挂载失败时任务/调用失败；框架打开错误使用 `UiOpenException` 并保留目标 `ResourceKey`，业务 `configure` 异常原样传递；
- `UiOpenException.Phase` 使用 `Loading`、`Preparing` 或 `Committing` 标识框架失败边界；兼容旧构造函数产生 `Unknown`；
- `OpenAsync` 被调用方、UiService 或主场景变更取消时抛出 `OperationCanceledException`；
- `Close`、`CloseTo` 的目标无效、不受管理或违反顶部顺序时抛出 `InvalidOperationException`；`TryClose`、`TryGoBack` 没有可操作目标时返回 `false`。

打开失败会回滚本次节点和管理状态，不隐藏原 View；关闭失败不会部分修改栈。模块内部不先向 ErrorHub 上报再抛出，业务调用边界负责补充上下文。

## 生命周期、线程与性能

- 所有 API 都必须在 Godot 主线程调用；异步资源等待期间不在后台线程访问 Godot 节点；
- GoDoUI 位于 CurrentScene 外，Scene 层通过内部场景事件清理，不形成 SceneService 到 UiService 的直接依赖；
- UiConfig 加载以 O(n) 建立索引，按 UiId 定位平均为 O(1)；打开、查询和关闭不应放在每帧路径；
- View 栈和复用缓存以节点内存换取状态保留或减少重新实例化，应根据真实界面规模控制；
- Debug 构建记录受管理实例、缓存和未完成请求，Debugger 的 UI 页按需生成快照；Release 不保留调试资源键和快照入口。

## 验证

- `dotnet build GoDoFramework.csproj -c Debug --no-restore`：验证 C# API、Godot 绑定和场景资源引用；
- `Verification/Automated/UiServiceRegression.tscn`：验证四层打开/关闭、UiConfig、查询与批量关闭、`CloseTo`、异步取消、场景清理、焦点、复用和失败回滚；
- `Verification/Automated/UiConfigEditorControllerRegression.gd`：验证编辑器配置发现、单配置直接显示与多配置管理；
- `Verification/Automated/DebuggerOverlayRegression.tscn`：验证 UI 实例、缓存和打开中请求的只读诊断；
- `Verification/Performance/UiFirstOpenBenchmark.tscn`：记录首开、首次入树、首帧和缓存重开成本，不把单机测量值承诺为跨平台预算；
- Modal 指针阻挡、Overlay 遮罩、键盘/手柄返回和焦点切换仍需在 Godot 中结合业务场景人工验证。
