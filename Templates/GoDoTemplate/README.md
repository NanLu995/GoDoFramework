# GoDoTemplate

GoDoFramework 的官方 Starter Template 源项目。它提供通用项目启动、流程、UI、设置与服务使用边界，不包含角色、战斗、背包、任务或关卡规则。

## 初始安装

1. 将 `GoDoTemplate` 目录复制为新游戏项目根目录。
2. 将 GoDoFramework 安装到项目的 `addons/godo_framework/`。
3. 使用 Godot 打开项目；`project.godot` 已声明 `GoDoRuntime` Autoload 和 GoDo EditorPlugin。
4. 等待 Godot 生成并加载 `GoDoTemplate.csproj`，再运行 `Boot/Boot.tscn`。

可选输入集成不随模板分发。需要输入 Context、改键或输入提示时，先按 GoDo 的 GUIDE Input 安装流程安装 GUIDE / GuideCs，然后配置模板将在后续阶段提供的 Profile。未安装 GUIDE 不应阻断菜单、设置、场景、UI 或存档能力。

## 目录职责

```text
Boot/           唯一业务启动入口和 BootstrapProcedure。
Shared/         跨流程日志、资源键、事件、配置与 Save 契约。
MainMenu/       主菜单流程与场景。
Gameplay/       无玩法假设的游戏中流程与可替换场景。
Settings/       设置 View 与应用边界。
Ui/             UiConfig、Modal 和 Overlay。
ExampleContent/ 可整体删除或替换的中性示例业务内容。
Audio/          项目音频资源的集中位置。
Localization/   翻译资源与语言配置。
```

当前模板已提供 Bootstrap、MainMenu、可替换的 Gameplay 示例场景，以及 Loading、Pause、Confirm、Toast、Settings、音量保存、语言切换、可选 GUIDE Input 接入和可删除的 Save 示例。Audio 资源位置已预留，项目可按自己的资源键接入 BGM 与 SFX。
