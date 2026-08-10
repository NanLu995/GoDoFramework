using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>管理非空间 BGM、SFX 播放和 Audio Bus 分组音量。</summary>
public sealed partial class AudioService : Node, IAudioService
{
    private readonly AudioBusController _buses = new();
    private BgmPlaybackController? _bgm;
    private SfxPoolController? _sfx;
    private Sfx3DPoolController? _sfx3D;

    /// <summary>BGM 播放器节点路径。</summary>
    [Export]
    public NodePath BgmPlayerPath { get; set; } = null!;

    /// <summary>交叉淡化使用的第二路 BGM 播放器节点路径。</summary>
    [Export]
    public NodePath SecondaryBgmPlayerPath { get; set; } = null!;

    /// <summary>SFX 播放节点的父节点路径。</summary>
    [Export]
    public NodePath SfxRootPath { get; set; } = null!;

    /// <summary>SFX 声部使用的场景资源。</summary>
    [Export]
    public PackedScene SfxVoiceScene { get; set; } = null!;

    /// <summary>SFX 声部总数上限。</summary>
    [Export(PropertyHint.Range, "1,256,1")]
    public int MaxSfxVoices { get; set; } = 32;

    /// <summary>启动时预热的 SFX 声部数量。</summary>
    [Export(PropertyHint.Range, "0,256,1")]
    public int InitialSfxVoices { get; set; } = 8;

    /// <summary>3D SFX 播放节点的父节点路径。</summary>
    [Export]
    public NodePath Sfx3DRootPath { get; set; } = null!;

    /// <summary>3D SFX 声部使用的场景资源。</summary>
    [Export]
    public PackedScene Sfx3DVoiceScene { get; set; } = null!;

    /// <summary>3D SFX 声部总数上限。</summary>
    [Export(PropertyHint.Range, "1,256,1")]
    public int MaxSfx3DVoices { get; set; } = 32;

    /// <summary>启动时预热的 3D SFX 声部数量。</summary>
    [Export(PropertyHint.Range, "0,256,1")]
    public int InitialSfx3DVoices { get; set; } = 8;

    /// <summary>同时活动或等待加载的 3D 跟随请求上限；0 表示禁用跟随。</summary>
    [Export(PropertyHint.Range, "0,256,1")]
    public int MaxFollowingSfx3DVoices { get; set; } = 16;

    /// <summary>服务是否已经完成节点与对象池初始化。</summary>
    internal bool IsInitialized => _bgm != null && _sfx != null && _sfx3D != null;

    /// <inheritdoc />
    public ResourceKey? CurrentBgm => GetBgm().CurrentBgm;
    /// <inheritdoc />
    public bool IsBgmPlaying => GetBgm().IsPlaying;
    /// <inheritdoc />
    public bool IsBgmLoading => GetBgm().IsLoading;
    /// <inheritdoc />
    public BgmPlaybackState BgmState => GetBgm().State;
    /// <inheritdoc />
    public int ActiveSfxCount => GetSfx().ActiveCount;
    /// <inheritdoc />
    public int PendingSfxCount => GetSfx().PendingCount;
    /// <inheritdoc />
    public int PreparedSfxVoiceCount => GetSfx().PreparedCount;
    /// <inheritdoc />
    public long RejectedSfxCount => GetSfx().RejectedCount;
    /// <inheritdoc />
    public long PreemptedSfxCount => GetSfx().PreemptedCount;
    /// <inheritdoc />
    public int ActiveSfx3DCount => GetSfx3D().ActiveCount;
    /// <inheritdoc />
    public int PendingSfx3DCount => GetSfx3D().PendingCount;
    /// <inheritdoc />
    public int PreparedSfx3DVoiceCount => GetSfx3D().PreparedCount;
    /// <inheritdoc />
    public long RejectedSfx3DCount => GetSfx3D().RejectedCount;
    /// <inheritdoc />
    public long PreemptedSfx3DCount => GetSfx3D().PreemptedCount;
    /// <inheritdoc />
    public int FollowingSfx3DCount => GetSfx3D().FollowingCount;

