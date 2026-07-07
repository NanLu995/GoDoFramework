using System;
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

    [Export]
    public NodePath BgmPlayerPath { get; set; } = null!;

    [Export]
    public NodePath SfxRootPath { get; set; } = null!;

    [Export]
    public PackedScene SfxVoiceScene { get; set; } = null!;

    [Export(PropertyHint.Range, "1,256,1")]
    public int MaxSfxVoices { get; set; } = 32;

    [Export(PropertyHint.Range, "0,256,1")]
    public int InitialSfxVoices { get; set; } = 8;

    /// <summary>服务是否已经完成节点与对象池初始化。</summary>
    internal bool IsInitialized => _bgm != null && _sfx != null;

    public ResourceKey? CurrentBgm => GetBgm().CurrentBgm;
    public bool IsBgmPlaying => GetBgm().IsPlaying;
    public bool IsBgmLoading => GetBgm().IsLoading;
    public int ActiveSfxCount => GetSfx().ActiveCount;

    public override void _Ready()
    {
        MainThreadGuard.VerifyAccess();

        if (MaxSfxVoices <= 0)
            throw new InvalidOperationException("MaxSfxVoices 必须大于 0。");
        if (InitialSfxVoices < 0 || InitialSfxVoices > MaxSfxVoices)
            throw new InvalidOperationException("InitialSfxVoices 必须在 0 到 MaxSfxVoices 之间。");
        if (SfxVoiceScene == null)
            throw new InvalidOperationException("AudioService 未配置 SfxVoiceScene。");

        AudioStreamPlayer? bgmPlayer = GetNodeOrNull<AudioStreamPlayer>(BgmPlayerPath);
        if (!IsInstanceValid(bgmPlayer))
            throw new InvalidOperationException("AudioService 未配置有效的 BgmPlayerPath。");

        Node? sfxRoot = GetNodeOrNull<Node>(SfxRootPath);
        if (!IsInstanceValid(sfxRoot))
            throw new InvalidOperationException("AudioService 未配置有效的 SfxRootPath。");

        _buses.Initialize();
        _bgm = new BgmPlaybackController(bgmPlayer);
        _sfx = new SfxPoolController(
            SfxVoiceScene,
            sfxRoot,
            MaxSfxVoices,
            InitialSfxVoices);
    }

    public override void _ExitTree()
    {
        if (_sfx != null)
        {
            _sfx.Dispose();
            _sfx = null;
        }

        if (_bgm != null)
        {
            _bgm.Stop();
            _bgm = null;
        }
    }

    public Task PlayBgmAsync(ResourceKey key, bool restart = false) =>
        GetBgm().PlayAsync(key, restart);

    public void PauseBgm() => GetBgm().Pause();

    public void ResumeBgm() => GetBgm().Resume();

    public void StopBgm() => GetBgm().Stop();

    public Task<bool> PlaySfxAsync(ResourceKey key) => GetSfx().PlayAsync(key);

    public void StopAllSfx() => GetSfx().StopAll();

    public float GetVolume(AudioGroup group)
    {
        VerifyReady();
        return _buses.GetVolume(group);
    }

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

    private void VerifyReady()
    {
        MainThreadGuard.VerifyAccess();
        if (!IsInsideTree() || !IsInitialized)
            throw new InvalidOperationException("AudioService 尚未初始化或已经退出场景树。");
    }
}
