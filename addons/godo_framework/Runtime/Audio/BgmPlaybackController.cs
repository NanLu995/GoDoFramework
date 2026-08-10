using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>AudioService 内部的双路 BGM 加载、播放与交叉淡化控制。</summary>
internal sealed class BgmPlaybackController : IDisposable
{
    private sealed class PlaybackRequest
    {
        public TaskCompletionSource<bool> Cancellation { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly Node _owner;
    private readonly AudioStreamPlayer[] _players;
    private readonly ResourceKey?[] _playerKeys = new ResourceKey?[2];
    private PlaybackRequest? _activeRequest;
    private Tween? _transitionTween;
    private TaskCompletionSource<bool>? _transitionCompletion;
    private AudioStreamPlayer? _fadeFrom;
    private AudioStreamPlayer? _fadeTo;
    private float _fadeFromStartVolume;
    private int _activePlayerIndex;
    private BgmPlaybackState _stateBeforePause;
    private bool _isLoading;
    private bool _pauseRequested;
    private bool _disposed;

    public ResourceKey? CurrentBgm { get; private set; }
    public bool IsPlaying => _players[0].Playing || _players[1].Playing;
    public bool IsLoading => _isLoading;
    public BgmPlaybackState State { get; private set; } = BgmPlaybackState.Stopped;

    public BgmPlaybackController(
        Node owner,
        AudioStreamPlayer primaryPlayer,
        AudioStreamPlayer secondaryPlayer)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _players =
        [
            primaryPlayer ?? throw new ArgumentNullException(nameof(primaryPlayer)),
            secondaryPlayer ?? throw new ArgumentNullException(nameof(secondaryPlayer)),
        ];

        for (int index = 0; index < _players.Length; index++)
        {
            _players[index].Bus = AudioBusController.BgmBus;
            _players[index].VolumeLinear = 1f;
        }

        _players[0].Finished += OnPrimaryFinished;
        _players[1].Finished += OnSecondaryFinished;
    }

    public async Task PlayAsync(ResourceKey key, bool restart)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        if (_activeRequest is not null)
            throw new InvalidOperationException("已有背景音乐请求正在执行，不能重复发起立即播放请求。");

        if (CurrentBgm == key)
        {
            if (restart)
            {
                AudioStreamPlayer player = _players[_activePlayerIndex];
                _pauseRequested = false;
                player.StreamPaused = false;
                player.VolumeLinear = 1f;
                player.Play();
                State = BgmPlaybackState.Playing;
            }

            return;
        }

        PlaybackRequest request = BeginRequest();
        _isLoading = true;
        State = BgmPlaybackState.Loading;

        try
        {
            AudioStream stream = await LoadStreamAsync(key, request, CancellationToken.None);
            VerifyCurrentRequest(request);
            _isLoading = false;

            int targetIndex = _activePlayerIndex;
            StopPlayer(1 - targetIndex);
            AudioStreamPlayer target = _players[targetIndex];
            target.Stop();
            target.Stream = stream;
            target.StreamPaused = false;
            target.VolumeLinear = 1f;
            target.Play();
            _pauseRequested = false;
            _playerKeys[targetIndex] = key;
            CurrentBgm = key;
            State = BgmPlaybackState.Playing;
        }
        catch (Exception exception) when (
            exception is not AudioPlaybackException &&
            exception is not OperationCanceledException)
        {
            RestoreStateIfCurrent(request);
            throw new AudioPlaybackException(
                key,
                AudioGroup.Bgm,
                $"背景音乐加载或播放准备失败: {key.Value}",
                exception);
        }
        catch
        {
            RestoreStateIfCurrent(request);
            throw;
        }
        finally
        {
            CompleteRequestIfCurrent(request);
        }
    }

    public async Task CrossfadeAsync(
        ResourceKey key,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        VerifyTransitionDuration(durationSeconds);
        cancellationToken.ThrowIfCancellationRequested();
        if (_activeRequest is null && CurrentBgm == key)
            return;

        PlaybackRequest request = BeginRequest();
        _isLoading = true;
        State = BgmPlaybackState.Loading;

        try
        {
            AudioStream stream = await LoadStreamAsync(key, request, cancellationToken);
            VerifyCurrentRequest(request);
            _isLoading = false;

            int sourceIndex = CurrentBgm.HasValue ? _activePlayerIndex : -1;
            int targetIndex = sourceIndex < 0 ? _activePlayerIndex : 1 - sourceIndex;
            AudioStreamPlayer target = _players[targetIndex];
            StopPlayer(targetIndex);
            target.Stream = stream;
            target.StreamPaused = false;
            target.VolumeLinear = 0f;
            target.Play();
            target.StreamPaused = _pauseRequested;
            _playerKeys[targetIndex] = key;

            AudioStreamPlayer? source = sourceIndex >= 0 ? _players[sourceIndex] : null;
            await RunTransitionAsync(
                source,
                target,
                durationSeconds,
                request,
                cancellationToken);
            VerifyCurrentRequest(request);

            if (sourceIndex >= 0)
                StopPlayer(sourceIndex);

            target.VolumeLinear = 1f;
            _activePlayerIndex = targetIndex;
            CurrentBgm = key;
            State = target.Playing
                ? BgmPlaybackState.Playing
                : BgmPlaybackState.Ended;
        }
        catch (Exception exception) when (
            exception is not AudioPlaybackException &&
            exception is not OperationCanceledException)
        {
            RestoreStateIfCurrent(request);
            throw new AudioPlaybackException(
                key,
                AudioGroup.Bgm,
                $"背景音乐加载或交叉淡化准备失败: {key.Value}",
                exception);
        }
        catch
        {
            RestoreStateIfCurrent(request);
            throw;
        }
        finally
        {
            CompleteRequestIfCurrent(request);
        }
    }

