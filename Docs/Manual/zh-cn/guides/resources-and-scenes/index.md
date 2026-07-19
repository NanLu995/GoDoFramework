# 管理资源清单、异步加载与场景切换

ResourceHub 是框架统一的 Godot Resource 加载入口；SceneService 在它之上加载并替换主内容场景。业务代码使用 `ResourceKey` 或资源清单中的语义 ID，不直接散落 `ResourceLoader` 和路径字符串。

## 1. 选择路径、UID 或语义 ID

直接路径适合局部且稳定的资源：

```csharp
ResourceKey key = ResourceKey.FromPath("res://Scenes/Gameplay.tscn");
```

UID 在文件移动后更稳定：

```csharp
ResourceKey key = ResourceKey.ResolveUid("res://Scenes/Gameplay.tscn");
```

`ResolveUid` 能解析时返回 `uid://`，否则保留原始 `res://`。不要手写 UID；由 Godot 生成和维护。

大型项目推荐让业务代码使用语义 ID：

```text
scene/gameplay -> uid://...
ui/icon_close  -> uid://...
audio/bgm_menu -> uid://...
```

语义 ID 与文件位置解耦，也便于按游戏包或功能模块拆分清单。

## 2. 创建和维护 ResourceManifest

启用 GoDo Framework 编辑器插件后，使用顶部菜单：

1. **创建资源清单...**：创建 `.tres` 或 `.res` 格式清单。
2. **选择资源并添加...**：多选项目资源，预览后写入目标清单。
3. **管理资源清单...**：编辑 ID、将路径转换为 UID 或删除映射。
4. **校验资源清单...**：只读检查空值、重复 ID、Locator 和资源可解析性。

添加资源时，工具默认用去掉 `res://` 和扩展名的路径作为 ID。提交前将重要条目改为稳定业务语义，例如 `ui/icon_close`。删除映射不会删除实际资源文件。

工具只在确认后写清单；为缺少 UID 的资源生成 UID 也需要确认。校验操作不会修复或修改文件。

## 3. 在启动阶段加载注册表

```csharp
ResourceManifest manifest =
    ResourceLoader.Load<ResourceManifest>("res://Data/ResourceManifest.tres");

ResourceRegistry.Load(manifest);
```

多个清单按顺序合并：

```csharp
ResourceRegistry.LoadMerge(new[]
{
    coreManifest,
    gameplayManifest,
    platformOverrideManifest,
});
```

后面的重复 ID 覆盖前面的值并产生 Warning，适合明确的覆盖层。空 ID 和 null 条目会被跳过。加载顺序应集中在 Boot，而不是由各场景争抢全局注册表。

业务代码解析后加载：

```csharp
ResourceKey iconKey = ResourceRegistry.GetKey("ui/icon_close");
Texture2D icon = ResourceHub.Load<Texture2D>(iconKey);
```

必需资源使用 `GetKey`；真正允许缺失的可选资源使用 `TryGetKey`。

## 4. 同步与异步加载

小型、启动前必需资源可同步加载：

```csharp
PackedScene scene = ResourceHub.Load<PackedScene>(key);
```

大型资源或需要加载 UI 时使用异步操作：

```csharp
ResourceLoadOperation<PackedScene> operation =
    ResourceHub.LoadAsync<PackedScene>(key);

operation.ProgressChanged += OnProgressChanged;
try
{
    PackedScene scene = await operation.Completion;
}
finally
{
    operation.ProgressChanged -= OnProgressChanged;
}
```

进度范围为 0–1，并在 Godot 主线程触发。使用具名方法并在 `finally` 解绑；不要 `.Wait()`、`.Result` 或自行轮询 Godot threaded API。

同一个 Key 和类型的并发异步请求共享同一个操作。异步期间对同一路径同步加载，或按另一种类型加载，会明确失败。完成后操作从活动表移除，后续加载继续使用 Godot 自身缓存。

## 5. 切换主内容场景

```csharp
ISceneService scenes = Services.Get<ISceneService>();
ResourceKey gameplay = ResourceRegistry.GetKey("scene/gameplay");

try
{
    Node newScene = await scenes.ChangeAsync(gameplay);
}
catch (SceneChangeException exception)
{
    ErrorHub.Report(exception, "Game.Procedure", context: gameplay.Value);
    ShowSceneLoadFailure();
}
```

SceneService 先完整加载、检查 PackedScene、实例化并挂入 SceneTree，成功后才更新 `CurrentScene`。提交前失败时旧场景保持不变。

成功提交后旧场景会 `QueueFree()` 并在帧末释放。`await` 返回后只使用返回的新场景，不再访问旧场景节点。

## 6. 显示加载进度

SceneService 当前提供轮询属性：

```csharp
public override void _Process(double delta)
{
    if (_scenes?.IsChanging == true)
        _progressBar.Value = _scenes.Progress * 100.0;
}
```

加载 UI 应由长期 UI 层或 GoDoRuntime 之外仍存活的界面承载，不能放在即将被替换的旧主场景中。切换完成后隐藏或关闭加载页面。

同一时间只允许一项场景切换；第二次 `ChangeAsync` 抛出 `InvalidOperationException`。由 Procedure 集中协调，禁用重复点击，不要用 fire-and-forget 丢失异常。

## 7. 失败、取消与关闭

- 资源缺失、类型错误或加载失败：`ResourceLoadException`。
- 场景加载、实例化或挂载失败：`SceneChangeException`，其中保存目标 Key。
- SceneService 离树或框架关闭：未提交切换以包含 `OperationCanceledException` 的 SceneChangeException 结束。
- ResourceHub 关闭：未完成操作的等待方收到 `OperationCanceledException`；Godot 底层加载可能继续结束。

模块不会先上报再抛出。由 Procedure 或启动边界补充业务上下文并记录一次。

## 8. 缓存和生命周期

ResourceHub 继续使用 Godot `CacheMode.Reuse`，不维护第二套 LRU、引用计数或手动 Unload。调用方只在需要期间持有 Resource 引用。

不要在 `_Process()` 中重复加载资源，也不要为“清缓存”随意释放仍被场景、材质或脚本引用的 Resource。远程下载、PCK/DLC、热更新、目录批量加载和高级缓存不属于当前能力。

## 常见错误

- Registry 尚未加载：Boot 没有先加载 Manifest。
- 移动文件后路径失效：使用 UID 或通过管理工具更新 Locator。
- 同一资源出现不同结果：按不同泛型类型并发请求，修正调用方类型。
- 加载进度 UI 随旧场景消失：把它放到长期 UI 层。
- 连续点击触发第二次切换：由 Procedure 串行化并锁定入口。
- 切换成功后访问旧节点崩溃：旧场景已进入 QueueFree 生命周期。
- 捕获后出现两条日志：底层和上层重复上报，只保留业务边界的一次。
- 希望 ResourceHub 卸载或热更新：这些能力尚未提供。

精确接口可查询 <xref:GoDo.ResourceKey>、<xref:GoDo.ResourceRegistry>、<xref:GoDo.ResourceHub>、<xref:GoDo.ResourceLoadOperation%601>、<xref:GoDo.ISceneService> 和 <xref:GoDo.SceneChangeException>。
