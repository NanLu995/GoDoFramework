# 设计多槽位存档、跨平台设置与本地化

SaveService 保存游戏进度，SettingsService 保存音量、语言和平台显示偏好，LocalizationService 查询当前语言内容。三者职责分离：不要把玩家设置塞进每个进度槽，也不要让本地化服务负责持久化语言选择。

## 1. 规划稳定的存档槽位

槽位只允许 ASCII 字母、数字、下划线和连字符，最长 64 字符：

```csharp
SaveSlot autosave = SaveSlot.Create("autosave");
SaveSlot slot1 = SaveSlot.Create("slot-1");
```

槽位名是文件协议，不使用玩家输入的名称。多槽位 UI 可以维护自己的显示标题，并把稳定槽位映射到它。

```csharp
SaveLoadResult<GameSave> result = saves.Load(slot1, GameSaveCodec.Instance);
switch (result.Status)
{
    case SaveLoadStatus.NotFound:
        ShowEmptySlot();
        break;
    case SaveLoadStatus.Loaded:
        ShowSlot(result.Value, result.SavedAtUtc);
        break;
    case SaveLoadStatus.RecoveredFromBackup:
        ErrorHub.Warn("存档已从备份恢复。", "Game.Save", slot1.Value);
        ShowSlot(result.Value, result.SavedAtUtc);
        break;
}
```

NotFound 是正常结果，不抛异常。删除槽位会同时删除正式、备份和临时文件；没有文件时返回 `false`，执行前必须让玩家确认。

## 2. 由 Codec 负责版本迁移

框架容器保护文件完整性，但不了解业务字段。Codec 根据 `dataVersion` 解码旧格式并返回当前模型：

```csharp
public GameSave Decode(ReadOnlySpan<byte> payload, int dataVersion)
{
    return dataVersion switch
    {
        1 => MigrateFromV1(DecodeV1(payload)),
        2 => MigrateFromV2(DecodeV2(payload)),
        CurrentVersion => DecodeCurrent(payload),
        _ => throw new InvalidDataException($"不支持的存档版本：{dataVersion}")
    };
}
```

读取成功后不要立即覆盖旧文件。先验证迁移后的数据能进入游戏，在下一次明确保存里程碑时再写当前版本。保留旧版本回归样本，确保每条受支持迁移路径都能测试。

字段改名、枚举重排、资源 ID 变化和数值单位变化都可能需要迁移。Debug 与 Release 必须使用同一权威 Codec 和容器格式。

## 3. 理解备份恢复

保存时框架先写 `.tmp` 并重新读取校验，再把健康旧正式文件提升为 `.bak`，最后提交新文件。损坏正式档不会覆盖健康备份。

`RecoveredFromBackup` 表示本次值来自 `.bak`；框架不会静默重写正式文件。建议：

1. 明确提示玩家已恢复备份，可能丢失最近一次进度。
2. 允许玩家进入游戏并检查结果。
3. 在下一次安全保存点写入新正式档。

正式档和备份都损坏、Codec 失败或 I/O 失败会抛 `SaveException`。只在能决定重试、选择其他槽位或返回标题页的边界上报一次。

## 4. 控制保存频率和 Payload

当前 API 是同步主线程 I/O，适合常规小型存档。不要每帧保存，也不要用 `Task.Run` 操作 Godot 文件路径。

推荐在明确里程碑保存：关卡完成、检查点、设置页面确认或应用失焦策略。自动保存前先构造纯数据快照，避免 Codec 遍历正在变化的场景树。

Payload 上限为 64 MiB。不要把贴图、音频、场景或其他大型 Resource 二进制塞进存档；保存稳定 ID 和必要状态。

## 5. 加载并应用设置

Boot 在启动首个 Procedure 前：

```csharp
ISettingsService settings = Services.Get<ISettingsService>();
SettingsLoadStatus status = settings.LoadAndApply();

if (status == SettingsLoadStatus.RecoveredFromBackup)
    ErrorHub.Warn("设置已从备份恢复。", "Game.Settings");
```

首次运行会应用默认值并返回 `DefaultsApplied`。设置方法立即修改内存并应用，但不自动写盘：

```csharp
settings.SetMasterVolume(0.8f);
settings.SetLocale("zh_CN");
settings.Save(); // 用户点击应用或确定时
```

