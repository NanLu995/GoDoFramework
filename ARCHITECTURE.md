# GoDoFramework 架构文档

> 本文只记录当前已经采用的架构事实、依赖规则和关键决策。模块 API、失败语义与验证结果见对应目录的 `USAGE.md`；未来路线见 `FRAMEWORK_DESIGN_PLAN.md`。

## 1. 框架边界

`GoDo.*` 只提供可跨游戏复用的机制，不感知角色、血量、子弹、关卡规则等具体玩法。业务代码放在 `GoDo.*` 之外，可以使用框架；框架不得反向引用业务程序集或业务类型。

判断标准：一段代码中的概念如果换一种游戏就不成立，它通常属于业务层，而不是框架。

## 2. 分层与依赖方向

```text
业务层（具体游戏）
        ↓
Runtime Services（Scene / Camera / Input / Audio / UI / Save / Settings / Procedure）── 对应第 4 节表格中的 Service 层
        ↓
Runtime Foundation（Resources / Pool）── 对应第 4 节表格中的 Foundation 层
        ↓
Core（ErrorHub / EventChannel / Services / GoDoRuntime）
        ↓
Godot 4.7 C# / .NET
```

> 上图只描述主依赖链路。Tools（Config）、Editor（Installer / Validator）、Development（Debugger）三层不在这条链路上，属于侧挂关系：它们可以依赖 Core / Foundation，但不允许被 Runtime Services 或业务层之外的运行时模块反向依赖（见规则 6）。第 4 节表格中的"层"一列与本图术语对应关系已在表头注明。

依赖只能整体向下：

1. Core 模块之间禁止通过 Services 横向查找，也不默认直接持有彼此；一对多通知使用 EventChannel。
2. ErrorHub 是明确例外：所有层都可以直接调用，ErrorHub 不反向依赖 EventChannel，避免错误处理循环。EventChannel 对 ErrorHub 的直接依赖属于这一例外的具体体现，不视为违反规则 1。
3. Services 只作为业务层访问长期框架服务的边界，不是框架内部的依赖捷径。
4. Runtime Service 可以依赖 Core 与 Runtime Foundation；服务之间若必须直接协作，依赖必须通过构造或初始化参数显式传入。
5. Scene、Audio、UI、Config 等加载 Godot Resource 时统一使用 ResourceHub，不重复封装 ResourceLoader。
6. Tools/Editor 能力不得成为运行时模块的依赖。

## 3. 运行时入口与生命周期

`addons/godo_framework/Core/GoDoRuntime.tscn` 是唯一 Autoload 入口，`GoDoRuntime.cs` 负责：

- 记录并验证 Godot 主线程；
- 初始化、更新和关闭 ResourceHub、ErrorHub；
- 获取长期 Node 服务并创建纯 C# 服务；
- 按接口注册服务，并在退出时按相反方向注销和清理；
- 安装进程级未处理异常兜底。

GoDoRuntime 初始化 UI 时，在 `/root` 下创建与自身平级的 `GoDoUI` 显示根。UiService 仍是 GoDoRuntime 的服务子节点，只管理引用和生命周期；业务 UI 实例挂载到 `GoDoUI` 的 Scene、View、Modal 三个显式 CanvasLayer。主场景切换成功后由 SceneService 广播内部事实事件，UiService 据此清空 Scene 层，不形成服务间直接引用。

GoDoRuntime 不承载菜单、关卡、登录等具体游戏流程。业务场景和测试场景不得重复初始化框架。

当前长期服务注册顺序为 Scene、Camera、Input、Audio、UI、Save、Settings、Procedure；Settings 通过构造函数显式依赖 Audio 与 Save。Camera 当前只依赖 Core 与 Godot Node 生命周期，不直接依赖 Scene 或具体摄像机插件。Input 当前只依赖 Core、Godot 值类型和 `IInputBackend` 边界；未安装后端时保持未就绪且不执行每帧采样。退出时反向注销。Services 只保存引用，不负责创建、释放或推断生命周期。Procedure 只注册顶层流程切换服务，GoDoRuntime 不主动进入任何业务流程。

具体摄像机插件通过核心外的可选 `CameraRig` 适配包接入。例如 `addons/godo_phantom_camera/` 依赖 GoDo Camera 与 Phantom Camera，但 GoDo 核心不反向依赖该包。

