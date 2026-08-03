using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理主内容场景的异步加载与替换。
/// <para>本服务必须位于场景树中，并且所有调用都必须发生在 Godot 主线程。</para>
/// </summary>
public sealed partial class SceneService : Node, ISceneService
{
    private ResourceLoadOperation<PackedScene>? _loadOperation;
    private TaskCompletionSource<bool>? _lifecycleCancellation;
    private int _lifecycleVersion;
#if DEBUG
    private ResourceKey? _currentChangeKey;
    private SceneDebugPhase _debugPhase;
    private ulong _debugStartedTicks;
    private ResourceKey? _lastChangeKey;
    private SceneDebugPhase _lastPhase;
    private SceneDebugResult _lastResult;
    private string? _lastDetail;
    private ulong _lastDurationMilliseconds;
#endif

    /// <summary>当前是否正在切换场景。</summary>
    public bool IsChanging { get; private set; }

    /// <summary>当前场景加载进度，范围为 0 到 1；失败或取消后复位为 0。</summary>
    public float Progress { get; private set; }

    /// <inheritdoc />
    public override void _EnterTree()
    {
        _lifecycleVersion++;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _lifecycleVersion++;
        _lifecycleCancellation?.TrySetResult(true);

        if (_loadOperation != null)
            _loadOperation.ProgressChanged -= OnLoadProgressChanged;
    }

    /// <summary>
    /// 异步加载并替换当前主场景。加载或实例化失败时保留旧场景。
    /// </summary>
    /// <exception cref="InvalidOperationException">服务未进入场景树，或已有切换正在执行。</exception>
    /// <exception cref="SceneChangeException">
    /// 加载、实例化或挂载目标场景失败，或服务离树导致切换取消；
    /// 生命周期取消时 <see cref="Exception.InnerException"/> 为 <see cref="OperationCanceledException"/>。
    /// </exception>
    public Task<Node> ChangeAsync(ResourceKey key) =>
        ChangeAsync(key, null, CancellationToken.None);

    /// <summary>
    /// 异步加载并替换当前主场景，同时报告该次请求的加载进度并允许调用方在提交前取消。
    /// </summary>
    /// <exception cref="InvalidOperationException">服务未进入场景树，或已有切换正在执行。</exception>
    /// <exception cref="OperationCanceledException">调用方在场景提交前取消请求。</exception>
    /// <exception cref="SceneChangeException">
    /// 加载、实例化或挂载目标场景失败，或服务离树导致切换取消；
    /// 生命周期取消时 <see cref="Exception.InnerException"/> 为 <see cref="OperationCanceledException"/>。
    /// </exception>
    public async Task<Node> ChangeAsync(
        ResourceKey key,
        Action<float>? onProgress,
        CancellationToken cancellationToken = default)
    {
        MainThreadGuard.VerifyAccess();

        if (!IsInsideTree())
            throw new InvalidOperationException("SceneService 必须进入场景树后才能切换场景。");

        if (IsChanging)
            throw new InvalidOperationException("已有场景切换正在执行，不能重复发起请求。");

        cancellationToken.ThrowIfCancellationRequested();

        SceneTree tree = GetTree();
        Node? currentScene = tree.CurrentScene;
        if (IsInstanceValid(currentScene) &&
            (currentScene == this || currentScene.IsAncestorOf(this)))
        {
            throw new InvalidOperationException(
                "SceneService 不能挂在当前主场景内部，否则切换时会随旧场景一起释放。");
        }

        IsChanging = true;
        Progress = 0f;
        SceneChangePhase phase = SceneChangePhase.Loading;
#if DEBUG
        _currentChangeKey = key;
        _debugPhase = SceneDebugPhase.Loading;
        _debugStartedTicks = Time.GetTicksMsec();
#endif
        int lifecycleVersion = _lifecycleVersion;
        var lifecycleCancellation = new TaskCompletionSource<bool>();
        _lifecycleCancellation = lifecycleCancellation;
        TaskCompletionSource<bool>? callerCancellation = null;
        CancellationTokenRegistration cancellationRegistration = default;

        try
        {
            _loadOperation = ResourceHub.LoadAsync<PackedScene>(key);
            _loadOperation.ProgressChanged += OnLoadProgressChanged;
            if (onProgress is not null)
                _loadOperation.ProgressChanged += onProgress;

            Task<PackedScene> loadCompletion = _loadOperation.Completion;
            if (cancellationToken.CanBeCanceled)
            {
                callerCancellation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    callerCancellation);
                await Task.WhenAny(
                    loadCompletion,
                    lifecycleCancellation.Task,
                    callerCancellation.Task);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                await Task.WhenAny(loadCompletion, lifecycleCancellation.Task);
            }

            VerifyLifecycle(lifecycleVersion, key, phase);
            MainThreadGuard.VerifyAccess();

            PackedScene packedScene = await loadCompletion;
            cancellationToken.ThrowIfCancellationRequested();
            phase = SceneChangePhase.Instantiating;
#if DEBUG
            _debugPhase = SceneDebugPhase.Instantiating;
#endif
            Node newScene = InstantiateScene(packedScene, key);
            if (cancellationToken.IsCancellationRequested)
            {
                newScene.QueueFree();
                cancellationToken.ThrowIfCancellationRequested();
            }
            phase = SceneChangePhase.Committing;
#if DEBUG
            _debugPhase = SceneDebugPhase.Committing;
#endif
            ReplaceCurrentScene(newScene, key, lifecycleVersion);
            EventChannel.Emit<FrameworkMainSceneChangedEvent>();
            Progress = 1f;
#if DEBUG
            RecordDebugResult(key, SceneDebugResult.Succeeded);
#endif
            return newScene;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            Progress = 0f;
#if DEBUG
            RecordDebugResult(key, SceneDebugResult.CallerCanceled, exception);
#else
            _ = exception;
#endif
            throw;
        }
        catch (SceneChangeException exception)
        {
            Progress = 0f;
#if DEBUG
            RecordDebugResult(
                key,
                exception.InnerException is OperationCanceledException
                    ? SceneDebugResult.LifecycleCanceled
                    : SceneDebugResult.Failed,
                exception);
#else
            _ = exception;
#endif
            throw;
        }
        catch (Exception exception)
        {
            Progress = 0f;
#if DEBUG
            RecordDebugResult(key, SceneDebugResult.Failed, exception);
#endif
            throw new SceneChangeException(
                key,
                phase,
                $"场景切换失败，旧场景保持不变: {key.Value}",
                exception);
        }
        finally
        {
            cancellationRegistration.Dispose();
            if (_loadOperation != null)
            {
                _loadOperation.ProgressChanged -= OnLoadProgressChanged;
                if (onProgress is not null)
                    _loadOperation.ProgressChanged -= onProgress;
            }

            _loadOperation = null;
            if (ReferenceEquals(_lifecycleCancellation, lifecycleCancellation))
                _lifecycleCancellation = null;

            IsChanging = false;
#if DEBUG
            _currentChangeKey = null;
            _debugPhase = SceneDebugPhase.Idle;
#endif
        }
    }

