# Project Structure

本文档说明用 GoDoFramework 做游戏时推荐的项目资产组织方式。

## 推荐原则

面向有经验开发者和中大型项目，默认采用“功能模块优先”：

```text
Boot.tscn
Boot.cs
Shared/
MainMenu/
Gameplay/
Result/
Audio/
```

脚本跟随功能模块走，不强制集中到全局 `Scripts/`。原因是维护一个功能时，最重要的是看到它的场景、UI、流程脚本和局部资源，而不是按文件类型分散查找。

## StarterGame 当前结构

```text
Templates/StarterGame/
├── Boot.tscn
├── Boot.cs
├── Audio/
├── Shared/
├── MainMenu/
├── Gameplay/
└── Result/
```

目录职责：

- `Boot.tscn` / `Boot.cs`：模板入口，只启动第一个业务 Procedure。
- `Audio/`：全局复用音频资源。
- `Shared/`：跨模块共享的资源键、配置、存档、事件、启动流程。
- `MainMenu/`：主菜单流程、场景和脚本。
- `Gameplay/`：游戏流程、主内容场景、HUD 和玩法相关 UI。
- `Result/`：结算流程、界面和脚本。

## 什么时候放进功能模块

放进功能模块：

- 只被该功能使用的场景、UI、脚本。
- 只被该功能使用的音效、图片、配置。
- 该功能自己的 Procedure、Controller、View、Panel。
- 该功能内部事件或局部数据结构。

示例：

```text
Inventory/
├── InventoryProcedure.cs
├── InventoryView.tscn
├── InventoryView.cs
├── InventorySlot.cs
└── InventoryIcons.tres
```

## 什么时候放进 Shared

放进 `Shared/`：

- 多个功能都会引用的资源键。
- 跨流程事件。
- 存档数据和 Save Codec。
- 全局配置 Resource。
- 多模块复用的轻量工具类型。
- 项目级常量和共享值对象。

不要把所有脚本都丢进 `Shared/`。如果一个类型只有某个功能使用，它应该留在功能目录。

## 什么时候用全局资产目录

可以保留 `Audio/`、`Art/`、`Fonts/` 这类全局目录，但只放真正跨模块复用或项目级资产。

如果某个音效只属于某个功能，优先放在功能模块内；如果它是通用点击音、全局 BGM 或项目主题资源，再放到全局目录。

## Godot 与 Bundle

Godot 日常开发中没有 Unity 那种默认 bundle 组织概念。主要依靠：

- `res://` 路径引用。
- `.tscn` / `.tres` 资源依赖。
- 导出配置。
- PCK 包和自定义加载方案。

因此，文件夹就是最重要的模块边界。目录结构应服务于长期维护，而不是只按文件类型整齐摆放。

