# 组织复杂 UI 与长期音频

UiService 管理屏幕空间 `Control` 的层级、实例和返回顺序；AudioService 管理非空间 BGM、短音效和分组音量。两者都由 GoDoRuntime 长期持有，因此主场景切换不会释放 View、Modal 或正在播放的音乐。

业务层仍负责界面内容、输入优先级、暂停策略、动画和具体音频资源选择。

## 1. 为每种界面选择正确层级

| 层级 | 用途 | 场景切换 | 返回行为 |
|---|---|---|---|
| `Scene` | HUD、准星、关卡提示 | 成功切换后自动清理 | 不进入返回栈 |
| `View` | 设置、背包、完整菜单 | 默认保留 | 新页面隐藏旧页面，返回时恢复 |
| `Modal` | 确认框、阻塞式选择 | 默认保留 | 只允许关闭最上层 |
| `Overlay` | Toast、加载提示、引导遮罩 | 默认保留 | 不进入返回栈 |

```csharp
Control hud = ui.Open(HudKey, UiLayer.Scene);
Control inventory = ui.Open(InventoryKey, UiLayer.View);
Control confirm = ui.Open(ConfirmKey, UiLayer.Modal);
Control toast = ui.Open(ToastKey, UiLayer.Overlay);
```

UI PackedScene 根节点必须继承 `Control`。世界空间血条、Node2D/Node3D 标签和跟随角色的界面仍由业务场景管理。

## 2. 用 UiConfig 集中维护界面

在 Godot Inspector 中新建 `UiConfig` Resource。展开 `Entries`，为每个 `UiConfigEntry` 填写：

- `Id`：业务使用的语义标识。
- `Locator`：通过文件选择器指定 UI PackedScene。
- `Layer`：默认 Scene、View、Modal 或 Overlay。
- `InstanceMode`：`Single` 或 `Multiple`。
- `ReuseInstance`：关闭后是否保留一个 `Single` 实例供下次复用。

在启动流程尚未打开任何 UI 时加载目录：

```csharp
private static readonly ResourceKey UiConfigKey =
    ResourceKey.Create("res://Config/UiConfig.tres");
private static readonly UiId SettingsId = UiId.Create("settings");

IUiService ui = context.GetService<IUiService>();
ui.LoadUiConfig(UiConfigKey);
Control settings = ui.Open(SettingsId);
```

配置会一次性校验空或重复 Id、非法资源定位、层级、实例策略和复用组合。`Single` 标识不能重复打开；`Multiple` 每次打开都会创建独立实例。目录未注册的 Id 不会静默返回 null，而是抛出异常。保留 `Open(ResourceKey, UiLayer)` 供不需要配置或需要临时指定层级的低层调用。

### 异步打开和取消

```csharp
SettingsView settings = await ui.OpenAsync<SettingsView>(
    SettingsId,
    view => view.Initialize(model),
    progress => loadingBar.Value = progress * 100f,
    cancellationToken);
```

资源在线程化加载后，节点仍在 Godot 主线程实例化、配置和挂载。取消令牌或 `CancelOpenRequests(SettingsId)` 会阻止当前请求继续挂载，但不会中止 ResourceHub 中可能共享的底层加载。场景切换会自动取消 Scene 层未完成请求。

### 查询、批量关闭和复用

```csharp
if (ui.TryGetTop<SettingsView>(SettingsId, out var settings))
    settings.Refresh();

ui.CloseAll(UiLayer.Overlay);
ui.CloseTo(SettingsId); // 保留设置页，关闭其显示顺序之上的 UI
ui.ClearCachedInstance(SettingsId);
```

`IsOpen`、`GetOpenCount`、`IsOpening` 和 `GetOpeningCount` 可用于业务状态判断。启用 `ReuseInstance` 后，关闭的 `Single` 节点仍占用内存并保留状态与信号连接；只对实测创建成本较高、且每次重开都能可靠重置的界面使用。

