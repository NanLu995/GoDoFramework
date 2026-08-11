// ==========================================
// EventScope.cs —— 批量事件生命周期管理
//
// 适用场景：纯 C# 类（没有 Node 生命周期的）
//   - 网络管理器
//   - 存档系统  
//   - 状态机
//   - 任何不继承 Node 的服务类
//
// Node 类请优先用 EventChannel.Bind()，自动解绑更方便
// ==========================================
using System;
using System.Collections.Generic;

namespace GoDo
{
    /// <summary>
    /// 为纯 C# 对象统一管理一批 EventChannel 监听。所有成员都应在 Godot 主线程调用；
    /// 所有者生命周期结束时必须调用 <see cref="Dispose"/>。
    /// </summary>
    public sealed class EventScope : IDisposable
    {
        // 记录所有注册的事件，用于批量注销
        // 每条记录：(注销动作)
        private readonly List<Action> _offActions = new(4);
        private bool _disposed;

        /// <summary>
        /// 通过 Scope 注册持续监听。
        /// Scope.Dispose() 时自动注销。
        /// </summary>
        /// <typeparam name="T">要监听的值类型消息。</typeparam>
        /// <param name="handler">同步接收事件的回调。</param>
        /// <param name="priority">执行优先级；数值越小越先执行，相同值保持注册顺序。</param>
        /// <returns>当前作用域，供链式注册。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ObjectDisposedException">当前作用域已经释放。</exception>
        public EventScope On<T>(Action<T> handler, int priority = 0)
            where T : struct, IEventMessage
        {
            ThrowIfDisposed();
            EventChannel.On<T>(handler, priority);
            _offActions.Add(() => EventChannel.Off<T>(handler));
            return this; // 支持链式调用
        }

        /// <summary>
        /// 通过 Scope 注册单次监听。
        /// 回调执行前自动标记移除；未触发时 Dispose() 也会清除。
        /// </summary>
        /// <typeparam name="T">要监听的值类型消息。</typeparam>
        /// <param name="handler">同步接收首个事件的回调。</param>
        /// <returns>当前作用域，供链式注册。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ObjectDisposedException">当前作用域已经释放。</exception>
        public EventScope Once<T>(Action<T> handler)
            where T : struct, IEventMessage
        {
            ThrowIfDisposed();
            EventChannel.Once<T>(handler);
            _offActions.Add(() => EventChannel.Off<T>(handler));
            return this;
        }

        /// <summary>
        /// 手动提前注销某个事件（不影响其他注册）。
        /// 注意：此方法无法从 _offActions 里移除对应条目，
        /// Dispose 时会再调一次 Off，Off 对不存在的 handler 是安全的（幂等）。
        /// </summary>
        /// <typeparam name="T">要停止监听的值类型消息。</typeparam>
        /// <param name="handler">注册时使用的同一个委托；为 <see langword="null"/> 时忽略。</param>
        public void Off<T>(Action<T> handler)
            where T : struct, IEventMessage
        {
            EventChannel.Off<T>(handler);
        }

        /// <summary>
        /// 清除所有通过此 Scope 注册的监听。
        /// 实现 IDisposable，可配合 using 语句使用。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _offActions.Count; i++)
                _offActions[i].Invoke();

            _offActions.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventScope),
                    "[EventScope] 此 Scope 已经 Dispose，不能继续注册事件。");
        }
    }
}
