# GoDoFramework

[![Documentation](https://github.com/NanLu995/GoDoFramework/actions/workflows/docs.yml/badge.svg)](https://github.com/NanLu995/GoDoFramework/actions/workflows/docs.yml)

[在线文档](https://nanlu995.github.io/GoDoFramework/) · [文档维护说明](Docs/README.md)

GoDoFramework 是面向 Godot 4.7.1 C# / .NET 的轻量游戏开发框架，用于沉淀跨项目可复用的通信、诊断、生命周期管理和运行时服务，让具体项目把主要精力放在游戏逻辑与内容上。

## 升级 Godot

`GoDoFramework.csproj` 是 Godot patch 版本的来源。升级工具会同步最低版本检查、本机 VS Code 路径、版本说明和文档哈希；CI 与验证脚本会动态读取 csproj，不需要再手工修改下载地址或可执行文件名。

```powershell
python Tools/update_godot_version.py 4.7.2 `
    --godot "E:\Godot\Godot_v4.7.2\Godot_v4.7.2-stable_mono_win64_console.exe" `
    --verify
```

只检查当前仓库是否一致：

```powershell
python Tools/update_godot_version.py --check
```

`project.godot` 的 `config/features` 使用 Godot 主次版本标签，patch 升级时仍保持 `4.7`；升级到新的主次版本时，需要额外复核该标签、离线 API 文档和 DataTable 导出限制。

框架不会替代 Godot，也不包含角色、战斗、关卡等具体玩法逻辑。所有框架 API 位于 `GoDo` 命名空间。

## 第三方插件

仓库只跟踪 GoDo 自有目录 `addons/godo_framework/`；GUIDE Input、Phantom Camera 等可选适配位于其 `Integrations/` 子目录。`addons/guideCS/` 与 `addons/phantom_camera/` 为本地安装的第三方依赖，不随仓库提交。

- 使用 GUIDE 输入适配包或 Demo3D 输入资源前，先复制 GUIDE / G.U.I.D.E-CSharp，再通过顶部 `GoDo → GUIDE Input 设置...` 显式安装并校验 Autoload 顺序；具体步骤见 `addons/godo_framework/Integrations/GuideInput/USAGE.md`。
- 使用 Phantom Camera 适配包或 Demo3D 镜头前，通过顶部 `GoDo → Phantom Camera 设置...` 检查并显式启用第三方插件；版本要求见 `addons/godo_framework/Integrations/PhantomCamera/USAGE.md`。

## 项目文档

- `AI/FRAMEWORK_OVERVIEW.md`：框架愿景、痛点和历史设想。
- `AI/FRAMEWORK_DESIGN_PLAN.md`：整体设计计划与开发路线。
- `AI/ARCHITECTURE.md`：当前架构事实、模块状态和依赖约束。
- `AGENTS.md`：AI 协作与代码规范。
- `AI/GODOT_GOTCHAS.md`：项目实际遇到的 Godot/C# 问题记录。

## 快速上手模板

- `Templates/Demo3D/`：框架能力验证 Demo，演示 Procedure、Scene、UI、Audio、Input、Camera、Save、Settings、Config、EventChannel、ErrorHub 和 Services 的组合用法；它依赖 GUIDE / G.U.I.D.E-CSharp 与 Phantom Camera。
- 新项目请按 `AI/AI_GAMEDEV_GUIDE.md` 和 `AI/PROJECT_STRUCTURE.md` 建立自己的业务目录与启动场景，不直接依赖仓库模板。
