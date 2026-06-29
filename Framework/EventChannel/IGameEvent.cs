// ==========================================
// IGameEvent.cs
// ==========================================
namespace GoDo
{
    /// <summary>
    /// 所有游戏事件的标记接口。
    /// 事件必须是 struct，保证零 GC 分配。
    /// </summary>
    public interface IGameEvent { }
}
