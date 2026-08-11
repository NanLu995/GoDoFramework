#nullable enable

namespace GoDo;

/// <summary>面向业务层的语义输入快照、Context 栈与可选输入能力服务。</summary>
/// <remarks>所有成员都必须从 GoDoRuntime 记录的 Godot 主线程调用。</remarks>
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
    /// <param name="rebinding">成功时为与当前后端共享生命周期的能力对象；不支持时为 <see langword="null"/>。</param>
    /// <returns>后端同时声明并实现重绑定能力时为 <see langword="true"/>。</returns>
    bool TryGetRebinding(out IInputRebinding? rebinding);

    /// <summary>在当前后端支持时取得绑定持久化能力。</summary>
    /// <param name="persistence">成功时为与当前后端共享生命周期的持久化对象；不支持时为 <see langword="null"/>。</param>
    /// <returns>后端同时声明并实现绑定持久化能力时为 <see langword="true"/>。</returns>
    bool TryGetRebindingPersistence(out IInputRebindingPersistence? persistence);

    /// <summary>在当前后端支持时取得非热路径输入提示查询能力。</summary>
    /// <param name="promptQuery">成功时为与当前后端共享生命周期的查询对象；不支持时为 <see langword="null"/>。</param>
    /// <returns>后端同时声明并实现提示查询能力时为 <see langword="true"/>。</returns>
    bool TryGetPromptQuery(out IInputPromptQuery? promptQuery);

    /// <summary>设置栈底 Context，并在后端成功应用后移除所有临时 Context。</summary>
    /// <param name="context">后端初始化时声明的非默认 Context ID。</param>
    /// <exception cref="System.ArgumentException"><paramref name="context"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">后端未就绪、Context 未注册或后端应用失败。</exception>
    void SetBaseContext(InputContextId context);

    /// <summary>把 Context 压入栈顶，并在后端成功应用后按指定模式组合更低层 Context。</summary>
    /// <param name="context">后端初始化时声明且尚未位于栈中的 Context ID。</param>
    /// <param name="mode">与更低层 Context 叠加或屏蔽更低层 Context。</param>
    /// <exception cref="System.ArgumentException"><paramref name="context"/> 是默认 ID。</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="mode"/> 未定义。</exception>
    /// <exception cref="InputOperationException">后端未就绪、尚未设置 Base、Context 未注册或重复，或后端应用失败。</exception>
    void PushContext(InputContextId context, InputContextMode mode = InputContextMode.Exclusive);

    /// <summary>仅在预期 ID 与栈顶匹配且后端成功应用时弹出临时 Context。</summary>
    /// <param name="expectedContext">调用方预期位于栈顶的临时 Context ID。</param>
    /// <exception cref="System.ArgumentException"><paramref name="expectedContext"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">没有临时项、栈顶不匹配、Context 未注册、后端未就绪或后端应用失败。</exception>
    void PopContext(InputContextId expectedContext);

    /// <summary>查询 Context 当前是否位于 Exclusive 规则计算出的最终有效集合中。</summary>
    /// <param name="context">后端初始化时声明的 Context ID。</param>
    /// <returns>Context 当前有效时为 <see langword="true"/>；位于被 Exclusive 屏蔽的低层时为 <see langword="false"/>。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="context"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">后端未就绪或 Context 未注册。</exception>
    bool IsContextActive(InputContextId context);
}
