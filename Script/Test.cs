using Godot;
using System;
using GoDoFramework.Example.Events;

public partial class Test : Node
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GoDo.EventChannel.Bind<GameOverEvent>(this, testhandle);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void testhandle(GameOverEvent evt)
	{
		GD.Print("OK");
	}

	public void OnButton()
	{
		GoDo.EventChannel.Emit<GameOverEvent>(new GameOverEvent());
	}
}
