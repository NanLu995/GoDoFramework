// ==========================================
// EventChannel.cs —— GoDo 事件系统核心
// ==========================================
using System;
using System.Collections.Generic;
using Godot;

namespace GoDo
{
    public static class EventChannel
    {
        // ── 注册表 ────────────────────────────────
        private static readonly Dictionary<Type, object> _registry = new();

        // ── 公开 API ──────────────────────────────

        /// <summary>
        /// 广播一个事件给所有监听者。
        /// 即使某个 handler 抛出异常，其余 handler 仍会继续执行。
        /// </summary>
        public static void Emit<T>(T evt) where T : struct, IGameEvent
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
            where T : struct, IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            GetOrCreate<T>().Add(handler, priority, once: false);
        }

        /// <summary>
        /// 监听一次，触发后自动移除。
        /// </summary>
        public static void Once<T>(Action<T> handler)
            where T : struct, IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            GetOrCreate<T>().Add(handler, priority: 0, once: true);
        }

        /// <summary>
        /// 手动取消监听。
        /// </summary>
        public static void Off<T>(Action<T> handler)
            where T : struct, IGameEvent
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
            where T : struct, IGameEvent
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

#if GODOT_DEBUG
            // R2: 节点不在树里时直接 return，防止 TreeExiting 永远不触发导致泄漏
            if (!node.IsInsideTree())
            {
                GD.PrintErr($"[EventChannel] Bind called on node '{node.Name}' that is not inside the tree. " +
                            $"Event: {typeof(T).Name} — 已跳过，防止监听泄漏。");
                ErrorHandler.Debug("Bind 目标节点不在场景树中，监听未注册", "EventChannel", context: $"Bind<{typeof(T).Name}> node={node?.Name}");
                return;
            }
#endif

            On<T>(handler, priority);

            // 用具名本地函数而非 lambda，避免匿名委托重复累积
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
        internal static void ClearAll<T>() where T : struct, IGameEvent
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
        public static int GetListenerCount<T>() where T : struct, IGameEvent
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

        private static HandlerGroup<T> GetOrCreate<T>() where T : struct, IGameEvent
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
        private class HandlerGroup<T> : IHandlerGroup where T : struct, IGameEvent
#else
        private class HandlerGroup<T> where T : struct, IGameEvent
#endif
        {
            // P3: 预设初始容量，大多数事件监听者在 4 个以内，避免首次扩容
            private readonly List<HandlerEntry> _handlers = new(4);
            private readonly List<HandlerEntry> _pendingAdd = new(2);
            private readonly List<Action<T>> _pendingRemove = new(2);
            private readonly List<Action<T>> _onceRemove = new(2);
            private bool _isDispatching;

            public int Count => _handlers.Count;

            public void Add(Action<T> handler, int priority, bool once)
            {
#if GODOT_DEBUG
                // R3: 同时检查 _handlers 和 _pendingAdd，覆盖派发中途注册的情况
                // 发现重复直接 return，阻止注册，而不只是打警告
                for (int i = 0; i < _handlers.Count; i++)
                {
                    if (_handlers[i].Handler == handler)
                    {
                        GD.PrintErr($"[EventChannel] 重复注册同一个 handler，事件类型: {typeof(T).Name}。" +
                                    $"已阻止本次注册，请检查是否在 _Ready 或循环中重复调用了 On/Bind。");
                        ErrorHandler.Warn("重复注册 handler，已跳过", "EventChannel", context: $"On<{typeof(T).Name}>");
                        return;
                    }
                }
                for (int i = 0; i < _pendingAdd.Count; i++)
                {
                    if (_pendingAdd[i].Handler == handler)
                    {
                        GD.PrintErr($"[EventChannel] 在派发过程中重复注册同一个 handler，事件类型: {typeof(T).Name}。" +
                                    $"已阻止本次注册。");
                        return;
                    }
                }
#endif
                // P2: HandlerEntry 改为 struct，避免堆分配
                var entry = new HandlerEntry(handler, priority, once);
                if (_isDispatching)
                    _pendingAdd.Add(entry);
                else
                    InsertSorted(entry);
            }

            public void Remove(Action<T> handler)
            {
                if (_isDispatching)
                    _pendingRemove.Add(handler);
                else
                    RemoveFromList(_handlers, handler);
            }

            public void Dispatch(T evt)
            {
                _isDispatching = true;

                // R4: 用 try/finally 保证 _isDispatching 一定被重置
                try
                {
                    // P4: for 循环代替 foreach，JIT 更友好，无枚举器开销
                    for (int i = 0; i < _handlers.Count; i++)
                    {
                        var entry = _handlers[i];
                        try
                        {
                            entry.Handler(evt);
                        }
                        catch (Exception ex)
                        {
                            // R1: 打完整异常信息含调用栈，方便定位 bug
                            GD.PrintErr($"[EventChannel] Handler 抛出异常，事件类型: {typeof(T).Name}\n{ex}");
                            ErrorHandler.Report(ex, "EventChannel", context: $"Emit<{typeof(T).Name}>");
                        }

                        if (entry.Once) _onceRemove.Add(entry.Handler);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }

                // 统一处理延迟操作，四个列表同一套模式
                for (int i = 0; i < _onceRemove.Count; i++) RemoveFromList(_handlers, _onceRemove[i]);
                for (int i = 0; i < _pendingRemove.Count; i++) RemoveFromList(_handlers, _pendingRemove[i]);
                for (int i = 0; i < _pendingAdd.Count; i++) InsertSorted(_pendingAdd[i]);

                _onceRemove.Clear();
                _pendingRemove.Clear();
                _pendingAdd.Clear();
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
