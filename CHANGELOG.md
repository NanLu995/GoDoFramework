# Changelog

## 0.6.2

- Settings 新增业务自有 `ISettingsModule<TSettings>` 注册机制，提供稳定顺序、独立槽位、版本迁移、验证、可选降级/关键阻断、统一保存与逆序关闭，同时保持框架系统设置存档格式不变。
- 新增纯 C# 泛型 `StateMachine<TContext, TState>` 基础设施，提供同步 Enter/Exit、可选 Update、生命周期内有界 FIFO 切换、重复实例忽略、循环切换防御、终止故障与幂等 Dispose 语义；该模块独立于 Godot Node、Services 和 Procedure。
- 深化 Debugger 的运行时诊断视图，为 Scheduler、Pool、Resource、DataTable、Procedure、Scene、UI、Audio 与 EventChannel 补充有界活动明细、来源、存活时间和最近结果，并保持 Release 构建零额外诊断状态。
- 将 Friflo ECS Debugger 扩展收敛到集成包自有目录，核心框架与可选集成继续保持独立发布边界。
- 建立统一的 12 阶段本地发布门禁、public API 兼容基线、核心 ZIP 安装/替换升级/安全移除回归，以及 Core 与文档 Push/PR CI；编辑器回归使用隔离临时项目，避免继承本机编辑器布局和网络更新状态。

## 0.6.1

- Debugger 新增可选的 Friflo ECS 只读诊断页，汇总 World、宿主、Entity、Archetype 和 System 树，但不会代替业务启用性能监控。
- 将 Procedure、Scene 与 UI 的联合诊断从 Runtime 子页调整为独立顶层“运行链路”，明确其只读组合视图边界。
- 概览中的 Warning 与 Error 状态卡支持整卡点击，可清空旧搜索并跳转到控制台独显对应等级，同时保留暂停状态。

## 0.6.0

- 编辑器插件收敛为项目设置式单窗口入口，按 Runtime、资源清单、UI 配置和编辑器扩展分页管理；资源与 UI 页面直接显示项目内现有配置列表。
- 新增框架级游戏导出过滤：Debug 与 Release 均排除 Editor/Tools，Release 额外排除 Debugger，源码发布包仍保留完整开发工具。
- 新增基于 Friflo.Engine.ECS 3.6.0 的可选场景级 ECS 宿主与独立发布包；核心框架不依赖 Friflo，目标项目按需添加 NuGet 依赖。
- Friflo ECS 编辑器扩展新增项目依赖检查；普通单项目缺少依赖时可在精确预览和非覆盖备份后确认写入，中央包管理、多个项目及无法确定的结构保持只读，且不自动执行 restore/build。
- GUIDE Input、Phantom Camera 与 Friflo ECS 设置页新增已验证版本、安装位置/方式和官方来源入口；第三方依赖仍由开发者下载，编辑器不自动下载或覆盖。
- GoDo Framework 窗口新增 C# 项目配置检查；可按已安装的 GUIDE、Phantom Camera、Friflo ECS 与 Debugger 补齐框架自有条件编译/Release 裁剪规则，修改前精确预览并创建非覆盖备份，复杂项目保持只读。
- Demo3D Gameplay 新增 512 实体的 Friflo ECS 群体展示，通过单个 MultiMesh 可视化批量更新，并验证暂停、恢复和跨场景 World 重建。

## 0.5.0

- Procedure、Scene 与 UI 增强顶层流程协作：补齐可取消异步切换、失败恢复、阶段化异常、诊断快照与关闭清理，并由 Demo3D 展示启动、主菜单、加载、进入游戏、暂停和返回菜单的完整流程。
- UI 完善 Scene、View、Modal、Overlay 四层管理，以及同步/异步打开、查询、指定或批量关闭、`CloseTo`、焦点恢复、可选 Single 实例复用和编辑器配置工具；GoDoTemplate 增加可复用的 Starter UI 示例与回归验证。
- 新增 DataTable 运行时服务、生成工具、编辑器工作流、导出校验和示例数据，并强化格式校验、加载诊断与峰值内存控制。
- 完善 Input、Localization、Settings、Debugger 与滚动日志能力，统一日志等级入口和模块通道，并补齐运行时诊断面板、输入提示、重绑定及本地化示例。
- 加固框架异常边界：Procedure、Scene、Resource、Pool、Audio、Camera 与 Runtime 初始化/关闭路径补齐取消、恢复和半初始化状态清理。
- 建立中英文用户手册、API Reference 检查和文档覆盖追踪，并增加 Godot 版本升级工具与只读本地 AI Worker。
- 发布包拆分为无第三方依赖的核心包、GuideInput 集成包和 PhantomCamera 集成包；三个压缩包均保留 `addons/godo_framework` 安装路径，核心包的编辑器宿主会安全忽略未安装的可选集成目录。

