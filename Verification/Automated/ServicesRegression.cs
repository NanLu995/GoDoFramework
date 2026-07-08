using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Services 的无交互回归验证入口。</summary>
public sealed partial class ServicesRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("缺失服务查询", VerifyMissingService);
            Run("注册与按接口获取", VerifyRegisterAndGet);
            Run("重复注册不覆盖原实例", VerifyDuplicateRegistration);
            Run("拒绝按具体类型注册", VerifyConcreteTypeRejection);
            Run("错误实例不能注销", VerifyWrongInstanceUnregister);
            Run("正确注销恢复缺失状态", VerifySuccessfulUnregister);

            GD.Print($"[ServicesRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ServicesRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[ServicesRegression] PASS: {name}");
    }

    private static void VerifyMissingService()
    {
        bool found = Services.TryGet<IMissingTestService>(out IMissingTestService? service);
        Assert(!found, "TryGet 对缺失服务返回了 true");
        Assert(service is null, "TryGet 对缺失服务没有输出 null");
        AssertThrows<InvalidOperationException>(
            static () => Services.Get<IMissingTestService>(),
            "Get 对缺失服务没有抛出 InvalidOperationException");
    }

    private static void VerifyRegisterAndGet()
    {
        var service = new TestService("first");
        try
        {
            Services.Register<ITestService>(service);

            Assert(ReferenceEquals(service, Services.Get<ITestService>()), "Get 没有返回注册实例");
            Assert(Services.TryGet<ITestService>(out ITestService? found), "TryGet 没有找到注册服务");
            Assert(ReferenceEquals(service, found), "TryGet 没有返回注册实例");
        }
        finally
        {
            Services.Unregister<ITestService>(service);
        }
    }

    private static void VerifyDuplicateRegistration()
    {
        var first = new DuplicateTestService("first");
        var second = new DuplicateTestService("second");
        try
        {
            Services.Register<IDuplicateTestService>(first);
            AssertThrows<InvalidOperationException>(
                () => Services.Register<IDuplicateTestService>(second),
                "重复注册没有抛出 InvalidOperationException");
            Assert(
                ReferenceEquals(first, Services.Get<IDuplicateTestService>()),
                "重复注册覆盖了原实例");
        }
        finally
        {
            Services.Unregister<IDuplicateTestService>(first);
        }
    }

    private static void VerifyConcreteTypeRejection()
    {
        var service = new ConcreteTestService();
        AssertThrows<ArgumentException>(
            () => Services.Register(service),
            "按具体类型注册没有抛出 ArgumentException");
    }

    private static void VerifyWrongInstanceUnregister()
    {
        var registered = new WrongInstanceTestService();
        var other = new WrongInstanceTestService();
        try
        {
            Services.Register<IWrongInstanceTestService>(registered);

            Assert(
                !Services.Unregister<IWrongInstanceTestService>(other),
                "错误实例注销返回了 true");
            Assert(
                ReferenceEquals(registered, Services.Get<IWrongInstanceTestService>()),
                "错误实例注销移除了原服务");
        }
        finally
        {
            Services.Unregister<IWrongInstanceTestService>(registered);
        }
    }

    private static void VerifySuccessfulUnregister()
    {
        var service = new UnregisterTestService();
        Services.Register<IUnregisterTestService>(service);

        Assert(Services.Unregister<IUnregisterTestService>(service), "正确实例注销返回了 false");
        Assert(
            !Services.TryGet<IUnregisterTestService>(out _),
            "注销后服务仍可被 TryGet 获取");
        Assert(
            !Services.Unregister<IUnregisterTestService>(service),
            "重复注销返回了 true");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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

    private interface IMissingTestService;
    private interface ITestService;
    private interface IDuplicateTestService;
    private interface IWrongInstanceTestService;
    private interface IUnregisterTestService;

    private sealed class TestService : ITestService
    {
        public string Name { get; }
        public TestService(string name) => Name = name;
    }

    private sealed class DuplicateTestService : IDuplicateTestService
    {
        public string Name { get; }
        public DuplicateTestService(string name) => Name = name;
    }

    private sealed class ConcreteTestService;
    private sealed class WrongInstanceTestService : IWrongInstanceTestService;
    private sealed class UnregisterTestService : IUnregisterTestService;
}
