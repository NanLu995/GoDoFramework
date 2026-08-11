# Audio：音乐、音效与播放预算

`AudioService` 管理长期 BGM、非空间 SFX、3D SFX 和 Master/BGM/SFX 三个 Audio Bus。业务层通过 `Services.Get<IAudioService>()` 获取它；不要让各个场景自行创建播放器、维护音效池或改全局音量。

## 你能用它完成什么

| 任务 | 选用能力 | 结果 |
| --- | --- | --- |
| 进入关卡时播放主题音乐 | BGM 播放或交叉淡化 | 音乐跨业务场景持续，流程可等待完成 |
| 按钮、命中、拾取反馈 | 非空间 SFX | 使用复用 Voice，容量满时可观察地拒绝 |
| 爆炸、车辆、角色技能 | 3D SFX | 声音位于世界坐标，或在固定物理频率跟随目标 |
| 设置页音量滑块 | Audio Bus 音量 | 一次改动 Master、BGM 或 SFX 分组 |
| 战斗前避免首次卡顿 | 准备资源与预热 Voice | 预加载资源或提前创建可复用播放节点 |

## 先从这里开始

```csharp
IAudioService audio = Services.Get<IAudioService>();

await audio.PlayBgmAsync(mainThemeKey);
bool played = await audio.PlaySfxAsync(buttonClickKey);
audio.SetVolume(AudioGroup.Bgm, 0.7f);
```

`played == false` 代表本次音效因容量或抢占策略没有开始播放，不是资源加载错误。资源不是 `AudioStream`、资源加载失败或播放准备失败时，异步方法会抛出 `AudioPlaybackException`。

## 能力全景图

下面列出业务层可调用的全部能力。每个小节先说明何时用，再给出最小调用；精确参数、返回值和异常以 [IAudioService API](xref:GoDo.IAudioService) 为准。

### BGM：播放、切换与状态

用于菜单、关卡和战斗等由 Procedure 或明确场景协调边界决定的长音频。不要在普通 UI 节点中各自播放 BGM。

<div class="godo-capability-list">
<section><h4>播放或重播</h4><p>当前没有 BGM，或需要立即切换。</p><pre class="godo-capability-call"><code>await audio.PlayBgmAsync(key, restart: false);</code></pre></section>
<section><h4>交叉淡化</h4><p>保留当前音乐直到新音乐加载完成，再平滑切换。</p><pre class="godo-capability-call"><code>await audio.CrossfadeBgmAsync(key, 0.5d);</code></pre></section>
<section><h4>淡出停止</h4><p>离开游戏、进入静音状态。</p><pre class="godo-capability-call"><code>await audio.FadeOutBgmAsync(0.5d);</code></pre></section>
<section><h4>暂停、恢复与立即停止</h4><p>暂停菜单、应用失焦，或需要取消加载并允许新的 BGM 请求。</p><pre class="godo-capability-call"><code>audio.PauseBgm();
audio.ResumeBgm();
audio.StopBgm();</code></pre></section>
<section><h4>读取状态</h4><p>更新播放 UI 或排查流程。</p><pre class="godo-capability-call"><code>audio.CurrentBgm
audio.IsBgmPlaying
audio.IsBgmLoading
audio.BgmState</code></pre></section>
</div>

新的交叉淡化或淡出会取代正在进行的过渡；等待旧请求的一方会收到 `OperationCanceledException`。`durationSeconds` 必须是有限正数。

### 非空间 SFX：一次播放、策略与句柄

用于不依赖世界位置的短反馈，例如按钮、背包、命中提示。基础重载不抢占已播放声音；需要控制容量、优先级或停止某一路时使用结构化选项。

```csharp
bool played = await audio.PlaySfxAsync(clickKey);

SfxPlaybackResult result = await audio.PlaySfxAsync(
    explosionKey,
    new SfxPlaybackOptions(
        volumeLinear: 0.8f,
        pitchScale: 1f,
        priority: SfxPriority.High,
        maxConcurrentPerKey: 4,
        allowStealLowerPriority: true));

if (result.Started)
    audio.TryStopSfx(result.Handle);
```

