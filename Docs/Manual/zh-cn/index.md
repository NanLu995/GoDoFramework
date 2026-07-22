# GoDoFramework 用户手册

GoDoFramework 是一套面向 Godot 4.7.1 C# 项目的游戏开发框架。它把场景切换、界面导航、音频、输入、存档、设置和游戏流程等常见工作整理成一致的使用方式，让你把更多精力放在游戏本身。

## 适合哪些项目

- 使用 Godot C# 开发，希望统一场景、UI、音频、输入、存档和流程使用方式的项目。
- 随着规模增长，需要明确失败行为、生命周期和线程限制的团队。

## 你可以用它做什么

- 组织从主菜单、游戏过程到结算界面的顶层流程。
- 切换主场景，并管理 HUD、菜单和弹窗。
- 播放 BGM 与音效，读取输入，控制主镜头。
- 保存游戏数据和玩家设置。
- 统一加载资源、读取配置并排查运行时问题。

框架不会替你设计角色、战斗或关卡规则，也不会限制你使用 Godot 原生节点和场景。它提供的是一组可以跨项目复用的基础能力。

## 从这里开始

第一次使用时，从 [5 分钟快速开始](getting-started/index.md) 安装并确认 Runtime，再依次完成第一个 Procedure、场景、UI、流程切换、音频、存档和本地化教程。

完成入门路线后，可以按当前任务选择：

- 框架接入与版本维护：[安装、升级与卸载](getting-started/install-upgrade-uninstall.md)。
- 建立框架使用模型：[服务与事件](guides/services-and-events/index.md)和 [Procedure 失败恢复](guides/procedure-recovery/index.md)。
- 制作游戏功能：[输入](guides/input/index.md)、[相机](guides/camera/index.md)、[UI 与音频](guides/ui-and-audio/index.md)。
- 管理游戏数据：[资源与场景](guides/resources-and-scenes/index.md)、[强类型配置](guides/configuration/index.md)、[数据表](guides/data-tables/index.md)。
- 完善发布质量：[存档、设置与本地化](guides/save-settings-localization/index.md)、[日志与诊断](guides/diagnostics/index.md)。
- 遇到问题时，从[故障排查](troubleshooting/index.md)按现象定位。

需要查询准确的类型、成员、参数或异常时，请使用 API Reference。