    /// <inheritdoc />
    public override void _Ready()
    {
        MainThreadGuard.VerifyAccess();
        SetPhysicsProcess(false);

        if (MaxSfxVoices <= 0)
            throw new InvalidOperationException("MaxSfxVoices 必须大于 0。");
        if (InitialSfxVoices < 0 || InitialSfxVoices > MaxSfxVoices)
            throw new InvalidOperationException("InitialSfxVoices 必须在 0 到 MaxSfxVoices 之间。");
        if (SfxVoiceScene == null)
            throw new InvalidOperationException("AudioService 未配置 SfxVoiceScene。");
        if (MaxSfx3DVoices <= 0)
            throw new InvalidOperationException("MaxSfx3DVoices 必须大于 0。");
        if (InitialSfx3DVoices < 0 || InitialSfx3DVoices > MaxSfx3DVoices)
            throw new InvalidOperationException("InitialSfx3DVoices 必须在 0 到 MaxSfx3DVoices 之间。");
        if (Sfx3DVoiceScene == null)
            throw new InvalidOperationException("AudioService 未配置 Sfx3DVoiceScene。");
        if (MaxFollowingSfx3DVoices < 0 ||
            MaxFollowingSfx3DVoices > MaxSfx3DVoices)
        {
            throw new InvalidOperationException(
                "MaxFollowingSfx3DVoices 必须在 0 到 MaxSfx3DVoices 之间。");
        }

        AudioStreamPlayer? bgmPlayer = GetNodeOrNull<AudioStreamPlayer>(BgmPlayerPath);
        if (!IsInstanceValid(bgmPlayer))
            throw new InvalidOperationException("AudioService 未配置有效的 BgmPlayerPath。");

        AudioStreamPlayer? secondaryBgmPlayer =
            GetNodeOrNull<AudioStreamPlayer>(SecondaryBgmPlayerPath);
        if (!IsInstanceValid(secondaryBgmPlayer))
            throw new InvalidOperationException("AudioService 未配置有效的 SecondaryBgmPlayerPath。");
        if (secondaryBgmPlayer == bgmPlayer)
            throw new InvalidOperationException("两路 BGM 播放器必须是不同节点。");

        Node? sfxRoot = GetNodeOrNull<Node>(SfxRootPath);
        if (!IsInstanceValid(sfxRoot))
            throw new InvalidOperationException("AudioService 未配置有效的 SfxRootPath。");

        Node3D? sfx3DRoot = GetNodeOrNull<Node3D>(Sfx3DRootPath);
        if (!IsInstanceValid(sfx3DRoot))
            throw new InvalidOperationException("AudioService 未配置有效的 Sfx3DRootPath。");

        _buses.Initialize();
        _bgm = new BgmPlaybackController(this, bgmPlayer, secondaryBgmPlayer);
        _sfx = new SfxPoolController(
            SfxVoiceScene,
            sfxRoot,
            MaxSfxVoices,
            InitialSfxVoices);
        _sfx3D = new Sfx3DPoolController(
            Sfx3DVoiceScene,
            sfx3DRoot,
            MaxSfx3DVoices,
            InitialSfx3DVoices,
            MaxFollowingSfx3DVoices,
            SetPhysicsProcess);
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        _sfx3D?.PhysicsUpdateFollowingVoices();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (_sfx3D != null)
        {
            _sfx3D.Dispose();
            _sfx3D = null;
        }

        if (_sfx != null)
        {
            _sfx.Dispose();
            _sfx = null;
        }

        if (_bgm != null)
        {
            _bgm.Dispose();
            _bgm = null;
        }
    }

    /// <inheritdoc />
    public Task PlayBgmAsync(ResourceKey key, bool restart = false) =>
        GetBgm().PlayAsync(key, restart);

    /// <inheritdoc />
    public Task CrossfadeBgmAsync(
        ResourceKey key,
        double durationSeconds,
        CancellationToken cancellationToken = default) =>
        GetBgm().CrossfadeAsync(key, durationSeconds, cancellationToken);