<div class="godo-capability-list">
<section><h4>默认播放</h4><p>不需要逐次参数。</p><pre class="godo-capability-call"><code>await audio.PlaySfxAsync(key)</code></pre></section>
<section><h4>单次音量与音高</h4><p>同一资源做轻微变化。</p><pre class="godo-capability-call"><code>await audio.PlaySfxAsync(key, volumeLinear, pitchScale)</code></pre></section>
<section><h4>准入策略和句柄</h4><p>限制同资源并发、优先级或允许抢占。</p><pre class="godo-capability-call"><code>await audio.PlaySfxAsync(key, options)</code></pre></section>
<section><h4>查询或停止一路</h4><p>循环或持续声音需要主动结束。</p><pre class="godo-capability-call"><code>audio.IsSfxPlaying(handle)
audio.TryStopSfx(handle)</code></pre></section>
<section><h4>停止全部</h4><p>场景重置或游戏退出。</p><pre class="godo-capability-call"><code>audio.StopAllSfx()</code></pre></section>
</div>

结构化结果的 `Started` 表示句柄有效；`GlobalCapacityReached`、`PerKeyLimitReached` 与 `PreemptedBeforeStart` 是可预期的准入结果。旧句柄在自然结束、停止或被抢占后失效。

### 3D SFX：固定坐标与跟随目标

爆炸、落点和命中应使用固定世界坐标；只有引擎声、持续技能等确实需要持续移动的声音才使用跟随。两者都使用独立的 3D Voice 容量。

```csharp
Sfx3DPlaybackResult explosion = await audio.PlaySfx3DAsync(
    explosionKey,
    explosionGlobalPosition,
    new Sfx3DPlaybackOptions(maxDistance: 80f, unitSize: 8f));

Sfx3DPlaybackResult engine = await audio.PlaySfx3DFollowAsync(
    engineKey,
    vehicle,
    new Vector3(0f, 0.5f, -1f),
    new Sfx3DPlaybackOptions(maxDistance: 60f, unitSize: 4f));
```

<div class="godo-capability-list">
<section><h4>固定坐标播放</h4><p>一次性世界事件。</p><pre class="godo-capability-call"><code>await audio.PlaySfx3DAsync(key, globalPosition, options)</code></pre></section>
<section><h4>跟随播放</h4><p>声源需随有效 <code>Node3D</code> 移动。</p><pre class="godo-capability-call"><code>await audio.PlaySfx3DFollowAsync(key, target, localOffset, options)</code></pre></section>
<section><h4>预热 3D Voice</h4><p>已知战斗会出现突发 3D 声音。</p><pre class="godo-capability-call"><code>audio.PrewarmSfx3DVoices(target)</code></pre></section>
<section><h4>查询或停止一路</h4><p>管理循环或持续 3D 音效。</p><pre class="godo-capability-call"><code>audio.IsSfx3DPlaying(handle)
audio.TryStopSfx3D(handle)</code></pre></section>
<section><h4>停止全部 3D</h4><p>清理世界音效，不影响普通 SFX。</p><pre class="godo-capability-call"><code>audio.StopAllSfx3D()</code></pre></section>
</div>

`Sfx3DPlaybackOptions` 必须设置有限正数的 `MaxDistance` 和 `UnitSize`。跟随目标必须在场景树中有效；加载期间失效会得到 `TargetUnavailableBeforeStart`，超过独立跟随预算会得到 `FollowCapacityReached`。

### 资源准备与 Voice 预热

这两种能力解决的问题不同：准备资源不创建 Voice；预热 Voice 不加载音频资源。都应放在加载流程中，而不是战斗帧或其他高频路径。

