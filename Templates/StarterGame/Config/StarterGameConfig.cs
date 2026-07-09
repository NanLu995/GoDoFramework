using System;
using Godot;
using GoDo;

namespace StarterGame;

/// <summary>StarterGame 模板的只读玩法配置。</summary>
[GlobalClass]
public sealed partial class StarterGameConfig : Resource, IConfigResource
{
    [Export(PropertyHint.Range, "1,300,1,or_greater")]
    public double RoundDurationSeconds { get; set; } = 10d;

    [Export(PropertyHint.Range, "1,1000,1,or_greater")]
    public int ScorePerClick { get; set; } = 1;

    public void Validate()
    {
        if (!double.IsFinite(RoundDurationSeconds) || RoundDurationSeconds <= 0d)
            throw new InvalidOperationException("游戏时长必须是大于 0 的有限数值。");
        if (ScorePerClick <= 0)
            throw new InvalidOperationException("单次点击得分必须大于 0。");
    }
}
