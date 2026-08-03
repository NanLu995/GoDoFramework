using System;
using System.Threading.Tasks;
using Demo3D;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>验证 Demo3D 的 Procedure、Scene 与 UI 完整业务调用链。</summary>
public sealed partial class Demo3DFlowRegression : Node
{
    private const int MaxWaitFrames = 300;

    public override async void _Ready()
    {
        SceneTree tree = GetTree();
        try
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Reparent(tree.Root);
            var placeholder = new Node { Name = "Demo3DFlowPlaceholder" };
            tree.Root.AddChild(placeholder);
            tree.CurrentScene = placeholder;
            Demo3DFlowCoordinator.EnsureInstalled(tree);
            Demo3DFlowCoordinator? coordinator =
                tree.Root.GetNodeOrNull<Demo3DFlowCoordinator>("Demo3DFlowCoordinator");
            Assert(coordinator is not null, "Demo3D 流程协调节点没有安装到 SceneTree.Root");

            IInputService input = Services.Get<IInputService>();
            await WaitUntilAsync(() => input.IsReady, "输入后端没有就绪");

            IProcedureService procedures = Services.Get<IProcedureService>();
            IUiService ui = Services.Get<IUiService>();

            await procedures.ChangeAsync<MainMenuProcedure>();
            AssertProcedure<MainMenuProcedure>(procedures, "首次进入主菜单");
            AssertCurrentScene("MainMenuScene");
            AssertTopView<MainMenuView>(ui, UiLayer.View, "主菜单 View");
#if DEBUG
            AssertDebugState<MainMenuProcedure>(
                procedures,
                ui,
                Demo3DKeys.MainMenuScene,
                sceneCount: 0,
                viewCount: 1,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 2);
#endif
            EventChannel.Emit<StartGameSelectedEvent>();
            await WaitForProcedureAsync<GameplayProcedure>(procedures);
            AssertCurrentScene("GameplayScene");
            AssertTopView<GameplayHud>(ui, UiLayer.Scene, "Gameplay HUD");
            AssertNoTopView(ui, UiLayer.View, "进入 Gameplay 后主菜单 View 仍然打开");
            Assert(!tree.Paused, "进入 Gameplay 后 SceneTree 错误地处于暂停状态");
#if DEBUG
            AssertDebugState<GameplayProcedure>(
                procedures,
                ui,
                Demo3DKeys.GameplayScene,
                sceneCount: 1,
                viewCount: 0,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 3);
#endif
            EventChannel.Emit<PauseRequestedEvent>();
            Assert(tree.Paused, "暂停请求没有暂停 SceneTree");
            AssertTopView<PauseModal>(ui, UiLayer.Modal, "Pause Modal");
#if DEBUG
            AssertDebugState<GameplayProcedure>(
                procedures,
                ui,
                Demo3DKeys.GameplayScene,
                sceneCount: 1,
                viewCount: 0,
                modalCount: 1,
                overlayCount: 0,
                cleanupCount: 3);
#endif

            EventChannel.Emit<ResumeSelectedEvent>();
            Assert(!tree.Paused, "恢复请求没有恢复 SceneTree");
            AssertNoTopView(ui, UiLayer.Modal, "恢复后 Pause Modal 仍然打开");
#if DEBUG
            AssertDebugState<GameplayProcedure>(
                procedures,
                ui,
                Demo3DKeys.GameplayScene,
                sceneCount: 1,
                viewCount: 0,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 3);
#endif

            for (int index = 0; index < 5; index++)
                EventChannel.Emit<CollectibleCollectedEvent>();

            await WaitForProcedureAsync<ResultProcedure>(procedures);
            Assert(tree.Paused, "进入 Result 后 Gameplay 没有暂停");
            AssertTopView<ResultView>(ui, UiLayer.View, "Result View");
            AssertNoTopView(ui, UiLayer.Scene, "进入 Result 后 Gameplay HUD 仍然打开");
#if DEBUG
            AssertDebugState<ResultProcedure>(
                procedures,
                ui,
                Demo3DKeys.GameplayScene,
                sceneCount: 0,
                viewCount: 1,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 3);
#endif
            EventChannel.Emit<RetrySelectedEvent>();
            await WaitForProcedureAsync<GameplayProcedure>(procedures);
            Assert(!tree.Paused, "Retry 进入 Gameplay 后 SceneTree 仍然暂停");
            AssertNoTopView(ui, UiLayer.View, "Retry 后 Result View 仍然打开");
#if DEBUG
            AssertDebugState<GameplayProcedure>(
                procedures,
                ui,
                Demo3DKeys.GameplayScene,
                sceneCount: 1,
                viewCount: 0,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 3);
#endif
            EventChannel.Emit<PauseRequestedEvent>();
#if DEBUG
            AssertDebugState<GameplayProcedure>(
                procedures,
                ui,
                Demo3DKeys.GameplayScene,
                sceneCount: 1,
                viewCount: 0,
                modalCount: 1,
                overlayCount: 0,
                cleanupCount: 3);
#endif
            EventChannel.Emit<ReturnToMenuSelectedEvent>();
            await WaitForProcedureAsync<MainMenuProcedure>(procedures);
            AssertCurrentScene("MainMenuScene");
            AssertTopView<MainMenuView>(ui, UiLayer.View, "返回后的主菜单 View");
            AssertNoTopView(ui, UiLayer.Scene, "返回主菜单后 Gameplay HUD 仍然打开");
            AssertNoTopView(ui, UiLayer.Modal, "返回主菜单后 Pause Modal 仍然打开");
            Assert(!tree.Paused, "返回主菜单后 SceneTree 仍然暂停");
            Assert(
                GodotObject.IsInstanceValid(coordinator) && coordinator!.GetParent() == tree.Root,
                "Demo3D 流程协调节点没有跨主场景切换存活");
#if DEBUG
            AssertDebugState<MainMenuProcedure>(
                procedures,
                ui,
                Demo3DKeys.MainMenuScene,
                sceneCount: 0,
                viewCount: 1,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 2);
#endif

            await procedures.ChangeAsync(new FinishedProcedure());
            AssertNoTopView(ui, UiLayer.View, "结束回归后主菜单 View 仍然打开");
#if DEBUG
            AssertDebugState<FinishedProcedure>(
                procedures,
                ui,
                Demo3DKeys.MainMenuScene,
                sceneCount: 0,
                viewCount: 0,
                modalCount: 0,
                overlayCount: 0,
                cleanupCount: 0);
#endif
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            GD.Print("[Demo3DFlowRegression] PASS");
            tree.Quit(0);
        }
        catch (Exception exception)
        {
            tree.Paused = false;
            GD.PushError($"[Demo3DFlowRegression] FAIL: {exception}");
            tree.Quit(1);
        }
    }

    private async Task WaitForProcedureAsync<TProcedure>(IProcedureService procedures)
        where TProcedure : IProcedure
    {
        await WaitUntilAsync(
            () => !procedures.IsChanging && procedures.Current is TProcedure,
            $"等待 Procedure {typeof(TProcedure).Name} 超时");
    }

    private async Task WaitUntilAsync(Func<bool> predicate, string failureMessage)
    {
        for (int frame = 0; frame < MaxWaitFrames; frame++)
        {
            if (predicate())
                return;

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        throw new InvalidOperationException(failureMessage);
    }

    private static void AssertProcedure<TProcedure>(
        IProcedureService procedures,
        string description)
        where TProcedure : IProcedure
    {
        Assert(
            procedures.Current is TProcedure && !procedures.IsChanging,
            $"{description}没有进入 {typeof(TProcedure).Name}");
    }

    private void AssertCurrentScene(string expectedName)
    {
        Node? scene = GetTree().CurrentScene;
        Assert(
            GodotObject.IsInstanceValid(scene) && scene!.Name == expectedName,
            $"当前主场景不是 {expectedName}");
    }

    private static void AssertTopView<TView>(
        IUiService ui,
        UiLayer layer,
        string description)
        where TView : Control
    {
        Assert(
            ui.TryGetTop(layer, out Control? view) && view is TView,
            $"{description}没有位于 {layer} 层顶部");
    }

    private static void AssertNoTopView(IUiService ui, UiLayer layer, string failureMessage)
    {
        Assert(!ui.TryGetTop(layer, out _), failureMessage);
    }

#if DEBUG
    private static void AssertDebugState<TProcedure>(
        IProcedureService procedures,
        IUiService ui,
        ResourceKey expectedSceneKey,
        int sceneCount,
        int viewCount,
        int modalCount,
        int overlayCount,
        int cleanupCount)
        where TProcedure : IProcedure
    {
        if (procedures is not ProcedureService procedureService)
            throw new InvalidOperationException("Demo3D 回归需要内置 ProcedureService Debug 快照。");
        if (ui is not UiService uiService)
            throw new InvalidOperationException("Demo3D 回归需要内置 UiService Debug 快照。");
        if (Services.Get<ISceneService>() is not SceneService sceneService)
            throw new InvalidOperationException("Demo3D 回归需要内置 SceneService Debug 快照。");

        ProcedureDebugSnapshot procedure = procedureService.GetDebugSnapshot();
        Assert(procedures.Current is TProcedure,
            $"当前 Procedure 不是 {typeof(TProcedure).Name}");
        Assert(procedure.CurrentName == procedures.Current?.Name,
            "Procedure Debug 当前流程与运行时状态不一致");
        Assert(procedure.LastSucceededName == procedures.Current?.Name,
            $"Procedure Debug 最近成功不是 {typeof(TProcedure).Name}");
        Assert(procedure.LastPhase == ProcedureDebugPhase.Entering,
            "Procedure Debug 最近阶段不是 Entering");
        Assert(procedure.LastResult == ProcedureDebugResult.Succeeded,
            "Procedure Debug 最近结果不是 Succeeded");
        Assert(procedure.HasActiveContext, "Procedure Debug 没有激活 Context");
        Assert(procedure.CleanupCount == cleanupCount,
            $"Procedure Debug 待清理项不是 {cleanupCount}");

        SceneDebugSnapshot scene = sceneService.GetDebugSnapshot();
        Assert(scene.CurrentPhase == SceneDebugPhase.Idle,
            "Scene Debug 完成后没有回到 Idle");
        Assert(scene.LastChangeKey == expectedSceneKey,
            $"Scene Debug 最近目标不是 {expectedSceneKey.Value}");
        Assert(scene.LastPhase == SceneDebugPhase.Committing,
            "Scene Debug 最近阶段不是 Committing");
        Assert(scene.LastResult == SceneDebugResult.Succeeded,
            "Scene Debug 最近结果不是 Succeeded");

        UiDebugSnapshot uiSnapshot = uiService.GetDebugSnapshot();
        Assert(uiSnapshot.Openings.Length == 0, "UI Debug 仍有未完成异步打开请求");
        Assert(uiSnapshot.LastResult == UiDebugOpenResult.None,
            "Demo3D 同步 UI 打开错误地污染了异步打开诊断");
        Assert(CountUiEntries(uiSnapshot, UiLayer.Scene) == sceneCount,
            $"UI Debug Scene 层数量不是 {sceneCount}");
        Assert(CountUiEntries(uiSnapshot, UiLayer.View) == viewCount,
            $"UI Debug View 层数量不是 {viewCount}");
        Assert(CountUiEntries(uiSnapshot, UiLayer.Modal) == modalCount,
            $"UI Debug Modal 层数量不是 {modalCount}");
        Assert(CountUiEntries(uiSnapshot, UiLayer.Overlay) == overlayCount,
            $"UI Debug Overlay 层数量不是 {overlayCount}");
    }

    private static int CountUiEntries(UiDebugSnapshot snapshot, UiLayer layer)
    {
        int count = 0;
        for (int index = 0; index < snapshot.Entries.Length; index++)
        {
            UiDebugEntry entry = snapshot.Entries[index];
            if (entry.Layer == layer && !entry.IsCached)
                count++;
        }

        return count;
    }
#endif

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FinishedProcedure : IProcedure
    {
        public string Name => "Demo3DFlowFinished";

        public Task EnterAsync(ProcedureContext context) => Task.CompletedTask;

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }
}
