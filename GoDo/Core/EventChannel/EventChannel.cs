// ==========================================
// EventChannel.cs —— GoDo 事件系统核心
// ==========================================
using System;
using System.Collections.Generic;
using Godot;

namespace GoDo
{
    /// <summary>
    /// GoDo 的进程内事件通道，用于跨系统的一对多通知。
    /// <para>仅允许在 Godot 主线程调用；本类型不提供线程安全保证。</para>
    /// <para>
    /// 需要返回结果时使用直接方法调用；场景树内关系明确的对象优先使用 Godot Signal；
    /// 发送者不应感知接收者的一对多通知才使用本类型。
    /// </para>
    /// </summary>
    public static class EventChannel
    {
        // ── 注册表 ────────────────────────────────
        private static readonly Dictionary<Type, object> _registry = new();

        // ── 公开 API ──────────────────────────────

        /// <summary>
        /// 广播一个事件给所有监听者。
        /// 即使某个 handler 抛出异常，其余 handler 仍会继续执行。
        /// </summary>
        public static void Emit<T>(T evt) where T : struct, IEventMessage
        {
            // P1: 缓存 typeof(T)，避免重复调用
            var type = typeof(T);

#if GODOT_DEBUG
            GD.Print($"[EventChannel] Emit → {type.Name}");
#endif
            if (_registry.TryGetValue(type, out var group))
                ((HandlerGroup<T>)group).Dispatch(evt);
        }

        /// <summary>
        /// 持续监听某类事件。
        /// priority 越小越先执行，默认 0。
        /// </summary>
        public static void On<T>(Action<T> handler, int priority = 0)
            where T : struct, IEventMessage
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            GetOrCreate<T>().Add(handler, priority, once: false);
        }

        /// <summary>
        /// 监听一次，触发后自动移除。
        /// </summary>
        public static void Once<T>(Action<T> handler)
            where T : struct, IEventMessage
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            GetOrCreate<T>().Add(handler, priority: 0, once: true);
        }

        /// <summary>
        /// 手动取消监听。
        /// </summary>
        public static void Off<T>(Action<T> handler)
            where T : struct, IEventMessage
        {
            if (handler == null) return;
            if (_registry.TryGetValue(typeof(T), out var group))
                ((HandlerGroup<T>)group).Remove(handler);
        }

        /// <summary>
        /// 将监听绑定到节点生命周期。
        /// 节点退出场景树时自动解绑，无需手动 Off。
        /// </summary>
        public static void Bind<T>(Node node, Action<T> handler, int priority = 0)
            where T : struct, IEventMessage
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            // 节点不在树里时直接 return，防止 TreeExiting 永远不触发导致泄漏。
            // 这是生命周期正确性检查，所有构建配置必须保持相同行为。
            if (!node.IsInsideTree())
            {
                ErrorHub.Warn(
                    "Bind 目标节点不在场景树中，监听未注册",
                    "EventChannel",
                    context: $"Bind<{typeof(T).Name}> node={node.Name}");
                return;
            }

            // 注册与生命周期连接必须是一个整体。重复注册被拒绝时，不能再挂接
            // TreeExiting，否则节点退出时会误删之前已经存在的监听。
            if (!GetOrCreate<T>().Add(handler, priority, once: false))
                return;

            void AutoOff()
            {
                Off<T>(handler);
                node.TreeExiting -= AutoOff; // 移除自身，防止多次触发
            }

            node.TreeExiting += AutoOff;
        }

        /// <summary>
        /// [仅限框架内部/测试] 移除某事件类型的全部监听。
        /// 普通业务代码请勿调用。
        /// </summary>
        internal static void ClearAll<T>() where T : struct, IEventMessage
        {
#if GODOT_DEBUG
            // R5: 提示开发者此操作的风险
            if (_registry.ContainsKey(typeof(T)))
                GD.PrintErr($"[EventChannel] ClearAll<{typeof(T).Name}> called — 所有监听已清除，" +
                            $"若在 Dispatch 中调用此方法请确认行为符合预期。");
#endif
            _registry.Remove(typeof(T));
        }

        // ── 调试 API ──────────────────────────────

#if GODOT_DEBUG
        // 让 DumpRegistry 可以统一调用 Count，无需知道泛型类型
        private interface IHandlerGroup { int Count { get; } }

        /// <summary>
        /// [仅 Debug 模式] 返回当前某事件类型的监听数量。
        /// </summary>
        public static int GetListenerCount<T>() where T : struct, IEventMessage
        {
            if (_registry.TryGetValue(typeof(T), out var group))
                return ((HandlerGroup<T>)group).Count;
            return 0;
        }

        /// <summary>
        /// [仅 Debug 模式] 打印所有已注册的事件类型及监听数量。
        /// </summary>
        public static void DumpRegistry()
        {
            GD.Print("── EventChannel Registry ──");
            foreach (var kv in _registry)
                GD.Print($"  {kv.Key.Name}: {((IHandlerGroup)kv.Value).Count} listener(s)");
            GD.Print("───────────────────────────");
        }
