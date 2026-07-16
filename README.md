# GoDoFramework

GoDoFramework 是面向 Godot 4.7 C# / .NET 的轻量游戏开发框架，用于沉淀跨项目可复用的通信、诊断、生命周期管理和运行时服务，让具体项目把主要精力放在游戏逻辑与内容上。

框架不会替代 Godot，也不包含角色、战斗、关卡等具体玩法逻辑。所有框架 API 位于 `GoDo` 命名空间。

## 第三方插件

仓库仅跟踪 GoDo 自有目录：`addons/godo_framework/`、`addons/godo_guide_input/` 和 `addons/godo_phantom_camera/`。`addons/guideCS/` 与 `addons/phantom_camera/` 为本地安装的第三方依赖，不随仓库提交。

- 使用 GUIDE 输入适配包或 Demo3D 输入资源前，安装 GUIDE / G.U.I.D.E-CSharp；具体顺序见 `addons/godo_guide_input/USAGE.md`。
- 使用 Phantom Camera 适配包或 Demo3D 镜头前，安装并启用 Phantom Camera；版本要求见 `addons/godo_phantom_camera/USAGE.md`。

## 项目文档

- `FRAMEWORK_OVERVIEW.md`：框架愿景、痛点和历史设想。
- `FRAMEWORK_DESIGN_PLAN.md`：整体设计计划与开发路线。
- `ARCHITECTURE.md`：当前架构事实、模块状态和依赖约束。
- `AGENTS.md`：AI 协作与代码规范。
- `GODOT_GOTCHAS.md`：项目实际遇到的 Godot/C# 问题记录。

## 快速上手模板

- `Templates/StarterGame/`：可复制的 Starter 模板，展示 Procedure、Scene、UI、Audio、Save、Settings、Config、EventChannel、ErrorHub 和 Services 的组合用法。
- `Demo/`：框架能力验证 Demo，偏演示与验证；新项目建议优先参考 Starter 模板。
