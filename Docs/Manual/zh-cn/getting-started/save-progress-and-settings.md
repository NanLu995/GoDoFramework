# 保存游戏进度与音量设置

本教程为现有流程加入两种持久化数据：完成游戏的次数，以及玩家选择的 BGM、SFX 音量。它们使用不同边界：游戏进度属于业务存档，音量属于跨存档共享的玩家设置。

SaveService 负责槽位、完整性校验、临时文件、备份和恢复；游戏负责数据结构、JSON 编码和版本迁移。SettingsService 已经封装音量等通用设置，不要再把音量字段复制进游戏存档。

## 1. 定义游戏进度和 Codec

创建 `res://Shared/GameProgress.cs`：

```csharp
namespace MyGame;

public sealed class GameProgress
{
    public int CompletedRuns { get; set; }
}
```

创建 `res://Shared/GameProgressCodec.cs`：

```csharp
using System;
using System.IO;
using System.Text.Json;
using GoDo;

namespace MyGame;

public sealed class GameProgressCodec : ISaveCodec<GameProgress>
{
    public const int CurrentVersion = 1;

    public byte[] Encode(GameProgress value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }

    public GameProgress Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion != CurrentVersion)
        {
            throw new InvalidDataException(
                $"不支持的游戏存档版本：{dataVersion}");
        }

        return JsonSerializer.Deserialize<GameProgress>(payload)
            ?? throw new InvalidDataException("游戏存档内容为空。");
    }
}
```

`CurrentVersion` 是业务 Payload 版本，不是框架容器版本。以后增加或改变字段时，在 `Decode()` 中按 `dataVersion` 迁移历史数据，再提升当前版本。

`System.Text.Json` 来自 .NET，不需要增加 NuGet 包。不要把 Godot Resource、Node 或大型二进制资源直接序列化进常规存档。

## 2. 封装游戏自己的槽位

创建 `res://Shared/GameProgressRepository.cs`：

```csharp
using GoDo;

namespace MyGame;

public sealed class GameProgressRepository
{
    private static readonly SaveSlot Slot = SaveSlot.Create("slot_1");
    private static readonly GameProgressCodec Codec = new();

    private readonly ISaveService _saves;

    public GameProgressRepository(ISaveService saves)
    {
        _saves = saves;
    }

    public GameProgress LoadOrCreate()
    {
        SaveLoadResult<GameProgress> result = _saves.Load(Slot, Codec);
        if (!result.HasValue)
            return new GameProgress();

        if (result.Status == SaveLoadStatus.RecoveredFromBackup)
        {
            ErrorHub.Warn(
                "正式存档不可用，已读取备份。",
                "GameProgress",
                Slot.Value);
        }

        return result.Value;
    }

    public void Save(GameProgress progress)
    {
        _saves.Save(
            Slot,
            progress,
            GameProgressCodec.CurrentVersion,
            Codec);
    }
}
```

槽位只能包含 ASCII 字母、数字、下划线和连字符，最长 64 字符。`NotFound` 是第一次运行的正常结果，不抛异常；主文件损坏但备份健康时会返回 `RecoveredFromBackup`。

读写、校验或 Codec 失败会抛出 `SaveException`。Repository 不静默吞掉它，最外层启动流程或玩家操作边界负责显示和上报失败。

## 3. 启动时加载设置和进度

在 `Boot.cs` 进入首个 Procedure 前加载设置：

```csharp
ISettingsService settings = Services.Get<ISettingsService>();
SettingsLoadStatus settingsStatus = settings.LoadAndApply();

if (settingsStatus == SettingsLoadStatus.DefaultsApplied)
{
    settings.SetBgmVolume(0.7f);
    settings.SetSfxVolume(0.9f);
}
else if (settingsStatus == SettingsLoadStatus.RecoveredFromBackup)
{
    ErrorHub.Warn("玩家设置已从备份恢复。", "GameBoot");
}

var progressRepository = new GameProgressRepository(
    Services.Get<ISaveService>());
GameProgress progress = progressRepository.LoadOrCreate();

IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync(
    new MainMenuProcedure(progressRepository, progress));
```

这段代码替代上一教程中直接调用 `IAudioService.SetVolume()` 的默认音量代码。`LoadAndApply()` 会把已保存的音量立即应用到 AudioService；第一次运行才采用教程默认值。

加载失败会由 `Boot` 现有的 `try/catch` 上报并停止进入主流程，避免在无法确认存档状态时静默覆盖文件。

## 4. 通过 Procedure 传递本次进度

`MainMenuProcedure` 和 `GameplayProcedure` 不再使用无参构造。为 `MainMenuProcedure` 增加：

```csharp
private readonly GameProgressRepository _progressRepository;
private readonly GameProgress _progress;

public MainMenuProcedure(
    GameProgressRepository progressRepository,
    GameProgress progress)
{
    _progressRepository = progressRepository;
    _progress = progress;
}
```

进入主菜单后可以输出当前进度，方便验证重启读取：

```csharp
GD.Print($"已完成游戏次数：{_progress.CompletedRuns}");
```

把开始游戏的请求改为实例重载：

```csharp
private void OnStartGameRequested(StartGameRequestedEvent _)
{
    _context!.RequestChange(
        new GameplayProcedure(_progressRepository, _progress));
}
```

