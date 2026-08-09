# 组织复杂 UI 与长期音频

UiService 管理屏幕空间 `Control` 的层级、实例和返回顺序；AudioService 管理非空间 BGM、非空间/3D 短音效和分组音量。两者都由 GoDoRuntime 长期持有，因此主场景切换不会释放 View、Modal 或正在播放的音乐。

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

打开界面的流程或协调器应保存所有权，并负责关闭自己创建的界面。Procedure 可以把 `UiScope` 直接交给激活生命周期清理：

```csharp
public async Task EnterAsync(ProcedureContext context)
{
    IUiService ui = context.GetService<IUiService>();
    UiScope<GameplayHud> hud = ui.OpenScoped<GameplayHud>(HudId);
    context.RegisterCleanup(hud);
    hud.View.Refresh();
}
```

`UiScope.Dispose()` 只能在 Godot 主线程调用，可安全重复释放；界面已由其他路径关闭时也会正常完成。非 Procedure 所有者仍可直接保存节点并通过 `Close`/`TryClose` 对称清理。

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

资源缺失、根节点不是 Control、实例化或挂载失败都会抛出 `UiOpenException`。`Phase` 区分 Loading、Preparing 和 Committing，恢复与日志应使用该结构化值而非解析消息。失败不会隐藏当前 View，也不会修改任何层的管理状态。

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

需要平滑切换时，使用独立的 Crossfade API，而不是改变现有立即切换调用的语义：

```csharp
using CancellationTokenSource transitionCancellation = new();

try
{
    await audio.CrossfadeBgmAsync(
        GameAudio.GameplayTheme,
        durationSeconds: 0.5d,
        cancellationToken: transitionCancellation.Token);
}
catch (OperationCanceledException)
{
    // 调用方取消、StopBgm、框架退出，或更新的播放请求取代了本次过渡。
}
```

目标资源加载期间旧音乐继续播放；加载完成后两台长期播放器使用等功率曲线交叉淡化。过渡时间不受 `Engine.TimeScale` 影响，但仍遵循 AudioService 所在场景树的暂停状态。更新的 Crossfade 请求以最新请求为准，旧任务收到 `OperationCanceledException`；若淡化已经开始，服务保留当时音量较高的一路，避免完全静音。现有 `PlayBgmAsync` 在任何 BGM 请求执行期间仍会拒绝并发调用。

`BgmState` 可区分加载、播放、暂停、过渡、自然结束和完全停止。正常 Crossfade 完成前，`CurrentBgm` 仍表示已经提交的旧音乐；完成后才切换到目标键。`PauseBgm()` 会同时暂停两路播放器和过渡进度，`StopBgm()` 会取消请求并清空两路。

需要平滑进入无 BGM 状态时不要自行循环修改 Bus 音量，直接淡出当前音乐：

```csharp
try
{
    await audio.FadeOutBgmAsync(
        durationSeconds: 0.5d,
        cancellationToken: context.LifetimeToken);
}
catch (OperationCanceledException)
{
    // 当前流程结束，或更新的 Crossfade/FadeOut 取代了本次淡出。
}
```

FadeOut 完成后状态为 `Stopped` 且 `CurrentBgm` 为空；没有当前音乐时直接完成。调用方取消已经开始的淡出时，当前音乐恢复正常音量继续播放。新的 Crossfade 或 FadeOut 会取消旧过渡并接管后续状态。

## 7. 正确处理短音效容量

```csharp
try
{
    bool played = await audio.PlaySfxAsync(
        GameAudio.ButtonClick,
        volumeLinear: 0.75f,
        pitchScale: 1.05f);
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

省略逐次参数时音量和音高倍率均为 1。`volumeLinear` 必须是 0–1 的有限值，`pitchScale` 必须是有限正数并会同时改变音高与播放速度；无效参数在加载资源和预占容量前抛出 `ArgumentOutOfRangeException`。这些值只影响本次播放，Voice 回收后会恢复默认值。随机音高或音量的规则属于具体游戏逻辑，应由业务层生成后传入。

### 在进入大型战斗前准备资源和 Voice

把确定会使用的音效资源加载和播放器实例化放进副本加载流程，而不是等第一轮技能同时触发：

```csharp
await Task.WhenAll(
    audio.PrepareSfxAsync(GameAudio.PlayerHit, loadingToken),
    audio.PrepareSfxAsync(GameAudio.RemoteExplosion, loadingToken),
    audio.PrepareSfxAsync(GameAudio.CriticalWarning, loadingToken));

