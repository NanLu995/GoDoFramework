using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的顶层游戏流程切换服务。</summary>
public interface IProcedureService
{
    /// <summary>当前已成功进入的流程；无流程或进入失败后为 null。</summary>
    IProcedure? Current { get; }

    /// <summary>当前是否正在切换流程。</summary>
    bool IsChanging { get; }

    /// <summary>退出当前流程并进入目标流程。</summary>
    Task ChangeAsync(IProcedure next);

    /// <summary>创建并进入无参构造的目标流程。</summary>
    Task ChangeAsync<TProcedure>() where TProcedure : IProcedure, new();
}
