using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>模板 UI 与 Procedure 之间传递玩家意图的内部事件标记。</summary>
internal interface IStarterEvent : IEventMessage { }

/// <summary>玩家请求从主菜单开始游戏。</summary>
internal readonly struct StartGameSelectedEvent : IStarterEvent { }

/// <summary>玩家请求打开设置界面。</summary>
internal readonly struct SettingsSelectedEvent : IStarterEvent { }

/// <summary>玩家请求关闭设置界面。</summary>
internal readonly struct SettingsCloseSelectedEvent : IStarterEvent { }

/// <summary>玩家请求打开暂停菜单。</summary>
internal readonly struct PauseSelectedEvent : IStarterEvent { }

/// <summary>玩家请求恢复游戏。</summary>
internal readonly struct ResumeSelectedEvent : IStarterEvent { }

/// <summary>玩家请求返回主菜单。</summary>
internal readonly struct ReturnToMainMenuSelectedEvent : IStarterEvent { }

/// <summary>玩家确认当前确认对话框。</summary>
internal readonly struct ConfirmAcceptedEvent : IStarterEvent { }

/// <summary>玩家取消当前确认对话框。</summary>
internal readonly struct ConfirmCancelledEvent : IStarterEvent { }

/// <summary>玩家触发当前输入 Context 的返回动作。</summary>
internal readonly struct BackSelectedEvent : IStarterEvent { }

/// <summary>玩家请求写入可删除的 Save 示例数据。</summary>
internal readonly struct ExampleSaveSelectedEvent : IStarterEvent { }
