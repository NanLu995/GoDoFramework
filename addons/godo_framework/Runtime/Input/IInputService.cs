#nullable enable

namespace GoDo;

/// <summary>面向业务层的语义输入快照与 Context 服务。</summary>
public interface IInputService
{
    /// <summary>是否已经安装并成功初始化输入后端。</summary>
    bool IsReady { get; }

    /// <summary>最近完成采样的当前帧只读句柄。</summary>
    InputFrame Frame { get; }

    /// <summary>最近产生有效输入的设备类别；后端未就绪时为 Unknown。</summary>
    InputDeviceKind ActiveDevice { get; }

    /// <summary>当前后端能力；后端未就绪时为 None。</summary>
    InputBackendCapabilities Capabilities { get; }

    /// <summary>在当前后端支持时取得运行时重绑定能力。</summary>
    bool TryGetRebinding(out IInputRebinding? rebinding);

    /// <summary>在当前后端支持时取得绑定持久化能力。</summary>
    bool TryGetRebindingPersistence(out IInputRebindingPersistence? persistence);

    /// <summary>在当前后端支持时取得非热路径输入提示查询能力。</summary>
    bool TryGetPromptQuery(out IInputPromptQuery? promptQuery);

    /// <summary>设置栈底 Context，并移除所有临时 Context。</summary>
    void SetBaseContext(InputContextId context);

    /// <summary>把 Context 压入栈顶并按指定模式组合更低层 Context。</summary>
    void PushContext(InputContextId context, InputContextMode mode = InputContextMode.Exclusive);

    /// <summary>仅在预期 ID 与栈顶匹配时弹出临时 Context。</summary>
    void PopContext(InputContextId expectedContext);

    /// <summary>查询 Context 当前是否位于最终有效集合中。</summary>
    bool IsContextActive(InputContextId context);
}
