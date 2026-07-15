using System;
using System.Collections.Generic;

namespace GoDo;

/// <summary>由可选输入适配包实现的低层后端边界；游戏业务不直接使用。</summary>
public interface IInputBackend
{
    /// <summary>后端支持的可选能力。</summary>
    InputBackendCapabilities Capabilities { get; }

    /// <summary>最近产生有效输入的设备类别。</summary>
    InputDeviceKind ActiveDevice { get; }

    /// <summary>初始化后保持数量、顺序和类型不变的 Action 描述。</summary>
    IReadOnlyList<InputActionDescriptor> Actions { get; }

    /// <summary>初始化后保持不变的可用 Context ID。</summary>
    IReadOnlyList<InputContextId> Contexts { get; }

    /// <summary>初始化后端；失败时不得留下需要调用方清理的半初始化状态。</summary>
    void Initialize();

    /// <summary>原子应用按优先级从低到高排列的有效 Context；失败时保持原映射不变。</summary>
    void ApplyContexts(ReadOnlySpan<InputContextId> contexts);

    /// <summary>按 <see cref="Actions"/> 的固定顺序写满样本缓冲区。</summary>
    void Sample(Span<InputActionSample> destination);

    /// <summary>释放后端状态和订阅；必须允许重复调用。</summary>
    void Shutdown();
}
