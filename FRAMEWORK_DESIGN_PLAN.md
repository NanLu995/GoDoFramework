# GoDoFramework 整体设计计划

> 本文描述框架为什么存在、建设顺序和待决策方向。当前架构事实见 `ARCHITECTURE.md`，模块细节见各目录 `USAGE.md`，历史设想见 `FRAMEWORK_OVERVIEW.md`。

## 1. 愿景

GoDoFramework 是建立在 Godot 4.7 C# 之上的工业级游戏开发框架，面向中大型、长期维护和多人协作的项目。它不替代 Godot，而是把多个项目反复遇到的生命周期、通信、诊断、资源管理和运行时服务问题沉淀为稳定、可组合、可验证的通用能力。

- Godot 负责节点、场景、资源、渲染、物理、音频和输入等引擎能力。
- GoDo 负责跨项目可复用的工程机制。
- 具体游戏负责角色、战斗、数值、任务、剧情等业务逻辑。

这里的“工业级”不等于照搬其他框架的模块数量，也不等于一次性建设大而全的平台。它表示框架在长期迭代中具备可预测的 API、可见的失败、可追踪的诊断、明确的迁移路径和可重复的验证流程。

框架是否成功，不看模块数量，而看它是否减少重复代码、生命周期错误和跨系统耦合，同时允许开发者直接使用 Godot 原生 API。

## 2. 目标与非目标

### 目标

1. 沉淀确有重复价值的事件、错误处理、资源、对象池和长期服务。
2. 保持框架与玩法边界清楚，依赖方向可见。
3. 贴合 Node、Resource、Signal、场景树和 Godot 主线程模型。
4. 支持渐进采用，避免可选模块成为隐式前置条件。
5. public API 有清晰失败语义、文档和与风险匹配的验证。
6. 只优化已知热点，让生命周期和分配成本可解释。
7. 为长期演进建立版本、兼容性、弃用和迁移说明，避免升级时出现不可见的全局破坏。
8. 为团队协作提供资源校验、编辑器工具、诊断入口、自动化验证和可重复发布流程。
9. 所有“稳定基线”必须记录验证平台与证据；未验证的平台不得宣称稳定。

### 非目标

- 不构建 ECS、依赖注入容器或另一套场景系统来取代 Godot。
- 不在 `GoDo.*` 中实现角色、战斗、技能、任务等具体玩法。
- 不追求所有代码“零 GC”，也不为未经验证的未来需求提前复杂化。
- 不屏蔽所有 Godot 原生 API，不一次性完成全部候选模块。

## 3. 设计原则

- **Godot 优先**：先组合 Godot 已有能力，再补齐工程缺口。
- **机制与业务分离**：框架提供机制，具体游戏定义玩法类型和业务事件。
- **小核心、可选服务**：Core 只保留地基能力，运行时服务独立存在。
- **显式依赖**：构造、初始化或注册过程必须能看出模块依赖；Services 不用于隐藏框架内部依赖。
- **失败可见**：异常、`Try...` 结果或结构化报告三选一，避免静默失败。
- **先验证再抽象**：先有真实痛点，再进入框架；经过不同场景验证后才稳定 public API。
- **工业化而不膨胀**：长期目标是生产可用性和可维护性；当前 0-1 阶段仍以最小可用能力、真实项目验证和逐模块交付为准，不因“未来可能需要”提前堆砌系统。
- **可演进交付**：public API 变化必须说明兼容影响、迁移步骤和验证证据；发布版本必须能被安装、升级和移除验证。

分层和硬性依赖规则以 `ARCHITECTURE.md` 为准，不在本计划重复维护。

## 4. 当前模块状态

