# 加入背景音乐与按钮音效

本教程为前面的主菜单与游戏流程加入两首 BGM 和一个按钮音效。Procedure 决定当前阶段播放哪首长期音乐，UI 只播放与点击直接相关的短音效。

AudioService 管理非空间 BGM、SFX 和分组音量。需要随 2D/3D 位置变化的声音仍应使用 Godot 的 `AudioStreamPlayer2D` 或 `AudioStreamPlayer3D`。

## 准备音频资源

准备你有权在项目中使用的音频文件：

```text
res://Audio/
├─ MenuTheme.ogg
├─ GameplayTheme.ogg
└─ ButtonClick.wav
```

在 Godot 的 Import 面板中为两首 BGM 启用循环并重新导入。按钮音效通常不循环。

文件名和格式不是框架要求；代码中的 `ResourceKey` 必须与实际路径和大小写完全一致。

## 1. 建立业务音频入口

创建 `res://Shared/GameAudio.cs`：

```csharp
using System;
using System.Threading.Tasks;
using GoDo;

namespace MyGame;

public static class GameAudio
{
    public static readonly ResourceKey MenuTheme =
        ResourceKey.FromPath("res://Audio/MenuTheme.ogg");
    public static readonly ResourceKey GameplayTheme =
        ResourceKey.FromPath("res://Audio/GameplayTheme.ogg");

    private static readonly ResourceKey ButtonClick =
        ResourceKey.FromPath("res://Audio/ButtonClick.wav");

    public static async Task PlayBgmAsync(
        IAudioService audio,
        ResourceKey key)
    {
        try
        {
            await audio.PlayBgmAsync(key);
        }
        catch (OperationCanceledException)
        {
            // 停止或替换加载请求时，无需当作资源损坏报告。
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "GameAudio", key.Value);
        }
    }

    public static async Task PlayButtonClickAsync(IAudioService audio)
    {
        try
        {
            // false 表示同时播放的音效已达到上限，属于正常容量分支。
            await audio.PlaySfxAsync(ButtonClick);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "GameAudio", ButtonClick.Value);
        }
    }
}
```

这个类只集中游戏自己的资源键和非关键失败策略，不创建第二套播放器或缓存。真正的加载、BGM 播放器和 SFX 对象池仍由 AudioService 管理。

`PlaySfxAsync()` 返回 `false` 表示 32 路默认并发已经用满，不代表资源加载失败。按钮点击可以直接跳过，不需要弹出错误。

## 2. 在主菜单流程播放 BGM

在 `MainMenuProcedure` 中增加字段：

```csharp
private IAudioService? _audio;
```

在 `EnterAsync()` 完成场景和 UI 初始化后、创建事件订阅前加入：

```csharp
_audio = context.GetService<IAudioService>();
await GameAudio.PlayBgmAsync(_audio, GameAudio.MenuTheme);
```

在 `ExitAsync()` 收尾时清空引用：

```csharp
_audio = null;
```

退出菜单流程时不调用 `StopBgm()`。下一个 GameplayProcedure 会请求另一首音乐；新资源加载完成后，AudioService 才替换当前 BGM。这样加载期间不会先出现一段无声空白。

同一首资源重复请求默认不会从头播放。确实需要重播时，才使用 `PlayBgmAsync(key, restart: true)`。

## 3. 在游戏流程切换 BGM

在 `GameplayProcedure` 中同样增加：

```csharp
private IAudioService? _audio;
```

在 `EnterAsync()` 完成游戏场景和 HUD 初始化后加入：

```csharp
_audio = context.GetService<IAudioService>();
await GameAudio.PlayBgmAsync(_audio, GameAudio.GameplayTheme);
```

并在 `ExitAsync()` 中清空引用：

```csharp
_audio = null;
```

现在主菜单进入游戏时会切换到 Gameplay BGM，返回菜单时又会切回 Menu BGM。主场景替换不会释放 AudioService，因为它由唯一的 GoDoRuntime 长期持有。

如果某个流程需要安静状态，应在该流程进入时显式调用 `StopBgm()`，而不是依赖场景切换停止音乐。

## 4. 为主菜单按钮播放音效

在 `MainMenu.cs` 中增加：

```csharp
using MyGame;
```

增加字段并在 `_Ready()` 中获取服务：

```csharp
private IAudioService? _audio;

// _Ready() 中，按钮检查通过后：
_audio = Services.Get<IAudioService>();
```

增加一个辅助方法：

```csharp
private void PlayButtonClick()
{
    _ = GameAudio.PlayButtonClickAsync(_audio!);
}
```

然后在按钮处理方法开头调用它：

```csharp
private void OnStartPressed()
{
    PlayButtonClick();
    EventChannel.Emit<StartGameRequestedEvent>();
}

private void OnSettingsPressed()
{
    PlayButtonClick();
    Open(SettingsKey, UiLayer.View);
}

private void OnQuitPressed()
{
    PlayButtonClick();
    Open(ConfirmQuitKey, UiLayer.Modal);
}
```

这里有意不等待短音效完成准备，避免第一次点击因为资源加载而延迟流程或界面操作。`GameAudio` 已经在异步边界内部处理异常，因此不会丢失播放失败。

不要用这种方式启动 BGM。长期音乐由 Procedure `await` 播放请求，确保流程切换顺序清楚。

## 5. 为返回按钮播放音效

在 `GameplayHud.cs` 中加入 `using MyGame;`，增加服务字段：

```csharp
private IAudioService? _audio;
```

在 `_Ready()` 的按钮检查通过后获取服务：

```csharp
_audio = Services.Get<IAudioService>();
```

修改返回处理方法：

```csharp
private void OnReturnPressed()
{
    _ = GameAudio.PlayButtonClickAsync(_audio!);
    EventChannel.Emit<ReturnToMenuRequestedEvent>();
}
```

即使 HUD 因流程切换而关闭，AudioService 仍然存在，已提交的短音效可以继续播放。

## 6. 设置初始音量

在 `Boot.cs` 启动首个 Procedure 之前设置一次分组音量：

```csharp
IAudioService audio = Services.Get<IAudioService>();
audio.SetVolume(AudioGroup.Bgm, 0.7f);
audio.SetVolume(AudioGroup.Sfx, 0.9f);

IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync<MainMenuProcedure>();
```

音量是 0–1 的有限线性值，越界会抛出 `ArgumentOutOfRangeException`。这里设置的是每次启动的默认值，尚未保存玩家选择；后续设置与存档指南会加入持久化。

如果项目没有预先建立 `BGM` 或 `SFX` Audio Bus，框架会在运行时创建并发出 Warning，但不会修改项目的持久化 Audio Bus Layout。

## 7. 运行并验证

确认以下行为：

1. 启动主菜单后播放 Menu BGM。
2. 点击任意主菜单按钮会播放短音效。
3. 点击“开始游戏”后切换到 Gameplay BGM。
4. 点击“返回主菜单”会播放音效并切回 Menu BGM。
5. 多次往返不会叠加播放多路 BGM。
6. 临时写错一个音频路径时，流程仍能进入，Godot 输出面板出现 ErrorHub 报告。

最后恢复正确路径。不要把无版权授权的示例音乐提交到仓库。

精确接口可查询 <xref:GoDo.IAudioService>、<xref:GoDo.AudioGroup> 和 <xref:GoDo.AudioPlaybackException>。
