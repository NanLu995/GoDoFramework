using Godot;
using System;
using System.Diagnostics;
using System.Threading;
using GoDo;
using GoDoFramework.Example.Events;
using GoDoFramework.Tests;

public partial class TestScene : Node2D
{
	private int _throwingListenerCount;
	private int _healthyListenerCount;
	private int _recursiveListenerCount;

	[Export]
	public PackedScene PoolTestScene { get; set; } = null!;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		EventChannel.Bind<GameOverEvent>(this, OnGameOver);
		RunErrorHubVerification();
		RunPoolVerification();
	}

	private void OnGameOver(GameOverEvent evt)
	{
		GD.Print("OnGameOver ", evt);
	}

	private void RunPoolVerification()
	{
		using var pool = new NodePool<PoolTestNode>(
			PoolTestScene,
			initialSize: 2,
			idleCapacity: 2);

		Assert(pool.IdleCount == 2, "初始化后应有 2 个空闲节点。");
		Assert(pool.ActiveCount == 0, "初始化后不应有活动节点。");

		PoolTestNode first = pool.Acquire(this);
		Assert(first.GetParent() == this, "Acquire 后节点应加入指定父节点。");
		Assert(first.AcquireCount == 1, "Acquire 应调用一次 OnAcquire。");

		Assert(pool.Release(first), "首次 Release 应成功。");
		Assert(first.GetParent() == null, "Release 后节点应离开场景树。");
		Assert(first.ReleaseCount == 1, "Release 应调用一次 OnRelease。");

		PoolTestNode reused = pool.Acquire(this);
		Assert(ReferenceEquals(first, reused), "再次 Acquire 应优先复用刚释放的节点。");
		Assert(reused.AcquireCount == 2, "复用节点应再次调用 OnAcquire。");

		PoolTestNode second = pool.Acquire(this);
		PoolTestNode overflow = pool.Acquire(this);
		Assert(pool.ActiveCount == 3, "活动节点数量可以超过空闲区容量。");

		Assert(pool.Release(reused), "复用节点释放应成功。");
		Assert(pool.Release(second), "第二个节点释放应成功。");
		Assert(pool.Release(overflow), "超出空闲区容量的节点也应完成释放流程。");
		Assert(pool.IdleCount == 2, "空闲节点不应超过 idleCapacity。");
		Assert(pool.ActiveCount == 0, "全部释放后活动节点数量应为 0。");
		Assert(!pool.Release(overflow), "重复释放必须被拒绝。");

		pool.Clear();
		Assert(pool.IdleCount == 0, "Clear 应移除全部空闲节点。");

		RunPoolFailureVerification();
		RunPoolStressVerification();

		GD.Print("[PoolTest] PASS：基础行为、异常路径、Dispose 和压力循环验证通过。");
	}

	private void RunErrorHubVerification()
	{
		var throwingReporter = new ErrorHubTestReporter(throwOnReport: true);
		var healthyReporter = new ErrorHubTestReporter(throwOnReport: false);
		ErrorLevel originalMinLevel = ErrorHub.MinLevel;

		ErrorHub.OnError += OnThrowingError;
		ErrorHub.OnError += OnHealthyError;
		ErrorHub.OnError += OnRecursiveError;
		ErrorHub.AddReporter(throwingReporter);
		ErrorHub.AddReporter(healthyReporter);

		try
		{
			ErrorHub.Warn("预期的异常隔离验证报告", "ErrorHubTest");
			Assert(_throwingListenerCount == 1, "抛异常的监听者应执行一次。");
			Assert(_healthyListenerCount == 1, "前一个监听者异常后，健康监听者仍应执行。");
			Assert(_recursiveListenerCount == 1, "递归监听者应执行一次且不产生无限递归。");
			Assert(throwingReporter.ReportCount == 1, "抛异常的 Reporter 应执行一次。");
			Assert(healthyReporter.ReportCount == 1, "前一个 Reporter 异常后，健康 Reporter 仍应执行。");

			var backgroundThread = new Thread(ReportFromBackgroundThread);
			backgroundThread.Start();
			backgroundThread.Join();
			Assert(_healthyListenerCount == 1, "后台线程报告不应直接调用主线程监听者。");

			ErrorHub.FlushPending();
			Assert(_healthyListenerCount == 2, "主线程 Flush 后应分发后台报告。");
			Assert(healthyReporter.ReportCount == 2, "后台报告应在主线程交给 Reporter。");

			ErrorHub.MinLevel = ErrorLevel.Error;
			ErrorHub.Warn("这条报告应被等级过滤", "ErrorHubTest");
			Assert(_healthyListenerCount == 2, "低于 MinLevel 的报告不应进入监听者。");
		}
		finally
		{
			ErrorHub.MinLevel = originalMinLevel;
			ErrorHub.OnError -= OnThrowingError;
			ErrorHub.OnError -= OnHealthyError;
			ErrorHub.OnError -= OnRecursiveError;
			ErrorHub.RemoveReporter(throwingReporter);
			ErrorHub.RemoveReporter(healthyReporter);
		}

		GD.Print("[ErrorHubTest] PASS：监听者隔离、Reporter 隔离、递归保护、后台队列和等级过滤验证通过。");
	}

	private static void ReportFromBackgroundThread()
	{
		ErrorHub.Warn("后台线程队列验证报告", "ErrorHubTest");
	}

	private void OnThrowingError(ErrorReport report)
	{
		_throwingListenerCount++;
		throw new InvalidOperationException("[ErrorHubTest] 预期的监听者异常。");
	}

	private void OnHealthyError(ErrorReport report)
	{
		_healthyListenerCount++;
	}

	private void OnRecursiveError(ErrorReport report)
	{
		_recursiveListenerCount++;
		ErrorHub.Warn("预期的递归保护验证报告", "ErrorHubTest");
	}

	private void RunPoolFailureVerification()
	{
		using (var callbackFailurePool = new NodePool<PoolTestNode>(PoolTestScene, idleCapacity: 1))
		{
			PoolTestNode callbackFailureNode = callbackFailurePool.Acquire(this);
			callbackFailureNode.ThrowOnRelease = true;

			bool exceptionThrown = false;
			try
			{
				callbackFailurePool.Release(callbackFailureNode);
			}
			catch (InvalidOperationException)
			{
				exceptionThrown = true;
			}

			Assert(exceptionThrown, "OnRelease 异常应向调用边界抛出。");
			Assert(callbackFailurePool.ActiveCount == 0, "回调失败节点不应继续保持活动状态。");
			Assert(callbackFailurePool.IdleCount == 0, "回调失败节点不应进入空闲区。");
		}

		var disposePool = new NodePool<PoolTestNode>(PoolTestScene, idleCapacity: 1);
		PoolTestNode activeNode = disposePool.Acquire(this);
		disposePool.Dispose();
		Assert(activeNode.IsQueuedForDeletion(), "Dispose 应强制释放仍然活动的节点。");
		Assert(disposePool.ActiveCount == 0, "Dispose 后不应保留活动节点引用。");

		using var externalFreePool = new NodePool<PoolTestNode>(PoolTestScene, idleCapacity: 1);
		PoolTestNode externallyFreedNode = externalFreePool.Acquire(this);
		externallyFreedNode.QueueFree();
		Assert(!externalFreePool.Release(externallyFreedNode), "外部 QueueFree 的节点不能进入空闲区。");
		Assert(externalFreePool.ActiveCount == 0, "外部 QueueFree 后 Release 应清理活动记录。");
	}

	private void RunPoolStressVerification()
	{
		using var stressPool = new NodePool<PoolTestNode>(PoolTestScene, initialSize: 1, idleCapacity: 1);
		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		var stopwatch = Stopwatch.StartNew();

		for (int i = 0; i < 10_000; i++)
		{
			PoolTestNode node = stressPool.Acquire(this);
			Assert(stressPool.Release(node), "压力循环中的 Release 应成功。");
		}

		stopwatch.Stop();
		long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
		Assert(stressPool.ActiveCount == 0, "压力循环后不应存在活动节点。");
		Assert(stressPool.IdleCount == 1, "压力循环后应只保留 1 个空闲节点。");

		GD.Print($"[PoolTest] STRESS：10000 次循环耗时 {stopwatch.ElapsedMilliseconds} ms，当前线程分配 {allocatedBytes} bytes。");
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException($"[PoolTest] FAIL：{message}");
	}
}
