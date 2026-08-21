# SettingsService 使用指南

## 定位与边界

SettingsService 管理音量、语言选择与平台显示偏好，并允许游戏注册自己拥有的业务设置模块。它自动检测当前平台并选择能力适配器，通过显式依赖使用 AudioService、SaveService 和 LocalizationService。语言有效性、翻译查询和变更通知由 Localization 负责。

框架只提供模块注册、稳定顺序、生命周期、错误隔离和持久化机制，不认识 Camera、Combat、Accessibility 等业务概念。具体字段、运行时依赖、验证和迁移始终由游戏层拥有。Settings 不管理角色进度、云同步或平台账户设置。

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

## 注册业务设置模块

业务模块实现 `ISettingsModule<TSettings>`，通过构造参数接收业务对象或业务接口，并在业务启动入口、第一次 `LoadAndApply` 之前注册：

```csharp
ISettingsService settings = Services.Get<ISettingsService>();
settings.RegisterModule(new SamplePreferencesModule(preferencesRuntime));
SettingsLoadStatus status = settings.LoadAndApply();
```

模块不能通过静态字段保存当前设置或查找业务全局对象。框架不引用模块的具体数据类型；业务模块可以引用业务层接口或实例，否则无法完成运行时应用。注册一旦开始加载或重置即关闭，不支持运行时注销、反射发现或依赖图。

模块顺序先按 `Order` 升序，再按稳定 `Id` 的序号顺序排列。相同 ID 会立即拒绝；ID 必须是 1–40 个小写 ASCII 字母、数字或非首尾连字符。模块元数据在注册时冻结，之后修改实现属性不会改变槽位或顺序。

一个不含具体游戏概念的完整示例见 `Verification/Automated/Fixtures/SamplePreferencesModule.cs`。其数据版本 1 只有 `NoticeLevel`，版本 2 增加 `CompactPresentation`；`Decode` 同时完成旧版本迁移。

## 生命周期与线程

- SettingsService 由 GoDoRuntime 创建并注册；业务层通过 `ISettingsService` 获取，不自行构造第二个长期实例。
- `LoadAndApply`、`Save`、`ResetToDefaults` 和所有 `Set*` 方法只能在 Godot 主线程调用。它们会同步访问 Audio、Localization、DisplayServer 或 SaveService，不应包进 `Task.Run`。
- `RegisterModule` 同样只能在主线程调用，且必须发生在第一次 `LoadAndApply` 或 `ResetToDefaults` 之前。
- `Current` 是不可变快照，但只在设置成功应用后替换；不支持的平台能力返回 `Unsupported` 并保持对应快照字段不变。
- 启动时应在首个依赖玩家设置的流程之前调用一次 `LoadAndApply`；退出前是否额外保存由业务的“应用/确定”策略决定，服务不会自动保存尚未确认的修改。
- `LoadAndApply` 失败后可以再次调用重试；重试会按完整稳定顺序重新加载，因此模块 `Apply` 必须可重复执行。
- GoDoRuntime 退出时按加载顺序的逆序调用模块 `Shutdown`。服务关闭后注册、加载、保存、重置和修改操作均失败；`Shutdown` 本身可重复调用。

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
- 框架系统设置继续使用固定槽位 `godo-settings`，格式和版本 1 保持不变。
- 每个业务模块使用 `godo-settings-module-{id}` 独占槽位，由 SettingsService 拥有命名权；模块不得直接读写该槽位。
- 独立槽位让单模块损坏、升级或移除不影响系统设置及其他模块，并直接复用 SaveService 的校验、备份和提交语义。
- 移除模块注册不会自动删除其槽位，避免暂时禁用模块造成不可恢复的数据丢失。
- NotFound 时应用默认值并返回 `DefaultsApplied`。
- 正式设置损坏但备份可用时返回 `RecoveredFromBackup`。
- Debug 与 Release 使用同一种权威格式。
- 模块通过 `CurrentVersion` 声明写入版本，通过 `Decode(payload, dataVersion)` 读取和迁移旧版本。
- 可选模块遇到不可识别的更高版本时会应用运行时默认值，但禁止本次服务实例覆盖该模块槽位；显式 `ResetToDefaults` 表示用户接受重置，之后才重新允许保存。
- `Save` 先提交系统设置，再按稳定顺序逐槽提交模块。多个槽位不具备跨文件事务，已经成功提交的槽位不会因后续关键模块失败而回滚。

