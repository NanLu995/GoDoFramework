using System;
using Godot;
using GoDo;

/// <summary>ConfigHub 运行时验证使用的最小强类型配置资源。</summary>
[GlobalClass]
public sealed partial class ConfigTestResource : Resource, IConfigResource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public int Value { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException("测试配置 Id 不能为空。");
        if (Value < 0)
            throw new InvalidOperationException("测试配置 Value 不能为负数。");
    }
}