#if DEBUG
    internal SceneDebugSnapshot GetDebugSnapshot()
    {
        MainThreadGuard.VerifyAccess();
        return new SceneDebugSnapshot(
            _currentChangeKey,
            _debugPhase,
            _lastChangeKey,
            _lastPhase,
            _lastResult,
            _lastDetail,
            _lastDurationMilliseconds);
    }

    private void RecordDebugResult(
        ResourceKey key,
        SceneDebugResult result,
        Exception? exception = null)
    {
        _lastChangeKey = key;
        _lastPhase = _debugPhase;
        _lastResult = result;
        _lastDurationMilliseconds = Time.GetTicksMsec() - _debugStartedTicks;
        if (exception is null)
        {
            _lastDetail = null;
            return;
        }

        Exception detailException = exception.GetBaseException();
        string detail = detailException.Message;
        _lastDetail = detail.Length <= 256 ? detail : detail[..256];
    }
#endif

    private static Node InstantiateScene(PackedScene packedScene, ResourceKey key)
    {
        if (!packedScene.CanInstantiate())
        {
            throw new SceneChangeException(
                key,
                SceneChangePhase.Instantiating,
                $"目标 PackedScene 不包含可实例化的节点: {key.Value}");
        }

        try
        {
            return packedScene.Instantiate();
        }
        catch (Exception exception)
        {
            throw new SceneChangeException(
                key,
                SceneChangePhase.Instantiating,
                $"目标 PackedScene 无法实例化: {key.Value}",
                exception);
        }
    }

    private void ReplaceCurrentScene(Node newScene, ResourceKey key, int lifecycleVersion)
    {
        SceneTree tree = GetTree();
        Node? oldScene = tree.CurrentScene;

        try
        {
            tree.Root.AddChild(newScene);
            VerifyLifecycle(
                lifecycleVersion,
                key,
                SceneChangePhase.Committing);
            tree.CurrentScene = newScene;
        }
        catch (SceneChangeException)
        {
            if (IsInstanceValid(newScene))
                newScene.QueueFree();

            throw;
        }
        catch (Exception exception)
        {
            if (IsInstanceValid(newScene))
                newScene.QueueFree();

            throw new SceneChangeException(
                key,
                SceneChangePhase.Committing,
                $"目标场景无法加入场景树: {key.Value}",
                exception);
        }

        if (IsInstanceValid(oldScene) && oldScene != newScene)
            oldScene.QueueFree();
    }

    private void VerifyLifecycle(
        int expectedVersion,
        ResourceKey key,
        SceneChangePhase phase)
    {
        if (_lifecycleVersion == expectedVersion && IsInsideTree())
            return;

        throw new SceneChangeException(
            key,
            phase,
            $"SceneService 在加载完成前退出或重新进入了场景树，已取消切换: {key.Value}",
            new OperationCanceledException("SceneService 生命周期已变化。"));
    }

    private void OnLoadProgressChanged(float progress)
    {
        Progress = progress;
    }
}
