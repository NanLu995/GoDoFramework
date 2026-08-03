using System;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>集中维护模板使用的稳定输入 Action 和 Context 标识。</summary>
internal static class StarterInput
{
    internal static readonly InputActionId Back = InputActionId.Create("ui.back");
    internal static readonly InputContextId Menu = InputContextId.Create("menu");
    internal static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
    internal static readonly InputContextId Pause = InputContextId.Create("pause");

    internal static void LoadBindingsAndSetMenuContext(ProcedureContext context)
    {
        IInputService input = context.GetService<IInputService>();
        if (!input.IsReady)
        {
            StarterLog.Input.Info("Input backend is not installed; Context setup is skipped.");
            return;
        }

        try
        {
            if (input.TryGetRebindingPersistence(out IInputRebindingPersistence? persistence) &&
                persistence != null)
            {
                persistence.LoadAndApply();
            }

            input.SetBaseContext(Menu);
        }
        catch (Exception exception)
        {
            StarterLog.Input.Error(exception, "Initialize");
        }
    }

    internal static bool IsReady(ProcedureContext context) =>
        context.GetService<IInputService>().IsReady;
}