为 `GameplayProcedure` 增加相同字段和构造函数：

```csharp
private readonly GameProgressRepository _progressRepository;
private readonly GameProgress _progress;

public GameplayProcedure(
    GameProgressRepository progressRepository,
    GameProgress progress)
{
    _progressRepository = progressRepository;
    _progress = progress;
}
```

普通返回同样改为携带当前对象：

```csharp
_context!.RequestChange(
    new MainMenuProcedure(_progressRepository, _progress));
```

需要携带本次业务数据时使用 `RequestChange(IProcedure next)`，不要把临时进度对象注册成全局 Services。

## 5. 在完成游戏时保存

在 `GameEvents.cs` 增加：

```csharp
public readonly struct CompleteRunRequestedEvent : IGameEvent
{
}
```

在 `GameplayHud.tscn` 墁加一个 `CompleteButton`，文字为“完成本局”，并像现有返回按钮一样导出、订阅和解绑。按钮处理方法只发送意图：

```csharp
private void OnCompletePressed()
{
    _ = GameAudio.PlayButtonClickAsync(_audio!);
    EventChannel.Emit<CompleteRunRequestedEvent>();
}
```

在 `GameplayProcedure.EnterAsync()` 创建 EventScope 时增加监听：

```csharp
_events = new EventScope()
    .On<ReturnToMenuRequestedEvent>(OnReturnToMenuRequested)
    .On<CompleteRunRequestedEvent>(OnCompleteRunRequested);
```

实现保存边界：

```csharp
private void OnCompleteRunRequested(CompleteRunRequestedEvent _)
{
    _progress.CompletedRuns++;

    try
    {
        _progressRepository.Save(_progress);
        _context!.RequestChange(
            new MainMenuProcedure(_progressRepository, _progress));
    }
    catch (SaveException exception)
    {
        ErrorHub.Report(exception, "Gameplay", "保存完成进度");
    }
}
```

SaveService 首版是同步主线程 API，适合常规小型存档。只在明确里程碑保存，不要放进 `_Process()`，也不要用 `Task.Run` 包装 Godot 文件操作。

保存失败时留在当前流程，让业务 UI 有机会提示重试。生产游戏还应显示玩家可理解的错误提示，而不只依赖开发输出。

## 6. 让设置页编辑并保存音量

在 `SettingsView.tscn` 中扩展节点树：

```text
SettingsView (Control)
└─ Content (VBoxContainer)
   ├─ Title (Label，文字为“设置”)
   ├─ BgmVolume (HSlider，Min 0，Max 1，Step 0.05)
   ├─ SfxVolume (HSlider，Min 0，Max 1，Step 0.05)
   ├─ SaveButton (Button，文字为“保存并返回”)
   └─ BackButton (Button，文字为“返回但不保存”)
```

在 `SettingsView.cs` 增加导出字段：

```csharp
[Export] private HSlider? _bgmVolume;
[Export] private HSlider? _sfxVolume;
[Export] private Button? _saveButton;
```

增加服务字段：

```csharp
private ISettingsService? _settings;
```

在 `_Ready()` 检查全部引用后，先设置滑块值，再订阅信号：

```csharp
_settings = Services.Get<ISettingsService>();
_ui = Services.Get<IUiService>();

_bgmVolume!.Value = _settings.Current.BgmVolume;
_sfxVolume!.Value = _settings.Current.SfxVolume;

_bgmVolume.ValueChanged += OnBgmVolumeChanged;
_sfxVolume.ValueChanged += OnSfxVolumeChanged;
_saveButton!.Pressed += OnSavePressed;
_backButton!.Pressed += OnBackPressed;
```

在 `_ExitTree()` 对称解绑这四个信号，然后实现：

```csharp
private void OnBgmVolumeChanged(double value)
{
    _settings!.SetBgmVolume((float)value);
}

private void OnSfxVolumeChanged(double value)
{
    _settings!.SetSfxVolume((float)value);
}

private void OnSavePressed()
{
    try
    {
        _settings!.Save();
        _ui!.TryGoBack();
    }
    catch (SaveException exception)
    {
        ErrorHub.Report(exception, "SettingsView", "保存玩家设置");
    }
}

private void OnBackPressed()
{
    _ui!.TryGoBack();
}
```

拖动滑块会立即改变运行时音量，但不会每次写盘；只有“保存并返回”才持久化当前快照。“返回但不保存”只是不写盘，本次运行中已经应用的音量仍然有效。

## 7. 运行并验证

按顺序检查：

1. 第一次启动使用 0.7 BGM 和 0.9 SFX 默认值。
2. 修改音量并“保存并返回”，重启后滑块和实际音量保持新值。
3. 完成一局后返回菜单，输出显示完成次数增加。
4. 完全退出并重启，完成次数仍然存在。
5. 普通“返回主菜单”不会增加或保存完成次数。

正式文件位于 `user://saves/`，不要在教程中依赖其平台绝对路径。测试删除存档时使用 `ISaveService.Delete()`；不要在文件管理器中只删除主文件而留下备份和临时文件。

精确接口可查询 <xref:GoDo.ISaveService>、<xref:GoDo.ISaveCodec%601>、<xref:GoDo.SaveLoadResult%601>、<xref:GoDo.ISettingsService> 和 <xref:GoDo.SettingsSnapshot>。
