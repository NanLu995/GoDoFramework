using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理屏幕空间 UI 的显示层、生命周期与返回顺序。
/// <para>本服务必须位于场景树中，并且所有调用都必须发生在 Godot 主线程。</para>
/// <para>
/// View 与 Modal 会隔离并恢复受管理 UI 的键盘/手柄焦点，但不会自动推断首次打开界面的默认焦点。
/// </para>
/// </summary>
public sealed partial class UiService : Node, IUiService
{
    private readonly List<Control> _sceneViews = new();
    private readonly List<Control> _views = new();
    private readonly List<ModalEntry> _modals = new();
    private readonly List<Control> _overlays = new();
    private readonly Dictionary<UiId, UiRuntimeConfigEntry> _uiConfigEntries = new();
    private readonly Dictionary<Control, UiId> _uiIds = new();
    private readonly Dictionary<UiId, List<UiOpenCancellation>> _openingCancellations = new();
    private readonly Dictionary<UiLayer, List<DirectUiOpenRequest>> _directOpeningRequests = new();
    private readonly Dictionary<UiId, Control> _cachedUiInstances = new();
    private readonly Dictionary<Control, Control> _lastFocusedControls = new();
#if DEBUG
    private readonly Dictionary<Control, ResourceKey> _debugKeys = new();
    private UiId _debugLastOpenId;
    private UiLayer _debugLastOpenLayer;
    private ResourceKey _debugLastOpenKey;
    private UiDebugOpenPhase _debugLastOpenPhase;
    private UiDebugOpenResult _debugLastOpenResult;
    private string? _debugLastOpenDetail;
    private ulong _debugLastOpenDurationMilliseconds;
#endif
    private UiRoot? _root;
    private bool _uiConfigLoaded;
    private int _sceneVersion;

    internal void Initialize(UiRoot root)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(root);
        if (_root != null)
            throw new InvalidOperationException("UiService 已经完成初始化。");
        if (!IsInsideTree() || !root.IsInsideTree() || !root.IsInitialized)
            throw new InvalidOperationException("UiService 和 UiRoot 必须完成场景树初始化。");

