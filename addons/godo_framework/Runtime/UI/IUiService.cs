using Godot;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的屏幕空间 UI 层级与生命周期服务。</summary>
public interface IUiService
{
    /// <summary>在指定层打开 UI 界面。</summary>
    Control Open(ResourceKey key, UiLayer layer);

    /// <summary>关闭受管理的 Scene 界面，或当前最上层的 View / Modal。</summary>
    void Close(Control view);

    /// <summary>优先关闭顶部模态，其次返回前一个 View；没有可返回界面时返回 false。</summary>
    bool TryGoBack();
}
