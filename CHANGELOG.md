# Changelog

## Unreleased

## 0.3.0

- Procedure 新增无参泛型 `ChangeAsync<TProcedure>()` 与 `RequestChange<TProcedure>()` 便捷重载；带业务参数的流程继续通过显式实例切换。
- `ResourceKey` 支持 `uid://`，并新增 `IsUid`、`FromPath`、`FromUid` 与 `ResolveUid`。
- 新增 `ResourceManifest` 与 `ResourceRegistry`，支持业务语义 ID 到 `ResourceKey` 的运行时映射。
- 编辑器入口改为顶部工具栏原生样式的 `GoDo` 下拉菜单，并新增 `ResourceManifest` 创建、受限资源多选、自动/显式目标清单选择、明确的预览/取消/成功状态、条目管理/编辑/删除与只读校验入口；添加资源时会经确认补齐缺失 UID，管理窗口新增 UID 状态列并可将已有路径定位显式转换为 `uid://`；创建逻辑改为直接实例化 C# 脚本资源，并绕过编辑器脚本缓存。
- `GoDoFramework.csproj` 依据本地插件目录条件编译 GUIDE、Phantom Camera 适配与关联示例；核心可在未安装可选第三方插件时独立构建。
- 新增干净核心包验证与按可选依赖拆分的自动回归套件。
- 移除 StarterGame 模板；新项目目录组织改由 `AI_GAMEDEV_GUIDE.md` 与 `PROJECT_STRUCTURE.md` 说明。

## 0.2.0

- 新增 Procedure 服务，用于组织顶层游戏流程切换。
- 新增 UI 服务，提供 Scene、View、Modal 三层屏幕空间 UI 管理。
- 新增 StarterGame 模板，采用功能模块优先的目录结构和 `Boot.tscn` 入口。
- 新增面向 AI 游戏开发的指南、项目结构说明和常用 Recipes。
- 移除旧 Demo 与 UI 手动验证资源，保留框架与模板的清晰边界。

## 0.1.0

- 建立 GoDoFramework 初始插件包与发布脚本。
