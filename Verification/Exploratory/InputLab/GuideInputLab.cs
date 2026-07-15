using System;
using System.Collections.Generic;
using Godot;
using GuideCs;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>G.U.I.D.E-CSharp 作为 GoDo 输入后端候选的隔离验证入口。</summary>
public sealed partial class GuideInputLab : Node
{
    private const string Root = "res://Verification/Exploratory/InputLab/Resources";
    private const string RemappingPath = "user://godo-guide-input-lab-remapping.tres";

    private int _passed;
    private int _virtualDeviceId;
    private Node _guideNode = null!;
    private GodotObject _inputState = null!;
    private GuideAction _look = null!;
    private GuideAction _jump = null!;
    private GuideAction _confirm = null!;
    private GuideMappingContext _gameplay = null!;
    private GuideMappingContext _menu = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            LoadResources();
            ConnectVirtualJoypad();
            Run("Gameplay/UI 上下文隔离", VerifyContextIsolation);
            Run("鼠标与手柄统一 Look Vector2", VerifyUnifiedLook);
            Run("重绑定冲突查询", VerifyCollisionQuery);
            Run("重绑定配置保存与恢复", VerifyRemappingPersistence);
            MeasureHotReadAllocations();

            GD.Print($"[GuideInputLab] PASS ({_passed}/4)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[GuideInputLab] FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            if (_virtualDeviceId != 0 && GodotObject.IsInstanceValid(_inputState))
                _inputState.Call("disconnect_virtual_stick", _virtualDeviceId);

            string absolutePath = ProjectSettings.GlobalizePath(RemappingPath);
            if (FileAccess.FileExists(RemappingPath))
                DirAccess.RemoveAbsolute(absolutePath);
        }
    }

    private void LoadResources()
    {
        _guideNode = GetNode("/root/GUIDE");
        _inputState = _guideNode.Get("_input_state").AsGodotObject();
        _look = Wrap<GuideAction>($"{Root}/look.tres");
        _jump = Wrap<GuideAction>($"{Root}/jump.tres");
        _confirm = Wrap<GuideAction>($"{Root}/confirm.tres");
        _gameplay = Wrap<GuideMappingContext>($"{Root}/gameplay_context.tres");
        _menu = Wrap<GuideMappingContext>($"{Root}/menu_context.tres");
    }

    private void ConnectVirtualJoypad()
    {
        _virtualDeviceId = _inputState.Call("connect_virtual_stick", 0).AsInt32();
        Assert(_virtualDeviceId == -2, "InputLab 第一个虚拟手柄设备 ID 不是 -2");
    }

    private void VerifyContextIsolation()
    {
        Guide.EnableMappingContext(_gameplay, disableOthers: true);
        InjectKey(Key.Space, pressed: true);
        Evaluate();
        Assert(_jump.ValueBool, "Gameplay 上下文没有触发 Jump");
        Assert(!_confirm.ValueBool, "Menu 未启用时错误触发 Confirm");
        InjectKey(Key.Space, pressed: false);
        Evaluate();

        Guide.EnableMappingContext(_menu, disableOthers: true);
        InjectKey(Key.Space, pressed: true);
        Evaluate();
        Assert(_confirm.ValueBool, "Menu 上下文没有触发 Confirm");
        Assert(!_jump.ValueBool, "Gameplay 被替换后仍触发 Jump");
        InjectKey(Key.Space, pressed: false);
        Evaluate();
    }

    private void VerifyUnifiedLook()
    {
        Guide.EnableMappingContext(_gameplay, disableOthers: true);
        Guide.InjectInput(new InputEventMouseMotion { Relative = new Vector2(8f, -4f) });
        Evaluate();
        Assert(_look.ValueAxis2d.IsEqualApprox(new Vector2(8f, -4f)), "鼠标相对位移没有输出预期 Look Vector2");

        _inputState.Call("_reset");
        Guide.InjectInput(new InputEventJoypadMotion
        {
            Device = _virtualDeviceId,
            Axis = JoyAxis.RightX,
            AxisValue = 0.6f,
        });
        Guide.InjectInput(new InputEventJoypadMotion
        {
            Device = _virtualDeviceId,
            Axis = JoyAxis.RightY,
            AxisValue = -0.4f,
        });
        Evaluate();
        Vector2 joyValue = _look.ValueAxis2d;
        Assert(joyValue.X > 0.4f && joyValue.Y < -0.2f, "手柄右摇杆没有输出经过死区处理的 Look Vector2");
    }

    private void VerifyCollisionQuery()
    {
        var remapper = new GuideRemapper();
        remapper.Initialize(new List<GuideMappingContext> { _gameplay, _menu }, new GuideRemappingConfig());
        List<ConfigItem> items = remapper.GetRemappableItems(_gameplay, action: _jump);
        Assert(items.Count == 1, "Jump 可重绑定项数量错误");

        var space = new GuideInputKey { Key = Key.Space };
        Assert(remapper.GetInputCollisions(items[0], space).Count == 1, "同一绑定没有被识别为冲突");
    }

    private void VerifyRemappingPersistence()
    {
        var remapper = new GuideRemapper();
        remapper.Initialize(new List<GuideMappingContext> { _gameplay }, new GuideRemappingConfig());
        ConfigItem item = remapper.GetRemappableItems(_gameplay, action: _jump)[0];
        remapper.SetBoundInput(item, new GuideInputKey { Key = Key.J });

        GuideRemappingConfig changed = remapper.GetMappingConfig();
        if (changed.BaseGuideObject is not Resource resource)
            throw new InvalidOperationException("重绑定配置后端不是 Resource。");

        Error saveError = ResourceSaver.Save(resource, RemappingPath);
        Assert(saveError == Error.Ok, $"保存重绑定配置失败: {saveError}");

        Resource loadedResource = ResourceLoader.Load<Resource>(RemappingPath) ??
            throw new InvalidOperationException("无法重新加载重绑定配置。");
        var loaded = new GuideRemappingConfig(loadedResource);
        Guide.SetRemappingConfig(loaded);

        var verifier = new GuideRemapper();
        verifier.Initialize(new List<GuideMappingContext> { _gameplay }, loaded);
        ConfigItem loadedItem = verifier.GetRemappableItems(_gameplay, action: _jump)[0];
        GuideInput bound = verifier.GetBoundInputOrNull(loadedItem);
        Assert(bound is GuideInputKey key && key.Key == Key.J, "重新加载后 Jump 没有保持 J 键绑定");
    }

    private void MeasureHotReadAllocations()
    {
        _ = _look.ValueAxis2d;
        long before = GC.GetAllocatedBytesForCurrentThread();
        Vector2 value = default;
        for (int index = 0; index < 10_000; index++)
            value = _look.ValueAxis2d;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(value);
        GD.Print($"[GuideInputLab] PERF: ValueAxis2d 10000 次读取分配 {allocated} bytes");
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[GuideInputLab] PASS: {name}");
    }

    private void Evaluate() => _guideNode.Call("_process", 1.0 / 60.0);

    private static void InjectKey(Key key, bool pressed)
    {
        Guide.InjectInput(new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
            Pressed = pressed,
        });
    }

    private static T Wrap<T>(string path)
        where T : GuideResource
    {
        Resource resource = ResourceLoader.Load<Resource>(path) ??
            throw new InvalidOperationException($"无法加载 InputLab 资源: {path}");
        return Utility.GetCachedOrNew<T>(resource);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
