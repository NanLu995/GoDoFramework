using System;
using Godot;
using GoDo;

namespace GoDoFramework.Verification;

public sealed partial class UiConfigurableControl : Control, IPoolable
{
    public string ConfiguredValue { get; set; } = string.Empty;

    public bool WasConfiguredBeforeReady { get; private set; }

    public string ConfiguredValueAtLastEnterTree { get; private set; } = string.Empty;

    public string ConfiguredValueAtLastAcquire { get; private set; } = string.Empty;

    public int ReadyCount { get; private set; }

    public int EnterTreeCount { get; private set; }

    public int AcquireCount { get; private set; }

    public int ReleaseCount { get; private set; }

    public bool ThrowOnAcquire { get; set; }

    public bool ThrowOnRelease { get; set; }

    public override void _EnterTree()
    {
        EnterTreeCount++;
        ConfiguredValueAtLastEnterTree = ConfiguredValue;
    }

    public override void _Ready()
    {
        ReadyCount++;
        WasConfiguredBeforeReady = ConfiguredValue == "configured";
    }

    public void OnAcquire()
    {
        AcquireCount++;
        ConfiguredValueAtLastAcquire = ConfiguredValue;
        if (ThrowOnAcquire)
            throw new InvalidOperationException("acquire failed");
    }

    public void OnRelease()
    {
        ReleaseCount++;
        if (ThrowOnRelease)
            throw new InvalidOperationException("release failed");

        ConfiguredValue = string.Empty;
    }
}
