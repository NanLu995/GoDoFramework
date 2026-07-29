using System.Threading.Tasks;
using Godot;

namespace GoDo;

/// <summary>面向业务层的主内容场景切换服务。</summary>
public interface ISceneService
{
    /// <summary>当前是否正在切换场景。</summary>
    bool IsChanging { get; }

    /// <summary>当前加载进度，范围为 0 到 1；失败或取消后复位为 0。</summary>
    float Progress { get; }

    /// <summary>
    /// 异步加载并替换当前主场景；服务离树时取消当前等待，但不取消 ResourceHub 的共享底层加载。
    /// </summary>
    Task<Node> ChangeAsync(ResourceKey key);
}
