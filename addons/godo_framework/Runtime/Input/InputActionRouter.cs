using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>从最新 Scope 向下稳定派发并消费离散输入 Action。</summary>
public sealed class InputActionRouter : IInputActionRouter
{
    private const InputActionTransitions AllTransitions =
        InputActionTransitions.Started |
        InputActionTransitions.Performed |
        InputActionTransitions.Completed |
        InputActionTransitions.Cancelled;

    private readonly InputService _input;
    private readonly List<ScopeEntry> _scopes = new();
    private readonly Dictionary<ulong, ScopeEntry> _scopesByToken = new();
    private readonly Dictionary<ulong, BindingEntry> _bindingsByToken = new();
    private readonly List<PendingMutation> _pendingMutations = new();
    private readonly HashSet<InputActionId> _retriggerGates = new();
    private InputActionId[] _layoutActions = Array.Empty<InputActionId>();
    private InputActionStatus[] _previousStatuses = Array.Empty<InputActionStatus>();
    private ulong _nextToken;
    private ulong _routeRevision;
    private ulong _observedRouteRevision;
    private ulong _observedContextRevision;
    private ulong _lastDispatchSequence;
    private bool _hasDispatched;
    private bool _dispatching;
    private bool _shutdown;

    internal ulong RouteRevision => _routeRevision;
    internal ulong LastDispatchSequence => _lastDispatchSequence;

    internal InputActionRouter(InputService input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _observedContextRevision = input.ContextRevision;
    }

