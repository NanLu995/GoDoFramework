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
    /// 事件作用域：统一管理一批事件的注册与注销。
    /// 持有此对象的类销毁时调用 Dispose() 即可清除所有监听。
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
        /// 触发后自动移除；未触发时 Dispose() 也会清除。
        /// </summary>
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
