using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>
/// 以确定的同步生命周期顺序管理一个当前状态。
/// <para>实例不是线程安全的；调用方必须从同一串行执行边界调用 <see cref="Change"/>、<see cref="Tick"/> 和 <see cref="Dispose"/>。</para>
/// </summary>
/// <typeparam name="TContext">状态共享的业务上下文类型。</typeparam>
/// <typeparam name="TState">由该状态机接受的状态基类型。</typeparam>
public class StateMachine<TContext, TState> : IDisposable
    where TState : class, IState<TContext>
{
    private readonly TContext _context;
    private Queue<TState>? _pendingStates;
    private Exception? _failure;
    private bool _currentStateNeedsExit;
    private bool _isProcessing;

    /// <summary>未显式指定时，单次 <see cref="Change"/> 允许完成的最大实际状态切换数。</summary>
    public const int DefaultMaxTransitionsPerChange = 64;

    /// <summary>使用由所有状态共享的上下文创建未初始化状态机。</summary>
    /// <param name="context">传递给状态生命周期和可选更新回调的上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <see langword="null"/>。</exception>
    public StateMachine(TContext context) : this(context, DefaultMaxTransitionsPerChange)
    {
    }

    /// <summary>使用共享上下文和单次 Change 切换上限创建未初始化状态机。</summary>
    /// <param name="context">传递给状态生命周期和可选更新回调的上下文。</param>
    /// <param name="maxTransitionsPerChange">最外层一次 Change 允许完成的最大实际状态切换数。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTransitionsPerChange"/> 小于 1。</exception>
    public StateMachine(TContext context, int maxTransitionsPerChange)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTransitionsPerChange, 1);
        _context = context;
        MaxTransitionsPerChange = maxTransitionsPerChange;
    }

    /// <summary>获取当前状态；首次成功调用 <see cref="Change"/> 前返回 <see langword="null"/>。</summary>
    public TState? CurrentState { get; private set; }

    /// <summary>获取状态机是否因生命周期异常或切换上限保护进入终止故障状态。</summary>
    public bool IsFaulted => _failure is not null;

    /// <summary>获取导致终止故障的原始异常；状态机未故障时返回 <see langword="null"/>。</summary>
    public Exception? Failure => _failure;

    /// <summary>获取状态机是否已关闭。</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>获取最外层一次 <see cref="Change"/> 允许完成的最大实际状态切换数。</summary>
    public int MaxTransitionsPerChange { get; }

    /// <summary>
    /// 切换到目标状态，并同步处理生命周期回调中登记的 FIFO 后续请求。
    /// <para>固定顺序为旧状态 Exit、更新 <see cref="CurrentState"/>、新状态 Enter。</para>
    /// </summary>
    /// <param name="nextState">目标状态实例。</param>
    /// <returns>请求已完成、已排队或因目标已经是当前实例而被忽略。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nextState"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">状态机已关闭或此前已因生命周期异常终止。</exception>
    /// <exception cref="StateMachineTransitionLimitException">FIFO 切换链在本次调用中尝试超过 <see cref="MaxTransitionsPerChange"/>。</exception>
    /// <remarks>Enter、Exit 或切换上限异常会原样传播，并使状态机永久进入终止故障状态。</remarks>
    public StateChangeResult Change(TState nextState)
    {
        ArgumentNullException.ThrowIfNull(nextState);
        EnsureOperational();

        if (ReferenceEquals(CurrentState, nextState))
            return StateChangeResult.IgnoredSameState;

        if (_isProcessing)
        {
            (_pendingStates ??= new Queue<TState>()).Enqueue(nextState);
            return StateChangeResult.Queued;
        }

        ProcessChanges(nextState);
        return StateChangeResult.Changed;
    }

    /// <summary>更新实现 <see cref="IUpdatableState{TContext}"/> 的当前状态；未初始化或当前状态不可更新时不执行任何操作。</summary>
    /// <param name="deltaSeconds">原样传递给状态的更新时间间隔（秒）。</param>
    /// <exception cref="InvalidOperationException">状态机已关闭或此前已因生命周期异常终止。</exception>
    /// <remarks>Update 异常原样传播，但不会改变状态机生命周期状态。</remarks>
    public void Tick(double deltaSeconds)
    {
        EnsureOperational();
        if (CurrentState is IUpdatableState<TContext> updatableState)
            updatableState.Update(_context, deltaSeconds);
    }

    /// <summary>
    /// 退出当前已成功进入的状态并永久关闭状态机。
    /// <para>重复调用没有副作用。故障状态不会重试失败或未完成的生命周期回调。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">从 Enter 或 Exit 回调中重入关闭状态机。</exception>
    /// <remarks>正常关闭时 Exit 异常原样传播，但状态机仍保持已关闭且不会重试 Exit。</remarks>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        if (_isProcessing)
            throw new InvalidOperationException("不能从状态生命周期回调中关闭状态机。");

        IsDisposed = true;
        _pendingStates?.Clear();

        TState? current = CurrentState;
        if (current is null || !_currentStateNeedsExit)
        {
            CurrentState = null;
            return;
        }

        _currentStateNeedsExit = false;
        try
        {
            current.Exit(_context);
        }
        catch (Exception exception)
        {
            _failure ??= exception;
            throw;
        }
        finally
        {
            CurrentState = null;
        }
    }

    private void ProcessChanges(TState firstState)
    {
        _isProcessing = true;
        TState nextState = firstState;
        int completedTransitions = 0;
        try
        {
            while (true)
            {
                if (!ReferenceEquals(CurrentState, nextState))
                {
                    if (completedTransitions >= MaxTransitionsPerChange)
                    {
                        var exception = new StateMachineTransitionLimitException(
                            MaxTransitionsPerChange);
                        _failure = exception;
                        throw exception;
                    }

                    ChangeSingle(nextState);
                    completedTransitions++;
                }

                if (_pendingStates is null || _pendingStates.Count == 0)
                    return;

                nextState = _pendingStates.Dequeue();
            }
        }
        catch
        {
            _pendingStates?.Clear();
            throw;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void ChangeSingle(TState nextState)
    {
        TState? previousState = CurrentState;
        if (previousState is not null)
        {
            _currentStateNeedsExit = false;
            try
            {
                previousState.Exit(_context);
            }
            catch (Exception exception)
            {
                _failure = exception;
                throw;
            }
        }

        CurrentState = nextState;
        _currentStateNeedsExit = false;
        try
        {
            nextState.Enter(_context);
            _currentStateNeedsExit = true;
        }
        catch (Exception exception)
        {
            _failure = exception;
            throw;
        }
    }

    private void EnsureOperational()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().FullName);
        if (_failure is not null)
        {
            throw new InvalidOperationException(
                "状态机已因生命周期异常终止，不能继续切换或更新。",
                _failure);
        }
    }
}
