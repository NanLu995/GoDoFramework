# GoDo Debugger 使用指南

## 定位与边界

GoDo Debugger 是 Debug 构建中的只读框架仪表盘，用于在游戏运行时快速观察长期服务、资源请求、事件监听和最近错误。它不接管模块逻辑，不提供命令控制台，也不允许修改游戏数据。

Release 构建不会由 GoDoRuntime 创建 Debugger 节点；Debugger 不是业务层可查询的 Service，也不注册到 Services。

## 使用方式

启用 `GoDoRuntime.tscn` Autoload 后，Debug 构建会自动加载紧凑状态栏，无需快捷键或 InputMap 配置。

- 默认折叠，仅显示紧凑的 FPS 按钮。
- 点击或触摸 FPS 按钮展开或收起中文诊断摘要。
- 面板尺寸按当前视口限制，桌面与移动设备的 Debug 构建均可操作。
- 展开状态下每 0.25 秒刷新一次。

## 首版显示内容

- 主线程状态。
- Services 当前注册的接口。
- SceneService 切换状态与进度。
- AudioService 的 BGM 状态和活动 SFX 数量。
- ResourceHub 活动加载数量。
- EventChannel 各事件类型的监听数量。
- 最近 5 条普通开发日志；仅显示 Debug 构建中 LogHub 保留的环形历史。
- ErrorHub 最近 16 条 Warning 以上报告中的最新 4 条摘要；Debug 消息不占用面板空间。

错误历史只保存时间、等级、模块和消息，不持有原始 Exception，避免调试面板延长异常对象及其引用图的生命周期。

## 生命周期与依赖

- GoDoRuntime 在完成内置服务注册后，通过 ResourceHub 加载 `DebuggerOverlay.tscn` 并添加为子节点。
- Overlay 跟随 GoDoRuntime 常驻，不受主内容场景切换影响。
- `_EnterTree()` 订阅 `ErrorHub.OnError`，`_ExitTree()` 对称解绑。
- Services 与 EventChannel 只暴露 `internal + DEBUG` 的快照入口，不增加 public API。
- NodePool 是独立实例模块，首版不为调试面板增加全局池注册，因此不显示池状态。

## 失败语义

- Debugger 场景加载失败时沿用 ResourceHub 的 `ResourceLoadException`。
- 场景缺少必要导出节点引用时抛出 `InvalidOperationException`，尽早暴露损坏的调试场景。
- Debugger 不捕获或吞掉框架模块错误；ErrorHub 仍是唯一错误分发出口。

## 性能

- 折叠时只刷新 FPS，继续收集 Warning 以上摘要，不创建服务与事件快照。
- 展开时低频创建服务和事件快照并更新文本；这些分配仅存在于 Debug 构建。
- 不应把面板刷新频率提高到每帧，也不应在此实现完整性能分析器。

## 常见误用

| 应该 | 避免 |
|---|---|
| 用面板观察框架状态 | 从面板修改服务或业务数据 |
| 为模块提供最小只读快照 | 为了显示而建立全局对象注册表 |
| 仅在 Debug 构建使用 | 让 Release 业务逻辑依赖 Debugger |
| 详细诊断留在对应模块 | 把 Debugger 变成新的全局管理器 |

## 验证状态

代码与场景已通过 `dotnet build`。已在 PC 端 Godot 运行时验证默认显示、点击展开/收起、错误摘要、主场景切换常驻和退出解绑；移动端触摸与视口适配留待目标平台确定后真机验证。