    /// <inheritdoc />
    public InputRouteScope PushScope(string debugName)
    {
        MainThreadGuard.VerifyAccess();
        VerifyRunning();
        ArgumentException.ThrowIfNullOrWhiteSpace(debugName);
        if (!string.Equals(debugName, debugName.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("输入路由 Scope 名称不能包含首尾空白。", nameof(debugName));

        ulong token = NextToken();
        var entry = new ScopeEntry(token, debugName);
        _scopesByToken.Add(token, entry);
        if (_dispatching)
            _pendingMutations.Add(new PendingMutation(PendingMutationKind.AddScope, token));
        else
        {
            _scopes.Add(entry);
            _routeRevision++;
        }

        return new InputRouteScope(this, token);
    }

    internal InputRouteBinding Bind(
        ulong scopeToken,
        InputActionId action,
        InputActionTransitions transitions,
        InputRouteHandler handler)
    {
        MainThreadGuard.VerifyAccess();
        VerifyRunning();
        if (action.IsEmpty)
            throw new ArgumentException("输入 Action ID 不能是默认值。", nameof(action));
        if (transitions == InputActionTransitions.None)
            throw new ArgumentException("输入路由 Transition 不能为空。", nameof(transitions));
        if ((transitions & ~AllTransitions) != 0)
            throw new ArgumentOutOfRangeException(nameof(transitions));
        ArgumentNullException.ThrowIfNull(handler);
        if (!_input.TryResolveActionIndex(action, out _))
            throw new InputOperationException($"输入 Action 未注册: {action.Value}");
        if (!_scopesByToken.TryGetValue(scopeToken, out ScopeEntry? scope) || scope.PendingDispose)
            throw new InputOperationException("InputRouteScope 已释放。");

        foreach (BindingEntry existing in scope.Bindings)
            VerifyNoOverlap(existing, action, transitions);
        foreach (BindingEntry pending in scope.PendingBindings)
            VerifyNoOverlap(pending, action, transitions);

        ulong token = NextToken();
        var binding = new BindingEntry(token, scope, action, transitions, handler);
        _bindingsByToken.Add(token, binding);
        if (_dispatching)
        {
            scope.PendingBindings.Add(binding);
            _pendingMutations.Add(new PendingMutation(PendingMutationKind.AddBinding, token));
        }
        else
        {
            scope.Bindings.Add(binding);
            _routeRevision++;
        }

        return new InputRouteBinding(this, token);
    }

    internal void ReleaseScope(ulong token)
    {
        MainThreadGuard.VerifyAccess();
        if (_shutdown || !_scopesByToken.TryGetValue(token, out ScopeEntry? scope) || scope.PendingDispose)
            return;

        if (_dispatching)
        {
            scope.PendingDispose = true;
            _pendingMutations.Add(new PendingMutation(PendingMutationKind.RemoveScope, token));
            return;
        }

        if (RemoveScope(scope))
            _routeRevision++;
    }

    internal void ReleaseBinding(ulong token)
    {
        MainThreadGuard.VerifyAccess();
        if (_shutdown ||
            !_bindingsByToken.TryGetValue(token, out BindingEntry? binding) ||
            binding.PendingDispose)
        {
            return;
        }

        if (_dispatching)
        {
            binding.PendingDispose = true;
            _pendingMutations.Add(new PendingMutation(PendingMutationKind.RemoveBinding, token));
            return;
        }

        if (RemoveBinding(binding))
            _routeRevision++;
    }

    internal void Dispatch(InputFrame frame)
    {
        MainThreadGuard.VerifyAccess();
        if (_shutdown)
            return;
        if (_hasDispatched && frame.Sequence == _lastDispatchSequence)
            return;

        EnsureActionLayout();
        ApplyRevisionGateBeforeDispatch(frame);
        ReleaseIdleGates(frame);
        _dispatching = true;
        try
        {
            for (int actionIndex = 0; actionIndex < _layoutActions.Length; actionIndex++)
            {
                InputActionId action = _layoutActions[actionIndex];
                InputActionFrameState state = frame.GetState(action);
                InputActionTransitions transitions = NormalizeTransitions(state.Transitions);
                if (transitions == InputActionTransitions.None || _retriggerGates.Contains(action))
                    continue;

                DispatchAction(action, state, transitions);
            }
        }
        finally
        {
            _dispatching = false;
            CommitPendingMutations();
            ApplyRevisionGateAfterDispatch(frame);
            for (int index = 0; index < _layoutActions.Length; index++)
                _previousStatuses[index] = frame.GetState(_layoutActions[index]).Status;
            _lastDispatchSequence = frame.Sequence;
            _hasDispatched = true;
        }
    }

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        if (_shutdown)
            return;
        _shutdown = true;
        _dispatching = false;
        _pendingMutations.Clear();
        _bindingsByToken.Clear();
        _scopesByToken.Clear();
        _scopes.Clear();
        _retriggerGates.Clear();
        _layoutActions = Array.Empty<InputActionId>();
        _previousStatuses = Array.Empty<InputActionStatus>();
    }

    internal bool IsRetriggerGated(InputActionId action) => _retriggerGates.Contains(action);

#if DEBUG
    internal InputRouterDebugSnapshot GetDebugSnapshot()
    {
        MainThreadGuard.VerifyAccess();
        var scopes = new InputRouterDebugScopeEntry[_scopes.Count];
        for (int source = _scopes.Count - 1, destination = 0; source >= 0; source--, destination++)
        {
            ScopeEntry scope = _scopes[source];
            scopes[destination] = new InputRouterDebugScopeEntry(
                destination,
                scope.DebugName,
                scope.Bindings.Count);
        }
        return new InputRouterDebugSnapshot(
            !_shutdown,
            _routeRevision,
            _lastDispatchSequence,
            scopes);
    }
#endif

    private void DispatchAction(
        InputActionId action,
        InputActionFrameState state,
        InputActionTransitions transitions)
    {
        if (DispatchPhaseAcrossScopes(
                action, state, transitions, InputActionTransitions.Started) ||
            DispatchPhaseAcrossScopes(
                action, state, transitions, InputActionTransitions.Performed) ||
            DispatchPhaseAcrossScopes(
                action, state, transitions, InputActionTransitions.Cancelled) ||
            DispatchPhaseAcrossScopes(
                action, state, transitions, InputActionTransitions.Completed))
        {
            return;
        }
    }

    private bool DispatchPhaseAcrossScopes(
        InputActionId action,
        InputActionFrameState state,
        InputActionTransitions transitions,
        InputActionTransitions phase)
    {
        for (int scopeIndex = _scopes.Count - 1; scopeIndex >= 0; scopeIndex--)
        {
            if (DispatchPhase(_scopes[scopeIndex], action, state, transitions, phase))
                return true;
        }

        return false;
    }

    private bool DispatchPhase(
        ScopeEntry scope,
        InputActionId action,
        InputActionFrameState state,
        InputActionTransitions transitions,
        InputActionTransitions phase)
    {
        if ((transitions & phase) == 0)
            return false;

        for (int index = 0; index < scope.Bindings.Count; index++)
        {
            BindingEntry binding = scope.Bindings[index];
            if (binding.Action != action ||
                (binding.Transitions & phase) == 0 ||
                binding.LastInvocationSequence == state.Sequence)
            {
                continue;
            }

            InputActionTransitions matched = binding.Transitions & transitions;
            binding.LastInvocationSequence = state.Sequence;
            try
            {
                InputRouteResult result = binding.Handler(state, matched);
                if (!Enum.IsDefined(result))
                {
                    throw new InvalidOperationException(
                        $"输入路由处理器返回了无效结果: {result}");
                }
                if (result == InputRouteResult.Handled)
                    return true;
            }
            catch (Exception exception)
            {
                ErrorHub.Report(
                    exception,
                    "InputRouter",
                    context: $"Scope={scope.DebugName}; Action={action.Value}; " +
                        $"Transitions={matched}; Sequence={state.Sequence}");
                return true;
            }
        }

        return false;
    }

    private void ApplyRevisionGateBeforeDispatch(InputFrame frame)
    {
        if (_observedContextRevision == _input.ContextRevision &&
            _observedRouteRevision == _routeRevision)
        {
            return;
        }

        GateNonIdleActions(frame, includePreviousState: true);
        _observedContextRevision = _input.ContextRevision;
        _observedRouteRevision = _routeRevision;
    }

    private void ApplyRevisionGateAfterDispatch(InputFrame frame)
    {
        if (_observedContextRevision == _input.ContextRevision &&
            _observedRouteRevision == _routeRevision)
        {
            return;
        }

        GateNonIdleActions(frame, includePreviousState: false);
        _observedContextRevision = _input.ContextRevision;
        _observedRouteRevision = _routeRevision;
    }

    private void GateNonIdleActions(InputFrame frame, bool includePreviousState)
    {
        for (int index = 0; index < _layoutActions.Length; index++)
        {
            InputActionId action = _layoutActions[index];
            if (!HasBinding(action))
                continue;
            if (frame.GetState(action).Status != InputActionStatus.Idle ||
                (includePreviousState && _hasDispatched &&
                    _previousStatuses[index] != InputActionStatus.Idle))
            {
                _retriggerGates.Add(action);
            }
        }
    }

    private void ReleaseIdleGates(InputFrame frame)
    {
        if (_retriggerGates.Count == 0)
            return;
        for (int index = 0; index < _layoutActions.Length; index++)
        {
            InputActionId action = _layoutActions[index];
            if (_retriggerGates.Contains(action) &&
                frame.GetState(action).Status == InputActionStatus.Idle)
            {
                _retriggerGates.Remove(action);
            }
        }
    }

    private bool HasBinding(InputActionId action)
    {
        for (int scopeIndex = 0; scopeIndex < _scopes.Count; scopeIndex++)
        {
            List<BindingEntry> bindings = _scopes[scopeIndex].Bindings;
            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                if (bindings[bindingIndex].Action == action)
                    return true;
            }
        }
        return false;
    }

