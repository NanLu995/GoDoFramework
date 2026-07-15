namespace GoDo;

/// <summary>由支持可靠存储的输入后端提供的低频绑定持久化能力。</summary>
public interface IInputRebindingPersistence
{
    /// <summary>加载并应用已保存绑定；不存在配置时应用默认绑定。</summary>
    InputBindingLoadStatus LoadAndApply();

    /// <summary>保存当前绑定；失败时保持当前运行时绑定不变并抛出异常。</summary>
    void Save();
}

/// <summary>由支持绑定持久化的低层输入后端实现，用于向 InputService 暴露可选能力。</summary>
public interface IInputRebindingPersistenceBackend
{
    /// <summary>与当前后端共享生命周期的绑定持久化实现。</summary>
    IInputRebindingPersistence RebindingPersistence { get; }
}