## 3. 明确谁拥有界面

打开界面的流程或协调器应保存实例，并负责关闭自己创建的界面：

```csharp
private IUiService? _ui;
private Control? _hud;

public async Task EnterAsync(ProcedureContext context)
{
    _ui = context.GetService<IUiService>();
    _hud = _ui.Open(HudKey, UiLayer.Scene);
}

public Task ExitAsync(ProcedureContext context)
{
    if (_ui != null && _hud != null && GodotObject.IsInstanceValid(_hud))
        _ui.Close(_hud);

    _hud = null;
    _ui = null;
    return Task.CompletedTask;
}
```

受管理界面应通过 `Close()` 或 `TryGoBack()` 退出，不要直接 `QueueFree()` 或 `RemoveChild()`。如果界面被外部释放，UiService 会在下一次操作时清理失效记录、恢复前一个有效 View，并回收空的 Modal Host；直接移除或重挂载仍会绕过正常所有权与返回顺序。View 被覆盖时只是隐藏，节点状态和内存仍保留；不要无限堆叠深层 View。

## 4. 集中处理返回输入

UiService 不监听 `ui_cancel`、Android 返回键或手柄按钮。游戏应在一个明确输入边界中决定顺序：

```csharp
private void HandleBackRequested()
{
    if (_ui.TryGoBack())
        return;

    EventChannel.Emit<PauseRequestedEvent>();
}
```

`TryGoBack()` 先关闭顶部 Modal，再关闭顶部 View；没有可返回页面时返回 `false`。Scene 和 Overlay 不参与返回。不要让 HUD、菜单和角色控制器同时处理同一个返回 Action。

Modal Host 会阻止鼠标事件落到底层 Control，但不会自动暂停 SceneTree，也不会阻止角色脚本读取键盘、手柄或 `_UnhandledInput`。打开暂停 Modal 时还应：

1. 由 Procedure 或暂停协调器决定 SceneTree 暂停策略。
2. 切换 InputService Context，屏蔽 Gameplay Action。
3. 关闭时按相反顺序恢复。

## 5. 处理 UI 打开失败

```csharp
try
{
    _ui.Open(SettingsKey, UiLayer.View);
}
catch (UiOpenException exception)
{
    ErrorHub.Report(exception, "Game.UI", context: SettingsKey.Value);
    ShowFallbackMessage();
}
```

资源缺失、根节点不是 Control、实例化或挂载失败都会抛出 `UiOpenException`。失败不会隐藏当前 View，也不会修改任何层的管理状态。

关闭非托管界面、非顶部 View 或非顶部 Modal 会抛出 `InvalidOperationException`。这通常说明所有权或调用顺序错误，不应捕获后静默忽略。

## 6. 让 Procedure 决定 BGM

```csharp
IAudioService audio = context.GetService<IAudioService>();

try
{
    await audio.PlayBgmAsync(GameAudio.GameplayTheme);
}
catch (OperationCanceledException)
{
    // StopBgm 或框架退出取消了尚未完成的加载。
}
catch (AudioPlaybackException exception)
{
    ErrorHub.Report(exception, "Game.Audio", GameAudio.GameplayTheme.Value);
}
```

同一资源重复请求默认不重播；确实需要从头开始时传 `restart: true`。加载新 BGM 时不要先 Stop，加载完成后服务会替换当前流，减少无声间隔。

同一时间只允许一个 BGM 加载请求。流程切换应串行协调，不要让多个页面同时争抢音乐。需要安静状态时显式 `StopBgm()`。

`StopBgm()` 会立即释放逻辑加载状态，因此可以马上发起新的 BGM 请求。旧等待方会在 ResourceHub 的共享底层加载完成后收到 `OperationCanceledException`，不会覆盖新请求状态。

