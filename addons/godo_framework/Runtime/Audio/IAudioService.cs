using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的背景音乐、非空间音效、3D 空间音效与 Audio Bus 音量服务。</summary>
/// <remarks>
/// 所有成员都必须从 GoDoRuntime 记录的 Godot 主线程调用，并且只在服务位于场景树且完成初始化后有效。
/// 异步任务的延续由调用方正常等待；不要使用 <see cref="Task.Wait()"/> 或 <see cref="Task{TResult}.Result"/> 阻塞主线程。
/// </remarks>
public interface IAudioService
{
    /// <summary>当前背景音乐资源；未设置时为 null。</summary>
    ResourceKey? CurrentBgm { get; }

    /// <summary>背景音乐播放器当前是否正在播放。</summary>
    bool IsBgmPlaying { get; }

    /// <summary>当前是否正在加载背景音乐。</summary>
    bool IsBgmLoading { get; }

    /// <summary>背景音乐当前的加载、播放与过渡状态。</summary>
    BgmPlaybackState BgmState { get; }

    /// <summary>当前活动音效数量。</summary>
    int ActiveSfxCount { get; }

    /// <summary>当前已经通过准入、仍在等待资源加载的音效数量。</summary>
    int PendingSfxCount { get; }

    /// <summary>当前已经实例化、可用于活动播放或空闲复用的 SFX Voice 总数。</summary>
    int PreparedSfxVoiceCount { get; }

    /// <summary>允许同时播放的最大音效数量。</summary>
    int MaxSfxVoices { get; }

    /// <summary>服务初始化以来因全局容量或同资源限制被拒绝的 SFX 请求数量。</summary>
    long RejectedSfxCount { get; }

    /// <summary>服务初始化以来被更高优先级请求取代的活动或待加载 SFX 数量。</summary>
    long PreemptedSfxCount { get; }

    /// <summary>当前活动的 3D 空间音效数量。</summary>
    int ActiveSfx3DCount { get; }

    /// <summary>当前已经通过 3D 准入、仍在等待资源加载的音效数量。</summary>
    int PendingSfx3DCount { get; }

    /// <summary>当前已经实例化、可用于活动播放或空闲复用的 3D SFX Voice 总数。</summary>
    int PreparedSfx3DVoiceCount { get; }

    /// <summary>允许同时播放的最大 3D 空间音效数量。</summary>
    int MaxSfx3DVoices { get; }

    /// <summary>当前正在固定物理频率下同步目标位置的 3D SFX 数量。</summary>
    int FollowingSfx3DCount { get; }

    /// <summary>允许同时活动或等待加载的 3D 跟随请求上限。</summary>
    int MaxFollowingSfx3DVoices { get; }

    /// <summary>服务初始化以来因 3D 全局容量、同资源限制或独立跟随预算被拒绝的请求数量。</summary>
    long RejectedSfx3DCount { get; }

    /// <summary>服务初始化以来被更高优先级请求取代的活动或待加载 3D SFX 数量。</summary>
    long PreemptedSfx3DCount { get; }

    /// <summary>
    /// 异步加载并播放背景音乐。Stop 后旧请求以取消结束，且不会覆盖后续请求状态。
    /// </summary>
    /// <param name="key">AudioStream 资源键。</param>
    /// <param name="restart">同一资源正在播放时是否从头重新播放。</param>
    /// <returns>在目标音乐完成加载并提交为当前播放后完成的任务；同一音乐且不重播时直接完成。</returns>
    /// <exception cref="InvalidOperationException">已有 BGM 请求正在执行，服务未就绪，或调用线程错误。</exception>
    /// <exception cref="OperationCanceledException">Stop、服务退出或请求生命周期使等待失效。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或播放准备失败。</exception>
    Task PlayBgmAsync(ResourceKey key, bool restart = false);

