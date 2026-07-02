using System;
using System.Threading;

namespace GoDo;

/// <summary>记录并验证 GoDoRuntime 所在的 Godot 主线程。</summary>
internal static class RuntimeThreadGuard
{
    private static int _mainThreadId;

    public static bool IsInitialized => Volatile.Read(ref _mainThreadId) != 0;

    public static bool IsMainThread =>
        IsInitialized && Environment.CurrentManagedThreadId == Volatile.Read(ref _mainThreadId);

    public static void Initialize()
    {
        int currentThreadId = Environment.CurrentManagedThreadId;
        int existingThreadId = Interlocked.CompareExchange(ref _mainThreadId, currentThreadId, 0);

        if (existingThreadId != 0 && existingThreadId != currentThreadId)
            throw new InvalidOperationException("GoDoRuntime 不能从多个线程初始化。");
    }

    public static void Reset()
    {
        Volatile.Write(ref _mainThreadId, 0);
    }

    public static void VerifyAccess()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("GoDoRuntime 尚未初始化，无法验证 Godot 主线程。");

        if (!IsMainThread)
            throw new InvalidOperationException("此操作只能在 GoDoRuntime 所在的 Godot 主线程执行。");
    }
}
