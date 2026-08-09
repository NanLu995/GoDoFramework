# AudioService 使用指南

## 定位与优势

AudioService 是业务层统一播放非空间 BGM、非空间/3D 空间 SFX 和管理分组音量的长期服务。资源统一通过 ResourceHub 加载，业务层通过 `Services.Get<IAudioService>()` 获取接口，不查找 Autoload 节点，也不自行维护播放器池。

当前实现由 GoDoRuntime 持有，使用双路 BGM 播放器、`NodePool<SfxVoice>` 和独立的 `NodePool<Sfx3DVoice>`；主场景切换不会中断音频。双播放器只在 BGM 交叉淡化期间同时工作，不承担任意多轨混音。

## 快速上手

```csharp
IAudioService audio = Services.Get<IAudioService>();

await audio.PlayBgmAsync(mainThemeKey);
await audio.CrossfadeBgmAsync(battleThemeKey, 0.5d);
await audio.FadeOutBgmAsync(0.5d);
await audio.PrepareSfxAsync(explosionKey);
audio.PrewarmSfxVoices(32);
bool played = await audio.PlaySfxAsync(explosionKey);
bool varied = await audio.PlaySfxAsync(explosionKey, 0.75f, 1.1f);
SfxPlaybackResult controlled = await audio.PlaySfxAsync(
    explosionKey,
    new SfxPlaybackOptions(
        0.75f,
        1.1f,
        SfxPriority.High,
        maxConcurrentPerKey: 4,
        allowStealLowerPriority: true));

if (controlled.Started)
    audio.TryStopSfx(controlled.Handle);

audio.PrewarmSfx3DVoices(32);
Sfx3DPlaybackResult spatial = await audio.PlaySfx3DAsync(
    explosionKey,
    explosionGlobalPosition,
    new Sfx3DPlaybackOptions(
        maxDistance: 80f,
        unitSize: 8f,
        priority: SfxPriority.High,
        maxConcurrentPerKey: 8,
        allowStealLowerPriority: true));

if (spatial.Started)
    audio.TryStopSfx3D(spatial.Handle);

Sfx3DPlaybackResult following = await audio.PlaySfx3DFollowAsync(
    engineLoopKey,
    vehicle,
    new Vector3(0f, 0.5f, -1f),
    new Sfx3DPlaybackOptions(maxDistance: 60f, unitSize: 4f));

audio.SetVolume(AudioGroup.Bgm, 0.8f);
audio.PauseBgm();
audio.ResumeBgm();
```

`PlaySfxAsync` 返回 false 表示达到并发上限，不是资源错误。

## BGM 语义

- 资源必须是 `AudioStream`，加载或播放准备失败抛出 `AudioPlaybackException`。
- 同一资源重复请求默认不重播；`restart: true` 才从头播放。
- `PlayBgmAsync` 保持立即切换语义；已有 BGM 请求执行时，第二个立即播放请求抛出 `InvalidOperationException`。
- `CrossfadeBgmAsync` 先加载目标资源，加载期间保留当前音乐，再用不受 `Engine.TimeScale` 影响的等功率曲线交叉淡化。时长必须为有限正数。
- Crossfade 采用 latest-request-wins：更新的 Crossfade 请求取消旧过渡。淡化已开始时保留当时音量较高的一路并停止另一路；被取代的等待方收到 `OperationCanceledException`。现有 `PlayBgmAsync` 在任何 BGM 请求执行期间仍保持并发拒绝语义。
- `FadeOutBgmAsync` 使用相同过渡核心将当前 BGM 淡出到静音，完成后停止两路、清空 `CurrentBgm` 并进入 `Stopped`；没有当前音乐时直接完成。调用方取消已开始的 FadeOut 时恢复当前音乐，新 Crossfade 或 FadeOut 请求可取代旧过渡。
- `CurrentBgm` 在正常 Crossfade 完成前仍表示已提交的旧音乐；过渡取消后改为实际保留的一路。
- `BgmState` 区分 `Stopped`、`Loading`、`Playing`、`Paused`、`Transitioning` 与自然播放结束后的 `Ended`。`IsBgmLoading` 仅表示资源加载阶段。
- `PauseBgm`/`ResumeBgm` 同时影响当前两路播放器和正在执行的过渡；`StopBgm` 取消请求、停止两路、释放流引用、清空 `CurrentBgm`，并立即允许发起新请求。
- 加载期间调用 Stop 会释放逻辑加载状态；旧等待方在 ResourceHub 的共享底层加载完成后收到 `OperationCanceledException`，且不会覆盖后续请求状态。

