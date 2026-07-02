# SceneService 使用指南

## 定位与优势

SceneService 管理主内容场景的异步加载与替换。它复用 ResourceHub 的类型检查、并发合并与主线程完成语义，并通过 `ISceneService` 注册到 Services，业务层不需要查找 Autoload 节点。

首版只管理一个 `SceneTree.CurrentScene`：不负责 UI 弹窗、返回栈、暂停菜单、过渡动画或统一错误场景。

## 快速上手

```csharp
ResourceKey levelKey = ResourceKey.Create("res://Scenes/Level01.tscn");
ISceneService scenes = Services.Get<ISceneService>();

try
{
    Node level = await scenes.ChangeAsync(levelKey);
}
catch (SceneChangeException exception)
{
    ErrorHub.Report(exception, "GameFlow", context: levelKey.Value);
}
```

加载、类型检查、实例化或挂载失败时抛出 `SceneChangeException`，`Key` 保存目标 ResourceKey。失败发生在切换提交前时，旧场景保持不变。

## 状态与进度

```csharp
ISceneService scenes = Services.Get<ISceneService>();

if (scenes.IsChanging)
    loadingBar.Value = scenes.Progress * 100.0;
```

`Progress` 范围为 0–1。首版提供轮询属性，不提供独立进度信号；UI 可在自身 Process 中读取，但不要在每帧重复调用 `ChangeAsync`。

## 切换语义

- 同一时间只允许一个切换请求；第二个请求抛出 `InvalidOperationException`。
- PackedScene 完整加载并成功实例化后，才加入 SceneTree 并设为 CurrentScene。
- 提交成功后旧场景 QueueFree，在帧末释放；不要在 await 返回后继续访问旧场景。
- SceneService 离树或重新入树会取消尚未提交的切换，并以带 `OperationCanceledException` 内层异常的 `SceneChangeException` 结束等待。
- SceneService 必须作为 CurrentScene 之外的长期节点存在；当前由 GoDoRuntime Autoload 持有。

## 生命周期与线程

- 只能在 Godot 主线程调用。
- 必须启用 `GoDoRuntime.tscn` Autoload，使 ResourceHub、Services 和 SceneService 按顺序初始化。
- 不要在业务场景中再创建第二个 SceneService，也不要重复初始化框架。
- SceneService 的失败异常由业务流程边界捕获并补充上下文，模块内部不重复 ErrorHub.Report。

## 常见误用

| 应该 | 避免 |
|---|---|
| 使用 ResourceKey 表达目标场景 | 在业务代码中散落 ChangeSceneToFile 字符串 |
| await 完成并处理 SceneChangeException | fire-and-forget 后丢失异常 |
| Loading UI 轮询 IsChanging/Progress | 切换期间再次发起 ChangeAsync |
| 把 UI 栈交给 UI 模块 | 用主场景切换实现弹窗和暂停菜单 |
| await 后只使用返回的新场景 | 继续访问已 QueueFree 的旧场景 |
