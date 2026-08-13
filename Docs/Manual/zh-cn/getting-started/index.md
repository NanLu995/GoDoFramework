# 快速开始：搭建可运行的游戏骨架

本路线面向已经会使用 Godot 与 C# 的开发者。目标不是演示每个 API，而是在一个小型项目中建立正确边界：Runtime 管框架服务，Procedure 管顶层游戏阶段，Scene 管主内容，UI 管屏幕界面。

完成安装后，按顺序完成以下页面；每页都会留下一个可观察结果。

1. 本页：安装 Runtime 并确认服务可用。
2. [创建第一个游戏流程](first-procedure.md)：从业务启动场景进入 Procedure。
3. [切换第一个主内容场景](first-scene.md)：让流程加载主场景。
4. [打开主菜单与确认框](first-ui.md)：建立 Scene、View 与 Modal UI。
5. [从主菜单进入游戏并返回](switch-procedures.md)：用事件连接 UI 意图与流程切换。
6. [添加音频](add-audio.md)、[保存进度与设置](save-progress-and-settings.md)、[本地化游戏文本](localize-game-text.md)：补齐常见游戏基础能力。

完成这条路线后，转到[模块指南](../guides/index.md)，按模块深入当前项目需要的能力。

## 前置条件

- Godot 4.7.1 .NET 版本。
- 可用的 C# 解决方案，并已至少成功完成一次 Debug 编译。
- 目标项目使用 .NET 8；Android 构建按项目要求使用 .NET 9。

## 1. 复制框架目录

将完整核心目录复制到目标项目，不拆分 Core、编辑器安装助手和核心 Runtime 模块：

```text
addons/godo_framework/
```

核心包可以不包含 `Integrations/`；缺少该可选目录不会阻止编辑器插件或 Runtime 安装。需要 GUIDE Input 或 Phantom Camera 时，再按[集成与扩展](../integrations/index.md)叠加对应包和第三方依赖。

不要复制本仓库的 `project.godot`、`.csproj`、验证场景或 Demo 作为目标项目配置。

## 2. 启用编辑器插件

在 Godot 中打开“项目设置 → 插件”，启用 `GoDo Framework`。启用插件只注册编辑器工具，不会自动安装 Autoload。

## 3. 检查并安装 Runtime

1. 完成一次目标项目 C# Debug 编译。
2. 打开编辑器顶部的“GoDo → 设置 (Setup)...”。
3. 处理检查窗口中的错误。
4. 检查全部通过后，显式点击“安装 Runtime”。

插件只会在明确操作后安装唯一的 `GoDoRuntime` Autoload；也可在“项目配置 → C# 项目”中预览并确认补齐 GoDo 自有条件编译规则。两类操作都不会修改输入映射、导出预设或业务场景。

## 4. 确认服务可用

在业务代码中可以通过 `Services.Get<T>()` 获取已经注册的长期服务：

```csharp
using GoDo;

IProcedureService procedures = Services.Get<IProcedureService>();
IUiService ui = Services.Get<IUiService>();
IAudioService audio = Services.Get<IAudioService>();
```

业务入口负责开始自己的第一个 Procedure。不要在业务场景中重复初始化 GoDoRuntime，也不要把菜单或关卡流程写进 GoDoRuntime。

## 预期结果

- Godot 的 Autoload 列表中只有一个 `GoDoRuntime`。
- Setup 窗口的框架检查通过。
- C# 代码能够获取已经注册的服务。