    private void EnsureActionLayout()
    {
        IReadOnlyList<InputActionDescriptor> descriptors = _input.ActionDescriptors;
        bool same = descriptors.Count == _layoutActions.Length;
        if (same)
        {
            for (int index = 0; index < descriptors.Count; index++)
            {
                if (_layoutActions[index] != descriptors[index].ActionId)
                {
                    same = false;
                    break;
                }
            }
        }
        if (same)
            return;

        _layoutActions = new InputActionId[descriptors.Count];
        _previousStatuses = new InputActionStatus[descriptors.Count];
        for (int index = 0; index < descriptors.Count; index++)
            _layoutActions[index] = descriptors[index].ActionId;
        _retriggerGates.Clear();
        _hasDispatched = false;
    }

    private void CommitPendingMutations()
    {
        if (_pendingMutations.Count == 0)
            return;
        bool changed = false;
        for (int index = 0; index < _pendingMutations.Count; index++)
        {
            PendingMutation mutation = _pendingMutations[index];
            switch (mutation.Kind)
            {
                case PendingMutationKind.AddScope:
                    if (_scopesByToken.TryGetValue(mutation.Token, out ScopeEntry? addedScope) &&
                        !addedScope.PendingDispose)
                    {
                        _scopes.Add(addedScope);
                        changed = true;
                    }
                    break;
                case PendingMutationKind.RemoveScope:
                    if (_scopesByToken.TryGetValue(mutation.Token, out ScopeEntry? removedScope))
                        changed |= RemoveScope(removedScope);
                    break;
                case PendingMutationKind.AddBinding:
                    if (_bindingsByToken.TryGetValue(mutation.Token, out BindingEntry? addedBinding))
                    {
                        addedBinding.Scope.PendingBindings.Remove(addedBinding);
                        if (!addedBinding.PendingDispose && !addedBinding.Scope.PendingDispose)
                        {
                            addedBinding.Scope.Bindings.Add(addedBinding);
                            changed = true;
                        }
                    }
                    break;
                case PendingMutationKind.RemoveBinding:
                    if (_bindingsByToken.TryGetValue(mutation.Token, out BindingEntry? removedBinding))
                        changed |= RemoveBinding(removedBinding);
                    break;
            }
        }
        _pendingMutations.Clear();
        if (changed)
            _routeRevision++;
    }

