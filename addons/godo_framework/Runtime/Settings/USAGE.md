# SettingsService 使用指南

## 定位与边界

SettingsService 管理音量、语言选择与平台显示偏好，并与游戏进度存档分离。它自动检测当前平台并选择能力适配器，通过显式依赖使用 AudioService、SaveService 和 LocalizationService。语言有效性、翻译查询和变更通知由 Localization 负责。

它不管理角色进度、键位映射、画质预设、云同步或平台账户设置。

## 首版调用方式

```csharp
ISettingsService settings = Services.Get<ISettingsService>();
SettingsLoadStatus status = settings.LoadAndApply();

settings.SetMasterVolume(0.8f);
settings.SetWindowMode(SettingsWindowMode.Borderless);

// 用户点击“应用/确定”后再持久化。
settings.Save();
```

设置方法立即更新内存并应用到运行时，但不会自动写盘，避免滑块拖动时频繁保存。

## 平台能力

```csharp
if (settings.Supports(SettingsCapability.Resolution))
    ShowResolutionOptions();
```

- WindowsDesktop：音量、语言、窗口模式、分辨率、VSync。
- Mobile：音量和语言；窗口相关能力返回 Unsupported。
- CommonOnly：未知平台安全降级，保留音量和语言并输出 Warning。
- 正式运行默认自动检测；测试允许显式覆盖平台适配器。

## 数据与持久化

- `Current` 返回不可变 `SettingsSnapshot`。
- Settings 使用 SaveService 的独立固定槽位，不与游戏进度混合。
- NotFound 时应用默认值并返回 `DefaultsApplied`。
- 正式设置损坏但备份可用时返回 `RecoveredFromBackup`。
- Debug 与 Release 使用同一种权威格式。

## 已确定规则

- 音量必须为 0–1 的有限值。
- Locale 不能为空，且必须是默认 Locale 或能由已加载翻译匹配的规范 Locale；可用语言列表由 Localization 提供。
- 分辨率必须为正数；移动端不应用桌面分辨率。
- 不支持的能力返回 `Unsupported`，不静默假装成功。
- `ResetToDefaults` 只应用默认值，调用方需要显式 `Save`。

## 实现状态

Windows 首版稳定基线已经完成。SettingsService、内部 Codec、Save/Audio/Localization 显式接入和 GoDoRuntime 注册均已落地；服务使用固定槽位 `godo-settings` 和数据版本 1，模块内部不通过 Services 横向查找依赖。

原有 `SettingsService(IAudioService, ISaveService)` 构造函数为源码兼容继续保留，但会创建独立 Localization 实例并已标记弃用；新代码应显式传入 GoDoRuntime 使用的 `LocalizationService`。

## 失败语义

- 非法音量、空或不受支持的 Locale、未知窗口模式和非正分辨率抛出参数异常，且不会更新 `Current`。
- SaveService 的读取、校验、Codec 或写入失败继续抛出 `SaveException`，SettingsService 不重复上报后再抛出。
- 平台声明支持某项能力但 Adapter 返回 `Unsupported` 时抛出 `InvalidOperationException`，避免能力声明与实际行为静默不一致。
- 单项不受支持时返回 `Unsupported`，且不会修改对应的内存设置。

## 验证结果与后续要求

Windows PC 已在 Godot 运行时通过以下验证：

- 首次加载默认值、内存修改、保存恢复、备份来源状态映射和非法参数。
- 使用独立测试槽位完成真实 SaveService 写入、重建服务读取和测试文件清理。
- Windows 窗口模式、分辨率、VSync、Locale 与 Master/BGM/SFX 真实应用，并在验证后恢复原状态。
- 移动模拟 Adapter 对桌面能力返回 `Unsupported`，且不修改当前快照。
- 100 次修改/保存/加载循环：Debug 耗时 0 ms，当前线程累计分配 104840 bytes。
- GoDoRuntime 能够提供已注册的 `ISettingsService`。

Settings 对正式文件损坏与双重损坏的处理沿用已验证的 SaveService 失败语义。Android/iOS 在具备导出环境后仍需补充真机验证，不能仅凭 Windows 上的模拟结果宣称移动端通过。
