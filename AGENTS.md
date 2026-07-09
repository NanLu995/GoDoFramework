# AGENTS.md — Godot C# 项目协作规则

> 本文件供 AI 使用。修改后需重启 Codex 会话才会作为启动规则生效。

## 项目与文档路由

- 引擎：Godot 4.7，C# / .NET 8；Android 构建目标为 .NET 9。框架根命名空间：`GoDo`。
- 目标平台待定，不自行假设平台特性。
- `FRAMEWORK_OVERVIEW.md`：历史愿景与痛点，仅讨论框架定位或重新规划时读取。
- `FRAMEWORK_DESIGN_PLAN.md`：目标、状态与路线；新增模块或调整顺序前读取。
- `ARCHITECTURE.md`：当前架构事实与依赖；修改 `addons/godo_framework/` 前读取。
- 模块 `USAGE.md`：API、失败语义、生命周期、性能和验证细节；处理对应模块时读取。
- `GODOT_GOTCHAS.md`：项目实际遇到的 Godot/C# 坑位，按需读取。
- 文档与代码冲突时，以源码和工程配置为准，同时指出并修正文档，不静默沿用旧描述。
- 常规搜索排除 `.godot/`、`bin/`、`obj/`、`*.Generated.cs` 和离线文档目录，不修改生成内容。

## 框架入口

- `addons/godo_framework/Core/GoDoRuntime.tscn` 是唯一 Autoload 入口；`GoDoRuntime.cs` 只负责框架初始化、服务注册与退出清理，不承载游戏流程。
- 业务场景和测试场景不得重复初始化框架；`TestScene.tscn` 仅用于验证。
- 新增长期服务前，先在设计计划确认依赖和顺序，再接入 GoDoRuntime。
- 不自动修改其他项目的 Autoload；未来由 EditorPlugin 提供显式安装能力。

## 工作方式

- 每次只做一件具体的事。需求包含多个改动时，先列步骤，等我确认再修改；长任务先拆步骤执行，不要一次性堆完。
- 改动前先说明思路和影响，优先最小改动，不大段重写无关代码。
- 修改已有文件时，优先说明改动点并给出增量修改内容，不整份重新粘贴未改动的代码或场景内容。
- 涉及 `.tscn` 或节点树时，修改前说明场景树变化。
- 不确定 Godot API 时明确说明，不凭记忆编造名称或签名；依次检查项目现有实现、离线 4.7 文档、官方在线文档，最后以编译器反馈为准。
- 工作区可能已有我的修改；必须保留并避开无关改动，无法避开时先说明。

## C# 与 Godot 规则

- 类/方法 PascalCase，私有字段 `_camelCase`，常量 PascalCase；public API 提供 XML 注释。
- 类名、方法名、字段名、事件名、资源键、场景名和目录名优先使用计算机/工程语义，少用口语化自然语言；命名应表达类型、职责、生命周期或数据含义，例如 `ProcedureContext`、`SaveSlot`、`ResourceKey`，避免 `DoSomething`、`HandleStuff`、`GameThing`、`Shell` 这类含义松散或需要上下文猜测的名称。
- 缩写遵循项目既有 C# 风格：类型和成员名使用 `Ui`、`Bgm`、`Sfx` 这类 PascalCase 形式；文档正文描述模块概念时可使用 `UI`、`BGM`、`SFX`。
- `Hub`、`Service`、`Controller`、`Adapter`、`Factory`、`Codec`、`Operation`、`Status` 等后缀只在职责与生命周期确实匹配时使用；不要为听起来像框架而套后缀。
- 重命名 public API、信号、资源键、场景名或目录名属于兼容性改动，必须先说明影响、迁移范围和验证方式，等待明确确认后再改。
- 节点引用优先使用 `[Export]`；必须查找时用 `GetNodeOrNull<T>()` 并处理缺失，不硬编码脆弱路径。
- 异步优先 Godot 信号/协程（`await ToSignal(...)`）；未明确要求时不使用 `Task.Run` 操作 Godot 对象。
- `QueueFree()` 后不要继续访问节点；不确定释放时序时先询问。

### Godot 4.x Signal