## 已确定规则

- 音量必须为 0–1 的有限值。
- Locale 不能为空，且必须是默认 Locale 或能由已加载翻译匹配的规范 Locale；可用语言列表由 Localization 提供。
- 分辨率必须为正数；移动端不应用桌面分辨率。
- 不支持的能力返回 `Unsupported`，不静默假装成功。
- `ResetToDefaults` 只应用默认值，调用方需要显式 `Save`。
- `ResetToDefaults` 同时处理全部已注册模块；可选模块失败会跳过，关键模块失败会阻断。

## 性能

- 模块机制不启用 `_Process` / `_PhysicsProcess`，只在注册、显式加载、重置、保存和关闭时工作。
- 模块在加载前按 `Order` 与 ID 排序，成本为 O(n log n)；加载和保存为 O(n) 次模块调用及独立同步文件 I/O。设置模块数量应保持为少量业务域，而不是按字段注册。
- 每个模块独立槽位会增加文件数量和逐槽提交成本，但换取损坏、版本和移除隔离；设置页面仍应在“应用/确定”时保存，不在滑块变化时逐次写盘。
- Payload 大小、哈希、临时文件和备份成本沿用 SaveService；模块 Codec 不应遍历场景树或捕获 Godot 对象图。

## 实现状态

Windows 系统设置稳定基线已经完成。SettingsService、内部 Codec、Save/Audio/Localization 显式接入和 GoDoRuntime 注册均已落地；系统设置继续使用固定槽位 `godo-settings` 和数据版本 1。业务模块扩展为首版能力，等待真实项目接入验证。

SettingsService 只保留显式接收共享 `LocalizationService` 的构造入口，避免创建与 GoDoRuntime 不一致的第二个本地化实例。

## 失败语义

- 非法音量、空或不受支持的 Locale、未知窗口模式和非正分辨率抛出参数异常，且不会更新 `Current`。
- SaveService 的读取、校验、Codec 或写入失败继续抛出 `SaveException`，SettingsService 不重复上报后再抛出。
- 平台声明支持某项能力但 Adapter 返回 `Unsupported` 时抛出 `InvalidOperationException`，避免能力声明与实际行为静默不一致。
- 单项不受支持时返回 `Unsupported`，且不会修改对应的内存设置。
- `Optional` 模块的读取或数据验证失败时尝试默认值；运行时应用失败不再次应用默认值，避免在未知的部分应用状态上继续修改。失败可从 `LastModuleFailures` 查看。
- `Critical` 模块失败抛出 `SettingsModuleException`，其中包含模块 ID、阶段和原始异常。初始化失败前已经应用的系统设置或前序模块不会回滚。
- 可选模块的保存失败会记录后继续；关键模块保存失败会在其余模块均尝试后统一抛出。系统设置保存失败仍立即抛出 `SaveException`。
- 模块 `Apply` 应先完成可能失败的准备，再一次性发布新状态；框架无法回滚任意业务对象。

## 验证结果与后续要求

Windows PC 已在 Godot 运行时通过以下验证：

- 首次加载默认值、内存修改、保存恢复、备份来源状态映射和非法参数。
- 使用独立测试槽位完成真实 SaveService 写入、重建服务读取和测试文件清理。
- Windows 窗口模式、分辨率、VSync、Locale 与 Master/BGM/SFX 真实应用，并在验证后恢复原状态。
- 移动模拟 Adapter 对桌面能力返回 `Unsupported`，且不修改当前快照。
- 100 次修改/保存/加载循环：Debug 耗时 0 ms，当前线程累计分配 104840 bytes。
- GoDoRuntime 能够提供已注册的 `ISettingsService`。

Settings 对正式文件损坏与双重损坏的处理沿用已验证的 SaveService 失败语义。Android/iOS 在具备导出环境后仍需补充真机验证，不能仅凭 Windows 上的模拟结果宣称移动端通过。

### 自动回归验证

`Verification/Automated/SettingsServiceRegression.tscn` 使用内存依赖和随机测试槽位验证无模块兼容行为、单模块加载/应用/重复保存、多模块稳定顺序、损坏与更高版本、可选降级、关键阻断、重复注册、关闭边界、模块迁移、系统设置版本 1 兼容，以及既有平台和 SaveService 失败语义。目标用例数为 11。
