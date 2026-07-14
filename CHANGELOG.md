# Changelog

## Unreleased

- Procedure 新增无参泛型 `ChangeAsync<TProcedure>()` 与 `RequestChange<TProcedure>()` 便捷重载；带业务参数的流程继续通过显式实例切换。
- `ResourceKey` 支持 `uid://`，并新增 `IsUid`、`FromPath`、`FromUid` 与 `ResolveUid`。
- 新增 `ResourceManifest` 与 `ResourceRegistry`，支持业务语义 ID 到 `ResourceKey` 的运行时映射。
- 编辑器入口改为顶部工具栏原生样式的 `GoDo` 下拉菜单，并新增 `ResourceManifest` 创建、受限资源多选、自动/显式目标清单选择、明确的预览/取消/成功状态、条目管理/编辑/删除与只读校验入口；添加资源时会经确认补齐缺失 UID，管理窗口新增 UID 状态列并可将已有路径定位显式转换为 `uid://`；创建逻辑改为直接实例化 C# 脚本资源，并绕过编辑器脚本缓存。
- StarterGame 模板资源键改为 `Root + relativePath` 形式，降低模板目录移动后的路径维护成本。

## 0.2.0

- 新增 Procedure 服务，用于组织顶层游戏流程切换。
- 新增 UI 服务，提供 Scene、View、Modal 三层屏幕空间 UI 管理。
- 新增 StarterGame 模板，采用功能模块优先的目录结构和 `Boot.tscn` 入口。
- 新增面向 AI 游戏开发的指南、项目结构说明和常用 Recipes。
- 移除旧 Demo 与 UI 手动验证资源，保留框架与模板的清晰边界。

## 0.1.0

- 建立 GoDoFramework 初始插件包与发布脚本。