## SFX 语义

- 默认预热 8 个 SfxVoice，最大并发 32，可在 `GoDoRuntime.tscn` 调整。
- `PrepareSfxAsync` 只通过 ResourceHub 提前完成 AudioStream 加载，不建立 Audio 缓存、不占活动或待加载 Voice 容量。重复调用继续服从 ResourceHub 与 Godot 的复用语义。
- 调用方取消 `PrepareSfxAsync` 只停止本次等待，底层共享加载可能继续；`StopAllSfx` 不取消资源准备，AudioService 退出会取消仍在等待的准备任务。加载或类型失败抛出 `AudioPlaybackException`。
- `PrewarmSfxVoices(target)` 在主线程同步实例化空闲 Voice，活动与空闲 Voice 都计入 `PreparedSfxVoiceCount`。目标必须在 0 到 `MaxSfxVoices` 之间；已达到或超过目标时返回 0，较小目标不会缩池。
- Voice 预热会把实例化成本前移并增加常驻节点，应在加载流程执行，不要在战斗帧或其他高频路径调用。3D 空间池使用独立预算与预热入口，不复用这组非空间 Voice。
- 加载请求会预占并发名额，防止多个请求同时完成后突破上限。
- 兼容 `PlaySfxAsync(key)` 与逐次参数重载达到 `MaxSfxVoices` 时返回 false，本身不主动抢占已有声音；它们按 Normal 优先级参与高级请求发起的统一抢占。
- `PlaySfxAsync(key, volumeLinear, pitchScale)` 为单次播放设置 0–1 线性音量和有限正数音高倍率；无效参数在加载资源和预占容量前抛出 `ArgumentOutOfRangeException`。
- 逐次参数只作用于本次 Voice；回收时音量和音高恢复为 1，不会污染后续默认播放。`pitchScale` 同时改变音高与播放速度，随机化策略由业务层决定。
- `PlaySfxAsync(key, SfxPlaybackOptions)` 返回 `SfxPlaybackResult`。`Started` 表示已开始并提供有效 Handle；`GlobalCapacityReached` 与 `PerKeyLimitReached` 是正常拒绝；`PreemptedBeforeStart` 表示加载预占被更高优先级请求取代。
- `MaxConcurrentPerKey` 同时统计相同 ResourceKey 的活动与待加载请求，0 表示不单独限制。全局上限仍由 `MaxSfxVoices` 控制。
- `AllowStealLowerPriority` 只在全局容量满时生效；候选必须低于新请求优先级。服务先选择最低优先级，再选择其中最早提交者，可取代活动 Voice 或待加载请求，不排队也不抢占同级声音。
- 统一抢占包含兼容 API 发起的 Normal 请求。活动播放被抢占后原 Handle 失效；待加载兼容请求最终返回 false，受控请求返回 `PreemptedBeforeStart`。
- `TryStopSfx` 只停止 Handle 对应的一路并立即回收；无效、自然结束、已停止或被抢占的旧 Handle 返回 false。`IsSfxPlaying` 对这些过期 Handle 同样返回 false。
- `PendingSfxCount`、`RejectedSfxCount` 与 `PreemptedSfxCount` 用于观察突发准入；后两者从服务初始化起累计，StopAll 不重置。
- 自然播放结束通过 `Finished` 自动归还 NodePool。
- `StopAllSfx` 主动归还全部 Voice、立即释放待加载请求预占的逻辑容量，并让旧等待方在底层加载完成后以 `OperationCanceledException` 结束。
- 循环 AudioStream 不会自然触发 Finished，必须由调用方 StopAll 或让服务退出。

## 3D SFX 语义

