# Changelog

## Unreleased

- `ResourceKey` 支持 `uid://`，并新增 `IsUid`、`FromPath`、`FromUid` 与 `ResolveUid`。
- 新增 `ResourceManifest` 与 `ResourceRegistry`，支持业务语义 ID 到 `ResourceKey` 的运行时映射。
- 编辑器菜单改为 `GoDo Framework` 子菜单，并新增只读 `ResourceManifest` 校验入口。
- StarterGame 模板资源键改为 `Root + relativePath` 形式，降低模板目录移动后的路径维护成本。

## 0.2.0

- 新增 Procedure 服务，用于组织顶层游戏流程切换。
- 新增 UI 服务，提供 Scene、View、Modal 三层屏幕空间 UI 管理。
- 新增 StarterGame 模板，采用功能模块优先的目录结构和 `Boot.tscn` 入口。
- 新增面向 AI 游戏开发的指南、项目结构说明和常用 Recipes。
- 移除旧 Demo 与 UI 手动验证资源，保留框架与模板的清晰边界。

## 0.1.0

- 建立 GoDoFramework 初始插件包与发布脚本。
