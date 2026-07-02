using System.Threading.Tasks;
using Godot;

namespace GoDo;

/// <summary>面向业务层的主内容场景切换服务。</summary>
public interface ISceneService
{
    /// <summary>当前是否正在切换场景。</summary>
    bool IsChanging { get; }

    /// <summary>当前加载进度，范围为 0 到 1。</summary>
    float Progress { get; }

    /// <summary>异步加载并替换当前主场景。</summary>
    Task<Node> ChangeAsync(ResourceKey key);
}
