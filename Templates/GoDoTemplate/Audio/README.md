# Audio 资源位置

将项目级 BGM 和 SFX 资源放在此目录，并在 `Shared/StarterKeys.cs` 中集中定义对应的 `ResourceKey`。业务流程通过 `IAudioService` 播放，设置界面通过 `ISettingsService` 立即应用并显式保存 Master、BGM 与 SFX 音量。

模板不附带占位音频，避免把某种审美或授权来源变成项目默认内容。