> **状态图例**：`稳定基线` = 接口和行为已长期使用、基本不再变动；`首版完成` = 首个可用版本已完成，接口仍可能调整；`首版验证中` = 已进入实现和回归阶段，但尚未完成运行时验证，不能视为可用基线；`Windows 稳定基线` = 仅在 Windows 上验证为稳定，其他平台待验证；`已采用` = 用于 GoDoRuntime 这类框架入口本身，它不是一个可独立替换/演进的"模块"，而是固定的运行时骨架，因此不套用其余三种成熟度分级。
>
| 模块 | 状态 | 首版边界或下一步 |
|---|---|---|
| EventChannel | 稳定基线 | 继续补关键回归测试；避免事件总线替代直接调用和 Signal |
| ErrorHub | 稳定基线 | 远程 Reporter 等真实需求出现后再实现 |
| LogHub | 首版完成 | Debug / Info 普通开发日志、控制台规范与 Debugger 历史已接入；框架调用已迁移，ErrorHub 仅保留 Warning / Error / Fatal 异常上报 |
| Services | 稳定基线 | 保持为业务层长期服务注册表，不扩张成 DI 容器 |
| GoDoRuntime | 已采用 | 只管理框架生命周期和服务注册，不承载游戏流程 |
| ResourceHub | 稳定基线 | 不加入远程下载、PCK/DLC、目录加载或第二套缓存 |
| NodePool | 稳定基线 | 当前只支持 Godot Node/PackedScene 与显式重置 |
| Scene | 稳定基线 | 当前只管理主内容场景切换，不承担 UI 栈 |
| Camera | 首版完成 | 已完成主镜头注册、激活、恢复、跨场景同 ID 与 Phantom 优先级适配；小地图输出后续交付 |
| Input | 核心接入 | 已完成语义 ID、零分配 Frame、Context 栈、GUIDE 适配、设备检测、运行时改键、SaveService 持久化与 Demo3D 真实 Profile；提示查询和跨平台真机验证后置 |
| Audio | 稳定基线 | 当前为单路 BGM、非空间 SFX 池和音量分组 |
| Save | 稳定基线 | 可靠容器与 Codec 边界；不包含云存档、自动 JSON 或加密承诺 |
| Settings | Windows 稳定基线 | 移动端真机验证后置；不包含键位、画质预设或云同步 |
| Localization | 首版完成 | 复用 TranslationServer、PO/CSV、复数、上下文、回退与伪本地化；动态语言包后置 |
| Config | 稳定基线 | Resource 强类型校验与唯一键表；CSV/JSON 导入按真实需求增加 |
| UI | 稳定基线 | 管理屏幕空间 Control 的 Scene、View、Modal 层与返回栈；不承担主场景和游戏流程 |
| Debugger | 二阶段完成 | 紧凑健康入口、路径式两层诊断页、Input/Scheduler 快照与分类控制台；Release 不创建节点 |
| EditorPlugin | 稳定基线 | 显式安装与检查 GoDoRuntime Autoload；不自动修改项目，不进入运行时依赖 |
| Procedure | 首版完成 | 顶层游戏流程状态机；只提供流程切换机制，不内置具体业务流程，不先抽象通用 StateMachine |

模块完成状态的证据、性能数据和详细边界只记录在对应 `USAGE.md`，避免多处同步。

## 5. 暂缓与候选方向

### Debugger

> **状态更新**：Debugger 已完成二阶段，采用紧凑健康入口和路径式两层只读页面；Input、Scheduler、Services、Events 与分类控制台已接入。以下设计初衷作为历史决策记录保留。

借鉴 Game Framework 的可扩展 Debugger 思路，但只考虑编辑器或 Debug 构建中的只读诊断页：展示事件监听、资源请求、池占用、场景和长期服务状态。诊断优先复用已有公开状态，不接管模块逻辑，也不成为运行时反向依赖。

### Procedure / StateMachine

Procedure 已完成首版。它借用状态机思想，但不先建设通用 StateMachine，也不作为某个 StateMachine 基类的子类。首版只解决“游戏顶层流程”这一类明确痛点，例如启动、主菜单、加载、游戏中、暂停、结算和返回菜单等阶段切换。

设计初衷：

> Procedure 用于将游戏顶层阶段切换沉淀为显式、串行、可清理、可验证的生命周期机制。它负责组织 Scene、UI、Audio、Save 等既有服务的使用顺序，但不替代这些服务，也不承载具体玩法逻辑。

边界：