`PauseBgm()` 和 `ResumeBgm()` 只影响当前 BGM，不会暂停 SFX 或 SceneTree。暂停菜单是否暂停音乐由游戏设计决定。

## 7. 正确处理短音效容量

```csharp
try
{
    bool played = await audio.PlaySfxAsync(GameAudio.ButtonClick);
    if (!played)
        LogHub.Debug("SFX capacity reached.", "Game.Audio");
}
catch (OperationCanceledException)
{
}
catch (AudioPlaybackException exception)
{
    ErrorHub.Report(exception, "Game.Audio", GameAudio.ButtonClick.Value);
}
```

`false` 表示并发 Voice 已满，是正常容量分支，不是资源损坏。默认预热 8 路、最大 32 路；加载中的请求也会预占名额，防止同时完成后突破上限。

`StopAllSfx()` 会立即归还活动 Voice 并释放待加载请求预占的逻辑容量。旧等待方会在共享底层加载完成后以 `OperationCanceledException` 结束，不会减少新一代请求的容量计数。

自然播放结束的非循环音效会自动归还池。循环 AudioStream 不会触发 Finished，当前 public API 没有单路 SFX Handle；必须用 `StopAllSfx()` 统一停止，或对需要独立控制的循环/空间声音直接使用业务层 `AudioStreamPlayer`、`AudioStreamPlayer2D/3D`。

不要用全局 SFX 播放脚步、发动机或环境循环等需要单独停止和定位的声音。

## 8. 音量、设置与 Audio Bus

```csharp
audio.SetVolume(AudioGroup.Master, settings.MasterVolume);
audio.SetVolume(AudioGroup.Bgm, settings.BgmVolume);
audio.SetVolume(AudioGroup.Sfx, settings.SfxVolume);
```

值是 0–1 的有限线性值。应在 SettingsService 加载玩家设置后立即应用，并在滑块变化时预览；保存策略由设置页面决定。

项目最好在 Audio Bus Layout 中预先建立 `BGM` 和 `SFX`。缺失时框架会在运行时创建并 Warning，但不会修改持久化 Bus Layout；不要把这个降级行为当成正式配置流程。

## 9. 场景和框架退出

- `GoDoUI`、AudioService 和播放器位于 CurrentScene 之外。
- 场景成功切换会清理 Scene UI，但保留 View、Modal 和音频。
- 流程退出时显式清理自己拥有的 View/Modal；不要清空其他系统页面。
- AudioService 退出时停止 BGM、取消加载并释放 SFX 池。
- `StopAllSfx()` 会归还活动 Voice，并取消尚未完成的 SFX 请求。

所有 UI 和 Audio public API 都只能从 Godot 主线程调用。打开界面和首次音频加载不应放在每帧路径。

## 常见错误

- Modal 打开后角色仍移动：Modal 只拦截 GUI 指针，需切换输入 Context 或暂停流程。
- 返回一次关闭了错误页面：多个节点同时处理返回输入，应集中到单一边界。
- View 跨场景意外保留：这是默认语义，拥有它的流程必须显式关闭。
- 直接 QueueFree 后界面顺序异常：继续使用 UiService 会清理失效记录，但业务仍应通过 Close/TryGoBack 维持明确所有权。
- BGM 请求偶尔被拒绝：上一项 BGM 仍在加载，流程没有串行等待。
- SFX 返回 false：并发容量已满，可跳过非关键声音或调整设计。
- 循环 SFX 永不归还：循环流不会自然 Finished，改用可独立管理的业务播放器。
- 重启后音量恢复默认：只调用 SetVolume 没有通过 SettingsService 保存。
- 空间声音听起来没有位置：AudioService 只负责非空间音频。

精确接口可查询 <xref:GoDo.IUiService>、<xref:GoDo.UiLayer>、<xref:GoDo.UiOpenException>、<xref:GoDo.IAudioService>、<xref:GoDo.AudioGroup> 和 <xref:GoDo.AudioPlaybackException>。
