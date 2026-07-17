# GoDo Debugger 使用指南

## 定位与边界

GoDo Debugger 是 Debug 构建中的只读框架仪表盘，用于在游戏运行时快速观察长期服务、资源请求、事件监听和最近错误。它不接管模块逻辑，不提供命令控制台，也不允许修改游戏数据。

Release 构建不会由 GoDoRuntime 创建 Debugger 节点；Debugger 不是业务层可查询的 Service，也不注册到 Services。

## 使用方式

启用 `GoDoRuntime.tscn` Autoload 后，Debug 构建会自动加载紧凑状态栏，无需快捷键或 InputMap 配置。

- 默认折叠，紧凑按钮显示 FPS、最近 Warning 与 Error 数量；文字颜色按最高严重度变化。
- 点击或触摸健康状态按钮展开或收起诊断窗口。
- 展开后使用“分类 + 分类内页面”的两层导航；内置页面按 `Overview`、`Runtime/Input`、`Runtime/Scheduler`、`Framework/Services`、`Framework/Events`、`Console/*` 路径组织。
- 健康状态按钮、两级标签和内容滚动区不取得键盘或手柄焦点；页面仍可用鼠标或触摸切换，不会让游戏中的方向输入意外切换 Debugger 标签。
- 面板尺寸按当前视口限制，桌面与移动设备的 Debug 构建均可操作。
- 页面切换时立即刷新；保持展开时每 0.25 秒刷新当前页面。

## 显示内容

- `概览`：主线程、资源请求、场景、音频、服务/事件数量、活动输入设备和 Scheduler 活跃/暂停任务数。
- `运行时 / Input`：后端、采样状态、活动设备、能力、Frame、完整 Context 栈及有效性、Action 当前值和边沿状态。
- `运行时 / Scheduler`：任务数量、三种时钟在 Process/Physics 的分布、最近派发、下次触发与累计失败/取消统计。
- `框架 / Services`：全部已注册服务接口。
- `框架 / Events`：事件类型、总监听器及各事件监听数量。
- `控制台`：最近普通日志与 ErrorHub 摘要，提供全部、Debug、Info、Warning、Error 子页过滤。

Input 页面最多显示前 32 个 Action，并明确显示省略数量，避免异常后端布局制造过长文本。控制台最多显示最近 20 条普通日志和匹配过滤条件的最近 12 条 Warning/Error；ErrorHub 摘要总容量仍为 16 条。

错误历史只保存时间、等级、模块和消息，不持有原始 Exception，避免调试面板延长异常对象及其引用图的生命周期。

## 生命周期与依赖

- GoDoRuntime 在完成内置服务注册后，通过 ResourceHub 加载 `DebuggerOverlay.tscn` 并添加为子节点。
- Overlay 跟随 GoDoRuntime 常驻，不受主内容场景切换影响。
- `_EnterTree()` 订阅 `ErrorHub.OnError`，`_ExitTree()` 对称解绑。
- Services、EventChannel、InputService 与 SchedulerService 只暴露 `internal + DEBUG` 的快照入口，不增加 public API。
- Debugger 内部按路径注册只读页面；当前不开放第三方 public 注册 API。
- NodePool 是独立实例模块，首版不为调试面板增加全局池注册，因此不显示池状态。

## 失败语义

- Debugger 场景加载失败时沿用 ResourceHub 的 `ResourceLoadException`。
- 场景缺少必要导出节点引用时抛出 `InvalidOperationException`，尽早暴露损坏的调试场景。
- Debugger 不捕获或吞掉框架模块错误；ErrorHub 仍是唯一错误分发出口。

## 性能

- 折叠时只刷新 FPS 与最近错误计数，继续收集 Warning 以上摘要，不创建模块快照。
- 展开时只生成当前页面所需的低频快照；Input、Services 与 Events 的小型数组分配仅存在于 Debug 构建。
- 页面定义与导航分组只在 Overlay 初始化时创建，不进入每帧输入或调度热路径。
- 不应把面板刷新频率提高到每帧，也不应在此实现完整性能分析器。

## 常见误用

| 应该 | 避免 |
|---|---|
| 用面板观察框架状态 | 从面板修改服务或业务数据 |
| 为模块提供最小只读快照 | 为了显示而建立全局对象注册表 |
| 仅在 Debug 构建使用 | 让 Release 业务逻辑依赖 Debugger |
| 详细诊断留在对应模块 | 把 Debugger 变成新的全局管理器 |

## 验证状态

自动回归入口：

```text
Verification/Automated/DebuggerOverlayRegression.tscn
```

覆盖默认折叠、点击展开、调试控件不取得键盘/手柄焦点、一级分类、Runtime 与 Console 二级页面切换和再次折叠。Input 快照由 `InputServiceRegression.tscn` 覆盖后端、设备、Frame、Action 状态及 Context 有效性。移动端触摸、窄视口和真实设备显示仍需在目标平台手动验证。
