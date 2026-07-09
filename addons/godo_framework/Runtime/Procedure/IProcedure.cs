using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>表示一个顶层游戏流程阶段。</summary>
public interface IProcedure
{
    /// <summary>流程名称，用于诊断和错误信息。</summary>
    string Name { get; }

    /// <summary>进入当前流程阶段。</summary>
    Task EnterAsync(ProcedureContext context);

    /// <summary>退出当前流程阶段。</summary>
    Task ExitAsync(ProcedureContext context);
}
