# 5 分钟快速开始

本页帮助你把 GoDoFramework 接入现有 Godot 4.7 C# 项目，并确认 Runtime 服务已经可用。

## 前置条件

- Godot 4.7 .NET 版本。
- 可用的 C# 解决方案，并已至少成功完成一次 Debug 编译。
- 目标项目使用 .NET 8；Android 构建按项目要求使用 .NET 9。

## 1. 复制框架目录

将完整目录复制到目标项目，不拆分内部模块：

```text
addons/godo_framework/
```

不要复制本仓库的 `project.godot`、`.csproj`、验证场景或 Demo 作为目标项目配置。

## 2. 启用编辑器插件

在 Godot 中打开“项目设置 → 插件”，启用 `GoDo Framework`。启用插件只注册编辑器工具，不会自动安装 Autoload。

## 3. 检查并安装 Runtime

1. 完成一次目标项目 C# Debug 编译。
2. 打开编辑器顶部的“GoDo → 设置 (Setup)...”。
3. 处理检查窗口中的错误。
4. 检查全部通过后，显式点击“安装 Runtime”。

插件只会安装唯一的 `GoDoRuntime` Autoload，不会修改 `.csproj`、输入映射、导出预设或业务场景。

## 4. 从业务代码获取服务

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
- C# 代码能够通过 `Services.Get<T>()` 获取已经注册的长期服务。

## 下一步

- 阅读“使用框架制作游戏”了解 Procedure、Scene、UI、Audio、Save 等模块如何组合。
- 阅读“推荐项目结构”建立自己的 Boot 场景和业务目录。
- 按模块指南确认失败语义、生命周期、性能和验证范围。