- Procedure 是顶层游戏流程状态机，不是通用状态机框架。
- 框架只提供 `Enter` / `Exit` / `Change` 机制，具体 `MainMenuProcedure`、`GameplayProcedure` 等业务流程由游戏项目自己定义。
- 不放入 GoDoRuntime 固定执行；GoDoRuntime 只负责注册服务，不主动进入任何业务流程。
- 不替代 SceneService、UiService、AudioService、SaveService，也不替代角色、AI、战斗阶段等局部状态机。
- 首版不做流程栈、层级状态机、黑板、反射自动发现、自动依赖注入或参数系统。

首版提供 `ProcedureContext.GetService<T>()`，让业务流程显式获取已注册长期服务；Procedure 模块本身不直接依赖 Scene、UI、Audio、Save 等具体服务，避免变成全局大管家。

切换语义：

1. `ChangeAsync(next)` 必须在 Godot 主线程调用。
2. 正在切换时再次切换，首版直接失败，不排队。
3. 正常顺序为 `旧流程 ExitAsync` → `新流程 EnterAsync` → 更新当前流程。
4. `ExitAsync` 失败时不进入新流程，异常可见。
5. `EnterAsync` 失败时异常可见，当前流程状态必须在 `USAGE.md` 中明确记录，不静默吞掉失败。

验证要求：

- 已新增独立自动回归场景，覆盖初始状态、首次进入、切换顺序、并发切换拒绝、`ExitAsync` 失败、`EnterAsync` 失败和清理语义。
- 手动验证应覆盖真实业务流程：主菜单进入游戏、连续点击防重入、返回菜单时 UI/场景/音频清理正常。
- Procedure 已写入 `ARCHITECTURE.md` 的已采用模块，并新增模块 `USAGE.md` 作为 API、失败语义和验证结果的唯一详细来源。

### Tick

当前不集中接管所有 `_Process` / `_PhysicsProcess`。只有性能数据证明大量空转更新是实际瓶颈，或确有分组频率需求时，才评估可选调度器。

### Scheduler

Timer 的真实缺口已经确认：业务需要统一的一次性/重复延迟、取消、独立暂停、三种时间语义、Process/Physics 阶段、Owner 生命周期和异步等待。采用单一长期 `SchedulerService`，不为每个任务创建 Timer Node，也不混入确定性 Tick、后台线程或持久化离线计时。详细设计见 `Docs/SchedulerServiceDesign.md`；人工时钟核心、完整调度状态、DelayAsync、跨线程取消、Owner 生命周期、真实时钟采样、GoDoRuntime 接入、性能基准与 Debug-only 快照已完成，Scheduler 进入首版完成状态。快照 UI 与 Input 诊断统一留到后续 Debugger 面板优化。

### 其他候选

- Async：不建设泛化协程框架，不用 `Task.Run` 操作 Godot 对象。
- Extensions：先验证 Godot 原生能力的真实缺口；Localization 已按 TranslationServer 薄封装完成首版。
- Remote Asset、PCK/DLC、热更新、高级缓存：作为未来独立扩展，不混入 ResourceHub。
- 不照搬全局大门面、Unity Entity/UI 抽象或 DataNode。

## 6. 分阶段路线

| 阶段 | 目标 | 状态 |
|---|---|---|
| 0. 基线整理 | 文档、边界、测试入口一致 | 完成 |
| 1. 稳定 Core | EventChannel、ErrorHub、Services、GoDoRuntime 可长期依赖 | 稳定基线 |
| 2. 高频运行时能力 | Resources、Pool、Scene、Audio | 稳定基线 |
| 3. 数据与产品能力 | Save、Settings、Config | 稳定基线；Settings 移动端验证后置 |
| 4. 开发体验 | 轻量 Debugger、经真实需求确认的 UI、安装检查工具（即 EditorPlugin/Installer） | Debugger、UI、EditorPlugin 均已升级为稳定基线（此前记录的"首版完成"已过期，见第 4 节最新状态表） |
| 5. 候选评估 | Procedure、Scheduler、Tick、Input、本地化与远程资源 | Procedure、Scheduler、Localization 首版完成；Input 核心接入、后端适配开发中；其余未排期 |
| 6. 工业化保障 | API 兼容与迁移、跨平台验证、资源与编辑器审计、自动化验证、发布流程 | 规划中；按真实项目规模逐项立项，不作为一次性大版本 |

