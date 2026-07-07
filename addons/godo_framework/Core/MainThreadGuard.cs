using System;
using System.Threading;

namespace GoDo;

/// <summary>
/// 记录由 GoDoRuntime 确认的 Godot 主线程，并验证框架 API 的线程访问。
/// </summary>
internal static class MainThreadGuard
{
    private static int _mainThreadId;

    /// <summary>
    /// 是否已经记录 Godot 主线程上下文。
    /// </summary>
    public static bool IsInitialized => Volatile.Read(ref _mainThreadId) != 0;

    /// <summary>
    /// 当前线程是否为已记录的 Godot 主线程；尚未记录时返回 false。
    /// </summary>
    public static bool IsMainThread =>
        IsInitialized && Environment.CurrentManagedThreadId == Volatile.Read(ref _mainThreadId);

    /// <summary>
    /// 将当前线程记录为 GoDoRuntime 使用的 Godot 主线程。
    /// </summary>
    public static void Initialize()
    {
        int currentThreadId = Environment.CurrentManagedThreadId;
        int existingThreadId = Interlocked.CompareExchange(ref _mainThreadId, currentThreadId, 0);

        if (existingThreadId != 0 && existingThreadId != currentThreadId)
            throw new InvalidOperationException("GoDoRuntime 不能从多个线程初始化，请确保只有一个线程调用 Initialize 方法。");
    }

    /// <summary>
    /// 清除已记录的 Godot 主线程 ID。
    /// </summary>
    public static void Reset()
    {
        Volatile.Write(ref _mainThreadId, 0);
    }

    /// <summary>
    /// 验证当前线程是否为 GoDoRuntime 所在的 Godot 主线程。
    /// </summary>
    public static void VerifyAccess()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("框架尚未记录 Godot 主线程，请确认 GoDoRuntime 已进入场景树。");

        if (!IsMainThread)
            throw new InvalidOperationException($"当前线程 ID: {Environment.CurrentManagedThreadId} 不是 Godot 主线程 ID: {_mainThreadId}");
    }
}