    private bool RemoveScope(ScopeEntry scope)
    {
        bool existed = _scopes.Remove(scope);
        _scopesByToken.Remove(scope.Token);
        for (int index = 0; index < scope.Bindings.Count; index++)
            _bindingsByToken.Remove(scope.Bindings[index].Token);
        for (int index = 0; index < scope.PendingBindings.Count; index++)
            _bindingsByToken.Remove(scope.PendingBindings[index].Token);
        scope.Bindings.Clear();
        scope.PendingBindings.Clear();
        return existed;
    }

    private bool RemoveBinding(BindingEntry binding)
    {
        bool existed = binding.Scope.Bindings.Remove(binding);
        existed |= binding.Scope.PendingBindings.Remove(binding);
        _bindingsByToken.Remove(binding.Token);
        return existed;
    }

    private ulong NextToken()
    {
        if (_nextToken == ulong.MaxValue)
            throw new InputOperationException("输入路由 Token 已耗尽。");
        return ++_nextToken;
    }

    private void VerifyRunning()
    {
        if (_shutdown)
            throw new InputOperationException("InputActionRouter 已关闭。");
    }

    private static void VerifyNoOverlap(
        BindingEntry existing,
        InputActionId action,
        InputActionTransitions transitions)
    {
        if (!existing.PendingDispose &&
            existing.Action == action &&
            (existing.Transitions & transitions) != 0)
        {
            throw new InputOperationException(
                $"同一 Scope 的 Action Transition 范围重叠: {action.Value}; " +
                $"{existing.Transitions & transitions}");
        }
    }

    private static InputActionTransitions NormalizeTransitions(InputActionTransitions transitions)
    {
        if ((transitions & InputActionTransitions.Cancelled) != 0)
            transitions &= ~InputActionTransitions.Completed;
        return transitions;
    }

    private sealed class ScopeEntry
    {
        public ulong Token { get; }
        public string DebugName { get; }
        public List<BindingEntry> Bindings { get; } = new();
        public List<BindingEntry> PendingBindings { get; } = new();
        public bool PendingDispose { get; set; }

        public ScopeEntry(ulong token, string debugName)
        {
            Token = token;
            DebugName = debugName;
        }
    }

    private sealed class BindingEntry
    {
        public ulong Token { get; }
        public ScopeEntry Scope { get; }
        public InputActionId Action { get; }
        public InputActionTransitions Transitions { get; }
        public InputRouteHandler Handler { get; }
        public ulong LastInvocationSequence { get; set; }
        public bool PendingDispose { get; set; }

        public BindingEntry(
            ulong token,
            ScopeEntry scope,
            InputActionId action,
            InputActionTransitions transitions,
            InputRouteHandler handler)
        {
            Token = token;
            Scope = scope;
            Action = action;
            Transitions = transitions;
            Handler = handler;
        }
    }

    private readonly record struct PendingMutation(PendingMutationKind Kind, ulong Token);

    private enum PendingMutationKind
    {
        AddScope,
        RemoveScope,
        AddBinding,
        RemoveBinding,
    }
}
