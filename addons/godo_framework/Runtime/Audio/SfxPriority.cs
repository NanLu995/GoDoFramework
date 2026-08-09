namespace GoDo;

/// <summary>受控 SFX 请求参与容量抢占时使用的相对优先级。</summary>
public enum SfxPriority
{
    /// <summary>环境碎屑、远处碰撞等允许优先丢弃的声音。</summary>
    Low = 0,

    /// <summary>普通玩法音效，也是兼容播放 API 使用的默认优先级。</summary>
    Normal = 1,

    /// <summary>玩家技能、受击确认等应优先保留的声音。</summary>
    High = 2,

    /// <summary>必须优先于其他 SFX 的关键提示声音。</summary>
    Critical = 3,
}
