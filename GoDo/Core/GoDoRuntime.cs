using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// GoDo 框架的全局运行时入口。
/// <para>
/// 本节点由 <c>GoDoRuntime.tscn</c> 作为 Autoload 创建，在整个游戏进程中常驻，
/// 负责安装进程级异常监听，并在退出时对称清理。
/// </para>
/// <para>
/// 这里只管理框架生命周期，不承载具体游戏流程。后续框架服务应从这里统一初始化，
/// 不要分散到业务场景或测试场景中。
/// </para>
/// </summary>
public sealed partial class GoDoRuntime : Node
{
    private static GoDoRuntime? _instance;
    private bool _subscribed;
    private SceneService? _sceneService;
    private AudioService? _audioService;
    private SaveService? _saveService;
    private SettingsService? _settingsService;

    [Export]
    public NodePath SceneServicePath { get; set; } = null!;

    [Export]
    public NodePath AudioServicePath { get; set; } = null!;

    public override void _EnterTree()
    {
        if (IsInstanceValid(_instance) && _instance != this)
        {
            ErrorHub.Warn(
                "检测到重复的框架运行时入口，已释放后创建的实例",
                "Runtime",
                context: GetPath().ToString());
            QueueFree();
            return;
        }

        _instance = this;
        MainThreadGuard.Initialize();
        ResourceHub.Initialize();
    }

    public override void _Ready()
    {
        if (_instance != this || _subscribed)
            return;

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        _subscribed = true;

        _sceneService = GetNodeOrNull<SceneService>(SceneServicePath);
        if (!IsInstanceValid(_sceneService))
            throw new InvalidOperationException("GoDoRuntime 未配置 SceneService 子节点。");

        _audioService = GetNodeOrNull<AudioService>(AudioServicePath);
        if (!IsInstanceValid(_audioService) || !_audioService.IsInitialized)
            throw new InvalidOperationException("GoDoRuntime 未配置或未能初始化 AudioService 子节点。");

        Services.Register<ISceneService>(_sceneService);
        Services.Register<IAudioService>(_audioService);
        _saveService = new SaveService();
        Services.Register<ISaveService>(_saveService);
        _settingsService = new SettingsService(_audioService, _saveService);
        Services.Register<ISettingsService>(_settingsService);

        ErrorHub.Debug("GoDo 运行时初始化完成", "Runtime");
    }

    public override void _Process(double delta)
    {
        ResourceHub.Update();
        ErrorHub.FlushPending();
    }

    public override void _ExitTree()
    {
        if (_subscribed)
        {
            AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
            _subscribed = false;
        }

        if (_instance == this)
        {
            if (_settingsService != null)
                Services.Unregister<ISettingsService>(_settingsService);
            if (_saveService != null)
                Services.Unregister<ISaveService>(_saveService);
            if (IsInstanceValid(_audioService))
                Services.Unregister<IAudioService>(_audioService);
            if (IsInstanceValid(_sceneService))
                Services.Unregister<ISceneService>(_sceneService);

            Services.Clear();
            ResourceHub.Shutdown();
            ErrorHub.Shutdown();
            _instance = null;
            _sceneService = null;
            _audioService = null;
            _saveService = null;
            _settingsService = null;
            MainThreadGuard.Reset();
        }
    }

    private static void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ErrorHub.Fatal(
                exception,
                module: "Runtime",
                context: e.IsTerminating ? "UnhandledException; IsTerminating=true" : "UnhandledException");
            return;
        }

        ErrorHub.Fatal(
            $"未知未处理异常对象: {e.ExceptionObject}",
            module: "Runtime",
            context: e.IsTerminating ? "UnhandledException; IsTerminating=true" : "UnhandledException");
    }
}
