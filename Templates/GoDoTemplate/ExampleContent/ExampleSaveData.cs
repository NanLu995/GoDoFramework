using System;

#nullable enable

namespace GoDoTemplate.ExampleContent;

/// <summary>
/// 可删除的最小业务存档数据示例。
/// <para>该类型不表达角色、关卡或玩法进度；实际项目应以自己的业务数据替换它。</para>
/// </summary>
internal sealed class ExampleSaveData
{
    internal int WriteCount { get; init; }
    internal DateTimeOffset LastSavedAtUtc { get; init; }
}
