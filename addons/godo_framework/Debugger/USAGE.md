# GoDo Debugger 使用指南

## 定位与边界

GoDo Debugger 是 Debug 构建中的只读框架仪表盘，用于在游戏运行时快速观察长期服务、资源请求、事件监听和最近错误。它不接管模块逻辑，不提供命令控制台，也不允许修改游戏数据。

Release 构建不会由 GoDoRuntime 创建 Debugger 节点；Debugger 不是业务层可查询的 Service，也不注册到 Services。

## 使用方式

启用 `GoDoRuntime.tscn` Autoload 后，Debug 构建会自动加载紧凑状态栏，无需快捷键或 InputMap 配置。

- 默认折叠，紧凑按钮显示 FPS、最近 Warning 与 Error 数量；文字颜色按最高严重度变化。
- 点击或触摸健康状态按钮展开或收起诊断窗口。
- 展开后使用树状导航；内置页面按 `Overview`、`Runtime/Input`、`Runtime/Scheduler`、`Runtime/Audio`、`Framework/Services`、`Framework/Events`、`Console` 路径组织。
- 拖动标题栏可移动面板，拖动右下角“拖动调整大小 ↘”可缩放整个 Debugger；“重置”恢复默认位置与尺寸，移动和缩放结果始终限制在当前视口内。
- 健康状态按钮、树状导航、内容区和普通操作按钮不取得键盘或手柄焦点；Input、Services、Events 与控制台搜索框仅在鼠标点击后取得焦点，提交搜索或离开对应页面时释放焦点。
- 页面切换时立即刷新；保持展开时每 0.25 秒刷新当前页面。

## 显示内容

- `概览`：以卡片式仪表盘显示 FPS、Warning/Error、资源请求、场景、音频、服务/事件数量、活动输入设备和 Scheduler 活跃/暂停任务数。
- `运行时 / Input`：以结构化仪表盘显示后端、采样状态、活动设备、能力、采样序号、完整 Context 栈及有效性，以及 Action 当前值和边沿状态。采样序号在每次成功采样后递增，失败时保持不变，后端重装或服务关闭后归零；Action 搜索匹配名称或值类型。
- `运行时 / Scheduler`：以结构化仪表盘显示任务数量、三种时钟在 Process/Physics 的分布、最近派发、下次触发与累计失败/取消统计。
- `运行时 / Audio`：显示 BGM 加载/播放状态、当前资源键、SFX 活跃声部与容量占用，以及 Master、BGM、SFX 三组线性音量。现有接口不能区分暂停与自然播放结束，因此有资源但未播放时统一显示“已加载 / 当前未播放”。
- `框架 / Services`：以结构化检查器显示注册接口数、实现类型数及每个“服务接口 → 当前实现”关系。搜索同时匹配接口和实现的短名称、完整类型名；选中列表项后在底部显示完整注册关系。
- `框架 / Events`：以结构化检查器显示事件类型数、总监听器、事件名称及各事件监听数量。搜索同时匹配短名称和完整类型名；选中列表项后在底部显示完整类型名。
- `控制台`：普通日志文本与 ErrorHub 摘要，提供 All、Debug、Info、Warning、Error 等级标签；默认 All，点击单个等级可快速独显，继续点击其他等级可组合筛选。搜索会扫描完整内存历史，普通日志按每页 100 条显示，可用“上一页 / 下一页”翻页，或用独立的“最新日志”按钮直接返回最后一页并滚到底部。位于最新页且未暂停时，新日志会自动跟随到底部；手动向上滚动会停止跟随并启用“最新日志”，手动滚到底部或点击该按钮后恢复跟随。暂停后停止自动刷新和滚动。“复制”仅复制当前筛选、搜索与分页下正在显示的文本。

Input 搜索扫描完整 Action 快照，但列表最多显示前 32 个匹配项，并明确显示总数，避免异常后端布局制造过长排版。LogHub 保留最多 1000 条聚合历史，连续相同日志显示为带首次/最后时间的 `×次数` 条目；控制台每页最多显示 100 条匹配普通日志和最近 12 条匹配 Warning/Error，ErrorHub 摘要总容量仍为 16 条。

错误历史只保存时间、等级、模块和消息，不持有原始 Exception，避免调试面板延长异常对象及其引用图的生命周期。

## 生命周期与依赖

- GoDoRuntime 在完成内置服务注册后，通过 ResourceHub 加载 `DebuggerOverlay.tscn` 并添加为子节点。
- Overlay 跟随 GoDoRuntime 常驻，不受主内容场景切换影响。
- `_EnterTree()` 订阅 `ErrorHub.OnError`，`_ExitTree()` 对称解绑。
- Services、EventChannel、InputService 与 SchedulerService 只暴露 `internal + DEBUG` 的快照入口；Audio 页面直接读取现有 `IAudioService` 只读属性，不增加 public API。
- Debugger 内部按路径注册只读页面；当前不开放第三方 public 注册 API。
- NodePool 是独立实例模块，首版不为调试面板增加全局池注册，因此不显示池状态。

## 失败语义

- Debugger 场景加载失败时沿用 ResourceHub 的 `ResourceLoadException`。
- 场景缺少必要导出节点引用时抛出 `InvalidOperationException`，尽早暴露损坏的调试场景。
- Debugger 不捕获或吞掉框架模块错误；ErrorHub 仍是唯一错误分发出口。

## 性能

- 折叠时只刷新 FPS 与最近错误计数，继续收集 Warning 以上摘要，不创建模块快照。
- 展开且未暂停时只生成当前页面所需的低频快照；控制台通过日志与错误摘要版本号跳过内容未变化的刷新，不重复复制历史、构建文本或更新 RichTextLabel。筛选和搜索扫描有界的 1000 条聚合历史，但 RichTextLabel 每次最多排版 100 条普通日志。滚到底部统一延迟到 GUI 完成本轮文本布局后执行，并合并同一轮的重复请求；滚动条变化信号也只安排一次延迟状态检查，不在原生回调内更新布局；不监听 RichTextLabel 的尺寸信号，避免滚动与重排形成反馈环。暂停停止新日志触发的自动刷新与滚动；切换页面、翻页、点击“最新日志”或提交搜索仍会按用户操作立即刷新。
- Input、Services 与 Events 的小型数组分配仅存在于 Debug 构建；Input 的 Frame 状态可独立更新，Context / Action 及 Services / Events 快照未变化时不重建列表。Audio 页面每次只读取常量数量的属性并更新既有 Label，不创建历史或集合。页面定义和导航树只在 Overlay 初始化时创建。
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

Windows Debug 回归覆盖默认折叠、点击展开、焦点策略、树状页面切换、Overview、Input 空布局、Scheduler 与 Audio 仪表盘、Services / Events 统计、搜索与选中详情、控制台等级多选、1000 条历史搜索、分页、搜索/暂停/复制、布局重置和再次折叠；同一场景使用 Release 程序集运行时，验证 GoDoRuntime 不创建 Debugger 节点。非空 Input 快照由 `InputServiceRegression.tscn` 覆盖后端、设备、Frame、Action 状态及 Context 有效性。窗口拖动与缩放的视觉手感、播放中的 Audio 状态、非空 Input 仪表盘、移动端触摸、窄视口和真实设备显示仍需在目标平台手动验证。
