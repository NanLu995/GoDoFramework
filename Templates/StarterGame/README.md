# StarterGame 模板

StarterGame 是 GoDoFramework 的可复制项目模板。一个小而完整的游戏骨架，开发者可以直接复制后替换成自己的业务代码。

## 运行

在 Godot 中直接运行：

```text
Templates/StarterGame/Boot.tscn
```

模板不修改项目主场景，也不重复初始化 GoDoRuntime。使用前请确认 GoDoRuntime Autoload 已安装。

## 流程

```text
Boot.tscn
  → BootProcedure
  → MainMenuProcedure（切换到 MainMenuScene.tscn）
  → GameplayProcedure
  → ResultProcedure
  → MainMenuProcedure / GameplayProcedure
```

## 目录结构

```text
Templates/StarterGame/
├── Boot.tscn       模板入口场景
├── Boot.cs         模板入口脚本
├── Audio/          全局复用的 BGM 与 SFX 资源
├── Shared/         跨流程共享的资源键、配置、存档、事件与启动流程
├── MainMenu/       主菜单流程、场景和脚本
├── Gameplay/       游戏流程、主内容场景、HUD 和分数面板
└── Result/         结算流程、界面和脚本
```

## 覆盖的框架用法

- Procedure：`Boot`、`MainMenu`、`Gameplay`、`Result` 顶层流程切换；流程通过 `EventScope` 监听业务事件，并通过 `RequestChange` 决定下一个流程。
- SceneService：启动后切到 MainMenuScene，进入 Gameplay 时切换主内容场景。
- UiService：GameplayHud 挂到 Scene 层，ResultView 挂到 View 层；主菜单作为主场景保持直观。
- AudioService：播放 BGM 和点击 SFX。
- SaveService：保存最高分、累计局数和上一局分数。
- SettingsService：读取、应用、保存 Master 音量。
- Config：读取并校验 `StarterGameConfig.tres`。
- EventChannel：UI/场景广播玩家选择和分数变化；Procedure 监听玩家选择，ScorePanel 通过生命周期绑定刷新显示。
- ErrorHub：业务边界捕获异常后补充上下文上报。
- ResourceKey：资源路径集中维护在 `StarterGameKeys`。
- Services：业务代码通过接口获取长期框架服务。

## 命名空间

模板代码默认使用：

```csharp
namespace StarterGame;
```

复制到真实项目后，建议统一替换为你的项目命名空间，例如：

```csharp
namespace MyGame;
```

模板代码属于业务侧示例，只引用 `GoDo`，不属于框架本体命名空间。

## 复制到新项目时建议保留

- `Boot.tscn`：作为模板入口场景，复制后可改名为项目自己的入口。
- `Shared/StarterGameKeys.cs`：改名后继续集中维护跨模块资源键。
- `Shared/` 中的配置、存档、事件：按自己的跨模块数据替换。
- `MainMenu/`、`Gameplay/`、`Result/`：按真实功能模块替换或扩展。

## 不建议在模板里继续加的东西

- 复杂角色、战斗、技能或关卡规则。
- 通用 StateMachine、黑板、流程栈或自动发现。
- 大型资源管理、远程下载或热更新。

这些应由真实项目需求推动，而不是模板预先内置。

## 手动验证建议

1. 运行 `Boot.tscn`。
2. 点击“开始游戏”。
3. 连续点击按钮直到时间结束。
4. 确认进入结算页，最高分和局数更新。
5. 返回主菜单，确认存档显示正确。
6. 调整音量并保存，重启后确认设置生效。
