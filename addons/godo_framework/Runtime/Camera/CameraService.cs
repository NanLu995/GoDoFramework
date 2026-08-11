using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>管理场景侧主镜头 Rig 的注册、激活和恢复历史。</summary>
public sealed class CameraService : ICameraService
{
    private readonly Dictionary<CameraId, List<RigRegistration>> _registrations = new();
    private readonly Stack<RigRegistration> _history = new();
    private RigRegistration? _activePrimary;

    /// <summary>当前仍然有效的主镜头 ID；没有活动镜头或其 Rig 已失效时为 null。</summary>
    /// <exception cref="InvalidOperationException">当前调用不在 GoDoRuntime 记录的 Godot 主线程。</exception>
    public CameraId? ActivePrimary
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return IsUsable(_activePrimary) ? _activePrimary.Driver.CameraId : null;
        }
    }

    /// <summary>激活指定主镜头；完整切换成功后记录此前的不同镜头实例以供恢复。</summary>
    /// <param name="id">已经注册的非默认镜头 ID。</param>
    /// <exception cref="InvalidOperationException">当前调用不在 GoDoRuntime 记录的 Godot 主线程。</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> 是默认的空 ID。</exception>
    /// <exception cref="CameraOperationException">
    /// 没有可用的匹配 Rig，或目标激活、当前镜头停用失败。目标激活失败时当前镜头保持不变；
    /// 当前镜头停用失败时会尝试停用目标以回滚。
    /// </exception>
    public void ActivatePrimary(CameraId id)
    {
        MainThreadGuard.VerifyAccess();
        VerifyId(id);
        PruneInvalidRegistrations();

        RigRegistration target = ResolveLatest(id);
        if (ReferenceEquals(target, _activePrimary))
            return;

        bool shouldRememberCurrent =
            IsUsable(_activePrimary) && _activePrimary.Driver.CameraId != target.Driver.CameraId;
        SwitchTo(target, shouldRememberCurrent);
    }

    /// <summary>恢复最近一个仍注册且有效的具体主镜头实例，并跳过失效历史。</summary>
    /// <returns>成功恢复一个历史镜头时为 <see langword="true"/>；没有可恢复实例时为 <see langword="false"/>。</returns>
    /// <exception cref="InvalidOperationException">当前调用不在 GoDoRuntime 记录的 Godot 主线程。</exception>
    /// <exception cref="CameraOperationException">历史目标激活或当前镜头停用失败；失败项仍保留在恢复历史中。</exception>
    public bool RestorePreviousPrimary()
    {
        MainThreadGuard.VerifyAccess();
        PruneInvalidRegistrations();

        while (_history.Count > 0)
        {
            RigRegistration target = _history.Peek();
            if (!IsRegistered(target) || ReferenceEquals(target, _activePrimary))
            {
                _history.Pop();
                continue;
            }

            SwitchTo(target, rememberCurrent: false);
            _history.Pop();
            return true;
        }

        return false;
    }

    internal void Register(ICameraRigDriver driver, Node sceneScopeRoot)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(sceneScopeRoot);
        VerifyId(driver.CameraId);

        PruneInvalidRegistrations();
        if (!_registrations.TryGetValue(driver.CameraId, out List<RigRegistration>? registrations))
        {
            registrations = new List<RigRegistration>();
            _registrations.Add(driver.CameraId, registrations);
        }

        for (int index = 0; index < registrations.Count; index++)
        {
            RigRegistration existing = registrations[index];
            if (ReferenceEquals(existing.Driver, driver))
                throw new CameraOperationException(driver.CameraId, $"镜头 Rig 已注册: {driver.CameraId.Value}");
            if (ReferenceEquals(existing.SceneScopeRoot, sceneScopeRoot))
            {
                throw new CameraOperationException(
                    driver.CameraId,
                    $"同一场景范围内存在重复镜头 ID: {driver.CameraId.Value}");
            }
        }

        registrations.Add(new RigRegistration(driver, sceneScopeRoot));
    }

    internal void Unregister(ICameraRigDriver driver)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(driver);

        if (!_registrations.TryGetValue(driver.CameraId, out List<RigRegistration>? registrations))
            return;

        for (int index = registrations.Count - 1; index >= 0; index--)
        {
            RigRegistration registration = registrations[index];
            if (!ReferenceEquals(registration.Driver, driver))
                continue;

            registrations.RemoveAt(index);
            if (ReferenceEquals(_activePrimary, registration))
                _activePrimary = null;
        }

        if (registrations.Count == 0)
            _registrations.Remove(driver.CameraId);
    }

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        _activePrimary = null;
        _history.Clear();
        _registrations.Clear();
    }

    private void SwitchTo(RigRegistration target, bool rememberCurrent)
    {
        RigRegistration? previous = IsUsable(_activePrimary) ? _activePrimary : null;
        try
        {
            target.Driver.Activate();
        }
        catch (Exception exception)
        {
            throw new CameraOperationException(
                target.Driver.CameraId,
                $"激活主镜头失败，当前镜头保持不变: {target.Driver.CameraId.Value}",
                exception);
        }

        if (previous != null)
        {
            try
            {
                previous.Driver.Deactivate();
            }
            catch (Exception exception)
            {
                TryRollbackTarget(target);
                throw new CameraOperationException(
                    previous.Driver.CameraId,
                    $"停用当前主镜头失败，已尝试撤销目标镜头: {previous.Driver.CameraId.Value}",
                    exception);
            }
        }

        if (rememberCurrent && previous != null)
            _history.Push(previous);

        _activePrimary = target;
    }

    private static void TryRollbackTarget(RigRegistration target)
    {
        try
        {
            target.Driver.Deactivate();
        }
        catch
        {
            // 保留原始停用失败作为主异常；适配器必须保证停用操作可重复调用。
        }
    }

    private RigRegistration ResolveLatest(CameraId id)
    {
        if (!_registrations.TryGetValue(id, out List<RigRegistration>? registrations))
            throw new CameraOperationException(id, $"主镜头尚未注册: {id.Value}");

        for (int index = registrations.Count - 1; index >= 0; index--)
        {
            RigRegistration registration = registrations[index];
            if (IsUsable(registration))
                return registration;
        }

        throw new CameraOperationException(id, $"主镜头已失效: {id.Value}");
    }

    private void PruneInvalidRegistrations()
    {
        var emptyIds = new List<CameraId>();
        foreach ((CameraId id, List<RigRegistration> registrations) in _registrations)
        {
            for (int index = registrations.Count - 1; index >= 0; index--)
            {
                RigRegistration registration = registrations[index];
                if (IsUsable(registration))
                    continue;

                registrations.RemoveAt(index);
                if (ReferenceEquals(_activePrimary, registration))
                    _activePrimary = null;
            }

            if (registrations.Count == 0)
                emptyIds.Add(id);
        }

        for (int index = 0; index < emptyIds.Count; index++)
            _registrations.Remove(emptyIds[index]);
    }

    private bool IsRegistered(RigRegistration registration)
    {
        if (!IsUsable(registration) ||
            !_registrations.TryGetValue(registration.Driver.CameraId, out List<RigRegistration>? registrations))
        {
            return false;
        }

        return registrations.Contains(registration);
    }

    private static bool IsUsable([NotNullWhen(true)] RigRegistration? registration) =>
        registration != null &&
        GodotObject.IsInstanceValid(registration.Driver.LifetimeOwner) &&
        GodotObject.IsInstanceValid(registration.SceneScopeRoot);

    private static void VerifyId(CameraId id)
    {
        if (id.IsEmpty)
            throw new ArgumentException("镜头 ID 不能是默认值。", nameof(id));
    }

    private sealed class RigRegistration
    {
        public ICameraRigDriver Driver { get; }
        public Node SceneScopeRoot { get; }

        public RigRegistration(ICameraRigDriver driver, Node sceneScopeRoot)
        {
            Driver = driver;
            SceneScopeRoot = sceneScopeRoot;
        }
    }
}
