using System;
using Godot;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>在启动场景中把一个 GuideInputProfile 安装到 GoDo InputService。</summary>
[GlobalClass]
public sealed partial class GuideInputBackendInstaller : Node
{
    private const string DefaultPersistenceSlot = "godo-input-bindings";

    /// <summary>当前游戏的 GoDo ID 与 G.U.I.D.E Resource 映射。</summary>
    [Export]
    public GuideInputProfile Profile { get; set; } = null!;

    /// <summary>通过 SaveService 保存绑定配置的独立槽位。</summary>
    [Export]
    public string PersistenceSlot { get; set; } = DefaultPersistenceSlot;

    /// <inheritdoc />
    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(Profile))
            throw new InvalidOperationException("GuideInputBackendInstaller 缺少 Profile。");

        InputService service = Services.Get<IInputService>() as InputService ??
            throw new InvalidOperationException("IInputService 不是可安装后端的 InputService 实例。");
        ISaveService saveService = Services.Get<ISaveService>();
        SaveSlot persistenceSlot = SaveSlot.Create(PersistenceSlot);

        service.InstallBackend(new GuideInputBackend(Profile, saveService, persistenceSlot));
    }
}
