using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 模板项目级配置的最小示例。
/// <para>具体游戏可替换或扩展该资源，但应继续通过 <see cref="ConfigHub"/> 加载并校验。</para>
/// </summary>
public sealed partial class StarterConfig : Resource, IConfigResource
{
    /// <summary>用于模板菜单和诊断日志的项目显示名称。</summary>
    [Export]
    public string ProjectTitle { get; set; } = string.Empty;

    /// <summary>验证模板项目配置是否包含可用的显示名称。</summary>
    /// <exception cref="InvalidOperationException">显示名称为空或仅包含空白字符。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectTitle))
            throw new InvalidOperationException("StarterConfig.ProjectTitle 不能为空。");
    }
}
