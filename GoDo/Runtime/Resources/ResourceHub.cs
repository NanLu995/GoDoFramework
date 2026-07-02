using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// GoDo 运行时统一资源入口。包装 Godot ResourceLoader，提供类型检查、
/// 线程化加载、进度和相同资源请求合并，不建立第二套资源缓存。
/// </summary>
public static class ResourceHub
{
    private static readonly Dictionary<ResourceKey, IResourceOperation> _operations = new();
    private static readonly List<IResourceOperation> _updateBuffer = new(8);
    private static bool _initialized;

    /// <summary>当前正在进行的唯一加载操作数量。</summary>
    public static int ActiveOperationCount
    {
        get
        {
            VerifyReady();
            return _operations.Count;
        }
    }

    internal static void Initialize()
    {
        RuntimeThreadGuard.VerifyAccess();
        if (_initialized)
            return;

        _initialized = true;
    }

    /// <summary>同步加载并验证资源类型。</summary>
    public static T Load<T>(ResourceKey key) where T : Resource
    {
        VerifyReady();
        ValidateKey(key);
        EnsureExists<T>(key);

        if (_operations.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"资源正在异步加载，不能同时执行同步加载: {key.Value}");
        }

        Resource? resource = ResourceLoader.Load(key.Value);
        if (resource is T typedResource)
            return typedResource;

        throw new ResourceLoadException(
            key,
            typeof(T),
            $"资源类型不匹配或加载失败。请求 {typeof(T).Name}，实际 {resource?.GetType().Name ?? "null"}，资源: {key.Value}");
    }

    /// <summary>
    /// 启动线程化加载。同一 ResourceKey 与资源类型的并发请求返回同一个操作实例。
    /// </summary>
    public static ResourceLoadOperation<T> LoadAsync<T>(ResourceKey key) where T : Resource
    {
        VerifyReady();
        ValidateKey(key);
        EnsureExists<T>(key);

        if (_operations.TryGetValue(key, out IResourceOperation? existingOperation))
        {
            if (existingOperation.ResourceType != typeof(T))
            {
                throw new ResourceLoadException(
                    key,
                    typeof(T),
                    $"同一资源正在按 {existingOperation.ResourceType.Name} 加载，不能同时请求 {typeof(T).Name}: {key.Value}");
            }

            return (ResourceLoadOperation<T>)existingOperation.PublicOperation;
        }

        Error error = ResourceLoader.LoadThreadedRequest(
            key.Value,
            typeHint: string.Empty,
            useSubThreads: false,
            cacheMode: ResourceLoader.CacheMode.Reuse);

        if (error != Error.Ok)
        {
            throw new ResourceLoadException(
                key,
                typeof(T),
                $"无法启动 Godot 线程化加载，请求返回 {error}，资源: {key.Value}",
                error);
        }

        var operation = new ResourceOperation<T>(new ResourceLoadOperation<T>(key));
        _operations.Add(key, operation);
        return operation.Operation;
    }

    internal static void Update()
    {
        if (!_initialized)
            return;

        RuntimeThreadGuard.VerifyAccess();
        _updateBuffer.Clear();

        foreach (IResourceOperation operation in _operations.Values)
            _updateBuffer.Add(operation);

        for (int i = 0; i < _updateBuffer.Count; i++)
        {
            IResourceOperation operation = _updateBuffer[i];
            operation.Poll();

            if (operation.IsFinished)
                _operations.Remove(operation.Key);
        }

        _updateBuffer.Clear();
    }

    internal static void Shutdown()
    {
        if (!_initialized)
            return;

        RuntimeThreadGuard.VerifyAccess();

        _updateBuffer.Clear();
        foreach (IResourceOperation operation in _operations.Values)
            _updateBuffer.Add(operation);
        _operations.Clear();
        _initialized = false;

        var exception = new OperationCanceledException("GoDoRuntime 关闭，资源加载操作已停止等待。底层 Godot 加载可能仍会完成。");
        for (int i = 0; i < _updateBuffer.Count; i++)
            _updateBuffer[i].Fail(exception);

        _updateBuffer.Clear();
    }

    private static void VerifyReady()
    {
        RuntimeThreadGuard.VerifyAccess();
        if (!_initialized)
            throw new InvalidOperationException("ResourceHub 尚未初始化。");
    }

    private static void ValidateKey(ResourceKey key)
    {
        if (!key.IsValid)
            throw new ArgumentException("ResourceKey 未初始化。", nameof(key));
    }

    private static void EnsureExists<T>(ResourceKey key) where T : Resource
    {
        if (!ResourceLoader.Exists(key.Value))
        {
            throw new ResourceLoadException(
                key,
                typeof(T),
                $"资源不存在或不是 Godot 可识别资源: {key.Value}");
        }
    }

    private interface IResourceOperation
    {
        ResourceKey Key { get; }
        Type ResourceType { get; }
        object PublicOperation { get; }
        bool IsFinished { get; }
        void Poll();
        void Fail(Exception exception);
    }

    private sealed class ResourceOperation<T> : IResourceOperation where T : Resource
    {
        public ResourceLoadOperation<T> Operation { get; }
        public ResourceKey Key => Operation.Key;
        public Type ResourceType => typeof(T);
        public object PublicOperation => Operation;
        public bool IsFinished => Operation.IsFinished;

        public ResourceOperation(ResourceLoadOperation<T> operation)
        {
            Operation = operation;
        }

        public void Poll() => Operation.Poll();

        public void Fail(Exception exception) => Operation.Fail(exception);
    }
}
