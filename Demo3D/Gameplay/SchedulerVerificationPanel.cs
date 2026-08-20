using System;
using System.Threading;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>Demo3D 中用于人工验收 Scheduler 真实时间行为的最小面板。</summary>
public sealed partial class SchedulerVerificationPanel : PanelContainer
{
    private const double CounterIntervalSeconds = 0.5d;

    [Export] public NodePath ClockStatusLabelPath { get; set; } = null!;
    [Export] public NodePath RuntimeStatusLabelPath { get; set; } = null!;
    [Export] public NodePath OperationStatusLabelPath { get; set; } = null!;
    [Export] public NodePath StartButtonPath { get; set; } = null!;
    [Export] public NodePath StopButtonPath { get; set; } = null!;
    [Export] public NodePath PauseButtonPath { get; set; } = null!;
    [Export] public NodePath SlowMotionButtonPath { get; set; } = null!;
    [Export] public NodePath OwnerCleanupButtonPath { get; set; } = null!;
    [Export] public NodePath StallButtonPath { get; set; } = null!;

    private Label? _clockStatusLabel;
    private Label? _runtimeStatusLabel;
    private Label? _operationStatusLabel;
    private Button? _startButton;
    private Button? _stopButton;
    private Button? _pauseButton;
    private Button? _slowMotionButton;
    private Button? _ownerCleanupButton;
    private Button? _stallButton;
    private ISchedulerService? _scheduler;
    private ScheduleHandle _gameTimeHandle;
    private ScheduleHandle _unscaledGameTimeHandle;
    private ScheduleHandle _realTimeHandle;
    private int _gameTimeCount;
    private int _unscaledGameTimeCount;
    private int _realTimeCount;

    /// <inheritdoc />
    public override void _Ready()
    {
        _clockStatusLabel = RequireNode<Label>(ClockStatusLabelPath, "时钟状态标签");
        _runtimeStatusLabel = RequireNode<Label>(RuntimeStatusLabelPath, "运行状态标签");
        _operationStatusLabel = RequireNode<Label>(OperationStatusLabelPath, "操作状态标签");
        _startButton = RequireNode<Button>(StartButtonPath, "开始按钮");
        _stopButton = RequireNode<Button>(StopButtonPath, "停止按钮");
        _pauseButton = RequireNode<Button>(PauseButtonPath, "场景暂停按钮");
        _slowMotionButton = RequireNode<Button>(SlowMotionButtonPath, "慢动作按钮");
        _ownerCleanupButton = RequireNode<Button>(OwnerCleanupButtonPath, "Owner 清理按钮");
        _stallButton = RequireNode<Button>(StallButtonPath, "卡帧按钮");
        _scheduler = Services.Get<ISchedulerService>();

        _startButton.Pressed += OnStartPressed;
        _stopButton.Pressed += OnStopPressed;
        _pauseButton.Pressed += OnPausePressed;
        _slowMotionButton.Pressed += OnSlowMotionPressed;
        _ownerCleanupButton.Pressed += OnOwnerCleanupPressed;
        _stallButton.Pressed += OnStallPressed;
        RefreshStatus();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (IsInstanceValid(_startButton))
            _startButton!.Pressed -= OnStartPressed;
        if (IsInstanceValid(_stopButton))
            _stopButton!.Pressed -= OnStopPressed;
        if (IsInstanceValid(_pauseButton))
            _pauseButton!.Pressed -= OnPausePressed;
        if (IsInstanceValid(_slowMotionButton))
            _slowMotionButton!.Pressed -= OnSlowMotionPressed;
        if (IsInstanceValid(_ownerCleanupButton))
            _ownerCleanupButton!.Pressed -= OnOwnerCleanupPressed;
        if (IsInstanceValid(_stallButton))
            _stallButton!.Pressed -= OnStallPressed;

        StopCounters();
        GetTree().Paused = false;
        Engine.TimeScale = 1d;
        _scheduler = null;
    }

