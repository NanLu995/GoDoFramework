using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>InputService 的 GoDoRuntime 注册、自动采样与关闭回归入口。</summary>
public sealed partial class InputRuntimeRegression : Node
{
    private static readonly InputActionId Confirm = InputActionId.Create("ui.confirm");

    /// <inheritdoc />
    public override async void _Ready()
    {
        InputService? service = null;
        try
        {
            service = Services.Get<IInputService>() as InputService ??
                throw new InvalidOperationException("IInputService 不是 InputService 实例。");
            Assert(!service.IsReady, "未安装后端时运行时 InputService 已就绪");

            var backend = new RuntimeFakeBackend();
            service.InstallBackend(backend);
            AssertThrows<InputOperationException>(() => _ = service.Frame, "运行时首次采样前仍能读取 Frame");

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            InputFrame frame = service.Frame;
            Assert(backend.SampleCount >= 1, "GoDoRuntime 没有自动采样输入后端");
            Assert(frame.Pressed(Confirm), "运行时采样没有提交后端状态");
            Assert(frame.Sequence >= 1, "运行时采样没有推进 Frame 序号");

            service.Shutdown();
            AssertEqual(1, backend.ShutdownCount, "InputService 没有关闭运行时后端一次");

            GD.Print("[InputRuntimeRegression] PASS (4/4)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            service?.Shutdown();
            GD.PushError($"[InputRuntimeRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class RuntimeFakeBackend : IInputBackend
    {
        private static readonly InputActionDescriptor[] ActionDescriptors =
        {
            new(Confirm, InputActionValueType.Bool),
        };

        public InputBackendCapabilities Capabilities => InputBackendCapabilities.None;
        public InputDeviceKind ActiveDevice => InputDeviceKind.KeyboardMouse;
        public IReadOnlyList<InputActionDescriptor> Actions => ActionDescriptors;
        public IReadOnlyList<InputContextId> Contexts => Array.Empty<InputContextId>();
        public int SampleCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void Initialize()
        {
        }

        public void ApplyContexts(ReadOnlySpan<InputContextId> contexts)
        {
            if (!contexts.IsEmpty)
                throw new InvalidOperationException("测试后端不包含 Context。");
        }

        public void Sample(Span<InputActionSample> destination)
        {
            SampleCount++;
            destination[0] = new InputActionSample(Vector3.One, pressed: true);
        }

        public void Shutdown() => ShutdownCount++;
    }
}
