# AI Game Development Guide

本文档面向使用 GoDoFramework 制作游戏的 AI。它不是框架内部设计文档，而是“做游戏时如何稳定使用框架”的入口。

## 开始前先读

按任务范围读取，不要全量浏览：

1. 修改 `addons/godo_framework/`：先读 `ARCHITECTURE.md` 和对应模块 `USAGE.md`。
2. 使用 StarterGame 模板：先读 `Templates/StarterGame/README.md` 和 `PROJECT_STRUCTURE.md`。
3. 新增流程、界面、存档或配置：先读 `Docs/Recipes/` 下对应菜谱。
4. 不确定 Godot API：先查项目现有实现，再查离线 Godot 4.7 文档，最后用编译验证。

## 项目边界

- `GoDo.*` 只放跨游戏复用机制，不放玩法概念。
- 游戏业务代码放在项目自己的命名空间，例如 `StarterGame`、`MyGame`。
- 默认不要改 `addons/godo_framework/`。只有用户明确要求修改框架能力时才进入框架源码。
- 不修改 `project.godot`、`.csproj` 或 Autoload 配置，除非用户明确要求。
- 不新增 NuGet 包，除非先说明原因并获得确认。

## 推荐游戏入口

模板入口是：

```text
Templates/StarterGame/Boot.tscn
```

真实游戏可以复制后改名为项目入口，例如：

```text
Boot.tscn
Main.tscn
```

入口场景只负责启动业务流程，不重复初始化 GoDoRuntime。

## 推荐组合方式

常规游戏流程：

```text
Boot.tscn
  -> BootProcedure
  -> MainMenuProcedure
  -> GameplayProcedure
  -> ResultProcedure
```

职责划分：

- `Procedure`：组织顶层阶段切换，决定何时调用 Scene、UI、Audio、Save、Config。
- `SceneService`：切换主内容场景。
- `UiService`：打开 HUD、菜单、结算页、弹窗等屏幕空间 UI。
- `AudioService`：播放 BGM 和 SFX。
- `SaveService`：读写游戏进度和设置外的业务存档。
- `SettingsService`：管理音量、显示等通用设置。
- `ConfigHub`：读取强类型 `.tres` 配置。
- `EventChannel`：一对多事实通知和 UI/场景到 Procedure 的玩家意图通知。
- `ResourceKey`：集中维护资源路径，不在业务代码中散落字符串。

## AI 写游戏代码的默认做法

新增一个功能时，优先按功能模块组织文件：

```text
FeatureName/
├── FeatureProcedure.cs
├── FeatureScene.tscn
├── FeatureScene.cs
├── FeatureView.tscn
└── FeatureView.cs
```

跨功能复用的内容放到 `Shared/`：

```text
Shared/
├── GameKeys.cs
├── GameEvents.cs
├── GameConfig.cs
├── GameConfig.tres
├── SaveData.cs
└── SaveCodec.cs
```

UI 和场景脚本不要直接强转 `IProcedureService.Current`。推荐方式是：

1. UI 或场景通过 `EventChannel.Emit(...)` 发布玩家意图。
2. 当前 Procedure 用 `EventScope` 监听事件。
3. Procedure 使用 `ProcedureContext.RequestChange(...)` 请求切换流程。

## 常见禁止事项

- 不把角色、血量、子弹、关卡规则写进 `GoDo.*`。
- 不在 `GoDoRuntime` 中主动进入具体游戏流程。
- 不用 `ResourceLoader.Load` 替代已有 ResourceHub / ResourceKey 路线。
- 不在 `_Process` 或 `_PhysicsProcess` 中每帧调用 `GetNode`、LINQ 热查询或大量分配。
- 不用匿名 lambda 订阅需要解绑的 Godot Signal。
- 不直接 `QueueFree()` 受 UiService 管理的 UI，应使用 `IUiService.Close` 或 `TryGoBack`。
- 不把 Procedure 当通用状态机，不放角色局部状态或 AI 状态。

## 验证标准

代码改动后默认至少执行：

```powershell
dotnet build GoDoFramework.sln
```

涉及场景、UI、输入、节点生命周期或场景切换时，还需要提示用户在 Godot 中手动验证。

编译通过只说明 C# 和资源绑定基本可解析，不等于玩法行为已验证。