    /// <summary>
    /// 异步加载背景音乐并从当前音乐交叉淡化到目标音乐。
    /// <para>
    /// 加载期间保留当前音乐；更新的 Crossfade 请求会取消旧请求并以最新请求为准。
    /// 取消已开始的过渡时，服务保留当时音量较高的一路并停止另一路。
    /// </para>
    /// </summary>
    /// <param name="key">目标 AudioStream 资源键。</param>
    /// <param name="durationSeconds">交叉淡化时长（秒），必须为有限且大于零的值。</param>
    /// <param name="cancellationToken">调用方取消标记；取消不会中止 ResourceHub 中可能共享的底层加载。</param>
    /// <returns>在无需切换时直接完成，否则在目标加载并完成交叉淡化后完成的任务。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="durationSeconds"/> 不是有限正数。</exception>
    /// <exception cref="OperationCanceledException">调用方取消、更新请求取代本请求，或服务退出场景树。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或播放准备失败。</exception>
    Task CrossfadeBgmAsync(
        ResourceKey key,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定时长内淡出当前背景音乐，完成后停止两路播放器并清空 <see cref="CurrentBgm"/>。
    /// <para>
    /// 没有当前音乐时直接完成。更新的 Crossfade 或 FadeOut 请求会取消旧请求；
    /// 调用方取消已经开始的淡出时，当前音乐恢复为正常音量并继续播放。
    /// </para>
    /// </summary>
    /// <param name="durationSeconds">淡出时长（秒），必须为有限且大于零的值。</param>
    /// <param name="cancellationToken">调用方取消标记。</param>
    /// <returns>在没有当前音乐时直接完成，否则在两路播放器停止且当前资源清空后完成的任务。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="durationSeconds"/> 不是有限正数。</exception>
    /// <exception cref="OperationCanceledException">调用方取消、更新请求取代本请求，或服务退出场景树。</exception>
    Task FadeOutBgmAsync(
        double durationSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>暂停背景音乐。</summary>
    void PauseBgm();

    /// <summary>恢复背景音乐。</summary>
    void ResumeBgm();

    /// <summary>停止背景音乐、释放当前资源引用，并立即允许发起新的加载请求。</summary>
    void StopBgm();

    /// <summary>
    /// 异步加载并播放一次音效。达到并发上限时返回 false，不抢占正在播放的音效。
    /// StopAll 后旧请求以取消结束，且不再占用新请求的逻辑容量。
    /// </summary>
    /// <param name="key">要播放的 AudioStream 资源键。</param>
    /// <returns>
    /// 音效成功开始播放时为 <see langword="true"/>；容量拒绝或加载完成前被更高优先级请求取代时为
    /// <see langword="false"/>。
    /// </returns>
    /// <exception cref="OperationCanceledException">StopAllSfx 或服务退出时请求仍在加载。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或播放准备失败。</exception>
    Task<bool> PlaySfxAsync(ResourceKey key);

    /// <summary>
    /// 使用逐次音量和音高异步加载并播放一次音效。达到并发上限时返回 false，
    /// 不抢占正在播放的音效。参数只影响本次播放，Voice 回收后恢复默认值。
    /// </summary>
    /// <param name="key">AudioStream 资源键。</param>
    /// <param name="volumeLinear">本次播放的线性音量，必须为 0 到 1 之间的有限值。</param>
    /// <param name="pitchScale">本次播放的音高与速度倍率，必须为有限正数。</param>
    /// <returns>
    /// 音效成功开始播放时为 <see langword="true"/>；容量拒绝或加载完成前被更高优先级请求取代时为
    /// <see langword="false"/>。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volumeLinear"/> 或 <paramref name="pitchScale"/> 无效。</exception>
    Task<bool> PlaySfxAsync(
        ResourceKey key,
        float volumeLinear,
        float pitchScale = 1f);

    /// <summary>
    /// 使用逐次参数和突发准入策略异步播放音效。相同资源限制同时统计活动与待加载请求；
    /// 全局容量满且允许抢占时，优先取代最低优先级中最早提交的请求。
    /// </summary>
    /// <param name="key">AudioStream 资源键。</param>
    /// <param name="options">逐次播放参数、优先级和并发限制；default 等同于默认选项。</param>
    /// <returns>包含准入状态以及成功播放句柄的结构化结果。</returns>
    /// <exception cref="ArgumentOutOfRangeException">选项中的音量、音高、优先级或同资源上限无效。</exception>
    /// <exception cref="OperationCanceledException">调用 StopAllSfx 或服务退出时请求仍在加载。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或播放准备失败。</exception>
    Task<SfxPlaybackResult> PlaySfxAsync(
        ResourceKey key,
        SfxPlaybackOptions options);

    /// <summary>
    /// 通过 ResourceHub 提前准备一个 SFX AudioStream，不建立第二套缓存，也不占用 Voice 容量。
    /// </summary>
    /// <param name="key">要准备的 AudioStream 资源键。</param>
    /// <param name="cancellationToken">只取消本次等待；底层共享加载可能仍会完成。</param>
    /// <returns>在资源由 ResourceHub 加载并通过 AudioStream 类型检查后完成的任务。</returns>
    /// <exception cref="OperationCanceledException">调用方取消或 AudioService 退出。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或类型校验失败。</exception>
    Task PrepareSfxAsync(
        ResourceKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将已实例化的非空间 SFX Voice 总数幂等预热到目标值，活动 Voice 也计入总数。
    /// </summary>
    /// <param name="targetVoiceCount">目标总数，必须在 0 到 MaxSfxVoices 之间。</param>
    /// <returns>本次新实例化的 Voice 数量；已经达到目标时为 0。</returns>
    /// <exception cref="ArgumentOutOfRangeException">目标超出有效范围。</exception>
    int PrewarmSfxVoices(int targetVoiceCount);

    /// <summary>
    /// 在指定世界坐标播放一次 3D 空间音效。3D Voice 使用独立容量；相同资源限制同时统计
    /// 活动与待加载请求，容量满且允许抢占时取代最低优先级中最早提交的请求。
    /// </summary>
    /// <param name="key">AudioStream 资源键。</param>
    /// <param name="globalPosition">声音发出位置，三个分量都必须是有限值。</param>
    /// <param name="options">距离衰减、逐次播放参数、优先级和并发限制。</param>
    /// <returns>包含 3D 准入状态以及成功播放句柄的结构化结果。</returns>
    /// <exception cref="ArgumentOutOfRangeException">位置或选项参数无效。</exception>
    /// <exception cref="OperationCanceledException">调用 StopAllSfx3D 或服务退出时请求仍在加载。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或播放准备失败。</exception>
    Task<Sfx3DPlaybackResult> PlaySfx3DAsync(
        ResourceKey key,
        Vector3 globalPosition,
        Sfx3DPlaybackOptions options);

    /// <summary>
    /// 播放并以固定物理频率跟随一个场景树内的 3D 目标。Voice 仍由持久 3D 池持有；
    /// 目标离树、失效或进入删除队列时自动停止并回收。没有活动跟随请求时服务关闭物理更新。
    /// </summary>
    /// <param name="key">AudioStream 资源键。</param>
    /// <param name="target">必须有效、位于场景树中且未进入删除队列的跟随目标。</param>
    /// <param name="localOffset">目标局部坐标中的有限偏移，随目标旋转和缩放。</param>
    /// <param name="options">距离衰减、逐次播放参数、优先级和并发限制。</param>
    /// <returns>
    /// 包含播放句柄或拒绝状态的结构化结果；目标在加载期间失效时返回
    /// TargetUnavailableBeforeStart，达到独立跟随预算时返回 FollowCapacityReached。
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="target"/> 当前不可跟随。</exception>
    /// <exception cref="ArgumentOutOfRangeException">偏移或播放选项无效。</exception>
    /// <exception cref="OperationCanceledException">调用 StopAllSfx3D 或服务退出时请求仍在加载。</exception>
    /// <exception cref="AudioPlaybackException">资源加载或播放准备失败。</exception>
    Task<Sfx3DPlaybackResult> PlaySfx3DFollowAsync(
        ResourceKey key,
        Node3D target,
        Vector3 localOffset,
        Sfx3DPlaybackOptions options);

    /// <summary>
    /// 将已实例化的 3D SFX Voice 总数幂等预热到目标值，活动 Voice 也计入总数。
    /// </summary>
    /// <param name="targetVoiceCount">目标总数，必须在 0 到 MaxSfx3DVoices 之间。</param>
    /// <returns>本次新实例化的 3D Voice 数量；已经达到目标时为 0。</returns>
    /// <exception cref="ArgumentOutOfRangeException">目标超出有效范围。</exception>
    int PrewarmSfx3DVoices(int targetVoiceCount);

    /// <summary>查询句柄对应的 3D SFX 是否仍处于活动播放状态。</summary>
    /// <param name="handle">成功播放 3D SFX 时取得的句柄。</param>
    /// <returns>句柄仍属于当前活动播放时为 <see langword="true"/>；无效、过期或属于其他播放时为 <see langword="false"/>。</returns>
    bool IsSfx3DPlaying(Sfx3DPlaybackHandle handle);

    /// <summary>尝试停止并回收句柄对应的活动 3D SFX。</summary>
    /// <param name="handle">要停止的 3D SFX 播放句柄。</param>
    /// <returns>找到并停止活动播放时为 <see langword="true"/>；无效、过期或尚未开始时为 <see langword="false"/>。</returns>
    bool TryStopSfx3D(Sfx3DPlaybackHandle handle);

    /// <summary>停止并回收全部活动 3D 音效，同时释放其待加载请求预占的逻辑容量。</summary>
    void StopAllSfx3D();

    /// <summary>查询句柄对应的非空间 SFX 是否仍处于活动播放状态。</summary>
    /// <param name="handle">成功播放非空间 SFX 时取得的句柄。</param>
    /// <returns>句柄仍属于当前活动播放时为 <see langword="true"/>；无效、过期或属于其他播放时为 <see langword="false"/>。</returns>
    bool IsSfxPlaying(SfxPlaybackHandle handle);

    /// <summary>尝试停止并回收句柄对应的活动非空间 SFX。</summary>
    /// <param name="handle">要停止的非空间 SFX 播放句柄。</param>
    /// <returns>找到并停止活动播放时为 <see langword="true"/>；无效、过期或尚未开始时为 <see langword="false"/>。</returns>
    bool TryStopSfx(SfxPlaybackHandle handle);

    /// <summary>停止并回收全部活动音效，同时释放待加载请求预占的逻辑容量。</summary>
    void StopAllSfx();

    /// <summary>获取指定 Audio Bus 分组的线性音量。</summary>
    /// <param name="group">Master、BGM 或 SFX 分组。</param>
    /// <returns>Godot AudioServer 当前保存的线性音量；框架设置入口使用 0 到 1 的范围。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="group"/> 不是已定义分组。</exception>
    /// <exception cref="InvalidOperationException">对应 Audio Bus 不存在，服务未就绪，或调用线程错误。</exception>
    float GetVolume(AudioGroup group);

    /// <summary>设置指定 Audio Bus 分组的线性音量。</summary>
    /// <param name="group">Master、BGM 或 SFX 分组。</param>
    /// <param name="linearVolume">0 到 1 之间的有限线性音量。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="group"/> 未定义，或 <paramref name="linearVolume"/> 超出范围。</exception>
    /// <exception cref="InvalidOperationException">对应 Audio Bus 不存在，服务未就绪，或调用线程错误。</exception>
    void SetVolume(AudioGroup group, float linearVolume);
}