int createdVoices = audio.PrewarmSfxVoices(32);
int created3DVoices = audio.PrewarmSfx3DVoices(32);
```

`PrepareSfxAsync` 复用 ResourceHub 与 Godot Resource 缓存，不建立第二套 Audio 缓存，也不占用活动或待加载 Voice 容量。调用方取消只停止当前等待，共享的底层加载可能继续；`StopAllSfx()` 不取消资源准备，AudioService 退出会取消等待。加载失败或资源不是 AudioStream 时抛出 `AudioPlaybackException`。

`PrewarmSfxVoices` 在主线程同步实例化非空间 Voice；`PrewarmSfx3DVoices` 预热独立的 3D Voice 池。两者都返回本次新增数量，活动与空闲 Voice 都计入对应的 Prepared 数量，重复或更小目标不会重复创建，也不会缩池。预热目标不能超过各自 Max 容量；实例化会增加加载阶段耗时和常驻对象，应在加载界面调用，不要放进战斗帧。

`StopAllSfx()` 会立即归还活动 Voice 并释放待加载请求预占的逻辑容量。旧等待方会在共享底层加载完成后以 `OperationCanceledException` 结束，不会减少新一代请求的容量计数。

### 处理大型战斗的突发音效

不要通过盲目提高 Voice 上限或排队播放来处理同帧爆发。为可丢弃的声音设置较低优先级和同资源上限，让关键提示在容量满时显式抢占：

```csharp
SfxPlaybackResult result = await audio.PlaySfxAsync(
    GameAudio.RemoteExplosion,
    new SfxPlaybackOptions(
        volumeLinear: 0.7f,
        pitchScale: 1f,
        priority: SfxPriority.Low,
        maxConcurrentPerKey: 4,
        allowStealLowerPriority: false));

if (result.Status == SfxPlaybackStatus.GlobalCapacityReached ||
    result.Status == SfxPlaybackStatus.PerKeyLimitReached)
{
    // 正常丢弃，不排队补播。
}
```

玩家技能确认或关键警告可以使用 `High` / `Critical` 并启用 `allowStealLowerPriority`。容量满时，AudioService 只选择更低优先级候选，先淘汰最低优先级，再淘汰其中最早提交的一路；活动 Voice 与仍在加载的请求都参与。旧 bool API 的请求按 `Normal` 参与统一仲裁，因此也可能被高级请求取代：尚未开始的旧请求返回 `false`，已经播放的声音提前停止。

`maxConcurrentPerKey` 同时统计相同资源的活动和待加载数量，适合限制爆炸、碰撞等重复声音。它不能替代距离截止；极端战斗仍可由业务按 Listener 距离跳过明显不可听请求。

成功结果提供 `SfxPlaybackHandle`。循环 AudioStream 不会自然触发 Finished，应保存 Handle 并调用 `TryStopSfx(handle)`；自然结束、停止、被抢占或服务退出后，`IsSfxPlaying(handle)` 和重复停止都返回 `false`。`StopAllSfx()` 仍用于流程退出时的统一清理。

### 播放静态世界坐标 3D 音效

爆炸、命中点和落地声可以提交世界坐标，并显式给出最大可听距离：

```csharp
Sfx3DPlaybackResult result = await audio.PlaySfx3DAsync(
    GameAudio.RemoteExplosion,
    explosion.GlobalPosition,
    new Sfx3DPlaybackOptions(
        maxDistance: 80f,
        unitSize: 8f,
        volumeLinear: 0.8f,
        priority: SfxPriority.Low,
        maxConcurrentPerKey: 8));

