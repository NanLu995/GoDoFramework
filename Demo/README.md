# 点击挑战验证 Demo

## 目的

这个 Demo 用最小游戏流程验证 GoDoFramework 的实际使用体验，不是正式游戏模板，也不为业务代码建立额外 Manager 或 Service。

## 运行

在 Godot 中直接运行 `Demo/Scenes/DemoBoot.tscn`。Demo 不修改项目主场景，也不重复初始化 GoDoRuntime。

流程：启动 → 主菜单 → 点击挑战 → 结算 → 重试或返回主菜单。

## 覆盖范围

- Services：获取 Scene、Audio、Save、Settings 长期服务。
- ResourceHub / Config：加载并校验局时与单击得分配置。
- SceneService：四个主场景之间异步切换。
- SaveService：保存最高分、累计局数和上一局成绩。
- SettingsService：读取、应用并保存 Master 音量。
- AudioService：循环 BGM、点击 SFX、音量联动与场景间常驻。
- EventChannel：保存成功后广播 `RunFinishedEvent`；当前没有真实的一对多监听需求，因此不为展示功能强行增加监听者。
- Debugger：运行时观察 FPS、场景、音频、服务与事件状态。

## 已验证

- 完整玩一局、结算、重试和返回主菜单正常。
- Config 数值生效，最高分与累计局数能够持久化。
- 设置保存后重新启动仍能应用。
- BGM、点击 SFX 和 Master 音量联动正常。
- Debug 构建编译为 0 警告、0 错误。

## 限制

`DemoBgm.tres` 和 `ClickSfx.tres` 是代码仓库内的极小 PCM 测试波形，只用于验证播放链路；BGM 的持续“呜”声是测试素材本身，不代表正式音频效果。

移动端触摸、窗口适配、发布导出和异常恢复仍需在目标平台与真实项目中验证。
