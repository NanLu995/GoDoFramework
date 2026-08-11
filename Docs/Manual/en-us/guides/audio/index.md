---
translation_of: Docs/Manual/zh-cn/guides/audio/index.md
translation_source_hash: sha256:99ffe3539d8f69590d534dedcb62800702f27eac0c3968ad822df957e56628ca
---

# Audio: music, sound effects, and playback budgets

`AudioService` owns long-lived BGM, non-spatial SFX, 3D SFX, and the Master/BGM/SFX Audio Buses. Obtain it through `Services.Get<IAudioService>()`; scenes should not create their own players, own SFX pools, or set global volume.

## What you can build

| Task | Capability | Result |
| --- | --- | --- |
| Play a level theme | BGM playback or crossfade | Music survives business-scene changes and callers can await it |
| Button, hit, and pickup feedback | Non-spatial SFX | Reused voices with observable capacity rejection |
| Explosions, vehicles, and skills | 3D SFX | A world position or a target followed at physics frequency |
| Volume sliders | Audio Bus volume | One change affects Master, BGM, or SFX |
| Avoid first-use hitches | Resource preparation and voice prewarming | Load streams or create reusable players before a burst |

## Start here

```csharp
IAudioService audio = Services.Get<IAudioService>();

await audio.PlayBgmAsync(mainThemeKey);
bool played = await audio.PlaySfxAsync(buttonClickKey);
audio.SetVolume(AudioGroup.Bgm, 0.7f);
```

`played == false` means this request did not start because of capacity or admission policy, not a resource-loading error. A resource that is not an `AudioStream`, load failures, and playback preparation failures raise `AudioPlaybackException`.

## Capability map

This page lists every business-facing capability. Each section explains when to use it and shows the smallest call; use the [IAudioService API](xref:GoDo.IAudioService) for exact parameters, return values, and exceptions.

### BGM: playback, transitions, and state

Use BGM for long audio whose boundary is chosen by a Procedure or another explicit scene coordinator.

<div class="godo-capability-list">
<section><h4>Play or restart</h4><p>There is no BGM, or it should switch immediately.</p><pre class="godo-capability-call"><code>await audio.PlayBgmAsync(key, restart: false);</code></pre></section>
<section><h4>Crossfade</h4><p>Keep the current track until the new one loads, then switch smoothly.</p><pre class="godo-capability-call"><code>await audio.CrossfadeBgmAsync(key, 0.5d);</code></pre></section>
<section><h4>Fade out</h4><p>Leave play or enter a muted state.</p><pre class="godo-capability-call"><code>await audio.FadeOutBgmAsync(0.5d);</code></pre></section>
<section><h4>Pause, resume, or stop now</h4><p>Pause menus, focus loss, or cancel loading and allow a new BGM request.</p><pre class="godo-capability-call"><code>audio.PauseBgm();
audio.ResumeBgm();
audio.StopBgm();</code></pre></section>
<section><h4>Read state</h4><p>Update playback UI or diagnose flow.</p><pre class="godo-capability-call"><code>audio.CurrentBgm
audio.IsBgmPlaying
audio.IsBgmLoading
audio.BgmState</code></pre></section>
</div>

A newer crossfade or fade-out replaces an active transition, so an older waiter receives `OperationCanceledException`. `durationSeconds` must be finite and positive.

### Non-spatial SFX: one-shot playback, policy, and handles

Use it for short feedback independent of world position. Basic overloads do not preempt active sounds; use structured options when you need admission policy or a handle.

```csharp
bool played = await audio.PlaySfxAsync(clickKey);

SfxPlaybackResult result = await audio.PlaySfxAsync(
    explosionKey,
    new SfxPlaybackOptions(
        volumeLinear: 0.8f,
        pitchScale: 1f,
        priority: SfxPriority.High,
        maxConcurrentPerKey: 4,
        allowStealLowerPriority: true));

if (result.Started)
    audio.TryStopSfx(result.Handle);
```

<div class="godo-capability-list">
<section><h4>Default playback</h4><p>No per-play settings are needed.</p><pre class="godo-capability-call"><code>await audio.PlaySfxAsync(key)</code></pre></section>
<section><h4>Per-play volume and pitch</h4><p>One stream needs small variation.</p><pre class="godo-capability-call"><code>await audio.PlaySfxAsync(key, volumeLinear, pitchScale)</code></pre></section>
<section><h4>Admission policy and a handle</h4><p>Limit per-key concurrency, set priority, or permit preemption.</p><pre class="godo-capability-call"><code>await audio.PlaySfxAsync(key, options)</code></pre></section>
<section><h4>Inspect or stop one sound</h4><p>A looping or sustained sound needs explicit control.</p><pre class="godo-capability-call"><code>audio.IsSfxPlaying(handle)
audio.TryStopSfx(handle)</code></pre></section>
<section><h4>Stop all</h4><p>Reset a scene or leave the game.</p><pre class="godo-capability-call"><code>audio.StopAllSfx()</code></pre></section>
</div>

`Started` means the handle is valid. `GlobalCapacityReached`, `PerKeyLimitReached`, and `PreemptedBeforeStart` are expected admission results. A handle expires after natural completion, stopping, or preemption.

### 3D SFX: fixed positions and following targets

Use a fixed world position for explosions, landings, and hits. Use following only for sounds that truly keep moving, such as an engine or sustained skill. Both use independent 3D Voice capacity.