- AudioService 新增双播放器等功率 `CrossfadeBgmAsync`、淡出到静音的 `FadeOutBgmAsync`、过渡请求 latest-request-wins 取消语义和结构化 `BgmPlaybackState`；SFX 新增 ResourceHub 资源准备、Voice 幂等预热、逐次参数、结构化准入结果、单路 Handle、同资源并发限制，以及覆盖活动与待加载请求的优先级抢占，并新增独立容量、预热、Handle、最大距离、静态世界坐标和受独立预算约束的固定物理帧 Node3D 跟随 3D SFX 线路，现有 BGM 与 bool SFX 播放入口保持兼容。

## 0.4.0

- 新增统一 `SchedulerService`，支持一次性与重复调度、取消、独立暂停、剩余时间查询和可取消 `DelayAsync`。
- Scheduler 提供 GameTime、UnscaledGameTime、RealTime 三种时间语义，以及 Process/Physics 两种派发阶段。
- 调度任务支持绑定场景 Owner，Owner 退出树时自动取消；框架退出会可靠取消全部任务与未完成异步等待。
- Scheduler 完成确定性核心回归、真实帧 Headless 回归、Debug/Release 性能基准和 Debug-only 诊断快照。
- Demo3D 将输入绑定加载移入 `BootProcedure`，使启动场景只负责进入顶层流程。
- 核心无第三方依赖构建改用独立配置输出，避免覆盖完整工作区的 Debug 程序集。

## 0.3.0

- Procedure 新增无参泛型 `ChangeAsync<TProcedure>()` 与 `RequestChange<TProcedure>()` 便捷重载；带业务参数的流程继续通过显式实例切换。
- `ResourceKey` 支持 `uid://`，并新增 `IsUid`、`FromPath`、`FromUid` 与 `ResolveUid`。
- 新增 `ResourceManifest` 与 `ResourceRegistry`，支持业务语义 ID 到 `ResourceKey` 的运行时映射。
- 编辑器入口改为顶部工具栏原生样式的 `GoDo` 下拉菜单，并新增 `ResourceManifest` 创建、受限资源多选、自动/显式目标清单选择、明确的预览/取消/成功状态、条目管理/编辑/删除与只读校验入口；添加资源时会经确认补齐缺失 UID，管理窗口新增 UID 状态列并可将已有路径定位显式转换为 `uid://`；创建逻辑改为直接实例化 C# 脚本资源，并绕过编辑器脚本缓存。
- `GoDoFramework.csproj` 依据本地插件目录条件编译 GUIDE、Phantom Camera 适配与关联示例；核心可在未安装可选第三方插件时独立构建。
- 新增干净核心包验证与按可选依赖拆分的自动回归套件。
- 移除 StarterGame 模板；新项目目录组织改由 `AI/AI_GAMEDEV_GUIDE.md` 与 `AI/PROJECT_STRUCTURE.md` 说明。

## 0.2.0

- 新增 Procedure 服务，用于组织顶层游戏流程切换。
- 新增 UI 服务，提供 Scene、View、Modal 三层屏幕空间 UI 管理。
- 新增 StarterGame 模板，采用功能模块优先的目录结构和 `Boot.tscn` 入口。
- 新增面向 AI 游戏开发的指南、项目结构说明和常用 Recipes。
- 移除旧 Demo 与 UI 手动验证资源，保留框架与模板的清晰边界。

## 0.1.0

- 建立 GoDoFramework 初始插件包与发布脚本。
