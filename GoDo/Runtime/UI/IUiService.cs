using Godot;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的 Control 页面、模态与返回栈服务。</summary>
public interface IUiService
{
    /// <summary>打开页面；当前页面会隐藏，关闭新页面后恢复。</summary>
    Control OpenPage(ResourceKey key);

    /// <summary>在页面之上打开模态界面，并阻止 GUI 指针输入穿透。</summary>
    Control OpenModal(ResourceKey key);

    /// <summary>关闭当前最上层且由本服务管理的界面。</summary>
    void Close(Control view);

    /// <summary>优先关闭顶部模态，其次关闭顶部页面；没有界面时返回 false。</summary>
    bool TryGoBack();
}
