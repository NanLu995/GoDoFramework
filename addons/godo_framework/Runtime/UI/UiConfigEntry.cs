using Godot;

#nullable enable

namespace GoDo;

/// <summary>UiConfig 中单个 UI 标识的资源、默认层级与实例策略。</summary>
[GlobalClass]
public partial class UiConfigEntry : Resource
{
    /// <summary>业务语义标识；运行时按区分大小写的文本匹配。</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>UI PackedScene 的 <c>res://</c> 路径或 <c>uid://</c> 定位串。</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string Locator { get; set; } = string.Empty;

    /// <summary>通过该标识打开 UI 时使用的默认显示层。</summary>
    [Export]
    public UiLayer Layer { get; set; } = UiLayer.View;

    /// <summary>同一标识是否允许同时存在多个实例。</summary>
    [Export]
    public UiInstanceMode InstanceMode { get; set; } = UiInstanceMode.Single;

    /// <summary>
    /// 关闭后是否保留一个节点实例供下次打开复用；仅支持 <see cref="UiInstanceMode.Single"/>。
    /// </summary>
    [Export]
    public bool ReuseInstance { get; set; }
}
