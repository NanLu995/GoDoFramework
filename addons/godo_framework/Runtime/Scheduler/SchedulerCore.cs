using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>不依赖真实时钟的 Scheduler 队列与时间推进核心。</summary>
internal sealed class SchedulerCore
{
    private const int ClockCount = 3;
    private const int PhaseCount = 2;
    private const int DefaultMaxCallbacksPerAdvance = 4096;

    private readonly SchedulerQueue[,] _queues = new SchedulerQueue[ClockCount, PhaseCount];
    private readonly double[,] _currentTimes = new double[ClockCount, PhaseCount];
    private readonly Dictionary<ulong, ScheduledEntry> _entries = new();
    private readonly ConcurrentQueue<CancellationRequest> _pendingCancellations = new();
    private readonly SchedulerOwnerRegistry _ownerRegistry;
    private readonly Action<SchedulerCallbackError>? _callbackErrorHandler;
    private readonly int _maxCallbacksPerAdvance;
    private ulong _nextHandleValue = 1;
    private long _nextSequence = 1;
    private bool _isShutdown;
#if DEBUG
    private int _lastProcessDispatchCount;
    private int _lastPhysicsDispatchCount;
    private long _canceledCount;
    private long _ownerCanceledCount;
    private long _callbackFailedCount;
#endif

    /// <summary>当前活动与独立暂停任务总数。</summary>
    internal int ActiveCount => _entries.Count;

    /// <summary>最近一次 Advance 是否因派发上限留下已到期任务。</summary>
    internal bool LastAdvanceHitDispatchLimit { get; private set; }

    /// <summary>等待下一次主线程更新处理的 CancellationToken 请求数。</summary>
    internal int PendingCancellationCount => _pendingCancellations.Count;

    /// <summary>当前绑定了至少一个任务的场景 Owner 数量，仅用于回归与诊断。</summary>
    internal int OwnerCount => _ownerRegistry.OwnerCount;

    /// <summary>当前绑定到场景 Owner 的任务数量，仅用于回归与诊断。</summary>
    internal int OwnedHandleCount => _ownerRegistry.HandleCount;

