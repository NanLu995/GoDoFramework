namespace GoDo;

/// <summary>UI 界面的显示层与生命周期语义。</summary>
public enum UiLayer
{
    /// <summary>与当前主内容场景关联，场景切换成功后自动清空。</summary>
    Scene,

    /// <summary>进入返回栈；新界面打开时隐藏前一个界面。</summary>
    View,

    /// <summary>显示在其他游戏 UI 上方，并阻止 GUI 指针输入穿透。</summary>
    Modal
}