- 优先使用 C# 事件风格：`source.HealthChanged += OnHealthChanged`，触发用 `EmitSignal(SignalName.HealthChanged, value)`。
- 需要 Callable 时使用 `Connect(SignalName.X, Callable.From(...))`；禁止 Godot 3 的 `Connect("signal", this, "Method")`。
- 生命周期不同或信号源更长寿时，在 `_EnterTree()` / `_Ready()` 订阅，并在 `_ExitTree()` 对称取消；取消前确认 Godot 对象仍有效。
- 已有 `EventChannel.Bind` 等自动生命周期机制时优先使用，不重复手动解绑。
- 不用匿名 lambda 订阅需要解绑的信号；使用 `OnXxx` 具名方法。
- 内置信号名不确定时先查项目与 4.7 文档。

### 高频路径

- `_Process` / `_PhysicsProcess` 中不每帧创建大数组、List 或拼接字符串，不使用 LINQ 热查询。
- 不在每帧路径调用 `GetNode` / `FindChild`，引用在初始化阶段缓存。
- 高频创建销毁的节点优先评估对象池；未要求对象池时先做简单版本，可提示后续优化。

## 禁止事项

- 不修改 `.csproj`、`project.godot`、`export_presets.cfg`，除非我明确要求。
- 不删除、重命名或改变现有 public API / 信号语义，除非我明确同意。
- 不引入 NuGet 包，除非先询问。
- 不自动执行 git commit / push。
- 用户明确要求执行 git commit 时，提交信息默认使用简体中文；用户指定其他语言时按其要求执行。

## 框架边界

- `GoDo.*` 只提供跨游戏机制，不包含角色、血量、子弹、关卡规则等玩法概念；业务代码不放入 `GoDo.*`。
- Core 模块不通过 Services 或直接引用横向耦合；遵循 `ARCHITECTURE.md` 使用 EventChannel。ErrorHub 是明确例外——因其承担全局错误上报职责，需被各层直接访问，不走 EventChannel 这层间接层；除 ErrorHub 外不再新增同类例外。
- Services 只供业务层访问长期服务，不是框架内部依赖捷径。
- 新增模块/API 前先检查现有事件、日志、错误、资源和对象池能力，避免重复实现。
- 设计框架功能前必须先说明性能、鲁棒性与稳定性取舍：是否处于高频路径、是否产生额外分配、失败是否可见、生命周期是否可清理、跨平台/Debug 与 Release 行为是否一致；没有验证证据时不要把接口标为稳定基线。
- Scene、Audio、UI、Config 等加载 Godot Resource 时统一使用 ResourceHub。
- ResourceHub API 使用 `ResourceKey` 与 `T : Resource`；失败抛 `ResourceLoadException`，不返回 null、不重复上报后再抛出，也不维护第二套缓存或引用计数。
- 远程下载、PCK/DLC、热更新、目录加载和高级缓存属于未来独立扩展。

## 测试与交付

- 每个独立模块必须有 `USAGE.md`，说明定位、适用/非适用场景、上手、public API、失败语义、生命周期/线程、性能与误用。
- public API、失败语义、生命周期或依赖变化时同步更新 `USAGE.md`，示例必须与源码一致。
- 修改后默认可运行 `dotnet build` 和不依赖编辑器的测试；会修改项目数据、场景或外部状态的测试必须先询问。
- 输入、物理、节点生命周期、场景切换或编辑器配置仍需提醒我在 Godot 中手动验证。
- 编译通过不等于功能验证通过；未执行的测试不得声称通过。
- 完成后简报：修改文件、public API/场景树影响、已执行验证和待手动验证，用列表列出即可，不必压缩成几句话。

## Godot 4.7 离线文档

- 目录：`Godot Engine 4.7 documentation in English MD/`，约 1593 个平铺 Markdown，文件名如 `gdd_1242_DisplayServer.md`。
- 禁止枚举、输出或整读全部文档。类 API 先按 `*ClassName.md` 定位单文件，再在该文件内搜索成员。
- 文档使用 snake_case；查询 C# API 时同时考虑映射名，如 `WindowSetMode` → `window_set_mode`，最终以当前项目编译确认绑定签名。
- 教程/概念先按文件名缩小候选；无法定位时才做目录内容搜索，并限制为最相关的 1–3 个文件。
- 只读取命中行附近的签名、说明和注意事项；结果过多时继续增加类名或成员名。
- 优先 `rg`；不可用时使用 `Select-String`，仍遵循“先文件名、后单文件、限制结果数”。

## 回复与不确定性

- 默认简体中文；注释跟随项目现有语言习惯。
- 缺少节点挂载、平台或业务上下文时直接询问，不自行假设。
- 可以提出更好方案，但默认先实现我要求的最小版本。
- 不写大段客套开场；正文之外不做无关寒暄。