    public async Task FadeOutAsync(
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        VerifyTransitionDuration(durationSeconds);
        cancellationToken.ThrowIfCancellationRequested();

        if (_activeRequest is null && !CurrentBgm.HasValue)
            return;

        PlaybackRequest request = BeginRequest();
        if (!CurrentBgm.HasValue)
        {
            CompleteRequestIfCurrent(request);
            return;
        }

        try
        {
            int sourceIndex = _activePlayerIndex;
            AudioStreamPlayer source = _players[sourceIndex];
            await RunTransitionAsync(
                source,
                null,
                durationSeconds,
                request,
                cancellationToken);
            VerifyCurrentRequest(request);

            StopPlayer(sourceIndex);
            CurrentBgm = null;
            State = BgmPlaybackState.Stopped;
        }
        catch
        {
            RestoreStateIfCurrent(request);
            throw;
        }
        finally
        {
            CompleteRequestIfCurrent(request);
        }
    }

    public void Pause()
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        if (State is BgmPlaybackState.Stopped or BgmPlaybackState.Ended || _pauseRequested)
            return;

        _pauseRequested = true;
        if (State != BgmPlaybackState.Loading)
            _stateBeforePause = State;
        for (int index = 0; index < _players.Length; index++)
        {
            if (_players[index].Stream != null && _players[index].Playing)
                _players[index].StreamPaused = true;
        }

        if (IsTweenValid(_transitionTween))
            _transitionTween!.Pause();