具体输入插件通过核心外的 `IInputBackend` 适配包接入。例如 `addons/godo_guide_input/` 依赖 GoDo Input 与 G.U.I.D.E-CSharp，但 GoDo 核心不反向依赖该包。业务只使用 GoDo 语义 ID 与 InputFrame。

所有直接操作 Godot 对象或框架共享状态的公共 API 默认限制在 GoDoRuntime 记录的主线程；具体约束以模块 `USAGE.md` 为准。

## 4. 已采用模块

> **层级术语对应**：本表"层"列的 Foundation / Service 对应第 2 节分层图中的 Runtime Foundation / Runtime Services；Tools、Editor、Development 三层是侧挂关系，不在主依赖链路图中单独画出。
>
> **状态图例**：`稳定基线` = 已长期使用、接口基本不再变动；`首版完成` = 首个可用版本已完成，接口可能仍会调整；`核心接入` = 核心边界和运行时生命周期已接入，但具体后端或真实游戏验证尚未完成；`Windows 稳定基线` = 目前仅在 Windows 平台验证为稳定，其他平台的行为尚未确认，跨平台前需在对应 `USAGE.md` 中核实；`已采用` = 专用于 GoDoRuntime 这类框架入口本身——它是固定的运行时骨架而非可替换/可独立演进的模块，因此不套用其余三种成熟度分级。

| 层 | 模块 | 核心职责 | 主要依赖 | 业务入口 | 状态 |
|---|---|---|---|---|---|
| Core | EventChannel | 类型安全的一对多同步通知与订阅生命周期 | ErrorHub（异常报告，见规则 2 的例外说明） | 静态 API | 稳定基线 |
| Core | ErrorHub | 结构化错误、控制台输出、Reporter 与后台队列 | 无框架内部依赖（仅使用 Godot 日志/文件 API）；不存在任何模块反向依赖它以外的模块 | 静态 API | 稳定基线 |
| Core | LogHub | Debug 构建中的普通开发日志、统一控制台格式与固定容量历史；异常、降级和失败仍由 ErrorHub 处理 | Core 主线程约束、Godot 控制台 API | 静态 API | 首版完成 |
| Core | Services | 按接口登记长期服务 | 无 | `Services.Get<T>()` | 稳定基线 |
| Core | GoDoRuntime | 唯一 Autoload 入口：初始化/关闭 ResourceHub 与 ErrorHub、注册与注销长期服务、安装进程级异常兜底（详见第 3 节） | Core 其余模块（ErrorHub、EventChannel、Services）、Godot Autoload 机制 | 无（框架内部入口，不面向业务代码直接调用） | 已采用 |
| Foundation | ResourceHub | `ResourceKey`、语义资源注册表、同步/线程化加载、类型检查与请求合并 | Core、Godot ResourceLoader | 静态 API | 稳定基线 |
| Foundation | NodePool | PackedScene 节点实例复用与显式重置生命周期 | Core、Godot Node | 实例 API | 稳定基线 |
| Service | Scene | 主内容场景异步加载与安全替换 | ResourceHub、Core | `ISceneService` | 稳定基线 |
| Service | Camera | 主镜头 Rig 的语义注册、激活、恢复与场景生命周期清理 | Core、Godot Node | `ICameraService` | 首版完成 |
| Service | Input | 语义 Action 当前帧快照、Context 栈与可替换后端边界 | Core、Godot 值类型、`IInputBackend` | `IInputService` | 核心接入 |
| Service | Audio | BGM、SFX 池与音量分组 | ResourceHub、NodePool、Core | `IAudioService` | 稳定基线 |
| Service | UI | 屏幕空间 UI 的 Scene、View、Modal 层与返回栈 | ResourceHub、Core | `IUiService` | 稳定基线 |
| Service | Save | 多槽位可靠容器、校验、备份和 Codec 边界 | Core、Godot FileAccess | `ISaveService` | 稳定基线 |
| Service | Settings | 音量、Locale、显示偏好及独立持久化 | Audio、Save、平台适配器 | `ISettingsService` | Windows 稳定基线（其他平台待验证，见上方图例） |
| Service | Procedure | 顶层游戏流程阶段的串行切换与进入/退出生命周期 | Core、Services | `IProcedureService` | 首版完成 |
| Tools | Config | 强类型 Resource 校验与唯一键只读表 | ResourceHub | `ConfigHub` / `ConfigTable` | 稳定基线 |
| Editor | Installer / Validator | GoDoRuntime Autoload 的显式安装、健康检查与 ResourceManifest 只读校验 | Godot Editor API | `项目 → 工具 → GoDo Framework` | 稳定基线 |
| Development | Debugger | Debug 构建的只读运行时仪表盘 | 各模块 Debug 快照 | GoDoRuntime 自动创建 | 稳定基线 |

