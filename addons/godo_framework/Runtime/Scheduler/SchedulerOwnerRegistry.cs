using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>按场景 Owner 聚合调度句柄，并在 Owner 退出场景树时取消整组任务。</summary>
internal sealed class SchedulerOwnerRegistry
{
    private readonly Dictionary<Node, OwnerRegistration> _registrations = new();
    private readonly Dictionary<ulong, Node> _ownersByHandle = new();
    private readonly Func<ScheduleHandle, bool> _cancel;

    internal int OwnerCount => _registrations.Count;

    internal int HandleCount => _ownersByHandle.Count;

    internal SchedulerOwnerRegistry(Func<ScheduleHandle, bool> cancel)
    {
        _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
    }

    internal void Validate(Node? owner)
    {
        if (owner is null)
            return;
        if (!GodotObject.IsInstanceValid(owner))
            throw new ArgumentException("Scheduler Owner 已失效。", nameof(owner));
        if (!owner.IsInsideTree())
            throw new InvalidOperationException("Scheduler Owner 必须已经位于场景树中。");
    }

    internal void Track(Node? owner, ScheduleHandle handle)
    {
        if (owner is null)
            return;

        if (!_registrations.TryGetValue(owner, out OwnerRegistration? registration))
        {
            registration = new OwnerRegistration(this, owner);
            _registrations.Add(owner, registration);
        }

        registration.Handles.Add(handle);
        _ownersByHandle.Add(handle.Value, owner);
    }

    internal void Untrack(ScheduleHandle handle)
    {
        if (!_ownersByHandle.Remove(handle.Value, out Node? owner) ||
            !_registrations.TryGetValue(owner, out OwnerRegistration? registration))
        {
            return;
        }

        registration.Handles.Remove(handle);
        if (registration.Handles.Count > 0)
            return;

        _registrations.Remove(owner);
        registration.Dispose();
    }

    internal void Clear()
    {
        foreach (OwnerRegistration registration in _registrations.Values)
            registration.Dispose();

        _registrations.Clear();
        _ownersByHandle.Clear();
    }

    private void CancelOwner(OwnerRegistration registration)
    {
        if (!_registrations.Remove(registration.Owner))
            return;

        registration.Dispose();
        ScheduleHandle[] handles = new ScheduleHandle[registration.Handles.Count];
        registration.Handles.CopyTo(handles);
        for (int index = 0; index < handles.Length; index++)
            _ownersByHandle.Remove(handles[index].Value);

        for (int index = 0; index < handles.Length; index++)
            _cancel(handles[index]);
    }

    private sealed class OwnerRegistration : IDisposable
    {
        private readonly SchedulerOwnerRegistry _registry;
        private bool _isDisposed;

        public Node Owner { get; }

        public HashSet<ScheduleHandle> Handles { get; } = new();

        public OwnerRegistration(SchedulerOwnerRegistry registry, Node owner)
        {
            _registry = registry;
            Owner = owner;
            Owner.TreeExiting += OnTreeExiting;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (GodotObject.IsInstanceValid(Owner))
                Owner.TreeExiting -= OnTreeExiting;
        }

        private void OnTreeExiting() => _registry.CancelOwner(this);
    }
}