- `PlaySfx3DAsync` 在提交时固定世界坐标，适合爆炸、落点和命中等短音效；它不产生持续的位置同步成本。
- `PlaySfx3DFollowAsync` 接受场景树内的 `Node3D` 与局部偏移，适合载具引擎、持续技能等确实需要移动的声音。开始播放后在固定物理帧同步 `target.ToGlobal(localOffset)`；目标离树、进入删除队列或失效时自动停止并回收 Voice。
- 3D 池默认预热 8 路、最大 32 路，可通过 `InitialSfx3DVoices` 与 `MaxSfx3DVoices` 调整。跟随 Voice 还受独立的 `MaxFollowingSfx3DVoices` 硬上限约束，默认 16；该上限包含活动与待加载跟随请求，达到时返回 `FollowCapacityReached`，不会为了跟随请求突破每物理帧预算。
- 3D 与非空间池分别维护活动、等待、已准备、拒绝和抢占统计，容量满时不会跨池抢占。`FollowingSfx3DCount` 只统计已开始的活动跟随 Voice。
- 资源仍由 `PrepareSfxAsync` 统一准备；`PrewarmSfx3DVoices(target)` 只预热 3D Voice，活动与空闲 Voice 都计入 `PreparedSfx3DVoiceCount`，重复或较小目标不创建也不缩池。
- `Sfx3DPlaybackOptions` 必须显式提供有限正数 `MaxDistance`，避免无界 3D Voice 在远距离持续参与混音；`UnitSize` 也必须为有限正数。音量、音高、衰减模型、优先级和同资源限制在占用容量前完成校验。
- `MaxDistance` 使用 Godot 原生线性截止并与 `UnitSize`、`AttenuationModel` 共同决定听感。它减少超距混音，不等同于提交前的玩法可听性筛选；极端战斗仍可由业务按 Listener 距离跳过明显不可听请求。
- 3D 准入复用非空间线路的规则：相同 ResourceKey 同时统计活动与待加载请求，容量满时只抢占更低优先级，先选最低优先级再选其中最早提交者。
- 跟随目标必须在提交时有效且位于场景树中，否则抛出 `ArgumentException`；局部偏移必须为有限值。若目标在异步加载完成前失效，返回 `TargetUnavailableBeforeStart`，不占用活动 Voice。
- `TryStopSfx3D` 与 `IsSfx3DPlaying` 只接受 3D Handle；`StopAllSfx3D` 只清理 3D 池，不停止非空间 SFX。自然结束、主动停止、抢占和服务退出都会使旧 Handle 过期。
- 可听播放需要当前 Viewport 中存在活动的 `Camera3D` 或 `AudioListener3D`。Windows Headless 自动回归也显式创建 Listener；没有 Listener 的极短测试流不会可靠推进到 `Finished`，不能据此验证自然回收。
- 循环 3D AudioStream 不会自然结束，业务必须保存 Handle 并主动停止。方向锥/Doppler 的逐次配置和 AudioStreamPlayer2D 留到独立后续批次。

## Audio Bus 与音量

```csharp
audio.SetVolume(AudioGroup.Master, 1.0f);
audio.SetVolume(AudioGroup.Bgm, 0.7f);
audio.SetVolume(AudioGroup.Sfx, 0.9f);

float bgmVolume = audio.GetVolume(AudioGroup.Bgm);
```

- 音量使用 0–1 的有限线性值，越界抛出 `ArgumentOutOfRangeException`。
- 缺少 BGM/SFX Bus 时运行时创建、发送到 Master，并通过 ErrorHub Warning 提示。
- 重复初始化不会创建同名 Bus。
- 运行时创建 Bus 不修改 `project.godot` 或持久化 Audio Bus Layout。

## 生命周期与线程

- `GoDoRuntime.tscn` 持有 AudioService、BgmPlayer、BgmSecondaryPlayer、SfxRoot 和 Sfx3DRoot，并注册 `IAudioService`。自定义 AudioService 场景必须配置两个不同的 BGM 播放器路径、两个 Voice 场景和各自播放根节点。
- 所有 API 只能在 Godot 主线程调用。
- AudioService 退出树时停止 BGM、取消未完成请求，并 Dispose 非空间与 3D SFX Pool。
- Services 只持有接口引用，不替 AudioService 管理释放。
- 业务场景中不要创建第二个 AudioService。

## 性能与验证基线

