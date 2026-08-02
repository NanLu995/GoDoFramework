using System;
using System.Threading.Tasks;
using Godot;
using GodotArray = Godot.Collections.Array;

#nullable enable

namespace GoDo;

/// <summary>一个由 ResourceHub 驱动的异步资源加载操作。</summary>
public sealed class ResourceLoadOperation<T> where T : Resource
{
    private readonly TaskCompletionSource<T> _completionSource = new();
    private readonly GodotArray _progressBuffer = new();
    private T? _result;
    private Exception? _completionException;
    private bool _completionPublished;

    /// <summary>目标资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>当前加载状态。</summary>
    public ResourceLoadStatus Status { get; private set; } = ResourceLoadStatus.Loading;

    /// <summary>当前进度，范围为 0 到 1。</summary>
    public float Progress { get; private set; }

    /// <summary>
    /// 在主线程完成或失败的任务。任务发布前，本操作已从 ResourceHub 活动表移除。
    /// </summary>
    public Task<T> Completion => _completionSource.Task;

    /// <summary>进度发生变化时在主线程触发。</summary>
    public event Action<float>? ProgressChanged;

    internal bool IsFinished => Status != ResourceLoadStatus.Loading;

    internal ResourceLoadOperation(ResourceKey key)
    {
        Key = key;
    }

    internal void Poll()
    {
        if (IsFinished)
            return;

        try
        {
            ResourceLoader.ThreadLoadStatus godotStatus =
                ResourceLoader.LoadThreadedGetStatus(Key.Value, _progressBuffer);

            switch (godotStatus)
            {
                case ResourceLoader.ThreadLoadStatus.InProgress:
                    UpdateProgress(ReadProgress());
                    break;

                case ResourceLoader.ThreadLoadStatus.Loaded:
                    Complete(ResourceLoader.LoadThreadedGet(Key.Value));
                    break;

                case ResourceLoader.ThreadLoadStatus.Failed:
                case ResourceLoader.ThreadLoadStatus.InvalidResource:
                    Fail(new ResourceLoadException(
                        Key,
                        typeof(T),
                        $"Godot 线程化加载失败，状态: {godotStatus}，资源: {Key.Value}"));
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception is ResourceLoadException
                ? exception
                : new ResourceLoadException(
                    Key,
                    typeof(T),
                    $"轮询资源加载状态失败: {Key.Value}",
                    innerException: exception));
        }
    }

    internal void Fail(Exception exception)
    {
        if (IsFinished)
            return;

        Status = ResourceLoadStatus.Failed;
        _completionException = exception;
    }

    internal void PublishCompletion()
    {
        if (!IsFinished || _completionPublished)
            return;

        _completionPublished = true;
        if (Status == ResourceLoadStatus.Completed)
        {
            _completionSource.TrySetResult(_result!);
        }
        else
        {
            _completionSource.TrySetException(
                _completionException ??
                new InvalidOperationException($"资源操作失败但没有异常信息: {Key.Value}"));
        }

        _result = null;
        _completionException = null;
        ProgressChanged = null;
    }

    private float ReadProgress()
    {
        if (_progressBuffer.Count == 0)
            return Progress;

        return Math.Clamp((float)_progressBuffer[0], 0f, 1f);
    }

    private void UpdateProgress(float value)
    {
        if (Math.Abs(value - Progress) < 0.0001f)
            return;

        Progress = value;
        NotifyProgress(value);
    }

    private void Complete(Resource resource)
    {
        if (resource is not T typedResource)
        {
            Fail(new ResourceLoadException(
                Key,
                typeof(T),
                $"资源类型不匹配。请求 {typeof(T).Name}，实际 {resource?.GetType().Name ?? "null"}，资源: {Key.Value}"));
            return;
        }

        _result = typedResource;
        Status = ResourceLoadStatus.Completed;
        UpdateProgress(1f);
    }

    private void NotifyProgress(float value)
    {
        var handlers = ProgressChanged;
        if (handlers == null)
            return;

        Delegate[] listeners = handlers.GetInvocationList();
        for (int i = 0; i < listeners.Length; i++)
        {
            try
            {
                ((Action<float>)listeners[i]).Invoke(value);
            }
            catch (Exception exception)
            {
                ErrorHub.Report(
                    exception,
                    "ResourceHub",
                    context: $"ProgressChanged<{typeof(T).Name}> key={Key.Value}");
            }
        }
    }
}