    public SchedulerCore(
        Action<SchedulerCallbackError>? callbackErrorHandler = null,
        int maxCallbacksPerAdvance = DefaultMaxCallbacksPerAdvance)
    {
        if (maxCallbacksPerAdvance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCallbacksPerAdvance),
                maxCallbacksPerAdvance,
                "单轮最大回调数必须大于 0。");
        }

        _callbackErrorHandler = callbackErrorHandler;
        _maxCallbacksPerAdvance = maxCallbacksPerAdvance;
        _ownerRegistry = new SchedulerOwnerRegistry(CancelOwned);
        for (int clock = 0; clock < ClockCount; clock++)
        {
            for (int phase = 0; phase < PhaseCount; phase++)
                _queues[clock, phase] = new SchedulerQueue();
        }
    }

    /// <summary>按当前人工时钟创建一次性任务。</summary>
    internal ScheduleHandle Schedule(
        double delaySeconds,
        Action callback,
        ScheduleOptions options = default)
    {
        ThrowIfShutdown();
        VerifySeconds(delaySeconds, nameof(delaySeconds), allowZero: true);
        return CreateEntry(delaySeconds, intervalSeconds: 0d, callback, options);
    }

    /// <summary>创建首次执行也等待完整间隔的重复任务。</summary>
    internal ScheduleHandle ScheduleRepeating(
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default)
    {
        ThrowIfShutdown();
        return ScheduleRepeating(intervalSeconds, intervalSeconds, callback, options);
    }

    /// <summary>按独立初始延迟创建重复任务。</summary>
    internal ScheduleHandle ScheduleRepeating(
        double initialDelaySeconds,
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default)
    {
        ThrowIfShutdown();
        VerifySeconds(initialDelaySeconds, nameof(initialDelaySeconds), allowZero: true);
        VerifySeconds(intervalSeconds, nameof(intervalSeconds), allowZero: false);
        return CreateEntry(initialDelaySeconds, intervalSeconds, callback, options);
    }

    /// <summary>创建由人工时钟完成、可从任意线程请求取消的异步等待。</summary>
    internal Task DelayAsync(
        double delaySeconds,
        ScheduleOptions options = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfShutdown();
        VerifySeconds(delaySeconds, nameof(delaySeconds), allowZero: true);
        VerifyOptions(options);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var completion = new TaskCompletionSource();
        ScheduleHandle handle = CreateEntry(
            delaySeconds,
            intervalSeconds: 0d,
            callback: null,
            options,
            completion);

        if (cancellationToken.CanBeCanceled)
        {
            var state = new CancellationRegistrationState(this, handle, cancellationToken);
            CancellationTokenRegistration registration = cancellationToken.Register(
                static value =>
                {
                    var registrationState = (CancellationRegistrationState)value!;
                    registrationState.Core.EnqueueCancellation(
                        registrationState.Handle,
                        registrationState.Token);
                },
                state);

            if (_entries.TryGetValue(handle.Value, out ScheduledEntry? entry))
                entry.SetCancellationRegistration(registration);
            else
                registration.Dispose();
        }

        return completion.Task;
    }

    /// <summary>取消活动或独立暂停任务。</summary>
    internal bool Cancel(ScheduleHandle handle)
    {
        if (!handle.IsValid || !_entries.TryGetValue(handle.Value, out ScheduledEntry? entry))
            return false;

        return CancelEntry(entry, ownerCancellation: false);
    }

    private bool CancelOwned(ScheduleHandle handle)
    {
        if (!handle.IsValid || !_entries.TryGetValue(handle.Value, out ScheduledEntry? entry))
            return false;

        return CancelEntry(entry, ownerCancellation: true);
    }

    private bool CancelEntry(ScheduledEntry entry, bool ownerCancellation)
    {
#if DEBUG
        _canceledCount++;
        if (ownerCancellation)
            _ownerCanceledCount++;
#endif

        if (entry.State == ScheduledState.Scheduled)
        {
            SchedulerQueue queue = GetQueue(entry.Clock, entry.Phase);
            queue.InvalidateActive();
            RemoveEntry(entry);
            CompactIfNeeded(entry.Clock, entry.Phase);
            CancelDelayCompletion(entry, cancellationToken: default, preserveToken: false);
            return true;
        }

        RemoveEntry(entry);
        CancelDelayCompletion(entry, cancellationToken: default, preserveToken: false);
        return true;
    }

    /// <summary>独立暂停任务并保存其自身时钟中的剩余时间。</summary>
    internal bool Pause(ScheduleHandle handle)
    {
        if (!handle.IsValid || !_entries.TryGetValue(handle.Value, out ScheduledEntry? entry))
            return false;

        if (entry.State == ScheduledState.Paused)
            return false;

        if (entry.State == ScheduledState.Executing)
        {
            if (!entry.IsRepeating || entry.PauseAfterDispatch)
                return false;

            entry.PauseAfterDispatch = true;
            return true;
        }

        double currentTime = GetCurrentTime(entry.Clock, entry.Phase);
        entry.RemainingSeconds = Math.Max(0d, entry.DueTime - currentTime);
        entry.State = ScheduledState.Paused;
        entry.Revision++;

        SchedulerQueue queue = GetQueue(entry.Clock, entry.Phase);
        queue.InvalidateActive();
        CompactIfNeeded(entry.Clock, entry.Phase);
        return true;
    }

    /// <summary>从当前人工时钟恢复独立暂停任务。</summary>
    internal bool Resume(ScheduleHandle handle)
    {
        if (!handle.IsValid ||
            !_entries.TryGetValue(handle.Value, out ScheduledEntry? entry) ||
            entry.State != ScheduledState.Paused)
        {
            return false;
        }

        double dueTime = GetCurrentTime(entry.Clock, entry.Phase) + entry.RemainingSeconds;
        VerifyDueTime(dueTime);
        entry.DueTime = dueTime;
        entry.RemainingSeconds = 0d;
        entry.State = ScheduledState.Scheduled;
        entry.Revision++;
        GetQueue(entry.Clock, entry.Phase).Enqueue(entry);
        return true;
    }

    /// <summary>任务仍处于活动或独立暂停状态时返回 true。</summary>
    internal bool IsScheduled(ScheduleHandle handle) =>
        handle.IsValid && _entries.ContainsKey(handle.Value);

    /// <summary>尝试取得任务自身时钟中的剩余秒数。</summary>
    internal bool TryGetRemainingSeconds(ScheduleHandle handle, out double remainingSeconds)
    {
        if (!handle.IsValid || !_entries.TryGetValue(handle.Value, out ScheduledEntry? entry))
        {
            remainingSeconds = 0d;
            return false;
        }

        remainingSeconds = entry.State switch
        {
            ScheduledState.Scheduled => Math.Max(
                0d,
                entry.DueTime - GetCurrentTime(entry.Clock, entry.Phase)),
            ScheduledState.Paused => entry.RemainingSeconds,
            _ => 0d,
        };
        return true;
    }

    /// <summary>推进指定阶段的三个时钟并派发本轮开始前已经存在的到期任务。</summary>
    internal int Advance(
        SchedulePhase phase,
        double scaledDeltaSeconds,
        double unscaledDeltaSeconds,
        bool isTreePaused)
    {
        ThrowIfShutdown();
        VerifyPhase(phase);
        VerifySeconds(scaledDeltaSeconds, nameof(scaledDeltaSeconds), allowZero: true);
        VerifySeconds(unscaledDeltaSeconds, nameof(unscaledDeltaSeconds), allowZero: true);

        DrainPendingCancellations();
        if (!isTreePaused)
        {
            AdvanceClock(ScheduleClock.GameTime, phase, scaledDeltaSeconds);
            AdvanceClock(ScheduleClock.UnscaledGameTime, phase, unscaledDeltaSeconds);
        }
        AdvanceClock(ScheduleClock.RealTime, phase, unscaledDeltaSeconds);

        long sequenceLimit = _nextSequence - 1;
        int dispatched = 0;
        for (int clock = 0; clock < ClockCount && dispatched < _maxCallbacksPerAdvance; clock++)
        {
            dispatched += DispatchQueue(
                (ScheduleClock)clock,
                phase,
                sequenceLimit,
                _maxCallbacksPerAdvance - dispatched);
        }

        LastAdvanceHitDispatchLimit =
            dispatched >= _maxCallbacksPerAdvance && HasDueTask(phase, sequenceLimit);
#if DEBUG
        if (phase == SchedulePhase.Process)
            _lastProcessDispatchCount = dispatched;
        else
            _lastPhysicsDispatchCount = dispatched;
#endif
        return dispatched;
    }