        if (State != BgmPlaybackState.Loading)
            State = BgmPlaybackState.Paused;
    }

    public void Resume()
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        if (!_pauseRequested)
            return;

        _pauseRequested = false;
        for (int index = 0; index < _players.Length; index++)
        {
            if (_players[index].Stream != null && _players[index].StreamPaused)
                _players[index].StreamPaused = false;
        }

        if (IsTweenValid(_transitionTween))
            _transitionTween!.Play();

        if (State == BgmPlaybackState.Paused)
            State = _stateBeforePause;
    }

    public void Stop()
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        CancelActiveRequest(settleTransition: false);
        StopPlayer(0);
        StopPlayer(1);
        CurrentBgm = null;
        _isLoading = false;
        _pauseRequested = false;
        State = BgmPlaybackState.Stopped;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _players[0].Finished -= OnPrimaryFinished;
        _players[1].Finished -= OnSecondaryFinished;
        _disposed = true;
    }

    private PlaybackRequest BeginRequest()
    {
        CancelActiveRequest(settleTransition: true);
        var request = new PlaybackRequest();
        _activeRequest = request;
        return request;
    }

    private async Task<AudioStream> LoadStreamAsync(
        ResourceKey key,
        PlaybackRequest request,
        CancellationToken cancellationToken)
    {
        ResourceLoadOperation<AudioStream> operation = ResourceHub.LoadAsync<AudioStream>(key);
        Task<AudioStream> loadCompletion = operation.Completion;
        TaskCompletionSource<bool>? callerCancellation = null;
        CancellationTokenRegistration cancellationRegistration = default;

        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                callerCancellation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    callerCancellation);
                await Task.WhenAny(
                    loadCompletion,
                    request.Cancellation.Task,
                    callerCancellation.Task);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                await Task.WhenAny(loadCompletion, request.Cancellation.Task);
            }

            VerifyCurrentRequest(request);
            MainThreadGuard.VerifyAccess();
            return await loadCompletion;
        }
        finally
        {
            cancellationRegistration.Dispose();
        }
    }

    private async Task RunTransitionAsync(
        AudioStreamPlayer? source,
        AudioStreamPlayer? target,
        double durationSeconds,
        PlaybackRequest request,
        CancellationToken cancellationToken)
    {
        _fadeFrom = source;
        _fadeTo = target;
        float sourceVolume = source?.VolumeLinear ?? 0f;
        _fadeFromStartVolume = float.IsFinite(sourceVolume) ? sourceVolume : 1f;
        _transitionCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _transitionTween = _owner.CreateTween()
            .SetIgnoreTimeScale()
            .SetPauseMode(Tween.TweenPauseMode.Bound);
        _transitionTween.Finished += OnTransitionFinished;
        _transitionTween.TweenMethod(
            Callable.From<double>(OnCrossfadeProgress),
            0d,
            1d,
            durationSeconds);
        State = BgmPlaybackState.Transitioning;
        if (_pauseRequested)
        {
            _stateBeforePause = BgmPlaybackState.Transitioning;
            _transitionTween.Pause();
            State = BgmPlaybackState.Paused;
        }

        TaskCompletionSource<bool>? callerCancellation = null;
        CancellationTokenRegistration cancellationRegistration = default;
        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                callerCancellation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    callerCancellation);
                await Task.WhenAny(
                    _transitionCompletion.Task,
                    request.Cancellation.Task,
                    callerCancellation.Task);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                await Task.WhenAny(_transitionCompletion.Task, request.Cancellation.Task);
            }

            VerifyCurrentRequest(request);
            MainThreadGuard.VerifyAccess();
            await _transitionCompletion.Task;
        }
        catch
        {
            if (_transitionTween != null)
                SettleInterruptedTransition();
            throw;
        }
        finally
        {
            cancellationRegistration.Dispose();
            ClearTransition(kill: true);
        }
    }

    private void OnCrossfadeProgress(double progress)
    {
        double angle = Math.Clamp(progress, 0d, 1d) * Math.PI * 0.5d;
        if (_fadeFrom != null)
            _fadeFrom.VolumeLinear = (float)(Math.Cos(angle) * _fadeFromStartVolume);
        if (_fadeTo != null)
            _fadeTo.VolumeLinear = (float)Math.Sin(angle);
    }

    private void OnTransitionFinished()
    {
        _transitionCompletion?.TrySetResult(true);
    }

    private void CancelActiveRequest(bool settleTransition)
    {
        PlaybackRequest? request = _activeRequest;
        if (request is null)
            return;

        request.Cancellation.TrySetResult(true);
        if (_transitionTween != null)
        {
            if (settleTransition)
                SettleInterruptedTransition();
            else
                ClearTransition(kill: true);
        }

        if (ReferenceEquals(_activeRequest, request))
            _activeRequest = null;
    }

    private void SettleInterruptedTransition()
    {
        AudioStreamPlayer? survivor;
        int survivorIndex;
        if (_fadeFrom == null ||
            (_fadeTo != null && _fadeTo.VolumeLinear > _fadeFrom.VolumeLinear))
        {
            survivor = _fadeTo;
            survivorIndex = Array.IndexOf(_players, survivor);
        }
        else
        {
            survivor = _fadeFrom;
            survivorIndex = Array.IndexOf(_players, survivor);
        }

        for (int index = 0; index < _players.Length; index++)
        {
            if (index != survivorIndex)
                StopPlayer(index);
        }

        if (survivor != null && survivorIndex >= 0)
        {
            survivor.VolumeLinear = 1f;
            _activePlayerIndex = survivorIndex;
            CurrentBgm = _playerKeys[survivorIndex];
        }

        ClearTransition(kill: true);
        RefreshState();
    }

    private void ClearTransition(bool kill)
    {
        if (_transitionTween != null)
        {
            _transitionTween.Finished -= OnTransitionFinished;
            if (kill && IsTweenValid(_transitionTween))
                _transitionTween.Kill();
        }

        _transitionTween = null;
        _transitionCompletion = null;
        _fadeFrom = null;
        _fadeTo = null;
        _fadeFromStartVolume = 0f;
    }

    private void RestoreStateIfCurrent(PlaybackRequest request)
    {
        if (!ReferenceEquals(_activeRequest, request))
            return;

        if (_transitionTween != null)
            SettleInterruptedTransition();
        else
            RefreshState();
    }

    private void CompleteRequestIfCurrent(PlaybackRequest request)
    {
        if (ReferenceEquals(_activeRequest, request))
        {
            _activeRequest = null;
            _isLoading = false;
        }
    }

    private void VerifyCurrentRequest(PlaybackRequest request)
    {
        if (!ReferenceEquals(_activeRequest, request) || request.Cancellation.Task.IsCompleted)
            throw new OperationCanceledException("背景音乐请求已被更新请求或服务生命周期取消。");
    }

    private static void VerifyTransitionDuration(double durationSeconds)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "BGM 过渡时长必须是有限正数。");
        }
    }

    private void RefreshState()
    {
        if (!CurrentBgm.HasValue)
        {
            State = BgmPlaybackState.Stopped;
            return;
        }

        AudioStreamPlayer player = _players[_activePlayerIndex];
        State = player.StreamPaused
            ? BgmPlaybackState.Paused
            : player.Playing
                ? BgmPlaybackState.Playing
                : BgmPlaybackState.Ended;
    }

    private void StopPlayer(int index)
    {
        AudioStreamPlayer player = _players[index];
        player.Stop();
        player.StreamPaused = false;
        player.VolumeLinear = 1f;
        player.Stream = null;
        _playerKeys[index] = null;
    }

    private void OnPrimaryFinished() => OnPlayerFinished(0);

    private void OnSecondaryFinished() => OnPlayerFinished(1);

    private void OnPlayerFinished(int playerIndex)
    {
        if (_disposed || _transitionTween != null || playerIndex != _activePlayerIndex)
            return;

        RefreshState();
    }

    private static bool IsTweenValid(Tween? tween) => tween != null && tween.IsValid();

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