#endif

        // ── 内部实现 ──────────────────────────────

        private static HandlerGroup<T> GetOrCreate<T>() where T : struct, IEventMessage
        {
            if (!_registry.TryGetValue(typeof(T), out var group))
            {
                group = new HandlerGroup<T>();
                _registry[typeof(T)] = group;
            }
            return (HandlerGroup<T>)group;
        }

        // ── HandlerGroup<T> ───────────────────────

#if GODOT_DEBUG
        private class HandlerGroup<T> : IHandlerGroup where T : struct, IEventMessage
#else
        private class HandlerGroup<T> where T : struct, IEventMessage
#endif
        {
            // P3: 预设初始容量，大多数事件监听者在 4 个以内，避免首次扩容
            private readonly List<HandlerEntry> _handlers = new(4);
            private readonly List<HandlerEntry> _pendingAdd = new(2);
            private readonly List<Action<T>> _pendingRemove = new(2);
            private int _dispatchDepth;

            public int Count => _handlers.Count;

            public bool Add(Action<T> handler, int priority, bool once)
            {
                // 重复注册会改变事件语义，因此所有构建配置必须保持相同行为。
                for (int i = 0; i < _handlers.Count; i++)
                {
                    if (_handlers[i].Handler == handler && !Contains(_pendingRemove, handler))
                    {
                        ErrorHub.Warn("重复注册 handler，已跳过", "EventChannel", context: $"On<{typeof(T).Name}>");
                        return false;
                    }
                }
                for (int i = 0; i < _pendingAdd.Count; i++)
                {
                    if (_pendingAdd[i].Handler == handler)
                    {
                        ErrorHub.Warn("派发期间重复注册 handler，已跳过", "EventChannel", context: $"On<{typeof(T).Name}>");
                        return false;
                    }
                }

                // P2: HandlerEntry 改为 struct，避免堆分配
                var entry = new HandlerEntry(handler, priority, once);
                if (_dispatchDepth > 0)
                    _pendingAdd.Add(entry);
                else
                    InsertSorted(entry);
                return true;
            }

            public void Remove(Action<T> handler)
            {
                if (_dispatchDepth > 0)
                {
                    if (!Contains(_pendingRemove, handler))
                        _pendingRemove.Add(handler);
                }
                else
                    RemoveFromList(_handlers, handler);
            }

            public void Dispatch(T evt)
            {
                _dispatchDepth++;

                try
                {
                    // P4: for 循环代替 foreach，JIT 更友好，无枚举器开销
                    for (int i = 0; i < _handlers.Count; i++)
                    {
                        var entry = _handlers[i];

                        // Off 在派发期间立即影响尚未执行的 handler；Once 在调用前
                        // 先标记移除，确保同类型事件重入时也只执行一次。
                        if (Contains(_pendingRemove, entry.Handler))
                            continue;
                        if (entry.Once)
                            _pendingRemove.Add(entry.Handler);

                        try
                        {
                            entry.Handler(evt);
                        }
                        catch (Exception ex)
                        {
                            ErrorHub.Report(ex, "EventChannel", context: $"Emit<{typeof(T).Name}>");
                        }
                    }
                }
                finally
                {
                    _dispatchDepth--;

                    // 同类型事件可以重入。只有最外层派发结束后才能修改主列表，
                    // 否则内层派发会破坏外层正在使用的索引和顺序。
                    if (_dispatchDepth == 0)
                        ApplyPendingChanges();
                }
            }

            private void ApplyPendingChanges()
            {
                for (int i = 0; i < _pendingRemove.Count; i++) RemoveFromList(_handlers, _pendingRemove[i]);
                for (int i = 0; i < _pendingAdd.Count; i++) InsertSorted(_pendingAdd[i]);

                _pendingRemove.Clear();
                _pendingAdd.Clear();
            }

            private static bool Contains(List<Action<T>> list, Action<T> handler)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == handler)
                        return true;
                }
                return false;
            }

            // 反向遍历删除，零 lambda 分配，且删除后不影响未遍历的索引
            private static void RemoveFromList(List<HandlerEntry> list, Action<T> handler)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Handler == handler)
                    {
                        list.RemoveAt(i);
                        break; // handler 唯一，找到即止
                    }
                }
            }

            private void InsertSorted(HandlerEntry entry)
            {
                int i = 0;
                while (i < _handlers.Count && _handlers[i].Priority <= entry.Priority) i++;
                _handlers.Insert(i, entry);
            }

            // P2: 改为 struct，三个字段的值类型，零堆分配
            private struct HandlerEntry
            {
                public readonly Action<T> Handler;
                public readonly int Priority;
                public readonly bool Once;

                public HandlerEntry(Action<T> handler, int priority, bool once)
                {
                    Handler = handler;
                    Priority = priority;
                    Once = once;
                }
            }
        }
    }
}