#if DEBUG
    /// <summary>按需汇总当前 Scheduler 状态，不在每帧热路径维护分组计数。</summary>
    internal SchedulerDebugSnapshot GetDebugSnapshot()
    {
        int pausedCount = 0;
        int repeatingCount = 0;
        int gameProcessCount = 0;
        int unscaledProcessCount = 0;
        int realProcessCount = 0;
        int gamePhysicsCount = 0;
        int unscaledPhysicsCount = 0;
        int realPhysicsCount = 0;
        double nextRemainingSeconds = double.PositiveInfinity;

        foreach (ScheduledEntry entry in _entries.Values)
        {
            if (entry.State == ScheduledState.Paused)
                pausedCount++;
            if (entry.IsRepeating)
                repeatingCount++;

            switch (entry.Clock, entry.Phase)
            {
                case (ScheduleClock.GameTime, SchedulePhase.Process):
                    gameProcessCount++;
                    break;
                case (ScheduleClock.UnscaledGameTime, SchedulePhase.Process):
                    unscaledProcessCount++;
                    break;
                case (ScheduleClock.RealTime, SchedulePhase.Process):
                    realProcessCount++;
                    break;
                case (ScheduleClock.GameTime, SchedulePhase.Physics):
                    gamePhysicsCount++;
                    break;
                case (ScheduleClock.UnscaledGameTime, SchedulePhase.Physics):
                    unscaledPhysicsCount++;
                    break;
                case (ScheduleClock.RealTime, SchedulePhase.Physics):
                    realPhysicsCount++;
                    break;
            }

            double remainingSeconds = entry.State == ScheduledState.Paused
                ? entry.RemainingSeconds
                : Math.Max(0d, entry.DueTime - GetCurrentTime(entry.Clock, entry.Phase));
            nextRemainingSeconds = Math.Min(nextRemainingSeconds, remainingSeconds);
        }

        return new SchedulerDebugSnapshot(
            _entries.Count,
            pausedCount,
            repeatingCount,
            gameProcessCount,
            unscaledProcessCount,
            realProcessCount,
            gamePhysicsCount,
            unscaledPhysicsCount,
            realPhysicsCount,
            _lastProcessDispatchCount,
            _lastPhysicsDispatchCount,
            _canceledCount,
            _ownerCanceledCount,
            _callbackFailedCount,
            double.IsPositiveInfinity(nextRemainingSeconds) ? null : nextRemainingSeconds);
    }
