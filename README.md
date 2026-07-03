# GoDoFramework

GoDoFramework 是面向 Godot 4.7 C# / .NET 的轻量游戏开发框架，用于沉淀跨项目可复用的通信、诊断、生命周期管理和运行时服务，让具体项目把主要精力放在游戏逻辑与内容上。

框架不会替代 Godot，也不包含角色、战斗、关卡等具体玩法逻辑。所有框架 API 位于 `GoDo` 命名空间。

## 项目文档

- `FRAMEWORK_OVERVIEW.md`：框架愿景、痛点和历史设想。
- `FRAMEWORK_DESIGN_PLAN.md`：整体设计计划与开发路线。
- `ARCHITECTURE.md`：当前架构事实、模块状态和依赖约束。
- `AGENTS.md`：AI 协作与代码规范。
- `GODOT_GOTCHAS.md`：项目实际遇到的 Godot/C# 问题记录。

## 当前状态

- EventChannel：基础实现完成，支持类型安全事件、优先级、生命周期绑定、重入派发和 `EventScope`。
- ErrorHub：本地稳定基线完成，支持框架与业务层上报、异常分级、主线程分发、监听者隔离和可插拔 Reporter。
- GoDoRuntime：已作为 Autoload 注册，统一负责框架初始化和退出清理；Runtime 表示游戏运行期，不特指 Release 构建。
- NodePool：首版稳定基线完成，支持 PackedScene 节点初始化、`Acquire/Release`、空闲区容量、异常回滚和显式生命周期回调。
- ResourceHub：首版稳定基线完成，支持类型安全同步/异步加载、进度、并发合并、主线程完成和 Shutdown 清理；首次线程加载与缓存压力测试已通过。
- 正式远程错误上传、Scene、Audio 等模块仍在规划中；Service Registry 延后到首个真实全局服务出现时设计。