每个模块单独设计、交付和验证，不等待整个阶段全部完成。

## 7. 单模块设计与验收模板

开工前回答：

1. 解决了哪个重复痛点，Godot 原生方案哪里不足？
2. 负责什么，明确不负责什么？
3. 谁调用它，依赖哪些模块，是否形成循环？
4. 生命周期、线程约束和失败语义是什么？
5. 首版最小 API 是什么，高频路径是否分配？
6. 自动化、测试场景和手动验证分别覆盖什么？

完成标准：

- 不泄漏玩法概念，不隐藏依赖。
- public API 有 XML 文档、失败语义和 `USAGE.md`。
- 生命周期订阅与释放对称，主线程约束明确。
- 编译通过，并完成与风险匹配的运行时验证。
- 修改既有 public API 时有明确授权和迁移说明。
- 只更新 `ARCHITECTURE.md` 中真正变化的架构事实；模块细节留在 `USAGE.md`。

## 8. 当前建议

当前仍处于 0-1 阶段：继续避免新增泛化模块，用真实游戏和模板迁移验证现有基线并记录重复出现的缺口。工业级定位是长期质量标准，不是立即扩大模块范围的理由。

接下来的能力建设分为两条轨道：一条是继续完成 Input 后端适配，在 Localization 首版完成后按真实需求单独设计 Tick、资源扩展等运行时能力；另一条是逐步补齐 API 兼容与迁移、跨平台验证、资源与编辑器审计、自动化验证和发布流程。Procedure 已按“顶层游戏流程状态机”完成首版，但不先抽象通用 StateMachine；UI 已完成首轮实际项目验证并进入稳定基线。

### 当前优先级

1. **Scheduler 真实项目验证**：首版实现、自动回归与性能基准已完成；暂停菜单、慢动作、窗口失焦、低 FPS 和长卡帧体验随真实游戏开发验证，不阻塞当前首版。详细边界见 `Docs/SchedulerServiceDesign.md`。
2. **独立核心示例**：核心包已通过临时干净项目验证；是否新增可直接打开的独立核心示例，等待当前代码梳理后决定。

Input 的活动设备、有效 Context 和 Action 当前值已通过 Debug-only 快照接入 Debugger，不扩大 `IInputService` public API。真实设备、窗口失焦与渲染/物理时序验证继续后置。

### 后置：版本发布

当前开发与验收收尾完成后，再建立正式版本发布流程，不在此阶段打断框架开发：

1. 使用语义化版本，并同步更新 `plugin.cfg` 版本号；
2. 使用 Git 标签标记版本，例如 `v0.1.0`；
3. 通过 GitHub Release 发布 ZIP，压缩包只保留 `addons/godo_framework/` 的运行与编辑器资源，Markdown 文档留在仓库中；
4. 使用 `CHANGELOG.md` 记录兼容版本、主要改动和迁移注意事项；
5. Godot Asset Library 发布后置，待版本、说明和展示资源稳定后再评估。

发布前必须完成干净项目迁移、编译、安装、运行、升级和移除验证。具体打包与迁移边界见 `addons/godo_framework/USAGE.md`。

## 9. 文档职责

- `AGENTS.md`：AI 必须遵守的协作与代码规则。
- `ARCHITECTURE.md`：当前架构事实、依赖规则和关键决策。
- `FRAMEWORK_DESIGN_PLAN.md`：目标、状态、路线和候选方向。
- `FRAMEWORK_OVERVIEW.md`：历史愿景、痛点和早期设想，不要求同步当前实现。
- 模块 `USAGE.md`：API、失败语义、生命周期、性能和验证结果的详细来源。
- `GODOT_GOTCHAS.md`：已实际遇到并确认的 Godot/C# 坑位。

真实项目反馈、性能数据和 Godot 版本变化都可以推动计划调整，但每次调整应说明证据和取舍，避免仅凭“以后可能需要”扩大框架。