```csharp
Sfx3DPlaybackResult explosion = await audio.PlaySfx3DAsync(
    explosionKey,
    explosionGlobalPosition,
    new Sfx3DPlaybackOptions(maxDistance: 80f, unitSize: 8f));

Sfx3DPlaybackResult engine = await audio.PlaySfx3DFollowAsync(
    engineKey,
    vehicle,
    new Vector3(0f, 0.5f, -1f),
    new Sfx3DPlaybackOptions(maxDistance: 60f, unitSize: 4f));
```

<div class="godo-capability-list">
<section><h4>Fixed-position playback</h4><p>A one-off world event.</p><pre class="godo-capability-call"><code>await audio.PlaySfx3DAsync(key, globalPosition, options)</code></pre></section>
<section><h4>Following playback</h4><p>A valid <code>Node3D</code> remains the sound source.</p><pre class="godo-capability-call"><code>await audio.PlaySfx3DFollowAsync(key, target, localOffset, options)</code></pre></section>
<section><h4>Prewarm 3D voices</h4><p>A combat burst is expected.</p><pre class="godo-capability-call"><code>audio.PrewarmSfx3DVoices(target)</code></pre></section>
<section><h4>Inspect or stop one sound</h4><p>Manage a looping or sustained 3D sound.</p><pre class="godo-capability-call"><code>audio.IsSfx3DPlaying(handle)
audio.TryStopSfx3D(handle)</code></pre></section>
<section><h4>Stop all 3D sounds</h4><p>Clear world audio without affecting normal SFX.</p><p><code>audio.StopAllSfx3D()</code></p></section>
</div>

`Sfx3DPlaybackOptions` requires finite positive `MaxDistance` and `UnitSize`. A following target must be valid and in the scene tree; loss while loading returns `TargetUnavailableBeforeStart`, while the independent follow budget returns `FollowCapacityReached`.

### Resource preparation and Voice prewarming

These solve different problems: preparation does not create Voices; prewarming does not load streams. Do both in loading flow, not in combat frames or another hot path.

<div class="godo-capability-list">
<section><h4>Prepare an <code>AudioStream</code></h4><p>Load through ResourceHub and validate its type early.</p><p><code>await audio.PrepareSfxAsync(key)</code></p></section>
<section><h4>Prewarm normal or 3D Voices</h4><p>Raise the matching pool total to a target.</p><pre class="godo-capability-call"><code>audio.PrewarmSfxVoices(target)
audio.PrewarmSfx3DVoices(target)</code></pre></section>
<section><h4>Inspect normal-pool pressure</h4><p>Decide whether configuration or gameplay requests need adjustment.</p><p><code>ActiveSfxCount</code>, <code>PendingSfxCount</code>, <code>PreparedSfxVoiceCount</code>, <code>MaxSfxVoices</code></p></section>
<section><h4>Inspect 3D-pool pressure</h4><p>Observe spatial and follow budgets.</p><p><code>ActiveSfx3DCount</code>, <code>PendingSfx3DCount</code>, <code>PreparedSfx3DVoiceCount</code>, <code>MaxSfx3DVoices</code>, <code>FollowingSfx3DCount</code>, <code>MaxFollowingSfx3DVoices</code></p></section>
<section><h4>Inspect accumulated rejection and preemption</h4><p>Diagnose dropped sounds.</p><p><code>RejectedSfxCount</code>, <code>PreemptedSfxCount</code>, <code>RejectedSfx3DCount</code>, <code>PreemptedSfx3DCount</code></p></section>
</div>

Prewarm targets must stay within the matching maximum Voice count. Cancelling `PrepareSfxAsync` cancels only this wait; ResourceHub's shared underlying load can continue.

### Audio Bus: read and set group volume

Use this for settings UI. Persist values with Settings rather than iterating over active Voices.

```csharp
audio.SetVolume(AudioGroup.Master, 1f);
audio.SetVolume(AudioGroup.Bgm, 0.7f);
audio.SetVolume(AudioGroup.Sfx, 0.9f);
float currentBgmVolume = audio.GetVolume(AudioGroup.Bgm);
```

The available groups are `Master`, `Bgm`, and `Sfx`; volume must be a finite linear value from 0 to 1.

## Choose the right call

<div class="godo-capability-list">
<section><h4>A flow transition with music</h4><p>Use <code>CrossfadeBgmAsync</code>; avoid multiple nodes each calling <code>PlayBgmAsync</code>.</p></section>
<section><h4>A simple click</h4><p>Use default <code>PlaySfxAsync</code>; avoid creating a player for every click.</p></section>
<section><h4>A critical hit sound</h4><p>Use <code>SfxPlaybackOptions</code>; do not assume a full pool still plays it.</p></section>
<section><h4>A one-off world sound</h4><p>Use <code>PlaySfx3DAsync</code>; avoid following a temporary explosion node.</p></section>
<section><h4>A moving sustained source</h4><p>Use <code>PlaySfx3DFollowAsync</code> and retain its handle; avoid unbounded following Voices.</p></section>
<section><h4>Loading a level's audio</h4><p>Use <code>PrepareSfxAsync</code> plus targeted prewarm; avoid repeatedly prewarming from <code>_Process</code>.</p></section>
</div>

## Constraints and troubleshooting

- Call all APIs from the Godot main thread, after service initialization, while the service remains in the scene tree.
- Handle stream-loading/type failures as exceptions. Capacity, per-key limits, preemption, and follow budgets are result states, not resource failures.
- Reliable 3D playback needs an active `Camera3D` or `AudioListener3D`. Looping sounds do not finish naturally: retain a handle and stop them explicitly.
- See the [Audio API Reference](xref:GoDo.IAudioService) for the complete failure and lifecycle contract; see the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for settings persistence.