    private void OnStartPressed()
    {
        StopCounters();
        _gameTimeCount = 0;
        _unscaledGameTimeCount = 0;
        _realTimeCount = 0;

        _gameTimeHandle = _scheduler!.ScheduleRepeating(
            CounterIntervalSeconds,
            () => IncrementCounter(ref _gameTimeCount),
            new ScheduleOptions(owner: this));
        _unscaledGameTimeHandle = _scheduler.ScheduleRepeating(
            CounterIntervalSeconds,
            () => IncrementCounter(ref _unscaledGameTimeCount),
            new ScheduleOptions(ScheduleClock.UnscaledGameTime, owner: this));
        _realTimeHandle = _scheduler.ScheduleRepeating(
            CounterIntervalSeconds,
            () => IncrementCounter(ref _realTimeCount),
            new ScheduleOptions(ScheduleClock.RealTime, owner: this));

        _operationStatusLabel!.Text = "计数已开始。请测试慢动作、场景暂停和窗口最小化。";
        RefreshStatus();
    }

    private void OnStopPressed()
    {
        StopCounters();
        _operationStatusLabel!.Text = "计数已停止。";
        RefreshStatus();
    }

    private void OnPausePressed()
    {
        GetTree().Paused = !GetTree().Paused;
        _operationStatusLabel!.Text = GetTree().Paused
            ? "场景已暂停：只有真实时间应继续计数。"
            : "场景已恢复：三种时钟都应继续计数。";
        RefreshStatus();
    }

    private void OnSlowMotionPressed()
    {
        Engine.TimeScale = Engine.TimeScale < 0.99d ? 1d : 0.25d;
        _operationStatusLabel!.Text = Engine.TimeScale < 0.99d
            ? "慢动作 x0.25：只有游戏时间应明显变慢。"
            : "时间倍率已恢复为 x1.00。";
        RefreshStatus();
    }

    private void OnOwnerCleanupPressed()
    {
        var owner = new Node();
        AddChild(owner);
        bool callbackTriggered = false;
        ScheduleHandle handle = _scheduler!.Schedule(
            0.5d,
            () => callbackTriggered = true,
            new ScheduleOptions(owner: owner));
        RemoveChild(owner);
        owner.Free();

        bool canceled = !_scheduler.IsScheduled(handle);
        _operationStatusLabel!.Text = canceled && !callbackTriggered
            ? "Owner 清理通过：关联的调度回调已取消。"
            : "Owner 清理失败：请检查 Scheduler 状态。";
    }

    private async void OnStallPressed()
    {
        _stallButton!.Disabled = true;
        _operationStatusLabel!.Text = "下一帧将模拟 750 毫秒主线程卡顿。";
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Thread.Sleep(750);

        if (!IsInsideTree())
            return;
        _stallButton.Disabled = false;
        _operationStatusLabel.Text = "卡顿已结束。请确认回调恢复后没有持续集中爆发。";
        RefreshStatus();
    }

    private void IncrementCounter(ref int counter)
    {
        counter++;
        RefreshStatus();
    }

    private void StopCounters()
    {
        Cancel(ref _gameTimeHandle);
        Cancel(ref _unscaledGameTimeHandle);
        Cancel(ref _realTimeHandle);
    }

    private void Cancel(ref ScheduleHandle handle)
    {
        if (_scheduler != null && handle.IsValid)
            _scheduler.Cancel(handle);
        handle = default;
    }

    private void RefreshStatus()
    {
        _clockStatusLabel!.Text =
            $"游戏时间：{_gameTimeCount} | 非缩放时间：{_unscaledGameTimeCount} | 真实时间：{_realTimeCount}";
        _runtimeStatusLabel!.Text =
            $"场景暂停：{(GetTree().Paused ? "是" : "否")} | 时间倍率：{Engine.TimeScale:0.00}";
        _pauseButton!.Text = GetTree().Paused ? "恢复场景" : "暂停场景";
        _slowMotionButton!.Text = Engine.TimeScale < 0.99d ? "恢复 x1.00" : "慢动作 x0.25";
    }

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!IsInstanceValid(node))
            throw new InvalidOperationException($"SchedulerVerificationPanel 缺少{description}引用。");

        return node!;
    }
}
