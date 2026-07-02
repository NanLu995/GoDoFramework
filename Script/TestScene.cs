using Godot;
using System;
using GoDo;
using GoDoFramework.Example.Events;
using GoDoFramework.Tests;

public partial class TestScene : Node2D
{
	[Export]
	public PackedScene PoolTestScene { get; set; } = null!;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		EventChannel.Bind<GameOverEvent>(this, OnGameOver);
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

		GD.Print("[PoolTest] PASS：初始化、复用、生命周期、重复释放和空闲区容量验证通过。");
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException($"[PoolTest] FAIL：{message}");
	}
}
