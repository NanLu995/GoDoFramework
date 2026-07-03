# AudioService 使用指南

## 定位与优势

AudioService 是业务层统一播放非空间 BGM、SFX 和管理分组音量的长期服务。资源统一通过 ResourceHub 加载，业务层通过 `Services.Get<IAudioService>()` 获取接口，不查找 Autoload 节点，也不自行维护播放器池。

当前实现由 GoDoRuntime 持有，使用单路 BGM 播放器和 `NodePool<SfxVoice>`；主场景切换不会中断音频。

## 快速上手

```csharp
IAudioService audio = Services.Get<IAudioService>();

await audio.PlayBgmAsync(mainThemeKey);
bool played = await audio.PlaySfxAsync(explosionKey);

audio.SetVolume(AudioGroup.Bgm, 0.8f);
audio.PauseBgm();
audio.ResumeBgm();
```

`PlaySfxAsync` 返回 false 表示达到并发上限，不是资源错误。

## BGM 语义

- 资源必须是 `AudioStream`，加载或播放准备失败抛出 `AudioPlaybackException`。
- 同一资源重复请求默认不重播；`restart: true` 才从头播放。
- 加载期间 `IsBgmLoading` 为 true，第二个 BGM 请求抛出 `InvalidOperationException`。
- `PauseBgm`/`ResumeBgm` 只影响当前流；`StopBgm` 停止播放、释放流引用并清空 `CurrentBgm`。
- 加载期间调用 Stop 会使等待方收到 `OperationCanceledException`。

## SFX 语义

- 默认预热 8 个 SfxVoice，最大并发 32，可在 `GoDoRuntime.tscn` 调整。
- 加载请求会预占并发名额，防止多个请求同时完成后突破上限。
- 达到 `MaxSfxVoices` 时返回 false，不抢占已有声音。
- 自然播放结束通过 `Finished` 自动归还 NodePool。
- `StopAllSfx` 主动归还全部 Voice，并取消尚未完成的 SFX 加载。
- 循环 AudioStream 不会自然触发 Finished，必须由调用方 StopAll 或让服务退出。

## Audio Bus 与音量

```csharp
audio.SetVolume(AudioGroup.Master, 1.0f);
audio.SetVolume(AudioGroup.Bgm, 0.7f);
audio.SetVolume(AudioGroup.Sfx, 0.9f);

float bgmVolume = audio.GetVolume(AudioGroup.Bgm);
```

- 音量使用 0–1 的有限线性值，越界抛出 `ArgumentOutOfRangeException`。
- 缺少 BGM/SFX Bus 时运行时创建、发送到 Master，并通过 ErrorHub Warning 提示。
- 重复初始化不会创建同名 Bus。
- 运行时创建 Bus 不修改 `project.godot` 或持久化 Audio Bus Layout。

## 生命周期与线程

- `GoDoRuntime.tscn` 持有 AudioService、BgmPlayer 和 SfxRoot，并注册 `IAudioService`。
- 所有 API 只能在 Godot 主线程调用。
- AudioService 退出树时停止 BGM、取消未完成请求，并 Dispose SFX Pool。
- Services 只持有接口引用，不替 AudioService 管理释放。
- 业务场景中不要创建第二个 AudioService。

## 性能与验证基线

- SFX 使用 NodePool，空闲 Voice 保持在场景树外，不在每次播放时 Instantiate/QueueFree。
- 100 次缓存音效播放/停止 Debug 验证为 2 ms、当前线程累计分配 44688 bytes、活动 Voice 0。
- 已验证 Bus 重复初始化、音量、失败语义、BGM 并发拒绝、加载取消、自然回收、32 路上限、StopAll 和服务离树清理。
- 分配数据包含 Task、测试代码和 Godot 包装层开销，不等同于泄漏结论。

## 不负责的能力

首版不包含淡入淡出、播放列表、跨 BGM 混音、随机音高、语音系统，以及 AudioStreamPlayer2D/3D 空间音频。

## 常见误用

| 应该 | 避免 |
|---|---|
| 使用 ResourceKey 加载 AudioStream | 业务层散落 ResourceLoader 和字符串路径 |
| 处理 false、取消与 AudioPlaybackException | 把容量满当成资源异常 |
| 循环音效显式 StopAll | 等待循环流自然 Finished |
| 使用 Bus 控制分组音量 | 遍历所有播放器逐个改 Volume |
| await 异步播放准备 | fire-and-forget 后丢失异常 |