        _root = root;
        EventChannel.Bind<FrameworkMainSceneChangedEvent>(this, OnMainSceneChanged);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        CancelAllOpenRequestsCore(lifecycle: true);
        ClearCachedInstancesCore();
        _sceneViews.Clear();
        _views.Clear();
        _modals.Clear();
        _overlays.Clear();
        _uiConfigEntries.Clear();
        _uiIds.Clear();
        _openingCancellations.Clear();
        _directOpeningRequests.Clear();
        _lastFocusedControls.Clear();
#if DEBUG
        _debugKeys.Clear();
#endif
        _uiConfigLoaded = false;
        _root = null;
    }

    /// <summary>加载并校验按语义标识打开 UI 所需的目录。</summary>
    public void LoadUiConfig(ResourceKey key)
    {
        VerifyReady();
        PruneInvalidEntries();
        if (_sceneViews.Count > 0 || _views.Count > 0 || _modals.Count > 0 ||
            _overlays.Count > 0 ||
            _openingCancellations.Count > 0)
        {
            throw new InvalidOperationException("存在已打开或正在打开的受管理 UI 时不能替换 UiConfig。");
        }

        UiConfig config = ConfigHub.Load<UiConfig>(key);
        var entries = new Dictionary<UiId, UiRuntimeConfigEntry>();
        for (int i = 0; i < config.Entries.Count; i++)
        {
            UiConfigEntry entry = config.Entries[i];
            UiId id = UiId.Create(entry.Id);
            entries.Add(
                id,
                new UiRuntimeConfigEntry(
                    ResourceKey.Create(entry.Locator),
                    entry.Layer,
                    entry.InstanceMode,
                    entry.ReuseInstance));
        }

        ClearCachedInstancesCore();
        _uiConfigEntries.Clear();
        foreach (KeyValuePair<UiId, UiRuntimeConfigEntry> pair in entries)
            _uiConfigEntries.Add(pair.Key, pair.Value);
        _uiConfigLoaded = true;
    }

    /// <summary>按已加载目录中的语义标识和默认层级打开 UI。</summary>
    public Control Open(UiId id)
    {
        VerifyReady();
        PruneInvalidEntries();
        UiRuntimeConfigEntry entry = GetUiConfigEntry(id);
        if (entry.InstanceMode == UiInstanceMode.Single && HasOpenOrOpeningSingleInstance(id))
            throw new InvalidOperationException($"Single UI 已经打开：{id.Value}");

        VerifyLayer(entry.Layer);
        Control view = TakeCachedOrInstantiate(id, entry);
        MountView(view, entry.Key, entry.Layer);
        return RegisterConfiguredView(view, id, entry);
    }

    /// <summary>按已加载配置中的语义标识打开强类型 UI，并在加入场景树前完成可选配置。</summary>
    public TView Open<TView>(UiId id, Action<TView>? configure = null)
        where TView : Control
    {
        VerifyReady();
        PruneInvalidEntries();
        UiRuntimeConfigEntry entry = GetUiConfigEntry(id);
        if (entry.InstanceMode == UiInstanceMode.Single && HasOpenOrOpeningSingleInstance(id))
            throw new InvalidOperationException($"Single UI 已经打开：{id.Value}");

        VerifyLayer(entry.Layer);
        TView view = TakeCachedOrInstantiate<TView>(id, entry);
        ConfigureAndMount(view, entry.Key, entry.Layer, configure);
        return RegisterConfiguredView(view, id, entry);
    }

    /// <summary>按已加载配置中的语义标识异步加载并打开强类型 UI。</summary>
    public Task<TView> OpenAsync<TView>(
        UiId id,
        Action<TView>? configure = null,
        Action<float>? onProgress = null,
        CancellationToken cancellationToken = default)
        where TView : Control
    {
        VerifyReady();
        PruneInvalidEntries();
        UiRuntimeConfigEntry entry = GetUiConfigEntry(id);
        bool isSingle = entry.InstanceMode == UiInstanceMode.Single;
        if (isSingle && HasOpenOrOpeningSingleInstance(id))
            throw new InvalidOperationException($"Single UI 已经打开或正在打开：{id.Value}");
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<TView>(cancellationToken);
        if (_cachedUiInstances.ContainsKey(id))
            return OpenCachedAsync(id, entry, configure, onProgress, cancellationToken);

        ResourceLoadOperation<PackedScene> resourceOperation =
            ResourceHub.LoadAsync<PackedScene>(entry.Key);
        if (onProgress is not null)
            resourceOperation.ProgressChanged += onProgress;
        var cancellation = new UiOpenCancellation(cancellationToken);
        AddOpening(id, cancellation);
        int sceneVersion = entry.Layer == UiLayer.Scene ? _sceneVersion : -1;

        return CompleteConfiguredOpenAsync(
            resourceOperation,
            entry.Key,
            entry.Layer,
            id,
            configure,
            onProgress,
            sceneVersion,
            cancellationToken,
            cancellation);
    }

    /// <summary>在指定层打开 UI 界面。</summary>
    public Control Open(ResourceKey key, UiLayer layer)
    {
        VerifyReady();
        PruneInvalidEntries();
        VerifyLayer(layer);
        Control view = InstantiateView(key);
        MountView(view, key, layer);
        RestoreOpenedFocus(view, layer);
        return view;
    }

    /// <summary>在指定层打开强类型 UI，并在加入场景树前完成可选配置。</summary>
    public TView Open<TView>(ResourceKey key, UiLayer layer, Action<TView>? configure = null)
        where TView : Control
    {
        VerifyReady();
        PruneInvalidEntries();
        VerifyLayer(layer);
        TView view = InstantiateView<TView>(key);
        ConfigureAndMount(view, key, layer, configure);
        RestoreOpenedFocus(view, layer);
        return view;
    }

    /// <summary>异步加载并在指定层打开强类型 UI。</summary>
    public Task<TView> OpenAsync<TView>(
        ResourceKey key,
        UiLayer layer,
        Action<TView>? configure = null,
        Action<float>? onProgress = null,
        CancellationToken cancellationToken = default)
        where TView : Control
    {
        VerifyReady();
        PruneInvalidEntries();
        VerifyLayer(layer);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<TView>(cancellationToken);

        ResourceLoadOperation<PackedScene> resourceOperation =
            ResourceHub.LoadAsync<PackedScene>(key);
        if (onProgress is not null)
            resourceOperation.ProgressChanged += onProgress;
        int sceneVersion = layer == UiLayer.Scene ? _sceneVersion : -1;
        var cancellation = new UiOpenCancellation(cancellationToken);
        AddDirectOpening(layer, key, cancellation);
        return CompleteDirectOpenAsync(
            resourceOperation,
            key,
            layer,
            configure,
            onProgress,
            sceneVersion,
            cancellationToken,
            cancellation);
    }

    private async Task<TView> CompleteDirectOpenAsync<TView>(
        ResourceLoadOperation<PackedScene> resourceOperation,
        ResourceKey key,
        UiLayer layer,
        Action<TView>? configure,
        Action<float>? onProgress,
        int sceneVersion,
        CancellationToken cancellationToken,
        UiOpenCancellation cancellation)
        where TView : Control
    {
        try
        {
            TView view = await CompleteOpenAsync(
                resourceOperation,
                key,
                layer,
                configure,
                onProgress,
                sceneVersion,
                cancellationToken,
                cancellation);
#if DEBUG
            RecordDebugOpenSuccess(default, key, layer, cancellation);
#endif
            return view;
        }
        catch (OperationCanceledException exception)
        {
#if DEBUG
            RecordDebugOpenCancellation(
                default,
                key,
                layer,
                sceneVersion,
                cancellation,
                exception);
#else
            _ = exception;
#endif
            throw;
        }
        catch (Exception exception)
        {
#if DEBUG
            RecordDebugOpenFailure(default, key, layer, cancellation, exception);
#else
            _ = exception;
#endif
            throw;
        }
        finally
        {
            RemoveDirectOpening(layer, cancellation);
        }
    }

    private Task<TView> OpenCachedAsync<TView>(
        UiId id,
        UiRuntimeConfigEntry entry,
        Action<TView>? configure,
        Action<float>? onProgress,
        CancellationToken cancellationToken)
        where TView : Control
    {
        var cancellation = new UiOpenCancellation(cancellationToken);
        AddOpening(id, cancellation);
        try
        {
            onProgress?.Invoke(1f);
            cancellation.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
#if DEBUG
            cancellation.SetDebugPhase(UiDebugOpenPhase.Preparing);
#endif
            TView view = TakeCachedOrInstantiate<TView>(id, entry);
#if DEBUG
            cancellation.SetDebugPhase(UiDebugOpenPhase.Committing);
#endif
            ConfigureAndMount(view, entry.Key, entry.Layer, configure);
            TView registered = RegisterConfiguredView(view, id, entry);
#if DEBUG
            RecordDebugOpenSuccess(id, entry.Key, entry.Layer, cancellation);
#endif
            return Task.FromResult(registered);
        }
        catch (OperationCanceledException exception)
        {
#if DEBUG
            RecordDebugOpenCancellation(
                id,
                entry.Key,
                entry.Layer,
                entry.Layer == UiLayer.Scene ? _sceneVersion : -1,
                cancellation,
                exception);
#endif
            return Task.FromCanceled<TView>(exception.CancellationToken);
        }
        catch (Exception exception)
        {
#if DEBUG
            RecordDebugOpenFailure(id, entry.Key, entry.Layer, cancellation, exception);
#endif
            return Task.FromException<TView>(exception);
        }
        finally
        {
            RemoveOpening(id, cancellation);
        }
    }

    private Control TakeCachedOrInstantiate(UiId id, UiRuntimeConfigEntry entry)
    {
        if (!_cachedUiInstances.Remove(id, out Control? cachedView))
            return InstantiateView(entry.Key);
        if (!IsInstanceValid(cachedView) || cachedView.IsQueuedForDeletion())
        {
            _lastFocusedControls.Remove(cachedView);
            return InstantiateView(entry.Key);
        }

        cachedView.Show();
        return cachedView;
    }

    private TView TakeCachedOrInstantiate<TView>(UiId id, UiRuntimeConfigEntry entry)
        where TView : Control
    {
        if (_cachedUiInstances.TryGetValue(id, out Control? cachedView) &&
            IsInstanceValid(cachedView) &&
            !cachedView.IsQueuedForDeletion() &&
            cachedView is not TView)
        {
            throw CreateViewTypeMismatchException<TView>(cachedView, entry.Key);
        }

        Control view = TakeCachedOrInstantiate(id, entry);
        return CastView<TView>(view, entry.Key);
    }

    private TView RegisterConfiguredView<TView>(
        TView view,
        UiId id,
        UiRuntimeConfigEntry entry)
        where TView : Control
    {
        _uiIds.Add(view, id);
        if (!entry.ReuseInstance || view is not IPoolable poolable)
        {
            RestoreOpenedFocus(view, entry.Layer);
            return view;
        }

        try
        {
            poolable.OnAcquire();
            RestoreOpenedFocus(view, entry.Layer);
            return view;
        }
        catch (Exception exception)
        {
            DiscardManagedView(view);
            throw new UiOpenException(
                entry.Key,
                UiOpenPhase.Committing,
                $"复用 UI 的 OnAcquire 执行失败：{id.Value}",
                exception);
        }
    }

    private TView ConfigureAndMount<TView>(
        TView view,
        ResourceKey key,
        UiLayer layer,
        Action<TView>? configure)
        where TView : Control
    {
        try
        {
            configure?.Invoke(view);
        }
        catch
        {
            _lastFocusedControls.Remove(view);
            view.QueueFree();
            throw;
        }

        MountView(view, key, layer);
        return view;
    }

    private async Task<TView> CompleteConfiguredOpenAsync<TView>(
        ResourceLoadOperation<PackedScene> resourceOperation,
        ResourceKey key,
        UiLayer layer,
        UiId id,
        Action<TView>? configure,
        Action<float>? onProgress,
        int sceneVersion,
        CancellationToken cancellationToken,
        UiOpenCancellation cancellation)
        where TView : Control
    {
        try
        {
            TView view = await CompleteOpenAsync(
                resourceOperation,
                key,
                layer,
                configure,
                onProgress,
                sceneVersion,
                cancellationToken,
                cancellation);
            TView registered = RegisterConfiguredView(view, id, GetUiConfigEntry(id));
#if DEBUG
            RecordDebugOpenSuccess(id, key, layer, cancellation);
#endif
            return registered;
        }
        catch (OperationCanceledException exception)
        {
#if DEBUG
            RecordDebugOpenCancellation(
                id,
                key,
                layer,
                sceneVersion,
                cancellation,
                exception);
#else
            _ = exception;
#endif
            throw;
        }
        catch (Exception exception)
        {
#if DEBUG
            RecordDebugOpenFailure(id, key, layer, cancellation, exception);
#else
            _ = exception;
#endif
            throw;
        }
        finally
        {
            RemoveOpening(id, cancellation);
        }
    }

    private async Task<TView> CompleteOpenAsync<TView>(
        ResourceLoadOperation<PackedScene> resourceOperation,
        ResourceKey key,
        UiLayer layer,
        Action<TView>? configure,
        Action<float>? onProgress,
        int sceneVersion,
        CancellationToken cancellationToken,
        UiOpenCancellation? cancellation)
        where TView : Control
    {
        CancellationTokenRegistration cancellationRegistration = default;
        try
        {
            PackedScene scene;
            try
            {
                if (cancellation is null)
                {
                    scene = await resourceOperation.Completion;
                }
                else
                {
                    if (cancellationToken.CanBeCanceled)
                    {
                        cancellationRegistration = cancellationToken.Register(
                            static state => ((UiOpenCancellation)state!).RequestFromCaller(),
                            cancellation);
                    }
                    Task completed = await Task.WhenAny(
                        resourceOperation.Completion,
                        cancellation.Completion);
                    if (completed != resourceOperation.Completion)
                        await cancellation.Completion;

                    scene = await resourceOperation.Completion;
                    cancellation.ThrowIfCancellationRequested();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new UiOpenException(
                    key,
                    UiOpenPhase.Loading,
                    $"UI 场景无法异步加载：{key.Value}",
                    exception);
            }

            VerifyReady();
            if (layer == UiLayer.Scene && sceneVersion != _sceneVersion)
            {
                throw new OperationCanceledException(
                    $"异步 Scene UI 加载期间主场景已经变更：{key.Value}");
            }

            PruneInvalidEntries();
#if DEBUG
            cancellation?.SetDebugPhase(UiDebugOpenPhase.Preparing);
#endif
            TView view = InstantiateView<TView>(scene, key);
#if DEBUG
            cancellation?.SetDebugPhase(UiDebugOpenPhase.Committing);
#endif
            ConfigureAndMount(view, key, layer, configure);
            RestoreOpenedFocus(view, layer);
            return view;
        }
        finally
        {
            cancellationRegistration.Dispose();
            if (onProgress is not null)
                resourceOperation.ProgressChanged -= onProgress;
        }
    }

    private sealed class UiOpenCancellation
    {
        private static readonly CancellationToken ServiceCancellationToken = new(true);
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _callerToken;
        private int _requested;
#if DEBUG
        public UiDebugOpenPhase DebugPhase { get; private set; } = UiDebugOpenPhase.Loading;
        public UiDebugOpenCancellationOrigin DebugCancellationOrigin { get; private set; }
        public ulong DebugStartedTicks { get; } = Time.GetTicksMsec();
#endif

        public UiOpenCancellation(CancellationToken callerToken)
        {
            _callerToken = callerToken;
        }

        public Task Completion => _completion.Task;

        public bool RequestFromService()
        {
#if DEBUG
            return Request(ServiceCancellationToken, UiDebugOpenCancellationOrigin.Service);
#else
            return Request(ServiceCancellationToken);
#endif
        }

        public bool RequestFromLifecycle()
        {
#if DEBUG
            return Request(ServiceCancellationToken, UiDebugOpenCancellationOrigin.Lifecycle);
#else
            return Request(ServiceCancellationToken);
#endif
        }

        public void RequestFromCaller()
        {
#if DEBUG
            _ = Request(_callerToken, UiDebugOpenCancellationOrigin.Caller);
#else
            _ = Request(_callerToken);
#endif
        }

#if DEBUG
        public void SetDebugPhase(UiDebugOpenPhase phase) => DebugPhase = phase;
#endif

        public void ThrowIfCancellationRequested()
        {
            if (_completion.Task.IsCanceled)
                _completion.Task.GetAwaiter().GetResult();
        }

        private bool Request(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _requested, 1) != 0)
                return false;

            _completion.TrySetCanceled(cancellationToken);
            return true;
        }