    /// <inheritdoc />
    public Task FadeOutBgmAsync(
        double durationSeconds,
        CancellationToken cancellationToken = default) =>
        GetBgm().FadeOutAsync(durationSeconds, cancellationToken);

    /// <inheritdoc />
    public void PauseBgm() => GetBgm().Pause();

    /// <inheritdoc />
    public void ResumeBgm() => GetBgm().Resume();

    /// <inheritdoc />
    public void StopBgm() => GetBgm().Stop();

    /// <inheritdoc />
    public Task<bool> PlaySfxAsync(ResourceKey key) => GetSfx().PlayAsync(key);

    /// <inheritdoc />
    public Task<bool> PlaySfxAsync(
        ResourceKey key,
        float volumeLinear,
        float pitchScale = 1f) =>
        GetSfx().PlayAsync(key, volumeLinear, pitchScale);

    /// <inheritdoc />
    public Task<SfxPlaybackResult> PlaySfxAsync(
        ResourceKey key,
        SfxPlaybackOptions options) =>
        GetSfx().PlayAsync(key, options);

    /// <inheritdoc />
    public Task PrepareSfxAsync(
        ResourceKey key,
        CancellationToken cancellationToken = default) =>
        GetSfx().PrepareAsync(key, cancellationToken);

    /// <inheritdoc />
    public int PrewarmSfxVoices(int targetVoiceCount) =>
        GetSfx().Prewarm(targetVoiceCount);

    /// <inheritdoc />
    public Task<Sfx3DPlaybackResult> PlaySfx3DAsync(
        ResourceKey key,
        Vector3 globalPosition,
        Sfx3DPlaybackOptions options) =>
        GetSfx3D().PlayAsync(key, globalPosition, options);

    /// <inheritdoc />
    public Task<Sfx3DPlaybackResult> PlaySfx3DFollowAsync(
        ResourceKey key,
        Node3D target,
        Vector3 localOffset,
        Sfx3DPlaybackOptions options) =>
        GetSfx3D().PlayFollowAsync(key, target, localOffset, options);

    /// <inheritdoc />
    public int PrewarmSfx3DVoices(int targetVoiceCount) =>
        GetSfx3D().Prewarm(targetVoiceCount);

    /// <inheritdoc />
    public bool IsSfx3DPlaying(Sfx3DPlaybackHandle handle) =>
        GetSfx3D().IsPlaying(handle);

    /// <inheritdoc />
    public bool TryStopSfx3D(Sfx3DPlaybackHandle handle) =>
        GetSfx3D().TryStop(handle);

    /// <inheritdoc />
    public void StopAllSfx3D() => GetSfx3D().StopAll();

    /// <inheritdoc />
    public bool IsSfxPlaying(SfxPlaybackHandle handle) =>
        GetSfx().IsPlaying(handle);

    /// <inheritdoc />
    public bool TryStopSfx(SfxPlaybackHandle handle) =>
        GetSfx().TryStop(handle);

    /// <inheritdoc />
    public void StopAllSfx() => GetSfx().StopAll();

    /// <inheritdoc />
    public float GetVolume(AudioGroup group)
    {
        VerifyReady();
        return _buses.GetVolume(group);
    }

    /// <inheritdoc />
    public void SetVolume(AudioGroup group, float linearVolume)
    {
        VerifyReady();
        _buses.SetVolume(group, linearVolume);
    }

    private BgmPlaybackController GetBgm()
    {
        VerifyReady();
        return _bgm!;
    }

    private SfxPoolController GetSfx()
    {
        VerifyReady();
        return _sfx!;
    }

    private Sfx3DPoolController GetSfx3D()
    {
        VerifyReady();
        return _sfx3D!;
    }

    private void VerifyReady()
    {
        MainThreadGuard.VerifyAccess();
        if (!IsInsideTree() || !IsInitialized)
            throw new InvalidOperationException("AudioService 尚未初始化或已经退出场景树。");
    }
}
