# 模块指南

快速开始教你把能力串成一条游戏流程；本节按模块组织，适合已经完成接入、需要解决具体工程问题时查阅。每页都说明适用场景、最小调用边界、失败语义和常见误用；精确签名仍以 API Reference 为准。

## 运行时协作与流程

- [Services 与 EventChannel](services-and-events/index.md)：选择直接调用、Godot Signal 或广播事件，并正确管理订阅生命周期。
- [Procedure](procedure-recovery/index.md)：组织顶层游戏阶段、取消、失败恢复和并发切换。
- [Scene 与 ResourceHub](resources-and-scenes/index.md)：维护资源清单、异步加载与主内容场景切换。
- [Scheduler](scheduler/index.md)：安排延迟、循环任务和受所有者生命周期约束的异步等待。
- [NodePool](node-pool/index.md)：复用高频创建销毁的节点，并在租借/归还边界恢复状态。
- [Friflo ECS](friflo-ecs/index.md)：在场景级 World 中批量更新大量同构数据，并管理启动、暂停与退出生命周期。

## 游戏交互与展示

- [UI 与 Audio](ui-and-audio/index.md)：管理屏幕 UI 层级、焦点、BGM、音效与容量策略。
- [Input](input/index.md)：读取语义输入、切换 Context 与显示设备提示。
- [运行时改键](input-rebinding/index.md)：提供改键、冲突处理、恢复默认和持久化。
- [Camera](camera/index.md)：注册、激活和恢复主镜头；Phantom Camera 是可选集成。

## 游戏数据与发布准备

- [Config](configuration/index.md)：创建可在 Inspector 维护且可校验的强类型配置。
- [DataTable](data-tables/index.md)：从 CSV 生成、校验并在运行时读取数据表。
- [Save、Settings 与 Localization](save-settings-localization/index.md)：组织存档槽位、平台设置和游戏文本。

## 可观测性

- [Diagnostics](diagnostics/index.md)：记录日志、上报错误，并使用 Debugger 观察框架运行状态。

需要使用第三方输入、镜头或 ECS 后端时，先阅读[集成与扩展](../integrations/index.md)，再进入对应模块页。
