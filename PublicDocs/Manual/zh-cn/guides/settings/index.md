# Settings：应用并持久化玩家设置

`SettingsService` 管理音量、语言、窗口模式、分辨率和 VSync 等平台相关设置。它与 Save 分开：设置可以即时应用且按平台能力降级，游戏进度不应承担这些兼容分支。

## 使用建议

1. 先查询目标平台支持的能力，再决定是否显示某个设置控件。
2. 玩家修改时先应用并检查结果，再在合适的确认点持久化。
3. 启动时加载快照并应用；缺失设置使用项目定义的默认值。

## 关键规则

- 不把桌面窗口能力假设到移动平台。
- UI 只表达玩家选择；平台适配与应用结果由 SettingsService 处理。
- 音量与语言改变会影响其他模块，应使用服务提供的状态和事件刷新 UI。

平台能力、保存、音量和语言联动见[Save、Settings 与 Localization 工作流](../save-settings-localization/index.md)。

## 能力全景图

<div class="godo-capability-list">
<section><h4>读取平台、能力与当前快照</h4><p>先判断控件是否应显示，再读取当前值。</p><pre class="godo-capability-call"><code>settings.Platform
settings.Capabilities
settings.Current
settings.Supports(SettingsCapability.WindowMode)</code></pre></section>
<section><h4>启动时加载并应用</h4><p>读取持久化快照；缺失或损坏时以结构化结果回退。</p><pre class="godo-capability-call"><code>SettingsApplyResult result = settings.LoadAndApply();</code></pre></section>
<section><h4>应用音量与语言</h4><p>修改立即作用于 Audio 或 Localization，稍后再显式保存。</p><pre class="godo-capability-call"><code>settings.SetMasterVolume(1f);
settings.SetBgmVolume(0.7f);
settings.SetSfxVolume(0.9f);
settings.SetLocale("zh-CN");</code></pre></section>
<section><h4>应用显示设置</h4><p>只在平台支持对应能力时展示和调用。</p><pre class="godo-capability-call"><code>settings.SetWindowMode(SettingsWindowMode.Windowed);
settings.SetResolution(new Vector2I(1920, 1080));
settings.SetVSync(true);</code></pre></section>
<section><h4>持久化或恢复默认值</h4><p>确认玩家选择后保存；重置会重新应用项目默认快照。</p><pre class="godo-capability-call"><code>settings.Save();
settings.ResetToDefaults();</code></pre></section>
</div>

设置应用必须在 Godot 主线程执行；不支持的平台能力以结果反馈。完整契约见 [ISettingsService API](xref:GoDo.ISettingsService)。
