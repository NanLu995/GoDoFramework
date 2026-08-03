using Godot;
using GoDo;

namespace GoDoFramework.Verification;

/// <summary>用于分离 UI 实例化、首次入树和复用重开成本的确定性重型控件。</summary>
public sealed partial class UiPerformanceFixture : Control, IPoolable
{
    public const int RowCount = 200;

    public int ReadyCount { get; private set; }

    public int EnterTreeCount { get; private set; }

    public int ExitTreeCount { get; private set; }

    public int ProcessCount { get; private set; }

    public int AcquireCount { get; private set; }

    public int ReleaseCount { get; private set; }

    public UiPerformanceFixture()
    {
        var content = new VBoxContainer();
        AddChild(content);

        for (int index = 0; index < RowCount; index++)
        {
            var row = new HBoxContainer();
            var label = new Label
            {
                Text = $"Entry {index:D3}",
                CustomMinimumSize = new Vector2(160f, 24f)
            };
            var progress = new ProgressBar
            {
                Value = index % 100,
                CustomMinimumSize = new Vector2(240f, 24f)
            };

            row.AddChild(label);
            row.AddChild(progress);
            content.AddChild(row);
        }
    }

    public override void _EnterTree() => EnterTreeCount++;

    public override void _ExitTree() => ExitTreeCount++;

    public override void _Ready() => ReadyCount++;

    public override void _Process(double delta) => ProcessCount++;

    public void OnAcquire() => AcquireCount++;

    public void OnRelease() => ReleaseCount++;
}
