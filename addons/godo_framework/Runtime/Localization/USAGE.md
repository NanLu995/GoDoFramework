# Localization 使用指南

## 定位与边界

LocalizationService 复用当前 Godot 4.x 的 `TranslationServer`，提供项目已加载语言的查询、翻译、语言有效性校验和语言变更通知。它不解析 PO/CSV，不维护翻译缓存，也不负责持久化玩家选择。

SettingsService 负责语言选择与保存；业务代码通过 Settings 切换语言，通过 Localization 查询翻译。

## 项目约定

- 正式项目应在 `project.godot` 配置 `internationalization/locale/fallback`，并注册项目翻译资源；未配置时框架默认使用 `en`。
- 默认语言始终是有效选择；正式内容应为它提供完整翻译，空白核心包则安全回退到源键。
- 翻译键使用稳定的语义 ID，例如 `UI.MAIN_MENU.PLAY`，不以英文文案作为键。
- PO 是首选格式，支持复数、上下文和协作工具；CSV 仅适合小型表格内容。

## 调用方式

```csharp
ILocalizationService localization = Services.Get<ILocalizationService>();
string play = localization.Translate("UI.MAIN_MENU.PLAY");
string items = localization.TranslatePlural("ITEM.COUNT", "ITEM.COUNT_PLURAL", count);

ISettingsService settings = Services.Get<ISettingsService>();
settings.SetLocale("fr");
settings.Save();
```

动态 UI 文本、缓存文案和非 Control 文本应通过 EventChannel 监听公开的 `LocaleChangedEvent` 后刷新。普通 Control 优先使用 Godot 自动翻译与应用 Locale 布局方向。

## 失败语义与生命周期

- 空白或不在项目已加载语言集合内的 Locale 由 Settings 抛出 `ArgumentException`；当前设置不变。
- 空翻译键抛出 `ArgumentException`。
- 缺失翻译键沿用 Godot 行为，返回源键，不抛异常也不在查询热路径记录日志。
- 服务由 GoDoRuntime 在 UI 前创建并注册；所有公开 API 限制于 Godot 主线程。

## UI、字体与 RTL

- Control 使用应用 Locale 的自动布局方向和 Auto 文本方向；RTL 需人工验证图标、焦点、滚动条、边距与自定义绘制。
- 字体覆盖与 fallback 链由项目 Theme 管理，Localization 不自动替换 Theme。
- 场景、音频和图片本地化沿用 Godot 项目资源机制；动态语言包不属于首版。

## 性能与诊断

- 翻译查询是同步薄调用，无框架缓存；不要在每帧重复格式化同一文本。
- `AvailableLocales` 在初始化时建立一次；运行时动态增删翻译资源不属于首版。
- Godot 伪本地化继续由项目设置和 `TranslationServer.PseudolocalizationEnabled` 控制，不写入玩家 Settings；`IsPseudolocalizationEnabled` 提供只读诊断。

## 验证结果与待办

- Debug 与 Release 编译通过；新增代码无编译警告。
- `LocalizationRegression` 覆盖默认/可用语言、PO 翻译、上下文、复数、相近 Locale 匹配、Settings 状态与内存 Codec 往返、事件去重、非法 Locale、伪本地化诊断和无翻译资源回退。
- Services 与 Debugger 既有 Headless 回归通过。
- 核心包已在无翻译资源、无 GUIDE、无 Phantom Camera 的临时干净项目中通过编译与 9/9 服务运行验证。
- 统一 `all` 套件通过：17/17 核心工作区检查、GUIDE 1/1、Phantom Camera 1/1、Demo3D 2/2。
- RTL 布局、字体覆盖、真实导出包及 Windows 之外平台仍需手动或真机验证，因此首版不标记为跨平台稳定基线。
