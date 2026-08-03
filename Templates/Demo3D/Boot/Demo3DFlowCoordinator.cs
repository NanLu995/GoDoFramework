using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>在主场景之外观察 Demo3D 流程失败，并提供单次主菜单恢复入口。</summary>
internal sealed partial class Demo3DFlowCoordinator : Node
{
    private const string CoordinatorName = "Demo3DFlowCoordinator";

    private IProcedureService? _procedures;

    public static void EnsureInstalled(SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        Node? existing = tree.Root.GetNodeOrNull(CoordinatorName);
        if (existing is Demo3DFlowCoordinator)
            return;
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"SceneTree.Root 已存在同名节点: {CoordinatorName}");
        }

        tree.Root.AddChild(new Demo3DFlowCoordinator { Name = CoordinatorName });
    }

    public override void _Ready()
    {
        _procedures = Services.Get<IProcedureService>();
        _procedures.RequestedChangeFailed += OnRequestedChangeFailed;
    }

    public override void _ExitTree()
    {
        if (_procedures is not null)
            _procedures.RequestedChangeFailed -= OnRequestedChangeFailed;

        _procedures = null;
    }

    private async void OnRequestedChangeFailed(ProcedureChangeException exception)
    {
        IProcedureService? procedures = _procedures;
        if (procedures is null)
            return;

        LogHub.Warn(DescribeFailure(exception), nameof(Demo3DFlowCoordinator));
        if (procedures.Current is not null || procedures.IsChanging)
            return;

        try
        {
            await procedures.ChangeAsync<MainMenuProcedure>();
        }
        catch (Exception recoveryException)
        {
            ErrorHub.Report(
                recoveryException,
                nameof(Demo3DFlowCoordinator),
                "Demo3D 流程恢复到主菜单失败");
        }
    }

    private static string DescribeFailure(ProcedureChangeException exception)
    {
        string detail = $"Procedure={exception.ProcedureName}, Phase={exception.Phase}";
        return exception.InnerException switch
        {
            SceneChangeException scene =>
                $"{detail}, Scene={scene.Key.Value}, ScenePhase={scene.Phase}",
            UiOpenException ui =>
                $"{detail}, UI={ui.Key.Value}, UiPhase={ui.Phase}",
            _ => detail,
        };
    }
}
