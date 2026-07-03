namespace GoDo;

/// <summary>可由 ConfigHub 加载并执行完整性校验的强类型配置资源。</summary>
public interface IConfigResource
{
    /// <summary>校验配置内容；无效时抛出描述具体原因的异常。</summary>
    void Validate();
}