- 非空间与 3D SFX 分别使用 NodePool，空闲 Voice 保持在场景树外，不在每次播放时 Instantiate/QueueFree。
- BGM 过渡不是高频调用；每次请求会创建异步等待状态和一个绑定 AudioService 生命周期的 Tween，空闲时不执行逐帧更新。
- 跟随位置只在存在活动跟随 Voice 时由 `_PhysicsProcess` 更新；最后一个跟随停止后立即关闭物理处理。热路径只遍历受 `MaxFollowingSfx3DVoices` 限制的预分配容器，不创建 List/数组、不使用 LINQ，也不扫描静态 3D Voice。
- Debug 构建可在 Debugger 的 `运行时 / Audio` 页面只读查看 BGM 状态与资源键，以及非空间/3D SFX 各自的活跃、等待、已准备、容量、跟随数量、累计拒绝和抢占；该页面直接读取现有接口，不维护音频历史或新增 AudioService 快照分配。
- `Verification/Automated/AudioServiceRegression.tscn` 提供 19/19 组可重复验证，除既有 BGM 与非空间 SFX 契约外，还覆盖 3D 世界坐标、跟随移动、空闲停更、独立跟随容量、目标离树/加载期间失效、衰减参数、停止、Handle、抢占、预热、自然回收和服务退出。
- 2026-08-09 Windows Godot 4.7.1 Mono Headless Debug 的本批多次复跑中，100 路缓存突发约为 11–18 ms、当前线程累计分配约 128–130 KB，停止后活动 Voice 为 0；Runner 每次执行都会打印当前环境结果。
- `Verification/Performance/AudioSfxBenchmark.tscn` 独立测量非空间首次播放、扩容、缓存批次、优先级抢占和预热，并测量 3D Voice 预热后的首次/稳定 32 路空间突发及 8/16/32 路跟随热更新；分开报告提交、Ready、更新耗时与当前线程分配，不设置跨机器耗时门槛。
- 2026-08-09 同机加入句柄与突发准入后的多次 Debug 复跑中，默认播放提交 P95 观察区间为 0.032–0.068 / 0.081–0.214 / 0.423–0.717 ms（1/8/32 路），32 路当前线程托管分配约 18.3 KB。32 路满载 Critical 抢占的提交 P95 为 0.579–0.883 ms、Ready P95 为 7.804–8.164 ms、平均分配约 1.2 KB；没有逐帧扫描，仍不设置跨机器硬门槛。
- 2026-08-09 同机单次准备对比中，冷首次播放约 46.6 ms，显式资源准备约 5.5 ms，准备后的首次播放 Ready 约 0.38 ms。播放时从 8 路扩容到 32 路 Ready 约 10.3 ms；显式创建 24 路 Voice 约 0.98 ms，预热后的 32 路突发 Ready 约 2.17 ms。数据只表示本机相对基线，资源、JIT、磁盘和 Headless 调度都会影响绝对值。
- 2026-08-09 同机 3D Debug 基准中，显式创建 24 路 3D Voice 约 0.63 ms；预热后的首次 32 路空间突发提交约 4.18 ms、Ready 约 11.12 ms、分配约 20.6 KB，同进程稳定批次提交约 0.18 ms、Ready 约 1.59 ms、分配约 18.8 KB。首次结果包含 3D 调用路径 JIT/引擎冷状态，不能用稳定批次替代。
- 2026-08-09 同机 Debug 下连续 10,000 次跟随更新基准中，8/16/32 路平均每次更新约 7.34/12.06/20.86 μs，三档当前线程托管分配均为 0 B。该结果证明本机热路径实现，不代表其他设备或导出平台预算；项目仍应按目标设备实测并保持跟随上限。
- 同机 Release 程序集复核中，8/16/32 路平均每次更新约 6.06/9.61/19.84 μs，三档当前线程托管分配仍为 0 B；测试后已恢复普通 Debug 构建。该结果仍不是目标平台性能承诺。
- 分配数据包含 Task、测试代码和 Godot 包装层开销，不等同于泄漏结论。

## 不负责的能力

当前不包含播放列表、两路以上的任意 BGM 混音、随机参数策略、语音系统、AudioStreamPlayer2D、方向锥/Doppler 逐次配置、遮挡或声学传播系统。跟随只同步世界位置，不替业务判断可听性，也不绑定目标的释放所有权。

## 常见误用

| 应该 | 避免 |
|---|---|
| 使用 ResourceKey 加载 AudioStream | 业务层散落 ResourceLoader 和字符串路径 |
| 处理 false、取消与 AudioPlaybackException | 把容量满当成资源异常 |
| await Crossfade 并处理被取代请求的取消 | 丢弃过渡任务后遗漏取消或加载异常 |
| 循环音效保存 Handle 并 TryStopSfx | 等待循环流自然 Finished |
| 为空间音效设置实测 MaxDistance 并预热独立 3D 池 | 使用无界距离或把 3D 预热预算算入非空间池 |
| 短音效使用静态坐标，仅为持续移动声使用跟随并设置实测上限 | 把所有 3D 音效都设为跟随或无限提高跟随容量 |
| 使用 Bus 控制分组音量 | 遍历所有播放器逐个改 Volume |
| await 异步播放准备 | fire-and-forget 后丢失异常 |
