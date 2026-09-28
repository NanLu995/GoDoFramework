# Localization：切换游戏文本语言

`LocalizationService` 协调 Godot 的翻译资源、当前语言与设置持久化。它适合游戏可见文本；资源路径、玩家存档和业务标识不应依赖会被翻译的字符串。

## 使用建议

- 为游戏概念定义稳定翻译键，不以中文或英文显示文本作为业务 ID。
- 语言切换后监听语言变化并刷新动态 UI；静态场景文本仍按 Godot 的本地化方式配置。
- 发布前检查缺失翻译、字体覆盖、RTL 布局和伪本地化结果。

## 关键规则

- 当前服务不提供远程语言包或运行时内容下载。
- 字体、复数规则和 RTL 是内容与布局的共同责任，不能只用一个翻译调用解决。
- 语言设置应通过 Settings 持久化，不写进游戏进度存档。

翻译键、设置、字体与发布检查见[Save、Settings 与 Localization 工作流](../save-settings-localization/index.md)。

## 能力全景图

<div class="godo-capability-list">
<section><h4>读取语言状态</h4><p>显示当前语言、默认语言、可选语言和伪本地化状态。</p><pre class="godo-capability-call"><code>localization.DefaultLocale
localization.CurrentLocale
localization.AvailableLocales
localization.IsPseudolocalizationEnabled</code></pre></section>
<section><h4>检查语言支持</h4><p>导入设置或外部语言代码前先规范化并检查。</p><pre class="godo-capability-call"><code>bool supported = localization.IsLocaleSupported(locale);</code></pre></section>
<section><h4>翻译动态文本</h4><p>用于代码生成的文本；可选 context 用于区分同键语义。</p><pre class="godo-capability-call"><code>string text = localization.Translate("menu.start", context: "main-menu");</code></pre></section>
<section><h4>翻译复数文本</h4><p>传入数量并让翻译资源选择目标语言的复数形式。</p><pre class="godo-capability-call"><code>string text = localization.TranslatePlural("item.one", "item.many", count, context);</code></pre></section>
</div>

语言切换由 Settings 统一应用和持久化；Localization 负责查询与翻译。完整签名见 [ILocalizationService API](xref:GoDo.ILocalizationService)。
