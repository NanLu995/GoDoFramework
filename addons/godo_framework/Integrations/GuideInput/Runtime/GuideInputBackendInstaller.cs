using System;
using Godot;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>在启动场景中把一个 GuideInputProfile 安装到 GoDo InputService。</summary>
/// <remarks>
/// 此节点必须只存在于一次性启动场景，并在 <see cref="Node._Ready"/> 时于 Godot 主线程安装一次。
/// 安装后的后端由 GoDoRuntime 持有；场景切换不会卸载它。缺少 Profile、SaveService 或可安装的
/// InputService 时会抛出 <see cref="InvalidOperationException"/>。
/// </remarks>
[GlobalClass]
public sealed partial class GuideInputBackendInstaller : Node
{
    private const string DefaultPersistenceSlot = "godo-input-bindings";

    /// <summary>当前游戏的 GoDo ID 与 G.U.I.D.E Resource 映射。</summary>
    /// <remarks>不能为空；其中的重复或无效映射由后端构造过程拒绝。</remarks>
    [Export]
    public GuideInputProfile Profile { get; set; } = null!;

    /// <summary>通过 SaveService 保存绑定配置的独立槽位。</summary>
    /// <remarks>默认值适用于单个本地玩家；不同本地档案应提供不同的有效 SaveSlot 文本。</remarks>
    [Export]
    public string PersistenceSlot { get; set; } = DefaultPersistenceSlot;

    /// <inheritdoc />
    public override void _Ready()
    {
        if (!IsInstanceValid(Profile))
            throw new InvalidOperationException("GuideInputBackendInstaller 缺少 Profile。");

        InputService service = Services.Get<IInputService>() as InputService ??
            throw new InvalidOperationException("IInputService 不是可安装后端的 InputService 实例。");
        ISaveService saveService = Services.Get<ISaveService>();
        SaveSlot persistenceSlot = SaveSlot.Create(PersistenceSlot);

        service.InstallBackend(new GuideInputBackend(Profile, saveService, persistenceSlot));
    }
}