#if DEBUG
        private bool Request(
            CancellationToken cancellationToken,
            UiDebugOpenCancellationOrigin origin)
        {
            if (Interlocked.Exchange(ref _requested, 1) != 0)
                return false;

            DebugCancellationOrigin = origin;
            _completion.TrySetCanceled(cancellationToken);
            return true;
        }
#endif
    }

    private static void VerifyLayer(UiLayer layer)
    {
        if (layer is not UiLayer.Scene and not UiLayer.View and not UiLayer.Modal and not UiLayer.Overlay)
            throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 UI 层。");
    }

    private Control MountView(Control view, ResourceKey key, UiLayer layer)
    {
        PrepareFocusForOpen(layer);
        try
        {
            return layer switch
            {
                UiLayer.Scene => MountSceneView(view, key),
                UiLayer.View => MountStackView(view, key),
                UiLayer.Modal => MountModal(view, key),
                UiLayer.Overlay => MountOverlay(view, key),
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 UI 层。")
            };
        }
        catch
        {
            _lastFocusedControls.Remove(view);
            RestoreActiveFocus();
            throw;
        }
    }

    /// <summary>判断指定已注册标识是否存在打开实例。</summary>
    public bool IsOpen(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        _ = GetUiConfigEntry(id);
        return HasOpenInstance(id);
    }

    /// <summary>获取指定已注册标识当前打开的实例数量。</summary>
    public int GetOpenCount(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        _ = GetUiConfigEntry(id);

        int count = 0;
        foreach (UiId openId in _uiIds.Values)
        {
            if (openId == id)
                count++;
        }

        return count;
    }

    /// <summary>判断指定已注册标识是否存在尚未完成的异步打开请求。</summary>
    public bool IsOpening(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        _ = GetUiConfigEntry(id);
        return _openingCancellations.ContainsKey(id);
    }

    /// <summary>获取指定已注册标识当前尚未完成的异步打开请求数量。</summary>
    public int GetOpeningCount(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        _ = GetUiConfigEntry(id);
        return _openingCancellations.TryGetValue(
            id,
            out List<UiOpenCancellation>? cancellations)
            ? cancellations.Count
            : 0;
    }

    /// <summary>取消指定已注册标识的全部未完成异步打开请求。</summary>
    public int CancelOpenRequests(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        _ = GetUiConfigEntry(id);
        return CancelOpenRequestsCore(id);
    }

    /// <summary>取消指定 UI 层的全部未完成异步打开请求。</summary>
    public int CancelOpenRequests(UiLayer layer)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        VerifyLayer(layer);
        return CancelOpenRequestsCore(layer);
    }

    private int CancelOpenRequestsCore(UiLayer layer, bool lifecycle = false)
    {
        int canceled = 0;
        foreach (KeyValuePair<UiId, List<UiOpenCancellation>> pair in _openingCancellations)
        {
            if (_uiConfigEntries[pair.Key].Layer == layer)
                canceled += CancelOpenRequestsCore(pair.Value, lifecycle);
        }
        if (_directOpeningRequests.TryGetValue(
                layer,
                out List<DirectUiOpenRequest>? directRequests))
        {
            canceled += CancelDirectOpenRequestsCore(directRequests, lifecycle);
        }

        return canceled;
    }

    /// <summary>判断指定已注册标识是否存在已关闭且可复用的缓存实例。</summary>
    public bool HasCachedInstance(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        _ = GetUiConfigEntry(id);

        if (!_cachedUiInstances.TryGetValue(id, out Control? view))
            return false;

        if (IsInstanceValid(view) && !view.IsQueuedForDeletion())
            return true;

        _cachedUiInstances.Remove(id);
        _lastFocusedControls.Remove(view);
        return false;
    }

    /// <summary>清理指定已注册标识的缓存实例；打开中和加载中的实例不受影响。</summary>
    public bool ClearCachedInstance(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        _ = GetUiConfigEntry(id);

        if (!_cachedUiInstances.Remove(id, out Control? view))
            return false;

        _lastFocusedControls.Remove(view);
        if (IsInstanceValid(view) && !view.IsQueuedForDeletion())
            view.QueueFree();
        return true;
    }

    /// <summary>清理全部已关闭且可复用的缓存实例；打开中和加载中的实例不受影响。</summary>
    public int ClearCachedInstances()
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        return ClearCachedInstancesCore();
    }

    /// <summary>尝试获取指定已注册标识最上层的实例。</summary>
    public bool TryGetTop(UiId id, out Control? view)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        _ = GetUiConfigEntry(id);

        view = FindTopmostById(id);
        return view is not null;
    }

    /// <summary>尝试获取指定已注册标识最上层的强类型实例。</summary>
    public bool TryGetTop<TView>(UiId id, out TView? view)
        where TView : Control
    {
        if (!TryGetTop(id, out Control? control))
        {
            view = null;
            return false;
        }

        Control openView = control!;
        if (openView is TView typedView)
        {
            view = typedView;
            return true;
        }

        throw new InvalidCastException(
            $"UI 根节点类型不匹配，期望 {typeof(TView).FullName}，实际 {openView.GetType().FullName}：{id.Value}");
    }

    /// <summary>尝试获取指定 UI 层最上层或最后打开的实例。</summary>
    public bool TryGetTop(UiLayer layer, out Control? view)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();

        view = layer switch
        {
            UiLayer.Scene => _sceneViews.Count > 0 ? _sceneViews[^1] : null,
            UiLayer.View => _views.Count > 0 ? _views[^1] : null,
            UiLayer.Modal => _modals.Count > 0 ? _modals[^1].View : null,
            UiLayer.Overlay => _overlays.Count > 0 ? _overlays[^1] : null,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 UI 层。")
        };
        return view is not null;
    }

    /// <summary>关闭由本服务管理的界面。</summary>
    public void Close(Control view)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(view);
        VerifyReady();
        PruneInvalidEntries();
        if (!IsInstanceValid(view))
            throw new InvalidOperationException("目标 UI 已经释放，不再受服务管理。");

        if (!TryCloseManaged(view))
            throw new InvalidOperationException("目标 UI 不受服务管理。");
    }

    /// <summary>尝试关闭指定受管理实例。</summary>
    public bool TryClose(Control view)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(view);
        VerifyReady();
        PruneInvalidEntries();
        return IsInstanceValid(view) && TryCloseManaged(view);
    }

    /// <summary>尝试关闭指定标识最上层的实例。</summary>
    public bool TryClose(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        _ = GetUiConfigEntry(id);

        Control? view = FindTopmostById(id);
        return view is not null && TryCloseManaged(view);
    }

    /// <summary>关闭指定标识的全部实例。</summary>
    public int CloseAll(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        _ = GetUiConfigEntry(id);

        int closed = 0;
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            if (HasId(_overlays[i], id))
            {
                CloseOverlayAt(i);
                closed++;
            }
        }

        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            if (HasId(_modals[i].View, id))
            {
                CloseModalAt(i);
                closed++;
            }
        }

        for (int i = _views.Count - 1; i >= 0; i--)
        {
            if (HasId(_views[i], id))
            {
                CloseViewAt(i);
                closed++;
            }
        }

        for (int i = _sceneViews.Count - 1; i >= 0; i--)
        {
            if (HasId(_sceneViews[i], id))
            {
                CloseSceneAt(i);
                closed++;
            }
        }

        return closed;
    }

    /// <summary>关闭指定层的全部实例。</summary>
    public int CloseAll(UiLayer layer)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        return CloseAllLayer(layer);
    }

    /// <summary>保留指定实例并关闭其显示层级之上的全部受管理 UI。</summary>
    public int CloseTo(Control view)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(view);
        VerifyReady();
        PruneInvalidEntries();
        if (!IsInstanceValid(view))
            throw new InvalidOperationException("目标 UI 已经释放，不再受服务管理。");

        return CloseToManaged(view);
    }

    /// <summary>保留指定标识最上层的实例并关闭其显示层级之上的全部受管理 UI。</summary>
    public int CloseTo(UiId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();
        _ = GetUiConfigEntry(id);

        Control? view = FindTopmostById(id);
        if (view is null)
            throw new InvalidOperationException($"指定 UI 当前没有打开：{id.Value}");

        return CloseToManaged(view);
    }

    /// <summary>优先关闭顶部模态，其次返回前一个 View；没有可返回界面时返回 false。</summary>
    public bool TryGoBack()
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();
        PruneInvalidEntries();

        if (_modals.Count > 0)
        {
            CloseModalAt(_modals.Count - 1);
            return true;
        }

        if (_views.Count > 0)
        {
            CloseViewAt(_views.Count - 1);
            return true;
        }

        return false;
    }

    private void PruneInvalidEntries()
    {
        bool restoreFocus = false;
        for (int i = _sceneViews.Count - 1; i >= 0; i--)
        {
            Control view = _sceneViews[i];
            if (IsInstanceValid(view))
                continue;

            _sceneViews.RemoveAt(i);
            RemoveMetadata(view);
        }

        bool restorePreviousView = _views.Count > 0 && !IsInstanceValid(_views[^1]);
        restoreFocus |= restorePreviousView;
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            Control view = _views[i];
            if (IsInstanceValid(view))
                continue;

            _views.RemoveAt(i);
            RemoveMetadata(view);
        }

        if (restorePreviousView && _views.Count > 0)
            _views[^1].Show();

        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            ModalEntry entry = _modals[i];
            bool viewValid = IsInstanceValid(entry.View);
            bool hostValid = IsInstanceValid(entry.Host);
            if (viewValid && hostValid)
                continue;

            restoreFocus |= i == _modals.Count - 1;
            _modals.RemoveAt(i);
            RemoveMetadata(entry.View);
            if (hostValid)
                entry.Host.QueueFree();
            else if (viewValid)
                entry.View.QueueFree();
        }

        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            Control overlay = _overlays[i];
            if (IsInstanceValid(overlay))
                continue;

            _overlays.RemoveAt(i);
            RemoveMetadata(overlay);
        }

        if (restoreFocus && GetViewport().GuiGetFocusOwner() is null)
            RestoreActiveFocus();
    }

    private bool TryCloseManaged(Control view)
    {
        int overlayIndex = _overlays.IndexOf(view);
        if (overlayIndex >= 0)
        {
            CloseOverlayAt(overlayIndex);
            return true;
        }

        int sceneIndex = _sceneViews.IndexOf(view);
        if (sceneIndex >= 0)
        {
            CloseSceneAt(sceneIndex);
            return true;
        }

        int modalIndex = FindModalIndex(view);
        if (modalIndex >= 0)
        {
            CloseModalAt(modalIndex);
            return true;
        }

        int viewIndex = _views.IndexOf(view);
        if (viewIndex >= 0)
        {
            CloseViewAt(viewIndex);
            return true;
        }

        return false;
    }

    private int CloseToManaged(Control view)
    {
        int overlayIndex = _overlays.IndexOf(view);
        if (overlayIndex >= 0)
        {
            int closed = 0;
            while (_overlays.Count - 1 > overlayIndex)
            {
                CloseOverlayAt(_overlays.Count - 1);
                closed++;
            }

            return closed;
        }

        int modalIndex = FindModalIndex(view);
        if (modalIndex >= 0)
        {
            int closed = CloseAllLayer(UiLayer.Overlay);
            while (_modals.Count - 1 > modalIndex)
            {
                CloseModalAt(_modals.Count - 1);
                closed++;
            }

            return closed;
        }

        int viewIndex = _views.IndexOf(view);
        if (viewIndex >= 0)
        {
            int closed =
                CloseAllLayer(UiLayer.Overlay) +
                CloseAllLayer(UiLayer.Modal);
            while (_views.Count - 1 > viewIndex)
            {
                CloseViewAt(_views.Count - 1);
                closed++;
            }

            return closed;
        }

        if (_sceneViews.IndexOf(view) >= 0)
        {
            return
                CloseAllLayer(UiLayer.Overlay) +
                CloseAllLayer(UiLayer.Modal) +
                CloseAllLayer(UiLayer.View);
        }

        throw new InvalidOperationException("目标 UI 不受服务管理。");
    }

    private int CloseAllLayer(UiLayer layer)
    {
        int closed = 0;
        switch (layer)
        {
            case UiLayer.Scene:
                while (_sceneViews.Count > 0)
                {
                    CloseSceneAt(_sceneViews.Count - 1);
                    closed++;
                }
                break;

            case UiLayer.View:
                while (_views.Count > 0)
                {
                    CloseViewAt(_views.Count - 1);
                    closed++;
                }
                break;

            case UiLayer.Modal:
                while (_modals.Count > 0)
                {
                    CloseModalAt(_modals.Count - 1);
                    closed++;
                }
                break;

            case UiLayer.Overlay:
                while (_overlays.Count > 0)
                {
                    CloseOverlayAt(_overlays.Count - 1);
                    closed++;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 UI 层。");
        }

        return closed;
    }

    private void CloseSceneAt(int index, bool allowReuse = true)
    {
        Control view = _sceneViews[index];
        _sceneViews.RemoveAt(index);
        ReleaseOrFreeView(view, allowReuse);
    }

    private void CloseViewAt(int index, bool allowReuse = true)
    {
        bool wasTop = index == _views.Count - 1;
        Control view = _views[index];
        bool restoreFocus = PrepareFocusForClose(view, wasTop);
        _views.RemoveAt(index);
        try
        {
            ReleaseOrFreeView(view, allowReuse);
        }
        finally
        {
            if (wasTop && _views.Count > 0)
                _views[^1].Show();
            if (restoreFocus)
                RestoreActiveFocus();
        }
    }

    private void CloseModalAt(int index, bool allowReuse = true)
    {
        bool wasTop = index == _modals.Count - 1;
        ModalEntry entry = _modals[index];
        bool restoreFocus = PrepareFocusForClose(entry.View, wasTop);
        _modals.RemoveAt(index);
        try
        {
            ReleaseOrFreeView(entry.View, allowReuse);
        }
        finally
        {
            entry.Host.QueueFree();
            if (restoreFocus)
                RestoreActiveFocus();
        }
    }

    private void CloseOverlayAt(int index, bool allowReuse = true)
    {
        Control overlay = _overlays[index];
        _overlays.RemoveAt(index);
        ReleaseOrFreeView(overlay, allowReuse);
    }

    private void ReleaseOrFreeView(Control view, bool allowReuse)
    {
        bool reuseInstance =
            _uiIds.TryGetValue(view, out UiId id) &&
            _uiConfigEntries.TryGetValue(id, out UiRuntimeConfigEntry entry) &&
            entry.ReuseInstance;
        Exception? releaseException = null;
        if (reuseInstance && view is IPoolable poolable)
        {
            try
            {
                poolable.OnRelease();
            }
            catch (Exception exception)
            {
                releaseException = exception;
            }
        }

        bool cacheInstance = reuseInstance && allowReuse && releaseException is null;
        RemoveMetadata(view, preserveFocusState: cacheInstance);
        if (cacheInstance)
        {
            Node? parent = view.GetParent();
            if (parent != null && IsInstanceValid(parent))
                parent.RemoveChild(view);
            view.Hide();
            _cachedUiInstances[id] = view;
            return;
        }

        view.QueueFree();
        if (releaseException is not null)
        {
            throw new InvalidOperationException(
                $"复用 UI 的 OnRelease 执行失败：{id.Value}",
                releaseException);
        }
    }

    private void DiscardManagedView(Control view)
    {
        int overlayIndex = _overlays.IndexOf(view);
        if (overlayIndex >= 0)
        {
            _overlays.RemoveAt(overlayIndex);
            RemoveMetadata(view);
            view.QueueFree();
            return;
        }

        int sceneIndex = _sceneViews.IndexOf(view);
        if (sceneIndex >= 0)
        {
            _sceneViews.RemoveAt(sceneIndex);
            RemoveMetadata(view);
            view.QueueFree();
            return;
        }

        int modalIndex = FindModalIndex(view);
        if (modalIndex >= 0)
        {
            bool wasTop = modalIndex == _modals.Count - 1;
            ModalEntry modal = _modals[modalIndex];
            _modals.RemoveAt(modalIndex);
            RemoveMetadata(view);
            modal.Host.QueueFree();
            if (wasTop)
                RestoreActiveFocus();
            return;
        }

        int viewIndex = _views.IndexOf(view);
        if (viewIndex >= 0)
        {
            bool wasTop = viewIndex == _views.Count - 1;
            _views.RemoveAt(viewIndex);
            RemoveMetadata(view);
            view.QueueFree();
            if (wasTop && _views.Count > 0)
                _views[^1].Show();
            if (wasTop)
                RestoreActiveFocus();
        }
    }

    private int FindModalIndex(Control view)
    {
        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            if (_modals[i].View == view)
                return i;
        }

        return -1;
    }

    private Control? FindTopmostById(UiId id)
    {
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            if (HasId(_overlays[i], id))
                return _overlays[i];
        }

        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            if (HasId(_modals[i].View, id))
                return _modals[i].View;
        }

        for (int i = _views.Count - 1; i >= 0; i--)
        {
            if (HasId(_views[i], id))
                return _views[i];
        }

        for (int i = _sceneViews.Count - 1; i >= 0; i--)
        {
            if (HasId(_sceneViews[i], id))
                return _sceneViews[i];
        }

        return null;
    }

    private bool HasOpenInstance(UiId id) => FindTopmostById(id) is not null;

    private bool HasOpenOrOpeningSingleInstance(UiId id) =>
        HasOpenInstance(id) || _openingCancellations.ContainsKey(id);

    private void AddOpening(UiId id, UiOpenCancellation cancellation)
    {
        if (!_openingCancellations.TryGetValue(id, out List<UiOpenCancellation>? cancellations))
        {
            cancellations = new List<UiOpenCancellation>();
            _openingCancellations.Add(id, cancellations);
        }

        cancellations.Add(cancellation);
    }

    private void RemoveOpening(UiId id, UiOpenCancellation cancellation)
    {
        if (!_openingCancellations.TryGetValue(
                id,
                out List<UiOpenCancellation>? cancellations))
        {
            return;
        }

        cancellations.Remove(cancellation);
        if (cancellations.Count == 0)
            _openingCancellations.Remove(id);
    }

    private int CancelOpenRequestsCore(UiId id) =>
        _openingCancellations.TryGetValue(
            id,
            out List<UiOpenCancellation>? cancellations)
            ? CancelOpenRequestsCore(cancellations)
            : 0;

    private static int CancelOpenRequestsCore(
        List<UiOpenCancellation> cancellations,
        bool lifecycle = false)
    {
        int canceled = 0;
        for (int index = 0; index < cancellations.Count; index++)
        {
            bool requested = lifecycle
                ? cancellations[index].RequestFromLifecycle()
                : cancellations[index].RequestFromService();
            if (requested)
                canceled++;
        }

        return canceled;
    }

    private int CancelAllOpenRequestsCore(bool lifecycle = false)
    {
        int canceled = 0;
        foreach (List<UiOpenCancellation> cancellations in _openingCancellations.Values)
            canceled += CancelOpenRequestsCore(cancellations, lifecycle);
        foreach (List<DirectUiOpenRequest> requests in _directOpeningRequests.Values)
            canceled += CancelDirectOpenRequestsCore(requests, lifecycle);
        return canceled;
    }

    private static int CancelDirectOpenRequestsCore(
        List<DirectUiOpenRequest> requests,
        bool lifecycle = false)
    {
        int canceled = 0;
        for (int index = 0; index < requests.Count; index++)
        {
            bool requested = lifecycle
                ? requests[index].Cancellation.RequestFromLifecycle()
                : requests[index].Cancellation.RequestFromService();
            if (requested)
                canceled++;
        }

        return canceled;
    }

    private void AddDirectOpening(
        UiLayer layer,
        ResourceKey key,
        UiOpenCancellation cancellation)
    {
        if (!_directOpeningRequests.TryGetValue(
                layer,
                out List<DirectUiOpenRequest>? requests))
        {
            requests = new List<DirectUiOpenRequest>();
            _directOpeningRequests.Add(layer, requests);
        }

        requests.Add(new DirectUiOpenRequest(key, cancellation));
    }

    private void RemoveDirectOpening(UiLayer layer, UiOpenCancellation cancellation)
    {
        if (!_directOpeningRequests.TryGetValue(
                layer,
                out List<DirectUiOpenRequest>? requests))
        {
            return;
        }

        for (int index = requests.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(requests[index].Cancellation, cancellation))
            {
                requests.RemoveAt(index);
                break;
            }
        }

        if (requests.Count == 0)
            _directOpeningRequests.Remove(layer);
    }

    private bool HasId(Control view, UiId id) =>
        _uiIds.TryGetValue(view, out UiId openId) && openId == id;

    private void PrepareFocusForOpen(UiLayer layer)
    {
        if (!ShouldManageFocusForOpen(layer))
            return;

        Viewport viewport = GetViewport();
        Control? focusOwner = viewport.GuiGetFocusOwner();
        if (focusOwner is null)
            return;

        Control? managedView = FindManagedFocusView(focusOwner);
        if (managedView is null)
            return;

        _lastFocusedControls[managedView] = focusOwner;
        viewport.GuiReleaseFocus();
    }

    private bool PrepareFocusForClose(Control view, bool restoreWhenNoFocus)
    {
        Viewport viewport = GetViewport();
        Control? focusOwner = viewport.GuiGetFocusOwner();
        if (focusOwner is null)
            return restoreWhenNoFocus;
        if (!ContainsFocus(view, focusOwner))
            return false;

        _lastFocusedControls[view] = focusOwner;
        viewport.GuiReleaseFocus();
        return true;
    }

    private void RestoreOpenedFocus(Control view, UiLayer layer)
    {
        if (ShouldManageFocusForOpen(layer))
            TryRestoreFocus(view);
    }

    private bool ShouldManageFocusForOpen(UiLayer layer) =>
        layer == UiLayer.Modal ||
        layer == UiLayer.View && _modals.Count == 0;

    private void RestoreActiveFocus()
    {
        if (GetViewport().GuiGetFocusOwner() is not null)
            return;

        Control? activeView =
            _modals.Count > 0
                ? _modals[^1].View
                : _views.Count > 0
                    ? _views[^1]
                    : _sceneViews.Count > 0
                        ? _sceneViews[^1]
                        : null;
        if (activeView is not null)
            TryRestoreFocus(activeView);
    }

    private void TryRestoreFocus(Control view)
    {
        if (!_lastFocusedControls.TryGetValue(view, out Control? focusTarget))
            return;

        if (!IsInstanceValid(view) ||
            !IsInstanceValid(focusTarget) ||
            focusTarget.IsQueuedForDeletion() ||
            !focusTarget.IsInsideTree() ||
            !focusTarget.IsVisibleInTree() ||
            focusTarget.GetFocusModeWithOverride() == Control.FocusModeEnum.None ||
            !ContainsFocus(view, focusTarget))
        {
            _lastFocusedControls.Remove(view);
            return;
        }

        focusTarget.GrabFocus();
    }

    private Control? FindManagedFocusView(Control focusOwner)
    {
        for (int index = _modals.Count - 1; index >= 0; index--)
        {
            if (ContainsFocus(_modals[index].View, focusOwner))
                return _modals[index].View;
        }

        for (int index = _views.Count - 1; index >= 0; index--)
        {
            if (ContainsFocus(_views[index], focusOwner))
                return _views[index];
        }

        for (int index = _sceneViews.Count - 1; index >= 0; index--)
        {
            if (ContainsFocus(_sceneViews[index], focusOwner))
                return _sceneViews[index];
        }

        return null;
    }

    private static bool ContainsFocus(Control view, Control focusTarget) =>
        view == focusTarget || view.IsAncestorOf(focusTarget);

    private UiRuntimeConfigEntry GetUiConfigEntry(UiId id)
    {
        if (!id.IsValid)
            throw new ArgumentException("UiId 未初始化。", nameof(id));
        if (!_uiConfigLoaded)
            throw new InvalidOperationException("UiService 尚未加载 UiConfig。");
        if (!_uiConfigEntries.TryGetValue(id, out UiRuntimeConfigEntry entry))
            throw new KeyNotFoundException($"UiConfig 中未找到 UI 标识：{id.Value}");

        return entry;
    }

    private void RemoveMetadata(Control view, bool preserveFocusState = false)
    {
        _uiIds.Remove(view);
        if (!preserveFocusState)
            _lastFocusedControls.Remove(view);
#if DEBUG
        _debugKeys.Remove(view);
#endif
    }

    private Control MountSceneView(Control view, ResourceKey key)
    {
        AddToRoot(view, _root!.SceneRoot, key, UiLayer.Scene);
        _sceneViews.Add(view);
#if DEBUG
        _debugKeys[view] = key;
#endif
        return view;
    }

    private Control MountStackView(Control view, ResourceKey key)
    {
        AddToRoot(view, _root!.ViewRoot, key, UiLayer.View);
        if (_views.Count > 0)
            _views[^1].Hide();
        _views.Add(view);
#if DEBUG
        _debugKeys[view] = key;
#endif
        return view;
    }

    private Control MountModal(Control view, ResourceKey key)
    {
        var host = new Control
        {
            Name = "ModalHost",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(view);

        try
        {
            _root!.ModalRoot.AddChild(host);
        }
        catch (Exception exception)
        {
            host.QueueFree();
            throw new UiOpenException(
                key,
                UiOpenPhase.Committing,
                $"UI 模态无法加入场景树: {key.Value}",
                exception);
        }

        _modals.Add(new ModalEntry(view, host));
#if DEBUG
        _debugKeys[view] = key;
#endif
        return view;
    }

    private Control MountOverlay(Control view, ResourceKey key)
    {
        AddToRoot(view, _root!.OverlayRoot, key, UiLayer.Overlay);
        _overlays.Add(view);
#if DEBUG
        _debugKeys[view] = key;
#endif
        return view;
    }

    private static void AddToRoot(Control view, Control root, ResourceKey key, UiLayer layer)
    {
        try
        {
            root.AddChild(view);
        }
        catch (Exception exception)
        {
            view.QueueFree();
            throw new UiOpenException(
                key,
                UiOpenPhase.Committing,
                $"UI 无法加入 {layer} 层: {key.Value}",
                exception);
        }
    }

    private static Control InstantiateView(ResourceKey key)
    {
        try
        {
            PackedScene scene = ResourceHub.Load<PackedScene>(key);
            return InstantiateView(scene, key);
        }
        catch (Exception exception) when (exception is not UiOpenException)
        {
            throw new UiOpenException(
                key,
                UiOpenPhase.Loading,
                $"UI 场景无法打开: {key.Value}",
                exception);
        }
    }

    private static Control InstantiateView(PackedScene scene, ResourceKey key)
    {
        try
        {
            if (!scene.CanInstantiate())
                throw new InvalidOperationException("PackedScene 不包含可实例化的节点。");

            Node node = scene.Instantiate();
            if (node is Control control)
                return control;

            node.QueueFree();
            throw new InvalidOperationException("UI 场景根节点必须继承 Control。");
        }
        catch (Exception exception) when (exception is not UiOpenException)
        {
            throw new UiOpenException(
                key,
                UiOpenPhase.Preparing,
                $"UI 场景无法打开：{key.Value}",
                exception);
        }
    }

    private static TView InstantiateView<TView>(ResourceKey key)
        where TView : Control
    {
        Control view = InstantiateView(key);
        return CastView<TView>(view, key);
    }

    private static TView InstantiateView<TView>(PackedScene scene, ResourceKey key)
        where TView : Control
    {
        Control view = InstantiateView(scene, key);
        return CastView<TView>(view, key);
    }

    private static TView CastView<TView>(Control view, ResourceKey key)
        where TView : Control
    {
        if (view is TView typedView)
            return typedView;

        UiOpenException exception = CreateViewTypeMismatchException<TView>(view, key);
        view.QueueFree();
        throw exception;
    }

    private static UiOpenException CreateViewTypeMismatchException<TView>(
        Control view,
        ResourceKey key)
        where TView : Control =>
        new(
            key,
            UiOpenPhase.Preparing,
            $"UI 场景根节点类型不匹配，期望 {typeof(TView).FullName}，实际 {view.GetType().FullName}: {key.Value}");

    private int ClearCachedInstancesCore(UiLayer? layer = null)
    {
        if (_cachedUiInstances.Count == 0)
            return 0;

        var ids = new List<UiId>(_cachedUiInstances.Count);
        foreach (KeyValuePair<UiId, Control> pair in _cachedUiInstances)
        {
            if (layer is not null &&
                (!_uiConfigEntries.TryGetValue(pair.Key, out UiRuntimeConfigEntry entry) ||
                 entry.Layer != layer.Value))
            {
                continue;
            }

            ids.Add(pair.Key);
            _lastFocusedControls.Remove(pair.Value);
            if (IsInstanceValid(pair.Value) && !pair.Value.IsQueuedForDeletion())
                pair.Value.QueueFree();
        }

        for (int i = 0; i < ids.Count; i++)
            _cachedUiInstances.Remove(ids[i]);
        return ids.Count;
    }

    private void OnMainSceneChanged(FrameworkMainSceneChangedEvent _)
    {
        _sceneVersion++;
        CancelOpenRequestsCore(UiLayer.Scene, lifecycle: true);
        for (int i = _sceneViews.Count - 1; i >= 0; i--)
            CloseSceneAt(i, allowReuse: false);
        ClearCachedInstancesCore(UiLayer.Scene);
    }

#if DEBUG
    internal UiDebugSnapshot GetDebugSnapshot()
    {
        MainThreadGuard.VerifyAccess();
        PruneInvalidEntries();
        PruneInvalidCachedInstances();
        Control? focusOwner = GetViewport().GuiGetFocusOwner();
        var entries =
            new UiDebugEntry[
                _sceneViews.Count +
                _views.Count +
                _modals.Count +
                _overlays.Count +
                _cachedUiInstances.Count];
        int entryIndex = 0;
        for (int index = 0; index < _sceneViews.Count; index++)
            entries[entryIndex++] =
                CreateDebugEntry(_sceneViews[index], UiLayer.Scene, index, focusOwner);
        for (int index = 0; index < _views.Count; index++)
            entries[entryIndex++] =
                CreateDebugEntry(_views[index], UiLayer.View, index, focusOwner);
        for (int index = 0; index < _modals.Count; index++)
            entries[entryIndex++] =
                CreateDebugEntry(_modals[index].View, UiLayer.Modal, index, focusOwner);
        for (int index = 0; index < _overlays.Count; index++)
            entries[entryIndex++] =
                CreateDebugEntry(_overlays[index], UiLayer.Overlay, index, focusOwner);
        foreach (KeyValuePair<UiId, Control> pair in _cachedUiInstances)
        {
            UiRuntimeConfigEntry configEntry = _uiConfigEntries[pair.Key];
            entries[entryIndex++] = CreateDebugEntry(
                pair.Value,
                configEntry.Layer,
                -1,
                focusOwner,
                configEntry.Key,
                pair.Key,
                isCached: true);
        }

        var openings = new List<UiDebugOpeningEntry>(_openingCancellations.Count);
        foreach (KeyValuePair<UiId, List<UiOpenCancellation>> pair in _openingCancellations)
        {
            UiRuntimeConfigEntry configEntry = _uiConfigEntries[pair.Key];
            openings.Add(new UiDebugOpeningEntry(
                pair.Key,
                configEntry.Layer,
                configEntry.Key,
                pair.Value.Count,
                GetDebugOpeningPhase(pair.Value)));
        }

        var directOpeningCounts =
            new Dictionary<
                (UiLayer Layer, ResourceKey Key),
                (int Count, UiDebugOpenPhase Phase)>();
        foreach (KeyValuePair<UiLayer, List<DirectUiOpenRequest>> pair in _directOpeningRequests)
        {
            for (int index = 0; index < pair.Value.Count; index++)
            {
                var openingKey = (pair.Key, pair.Value[index].Key);
                directOpeningCounts.TryGetValue(openingKey, out var state);
                UiDebugOpenPhase phase = pair.Value[index].Cancellation.DebugPhase;
                directOpeningCounts[openingKey] = (
                    state.Count + 1,
                    phase > state.Phase ? phase : state.Phase);
            }
        }
        foreach (KeyValuePair<
                     (UiLayer Layer, ResourceKey Key),
                     (int Count, UiDebugOpenPhase Phase)> pair in directOpeningCounts)
        {
            openings.Add(new UiDebugOpeningEntry(
                default,
                pair.Key.Layer,
                pair.Key.Key,
                pair.Value.Count,
                pair.Value.Phase));
        }

        return new UiDebugSnapshot(
            entries,
            openings.ToArray(),
            _debugLastOpenId,
            _debugLastOpenLayer,
            _debugLastOpenKey,
            _debugLastOpenPhase,
            _debugLastOpenResult,
            _debugLastOpenDetail,
            _debugLastOpenDurationMilliseconds);
    }

    private static UiDebugOpenPhase GetDebugOpeningPhase(
        List<UiOpenCancellation> cancellations)
    {
        UiDebugOpenPhase phase = UiDebugOpenPhase.Loading;
        for (int index = 0; index < cancellations.Count; index++)
        {
            if (cancellations[index].DebugPhase > phase)
                phase = cancellations[index].DebugPhase;
        }

        return phase;
    }

    private void RecordDebugOpenSuccess(
        UiId id,
        ResourceKey key,
        UiLayer layer,
        UiOpenCancellation cancellation)
    {
        RecordDebugOpenResult(
            id,
            key,
            layer,
            cancellation,
            UiDebugOpenResult.Succeeded,
            null);
    }

    private void RecordDebugOpenCancellation(
        UiId id,
        ResourceKey key,
        UiLayer layer,
        int sceneVersion,
        UiOpenCancellation cancellation,
        OperationCanceledException exception)
    {
        UiDebugOpenResult result = cancellation.DebugCancellationOrigin switch
        {
            UiDebugOpenCancellationOrigin.Caller => UiDebugOpenResult.CallerCanceled,
            UiDebugOpenCancellationOrigin.Service => UiDebugOpenResult.ServiceCanceled,
            UiDebugOpenCancellationOrigin.Lifecycle => UiDebugOpenResult.LifecycleCanceled,
            _ when layer == UiLayer.Scene && sceneVersion != _sceneVersion =>
                UiDebugOpenResult.LifecycleCanceled,
            _ => UiDebugOpenResult.LifecycleCanceled,
        };
        RecordDebugOpenResult(id, key, layer, cancellation, result, exception.Message);
    }

    private void RecordDebugOpenFailure(
        UiId id,
        ResourceKey key,
        UiLayer layer,
        UiOpenCancellation cancellation,
        Exception exception)
    {
        RecordDebugOpenResult(
            id,
            key,
            layer,
            cancellation,
            UiDebugOpenResult.Failed,
            exception.GetBaseException().Message);
    }

    private void RecordDebugOpenResult(
        UiId id,
        ResourceKey key,
        UiLayer layer,
        UiOpenCancellation cancellation,
        UiDebugOpenResult result,
        string? detail)
    {
        if (detail?.Length > 256)
            detail = detail[..256];
        _debugLastOpenId = id;
        _debugLastOpenLayer = layer;
        _debugLastOpenKey = key;
        _debugLastOpenPhase = cancellation.DebugPhase;
        _debugLastOpenResult = result;
        _debugLastOpenDetail = detail;
        _debugLastOpenDurationMilliseconds =
            Time.GetTicksMsec() - cancellation.DebugStartedTicks;
    }

    private UiDebugEntry CreateDebugEntry(
        Control view,
        UiLayer layer,
        int index,
        Control? focusOwner,
        ResourceKey key = default,
        UiId id = default,
        bool isCached = false)
    {
        bool isValid = IsInstanceValid(view);
        if (!key.IsValid)
            _debugKeys.TryGetValue(view, out key);
        if (!id.IsValid)
            _uiIds.TryGetValue(view, out id);
        bool hasFocus =
            isValid &&
            !isCached &&
            IsInstanceValid(focusOwner) &&
            ContainsFocus(view, focusOwner!);
        return new UiDebugEntry(
            layer,
            index,
            key,
            id,
            isValid ? view.Name.ToString() : "<已释放>",
            isValid && !isCached && view.Visible,
            isValid,
            isCached,
            hasFocus,
            hasFocus ? focusOwner!.Name.ToString() : string.Empty);
    }

    private void PruneInvalidCachedInstances()
    {
        if (_cachedUiInstances.Count == 0)
            return;

        var ids = new List<UiId>();
        foreach (KeyValuePair<UiId, Control> pair in _cachedUiInstances)
        {
            if (!IsInstanceValid(pair.Value) || pair.Value.IsQueuedForDeletion())
            {
                ids.Add(pair.Key);
                _lastFocusedControls.Remove(pair.Value);
            }
        }

        for (int index = 0; index < ids.Count; index++)
            _cachedUiInstances.Remove(ids[index]);
    }
#endif

    private void VerifyReady()
    {
        MainThreadGuard.VerifyAccess();
        if (!IsInsideTree() || !IsInstanceValid(_root) || !_root.IsInitialized)
            throw new InvalidOperationException("UiService 必须完成 UiRoot 初始化后才能使用。");
    }

    private readonly struct ModalEntry
    {
        public Control View { get; }
        public Control Host { get; }

        public ModalEntry(Control view, Control host)
        {
            View = view;
            Host = host;
        }
    }

    private readonly struct UiRuntimeConfigEntry
    {
        public ResourceKey Key { get; }
        public UiLayer Layer { get; }
        public UiInstanceMode InstanceMode { get; }
        public bool ReuseInstance { get; }

        public UiRuntimeConfigEntry(
            ResourceKey key,
            UiLayer layer,
            UiInstanceMode instanceMode,
            bool reuseInstance)
        {
            Key = key;
            Layer = layer;
            InstanceMode = instanceMode;
            ReuseInstance = reuseInstance;
        }
    }

    private readonly struct DirectUiOpenRequest
    {
        public ResourceKey Key { get; }
        public UiOpenCancellation Cancellation { get; }

        public DirectUiOpenRequest(ResourceKey key, UiOpenCancellation cancellation)
        {
            Key = key;
            Cancellation = cancellation;
        }
    }
}