滑块拖动时可以即时预览，松开或确认时再 Save，避免频繁写盘。`ResetToDefaults()` 也只应用默认值，需要显式 Save 才持久化。

## 6. 按平台能力构建设置页面

```csharp
resolutionPanel.Visible = settings.Supports(SettingsCapability.Resolution);
windowModePanel.Visible = settings.Supports(SettingsCapability.WindowMode);
vsyncPanel.Visible = settings.Supports(SettingsCapability.VSync);
```

Windows Desktop 支持音量、语言、窗口模式、分辨率和 VSync；Mobile 只保证音量与语言；未知平台安全降级到公共能力。

不支持的设置返回 `SettingsApplyResult.Unsupported`，且不修改当前快照。不要展示无法生效的控件，也不要把 Unsupported 当成成功。

`Current` 是不可变快照。设置非法音量、Locale 或分辨率时抛参数异常，并保持原状态。

## 7. 组织翻译键和动态文本

翻译键使用稳定语义 ID：

```csharp
string title = localization.Translate("UI.SETTINGS.TITLE");
string count = localization.TranslatePlural(
    "INVENTORY.ITEM_COUNT",
    "INVENTORY.ITEM_COUNT_PLURAL",
    itemCount);
```

不要以英文原句作为键。普通 Control 优先使用 Godot 自动翻译；运行时拼接、缓存文本和非 Control 内容在 `LocaleChangedEvent` 后刷新。

缺失翻译返回源键，不抛异常，也不会在查询热路径制造日志。内容验收应在发布前发现缺失键，而不是依赖运行时错误。

AvailableLocales 在服务初始化时建立。首版不支持运行时动态加入语言包；`SetLocale` 只接受默认语言或项目已加载翻译可匹配的规范 Locale。

## 8. 字体、RTL 和伪本地化

LocalizationService 不替换 Theme 字体。项目 Theme 必须配置覆盖目标字符集的 fallback 链，并在真实目标平台检查字体导入和内存。

RTL 语言需要人工验证：

- Control 布局方向和文本 Auto 方向。
- 图标含义与位置、边距和滚动条。
- 焦点移动顺序和手柄导航。
- 自定义绘制、数字、路径与混合方向文本。

伪本地化由 Godot 项目设置或 `TranslationServer.PseudolocalizationEnabled` 控制，不保存为玩家设置。用它检查文本膨胀、硬编码字符串、裁切和布局假设；`IsPseudolocalizationEnabled` 只供诊断。

## 9. 发布前检查

- 用空槽位、正常槽位、备份恢复和双重损坏分别测试存档 UI。
- 为每个仍支持的 dataVersion 保存固定回归样本。
- 确认设置页面只展示当前平台能力，并在真机验证移动端。
- 检查默认 Locale 已配置并拥有完整核心文本。
- 遍历所有支持语言，检查缺失键、复数、上下文和动态刷新。
- 使用伪本地化检查扩展文本，再用至少一种 RTL 语言检查布局。
- 在导出包中验证翻译资源和字体确实被包含。

## 常见错误

- 把设置存在每个进度槽：语言和音量会随槽位切换，改用 SettingsService 固定槽。
- 备份恢复后立刻静默覆盖：玩家无法判断丢失范围，应先提示并在安全点保存。
- 迁移只在当前模型上测试：必须保留真实旧 Payload 样本。
- 滑块每次变化都写盘：即时 Apply，确认时 Save。
- 移动端显示分辨率设置：先检查 `Supports`。
- 切换语言后部分文本不变：动态或缓存文本没有监听 LocaleChangedEvent。
- 中文或阿拉伯文显示方框：Theme 字体 fallback 不完整。
- RTL 只镜像文字未检查交互：焦点、图标和自定义绘制仍需人工验收。
- 把压缩或 SHA-256 当加密：它们不提供隐私或防作弊保证。

精确接口可查询 <xref:GoDo.ISaveService>、<xref:GoDo.ISaveCodec%601>、<xref:GoDo.SaveLoadResult%601>、<xref:GoDo.ISettingsService>、<xref:GoDo.SettingsCapability> 和 <xref:GoDo.ILocalizationService>。