if (result.Started)
{
    // 循环流或需要提前终止时保存 Handle。
    audio.TryStopSfx3D(result.Handle);
}
```

`MaxDistance` 和 `UnitSize` 必须是有限正数。最大距离让 Godot 在 Listener 超距后停止混音，不能省略为无界值；衰减听感同时取决于 `AttenuationModel`。3D 池与非空间池分别维护容量、预热、拒绝和抢占统计，不会互相抢占。资源仍通过同一个 `PrepareSfxAsync` 准备。

短促的爆炸、命中和脚步应继续使用静态坐标，避免持续同步成本。移动发动机声、持续技能等确实需要追随目标的声音可以使用跟随入口：

```csharp
Sfx3DPlaybackResult engineLoop = await audio.PlaySfx3DFollowAsync(
    GameAudio.VehicleEngine,
    vehicle,
    new Vector3(0f, 0.5f, -1f),
    new Sfx3DPlaybackOptions(maxDistance: 60f, unitSize: 4f));
```

跟随位置按固定物理帧更新，不按渲染帧更新；没有活动跟随 Voice 时 AudioService 会关闭这条物理更新。`MaxFollowingSfx3DVoices` 是独立硬预算，默认 16 且不能高于 `MaxSfx3DVoices`；活动与待加载跟随请求达到上限时返回 `FollowCapacityReached`。目标提交时必须位于场景树中，加载期间失效返回 `TargetUnavailableBeforeStart`，播放中离树或删除则自动停止。循环声仍需保存 Handle，并在业务所有权结束时主动停止。

Viewport 必须有活动的 `Camera3D` 或 `AudioListener3D`。使用 `TryStopSfx3D`、`IsSfx3DPlaying` 和 `StopAllSfx3D` 管理独立 3D Handle 与池；这些方法不会停止非空间 SFX。

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
- AudioService 退出时停止两路 BGM、取消加载和过渡，并释放非空间与 3D SFX 池。
- `StopAllSfx()` 与 `StopAllSfx3D()` 分别归还各自活动 Voice，并取消对应的待加载请求。

所有 UI 和 Audio public API 都只能从 Godot 主线程调用。打开界面和首次音频加载不应放在每帧路径。

## 常见错误

- Modal 打开后角色仍移动：Modal 只拦截 GUI 指针，需切换输入 Context 或暂停流程。
- 返回一次关闭了错误页面：多个节点同时处理返回输入，应集中到单一边界。
- View 跨场景意外保留：这是默认语义，拥有它的流程必须显式关闭。
- 直接 QueueFree 后界面顺序异常：继续使用 UiService 会清理失效记录，但业务仍应通过 Close/TryGoBack 维持明确所有权。
- BGM 请求偶尔被拒绝：上一项 BGM 仍在加载，流程没有串行等待。
- Crossfade 任务被取消：更新的播放请求取代了它，或调用方/Stop/框架生命周期发出了取消；按流程所有权处理，不要当作资源损坏。
- SFX 返回 false：兼容入口容量已满，或其待加载预占被高级请求取代；可跳过非关键声音。需要区分原因时使用结构化受控入口。
- 第一次大规模 SFX 仍然卡顿：在副本加载流程调用 `PrepareSfxAsync`，并把非空间/3D Voice 分别预热到实测预算；不要仅提高最大容量。
- 循环 SFX 永不归还：循环流不会自然 Finished，保存成功结果的 Handle 并在所有权结束时调用 `TryStopSfx`。
- 重启后音量恢复默认：只调用 SetVolume 没有通过 SettingsService 保存。
- 3D 声音听起来没有位置：确认使用 `PlaySfx3DAsync` 或 `PlaySfx3DFollowAsync`、Viewport 有活动 Listener，且坐标、`UnitSize`、`MaxDistance` 与游戏世界尺度匹配。
- 移动物体的 3D 声音留在旧位置：静态入口只采样提交时坐标；持续移动声应使用 `PlaySfx3DFollowAsync`，并确认请求没有因跟随预算或目标生命周期被拒绝。

精确接口可查询 <xref:GoDo.IUiService>、<xref:GoDo.UiLayer>、<xref:GoDo.UiOpenException>、<xref:GoDo.IAudioService>、<xref:GoDo.BgmPlaybackState>、<xref:GoDo.SfxPlaybackOptions>、<xref:GoDo.SfxPlaybackResult>、<xref:GoDo.SfxPlaybackHandle>、<xref:GoDo.Sfx3DPlaybackOptions>、<xref:GoDo.Sfx3DPlaybackResult>、<xref:GoDo.Sfx3DPlaybackHandle>、<xref:GoDo.AudioGroup> 和 <xref:GoDo.AudioPlaybackException>。