模块的完整公共 API、失败语义、线程限制、性能注意事项和验证范围以各自 `USAGE.md` 为唯一详细来源。

## 5. 关键设计决策

### 5.1 EventChannel 事件分层

- `IEventMessage` 是 EventChannel 的公共底层标记，消息必须为 `struct`。
- GoDo 内部事件额外实现 internal `IFrameworkEvent`。
- 具体游戏可定义自己的业务标记，例如 `IGameEvent : IEventMessage`。
- 当前框架与业务代码在同一程序集，internal 只是可检查的架构约定；拆分程序集后才形成编译器级外部限制。
- 框架事件优先由所属模块维护，业务事件留在业务命名空间；不建立混合所有事件的全局文件。
- 需要返回结果时直接调用接口；场景树内局部关系优先用 Godot Signal；一对多事实通知才使用 EventChannel。

### 5.2 ErrorHub 例外

EventChannel 的监听者异常由 ErrorHub 报告，因此 ErrorHub 不得再通过 EventChannel 分发。`Fatal` 只表示最高严重等级，不主动退出；退出策略属于调用边界。

### 5.3 Services 边界

Services 只允许按接口注册；重复注册、缺失查询和错误实例注销都有明确结果。它不是依赖注入容器，也不允许把短生命周期节点或任意对象变成全局变量。

### 5.4 资源加载边界

ResourceHub 只包装 Godot 资源加载机制，不建立第二套引用计数或缓存。公共 API 使用 `ResourceKey` 与 `T : Resource`；`ResourceKey` 支持 `res://` 路径与 `uid://` Godot Resource UID。`ResourceManifest` 与 `ResourceRegistry` 只提供业务语义 ID 到 `ResourceKey` 的运行时映射，不负责自动扫描、生成清单或远程资源管理。加载失败抛出 `ResourceLoadException`，不返回 null，也不先上报再抛出。

远程下载、PCK/DLC、热更新、目录批量加载和高级缓存属于未来独立扩展，不混入首版核心。

### 5.5 Save 与 Settings 分离

Save 负责可靠容器和 Codec 边界，不理解具体业务数据。Settings 复用 Save 的独立固定槽位，但与游戏进度存档分离；设置修改立即应用，只有显式 `Save()` 才写盘。

## 6. 目录职责

```text
addons/
└── godo_framework/       可整体复制的 GoDo 插件包
    ├── plugin.cfg        EditorPlugin 发现入口
    ├── Editor/           安装、健康检查与资源清单校验，不进入运行时依赖（对应第 4 节 Editor 层）
    ├── Core/             最小稳定核心与 GoDoRuntime
    ├── Runtime/          Resources、Pool 与长期运行时服务
    │                     （内部再分 Foundation 子目录如 Resources/Pool，
    │                      与 Service 子目录如 Scene/Camera/Input/Audio/UI/Save/Settings/Procedure，
    │                      对应第 4 节表格中的 Foundation 层与 Service 层）
    ├── Tools/            相对独立的配置工具（对应第 4 节 Tools 层）
    └── Debugger/         仅 Debug 运行时创建的只读诊断面板（对应第 4 节 Development 层）
```

每个独立模块目录必须提供 `USAGE.md`。源码与文档冲突时以源码和工程配置为准，同时修正文档。

## 7. 通用约定

- 框架错误统一通过 ErrorHub；不要直接散落 `GD.PrintErr`。
- Debug 专属实现使用 `#if DEBUG`，覆盖编辑器 Debug 与 ExportDebug，不进入 Release / ExportRelease。
  当前源码未使用 `GODOT_DEBUG`；如未来新增 Debug 专属代码，继续沿用 `DEBUG`。
- public API 必须提供 XML 注释，并明确失败语义。
- 新模块开工前先检查现有模块是否已有同类能力，并在设计计划中确认依赖方向。
- 不清楚的架构决策先讨论，不让实现细节替代设计决策。
