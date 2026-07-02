# AudioService 使用指南

## 定位

AudioService 是业务层统一播放 BGM、SFX 和管理分组音量的长期服务。音频资源通过 ResourceHub 加载，业务层通过 `Services.Get<IAudioService>()` 获取接口，不直接查找 Autoload 节点。

当前实现由 GoDoRuntime 持有，使用单路 BGM 播放器和 `NodePool<SfxVoice>`，并在启动时确保 BGM/SFX Bus 存在。

## 计划用法

```csharp
IAudioService audio = Services.Get<IAudioService>();

await audio.PlayBgmAsync(mainThemeKey);
bool played = await audio.PlaySfxAsync(explosionKey);

audio.SetVolume(AudioGroup.Bgm, 0.8f);
audio.PauseBgm();
audio.ResumeBgm();
```

## 已确定语义

- 所有资源键必须指向 Godot `AudioStream`。
- 同一 BGM 重复请求默认不重播，`restart: true` 才从头播放。
- BGM 加载期间 `IsBgmLoading` 为 true，并发 BGM 请求会明确失败；Stop 会使未完成请求取消。
- SFX 达到 `MaxSfxVoices` 时返回 false，不抢占现有声音。
- SFX 加载请求会预占并发名额；自然播放结束自动归还 NodePool，StopAll 主动归还且取消未完成请求。
- 音量使用 0–1 线性值；越界参数将明确失败，不静默截断。
- 资源加载或播放准备失败抛出 `AudioPlaybackException`。
- BGM 和 SFX 不因主场景切换自动停止。
- 首版不包含淡入淡出、空间音频、播放列表和跨 BGM 混音。

## 生命周期与配置

- `GoDoRuntime.tscn` 持有 AudioService、BgmPlayer 和 SfxRoot，并注册 `IAudioService`。
- 默认预热 8 个 SFX Voice，最大并发 32；可在 GoDoRuntime 场景中调整。
- 缺少 BGM/SFX Bus 时运行时创建并发送到 Master，同时通过 ErrorHub 给出 Warning。
- AudioService 退出树时停止 BGM、取消未完成请求并 Dispose SFX Pool。
- 所有 API 只能从 Godot 主线程调用。