#endif

    /// <summary>取消全部活动任务并永久关闭当前核心实例。</summary>
    internal void Shutdown()
    {
        if (_isShutdown)
            return;

        _isShutdown = true;
        ScheduledEntry[] entries = new ScheduledEntry[_entries.Count];
        _entries.Values.CopyTo(entries, 0);
        _entries.Clear();
        _ownerRegistry.Clear();
        for (int clock = 0; clock < ClockCount; clock++)
        {
            for (int phase = 0; phase < PhaseCount; phase++)
                _queues[clock, phase].Clear();
        }

        while (_pendingCancellations.TryDequeue(out _))
        {
        }

        for (int index = 0; index < entries.Length; index++)
            CancelDelayCompletion(entries[index], cancellationToken: default, preserveToken: false);

        while (_pendingCancellations.TryDequeue(out _))
        {
        }

        LastAdvanceHitDispatchLimit = false;
    }

    /// <summary>取得指定时钟与阶段的当前人工时间。</summary>
    internal double GetCurrentTime(ScheduleClock clock, SchedulePhase phase)
    {
        VerifyClock(clock);
        VerifyPhase(phase);
        return _currentTimes[(int)clock, (int)phase];
    }

    /// <summary>取得队列当前保留的有效与失效项数量，仅用于确定性回归。</summary>
    internal int GetQueuedItemCount(ScheduleClock clock, SchedulePhase phase)
    {
        VerifyClock(clock);
        VerifyPhase(phase);
        return GetQueue(clock, phase).ItemCount;
    }

    private ScheduleHandle CreateEntry(
        double delaySeconds,
        double intervalSeconds,
        Action? callback,
        ScheduleOptions options,
        TaskCompletionSource? delayCompletion = null)
    {
        if (callback is null && delayCompletion is null)
            throw new ArgumentNullException(nameof(callback));
        VerifyOptions(options);
        _ownerRegistry.Validate(options.Owner);

        if (_nextHandleValue == 0)
            throw new InvalidOperationException("Scheduler 句柄空间已耗尽。");
        ulong handleValue = _nextHandleValue++;

        if (_nextSequence <= 0)
            throw new InvalidOperationException("Scheduler 创建序号空间已耗尽。");
        long sequence = _nextSequence++;

        double dueTime = GetCurrentTime(options.Clock, options.Phase) + delaySeconds;
        VerifyDueTime(dueTime);
        var entry = new ScheduledEntry(
            new ScheduleHandle(handleValue),
            sequence,
            dueTime,
            intervalSeconds,
            callback,
            delayCompletion,
            options.Clock,
            options.Phase);
        _entries.Add(handleValue, entry);
        _ownerRegistry.Track(options.Owner, entry.Handle);
        GetQueue(options.Clock, options.Phase).Enqueue(entry);
        return entry.Handle;
    }

    private void AdvanceClock(ScheduleClock clock, SchedulePhase phase, double deltaSeconds)
    {
        int clockIndex = (int)clock;
        int phaseIndex = (int)phase;
        double nextTime = _currentTimes[clockIndex, phaseIndex] + deltaSeconds;
        if (!double.IsFinite(nextTime))
            throw new InvalidOperationException("Scheduler 人工时钟已超出有限 double 范围。");
        _currentTimes[clockIndex, phaseIndex] = nextTime;
    }

    private void EnqueueCancellation(ScheduleHandle handle, CancellationToken cancellationToken)
    {
        _pendingCancellations.Enqueue(new CancellationRequest(handle, cancellationToken));
    }

    private void DrainPendingCancellations()
    {
        while (_pendingCancellations.TryDequeue(out CancellationRequest request))
        {
            if (!_entries.TryGetValue(request.Handle.Value, out ScheduledEntry? entry) ||
                entry.DelayCompletion is null)
            {
                continue;
            }

            if (entry.State == ScheduledState.Scheduled)
            {
                SchedulerQueue queue = GetQueue(entry.Clock, entry.Phase);
                queue.InvalidateActive();
                RemoveEntry(entry);
                CompactIfNeeded(entry.Clock, entry.Phase);
            }
            else
            {
                RemoveEntry(entry);
            }

#if DEBUG
            _canceledCount++;
#endif
            CancelDelayCompletion(entry, request.CancellationToken, preserveToken: true);
        }
    }

    private static void CancelDelayCompletion(
        ScheduledEntry entry,
        CancellationToken cancellationToken,
        bool preserveToken)
    {
        entry.DisposeCancellationRegistration();
        if (entry.DelayCompletion is null)
            return;

        if (preserveToken)
            entry.DelayCompletion.TrySetCanceled(cancellationToken);
        else
            entry.DelayCompletion.TrySetCanceled();
    }

    private int DispatchQueue(
        ScheduleClock clock,
        SchedulePhase phase,
        long sequenceLimit,
        int callbackBudget)
    {
        SchedulerQueue queue = GetQueue(clock, phase);
        double currentTime = GetCurrentTime(clock, phase);
        int dispatched = 0;
        while (dispatched < callbackBudget &&
               queue.TryPeek(out QueueItem item, out SchedulePriority priority))
        {
            if (priority.DueTime > currentTime || item.Sequence > sequenceLimit)
                break;

            queue.Dequeue();
            if (!TryResolveActive(item, clock, phase, out ScheduledEntry entry))
            {
                queue.RemoveStale();
                continue;
            }

            queue.RemoveActive();
            entry.State = ScheduledState.Executing;
            if (entry.DelayCompletion is not null)
            {
                RemoveEntry(entry);
                entry.DisposeCancellationRegistration();
                entry.DelayCompletion.TrySetResult();
                dispatched++;
                continue;
            }

            bool callbackFailed = false;
            try
            {
                entry.Callback!();
            }
            catch (Exception exception)
            {
                callbackFailed = true;
                RemoveEntry(entry);
#if DEBUG
                _canceledCount++;
                _callbackFailedCount++;
#endif
                _callbackErrorHandler?.Invoke(
                    new SchedulerCallbackError(
                        entry.Handle,
                        entry.Clock,
                        entry.Phase,
                        entry.IsRepeating,
                        exception));
            }

            dispatched++;
            if (callbackFailed || !_entries.ContainsKey(entry.Handle.Value))
                continue;

            if (!entry.IsRepeating)
            {
                RemoveEntry(entry);
                entry.DisposeCancellationRegistration();
                continue;
            }

            double nextDueTime = CalculateNextRepeatedDueTime(
                entry.DueTime,
                entry.IntervalSeconds,
                currentTime);
            if (entry.PauseAfterDispatch)
            {
                entry.DueTime = nextDueTime;
                entry.RemainingSeconds = Math.Max(0d, nextDueTime - currentTime);
                entry.State = ScheduledState.Paused;
                entry.PauseAfterDispatch = false;
                entry.Revision++;
                continue;
            }

            entry.DueTime = nextDueTime;
            entry.State = ScheduledState.Scheduled;
            entry.Revision++;
            queue.Enqueue(entry);
        }

        CompactIfNeeded(clock, phase);
        return dispatched;
    }

    private bool HasDueTask(SchedulePhase phase, long sequenceLimit)
    {
        for (int clockIndex = 0; clockIndex < ClockCount; clockIndex++)
        {
            ScheduleClock clock = (ScheduleClock)clockIndex;
            SchedulerQueue queue = GetQueue(clock, phase);
            PruneStaleHead(queue, clock, phase);
            if (queue.TryPeek(out QueueItem item, out SchedulePriority priority) &&
                priority.DueTime <= GetCurrentTime(clock, phase) &&
                item.Sequence <= sequenceLimit)
            {
                return true;
            }
        }
        return false;
    }

    private void PruneStaleHead(
        SchedulerQueue queue,
        ScheduleClock clock,
        SchedulePhase phase)
    {
        while (queue.TryPeek(out QueueItem item, out _))
        {
            if (TryResolveActive(item, clock, phase, out _))
                return;

            queue.Dequeue();
            queue.RemoveStale();
        }
    }

    private bool TryResolveActive(
        QueueItem item,
        ScheduleClock clock,
        SchedulePhase phase,
        out ScheduledEntry entry)
    {
        if (_entries.TryGetValue(item.HandleValue, out ScheduledEntry? candidate) &&
            candidate.State == ScheduledState.Scheduled &&
            candidate.Revision == item.Revision &&
            candidate.Clock == clock &&
            candidate.Phase == phase)
        {
            entry = candidate;
            return true;
        }

        entry = null!;
        return false;
    }

    private void CompactIfNeeded(ScheduleClock clock, SchedulePhase phase)
    {
        SchedulerQueue queue = GetQueue(clock, phase);
        if (!queue.ShouldCompact)
            return;

        queue.Clear();
        foreach (ScheduledEntry entry in _entries.Values)
        {
            if (entry.State == ScheduledState.Scheduled &&
                entry.Clock == clock &&
                entry.Phase == phase)
            {
                queue.Enqueue(entry);
            }
        }
    }

    private SchedulerQueue GetQueue(ScheduleClock clock, SchedulePhase phase) =>
        _queues[(int)clock, (int)phase];

    private bool RemoveEntry(ScheduledEntry entry)
    {
        if (!_entries.Remove(entry.Handle.Value))
            return false;

        _ownerRegistry.Untrack(entry.Handle);
        return true;
    }

    private static double CalculateNextRepeatedDueTime(
        double previousDueTime,
        double intervalSeconds,
        double currentTime)
    {
        double elapsed = Math.Max(0d, currentTime - previousDueTime);
        double periods = Math.Floor(elapsed / intervalSeconds) + 1d;
        double nextDueTime = previousDueTime + periods * intervalSeconds;
        if (!double.IsFinite(nextDueTime) || nextDueTime <= currentTime)
            nextDueTime = currentTime + intervalSeconds;
        VerifyDueTime(nextDueTime);
        return nextDueTime;
    }

    private static void VerifyOptions(ScheduleOptions options)
    {
        VerifyClock(options.Clock);
        VerifyPhase(options.Phase);
    }

    private static void VerifyClock(ScheduleClock clock)
    {
        if (!Enum.IsDefined(clock))
            throw new ArgumentOutOfRangeException(nameof(clock), clock, "未知的调度时钟。");
    }

    private static void VerifyPhase(SchedulePhase phase)
    {
        if (!Enum.IsDefined(phase))
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知的调度阶段。");
    }

    private static void VerifySeconds(double seconds, string parameterName, bool allowZero)
    {
        bool belowMinimum = allowZero ? seconds < 0d : seconds <= 0d;
        if (!double.IsFinite(seconds) || belowMinimum)
        {
            string requirement = allowZero ? "有限且不能小于 0" : "有限且必须大于 0";
            throw new ArgumentOutOfRangeException(parameterName, seconds, $"秒数必须{requirement}。");
        }
    }

    private static void VerifyDueTime(double dueTime)
    {
        if (!double.IsFinite(dueTime))
            throw new ArgumentOutOfRangeException(nameof(dueTime), dueTime, "任务到期时间超出有限 double 范围。");
    }

    private void ThrowIfShutdown()
    {
        if (_isShutdown)
            throw new ObjectDisposedException(nameof(SchedulerCore), "Scheduler 已关闭。");
    }

    private sealed class SchedulerQueue
    {
        private const int MinimumStaleItemsBeforeCompaction = 64;
        private readonly PriorityQueue<QueueItem, SchedulePriority> _items = new();
        private int _activeCount;
        private int _staleCount;

        public int ItemCount => _items.Count;

        public bool ShouldCompact =>
            _staleCount >= MinimumStaleItemsBeforeCompaction &&
            _staleCount > _activeCount;

        public void Enqueue(ScheduledEntry entry)
        {
            _items.Enqueue(
                new QueueItem(
                    entry.Handle.Value,
                    entry.Revision,
                    entry.Sequence),
                new SchedulePriority(entry.DueTime, entry.Sequence));
            _activeCount++;
        }

        public bool TryPeek(out QueueItem item, out SchedulePriority priority) =>
            _items.TryPeek(out item, out priority);

        public void Dequeue() => _items.Dequeue();

        public void InvalidateActive()
        {
            _activeCount--;
            _staleCount++;
        }

        public void RemoveActive() => _activeCount--;

        public void RemoveStale() => _staleCount--;

        public void Clear()
        {
            _items.Clear();
            _activeCount = 0;
            _staleCount = 0;
        }
    }

    private sealed class ScheduledEntry
    {
        public ScheduleHandle Handle { get; }
        public long Sequence { get; }
        public double IntervalSeconds { get; }
        public Action? Callback { get; }
        public TaskCompletionSource? DelayCompletion { get; }
        public ScheduleClock Clock { get; }
        public SchedulePhase Phase { get; }
        public bool IsRepeating => IntervalSeconds > 0d;
        public double DueTime { get; set; }
        public double RemainingSeconds { get; set; }
        public int Revision { get; set; }
        public ScheduledState State { get; set; } = ScheduledState.Scheduled;
        public bool PauseAfterDispatch { get; set; }
        private CancellationTokenRegistration _cancellationRegistration;
        private bool _hasCancellationRegistration;

        public ScheduledEntry(
            ScheduleHandle handle,
            long sequence,
            double dueTime,
            double intervalSeconds,
            Action? callback,
            TaskCompletionSource? delayCompletion,
            ScheduleClock clock,
            SchedulePhase phase)
        {
            Handle = handle;
            Sequence = sequence;
            DueTime = dueTime;
            IntervalSeconds = intervalSeconds;
            Callback = callback;
            DelayCompletion = delayCompletion;
            Clock = clock;
            Phase = phase;
        }

        public void SetCancellationRegistration(CancellationTokenRegistration registration)
        {
            _cancellationRegistration = registration;
            _hasCancellationRegistration = true;
        }

        public void DisposeCancellationRegistration()
        {
            if (!_hasCancellationRegistration)
                return;

            _cancellationRegistration.Dispose();
            _cancellationRegistration = default;
            _hasCancellationRegistration = false;
        }
    }

    private enum ScheduledState
    {
        Scheduled,
        Paused,
        Executing,
    }

    private readonly record struct QueueItem(
        ulong HandleValue,
        int Revision,
        long Sequence);

    private readonly record struct CancellationRequest(
        ScheduleHandle Handle,
        CancellationToken CancellationToken);

    private sealed class CancellationRegistrationState
    {
        public SchedulerCore Core { get; }
        public ScheduleHandle Handle { get; }
        public CancellationToken Token { get; }

        public CancellationRegistrationState(
            SchedulerCore core,
            ScheduleHandle handle,
            CancellationToken token)
        {
            Core = core;
            Handle = handle;
            Token = token;
        }
    }

    private readonly record struct SchedulePriority(double DueTime, long Sequence)
        : IComparable<SchedulePriority>
    {
        public int CompareTo(SchedulePriority other)
        {
            int dueTimeComparison = DueTime.CompareTo(other.DueTime);
            return dueTimeComparison != 0
                ? dueTimeComparison
                : Sequence.CompareTo(other.Sequence);
        }
    }
}

/// <summary>Scheduler callback 异常的内部结构化上下文。</summary>
internal readonly record struct SchedulerCallbackError(
    ScheduleHandle Handle,
    ScheduleClock Clock,
    SchedulePhase Phase,
    bool IsRepeating,
    Exception Exception);

#if DEBUG
/// <summary>Scheduler 在 Debug 构建中按需生成的只读诊断快照。</summary>
internal readonly record struct SchedulerDebugSnapshot(
    int ActiveCount,
    int PausedCount,
    int RepeatingCount,
    int GameProcessCount,
    int UnscaledProcessCount,
    int RealProcessCount,
    int GamePhysicsCount,
    int UnscaledPhysicsCount,
    int RealPhysicsCount,
    int LastProcessDispatchCount,
    int LastPhysicsDispatchCount,
    long CanceledCount,
    long OwnerCanceledCount,
    long CallbackFailedCount,
    double? NextRemainingSeconds);
#endif