<div class="godo-capability-list">
<section><h4>准备 <code>AudioStream</code></h4><p>经 ResourceHub 提前完成资源加载和类型检查。</p><p><code>await audio.PrepareSfxAsync(key)</code></p></section>
<section><h4>预热普通 / 3D Voice</h4><p>将对应池总数提升到目标值。</p><pre class="godo-capability-call"><code>audio.PrewarmSfxVoices(target)
audio.PrewarmSfx3DVoices(target)</code></pre></section>
<section><h4>查看普通池压力</h4><p>决定是否需要调整配置或玩法请求。</p><p><code>ActiveSfxCount</code>、<code>PendingSfxCount</code>、<code>PreparedSfxVoiceCount</code>、<code>MaxSfxVoices</code></p></section>
<section><h4>查看 3D 池压力</h4><p>观察空间音效与跟随预算。</p><p><code>ActiveSfx3DCount</code>、<code>PendingSfx3DCount</code>、<code>PreparedSfx3DVoiceCount</code>、<code>MaxSfx3DVoices</code>、<code>FollowingSfx3DCount</code>、<code>MaxFollowingSfx3DVoices</code></p></section>
<section><h4>查看累计拒绝与抢占</h4><p>定位声音丢失原因。</p><p><code>RejectedSfxCount</code>、<code>PreemptedSfxCount</code>、<code>RejectedSfx3DCount</code>、<code>PreemptedSfx3DCount</code></p></section>
</div>

预热目标必须在相应最大 Voice 数以内。`PrepareSfxAsync` 的取消只取消当前等待，ResourceHub 的共享底层加载可能继续。

### Audio Bus：读取与设置分组音量

用于设置页；业务保存逻辑应与 Settings 协作持久化数值，而不是遍历正在播放的 Voice。

```csharp
audio.SetVolume(AudioGroup.Master, 1f);
audio.SetVolume(AudioGroup.Bgm, 0.7f);
audio.SetVolume(AudioGroup.Sfx, 0.9f);
float currentBgmVolume = audio.GetVolume(AudioGroup.Bgm);
```

可用分组是 `Master`、`Bgm` 和 `Sfx`；音量必须为 0 到 1 的有限线性值。

## 选择正确的调用方式

<div class="godo-capability-list">
<section><h4>有背景音乐的流程切换</h4><p>使用 <code>CrossfadeBgmAsync</code>；避免多个节点各自 <code>PlayBgmAsync</code>。</p></section>
<section><h4>简单一次点击声</h4><p>使用默认 <code>PlaySfxAsync</code>；避免为每次点击创建播放器。</p></section>
<section><h4>高优先级命中声</h4><p>使用 <code>SfxPlaybackOptions</code>；不要假设容量满时仍一定播放。</p></section>
<section><h4>一次性空间声</h4><p>使用 <code>PlaySfx3DAsync</code>；避免跟随一个临时爆炸节点。</p></section>
<section><h4>持续移动的声源</h4><p>使用 <code>PlaySfx3DFollowAsync</code> 并保留句柄；避免无上限地创建跟随 Voice。</p></section>
<section><h4>预加载关卡音频</h4><p>使用 <code>PrepareSfxAsync</code> 加上按需预热；避免在 <code>_Process</code> 反复调用预热。</p></section>
</div>

## 使用约束与排查

- 所有 API 必须从 Godot 主线程、服务已初始化且仍在场景树时调用。
- `AudioStream` 加载/类型问题使用异常处理；容量、同资源限制、抢占和跟随预算属于返回结果，不要把它们当作资源错误。
- 3D 声音要可靠听见，需要活动 `Camera3D` 或 `AudioListener3D`；循环声音不会自然结束，应保存句柄并主动停止。
- BGM、普通 SFX 和 3D SFX 的完整失败语义与生命周期，见 [Audio API Reference](xref:GoDo.IAudioService)；设置持久化示例见[存档、设置与本地化工作流](../save-settings-localization/index.md)。
