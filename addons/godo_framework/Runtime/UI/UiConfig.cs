using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

#nullable enable

namespace GoDo;

/// <summary>可在 Godot Inspector 中维护的 UI 语义标识配置。</summary>
[GlobalClass]
public partial class UiConfig : Resource, IConfigResource
{
    /// <summary>UI 配置条目；标识必须唯一且每项必须包含有效资源定位。</summary>
    [Export]
    public Array<UiConfigEntry> Entries { get; set; } = new();

    /// <summary>
    /// 验证条目、标识、资源定位、层级与实例策略；失败时抛出包含条目位置的异常。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// 目录为空，或条目包含 null、空标识、非法资源定位、重复标识、未知层级或未知实例策略。
    /// </exception>
    public void Validate()
    {
        if (Entries.Count == 0)
            throw new InvalidOperationException("UiConfig 至少需要一个配置条目。");

        var ids = new HashSet<UiId>();
        for (int i = 0; i < Entries.Count; i++)
        {
            UiConfigEntry? entry = Entries[i];
            if (entry is null)
                throw new InvalidOperationException($"UiConfig 条目 {i} 不能为 null。");

            UiId id;
            try
            {
                id = UiId.Create(entry.Id);
                _ = ResourceKey.Create(entry.Locator);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"UiConfig 条目 {i} 的标识或资源定位无效。",
                    exception);
            }

            if (!ids.Add(id))
                throw new InvalidOperationException($"UiConfig 包含重复 UI 标识：{id.Value}");
            if (!Enum.IsDefined(entry.Layer))
                throw new InvalidOperationException(
                    $"UiConfig 条目 {id.Value} 包含未知层级：{entry.Layer}");
            if (!Enum.IsDefined(entry.InstanceMode))
                throw new InvalidOperationException(
                    $"UiConfig 条目 {id.Value} 包含未知实例策略：{entry.InstanceMode}");
            if (entry.ReuseInstance && entry.InstanceMode != UiInstanceMode.Single)
                throw new InvalidOperationException(
                    $"UiConfig 条目 {id.Value} 仅能为 Single UI 启用实例复用。");

        }
    }
}
