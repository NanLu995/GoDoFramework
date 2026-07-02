# AGENTS.md — Godot C# 项目协作规则

> 这份文件给AI（Codex / 本地模型）看，不是给人看的README。
> 每次AI开始工作前会先读这份文件。改完这份文件要重启Codex会话才会生效。

## 项目基本信息

- 引擎：Godot 4.7，使用 C# / .NET 8（不是 GDScript；Android 构建目标为 .NET 9）
- 项目名称：GoDoFramework；框架根命名空间：`GoDo`
- 目标平台：待确定，不要自行假设平台特性
- 当前是单人开发，没有团队协作流程，但希望AI按专业标准做事

## 开始工作前

- `FRAMEWORK_OVERVIEW.md` 记录框架愿景、历史痛点和早期设想，仅在讨论框架定位或重新规划时读取。
- `FRAMEWORK_DESIGN_PLAN.md` 记录建设范围、优先级和阶段路线；新增模块或调整开发顺序前必须读取。
- `ARCHITECTURE.md` 记录当前已经采用的架构事实与依赖规则；修改 `GoDo/` 下的框架代码前必须读取。
- `GODOT_GOTCHAS.md` 记录项目实际遇到的 Godot/C# 坑位；处理相关问题时按需读取。
- `AGENTS.md` 规定协作方式和通用代码规范，`ARCHITECTURE.md` 规定框架边界和模块依赖；两者都必须遵守。如有冲突或含义不清，先指出具体冲突并询问我。
- 文档与代码状态冲突时，以代码和工程配置的实际状态为依据，同时指出需要同步的文档，不能静默沿用过期描述。
- 搜索和分析项目文件时排除 `.godot/`、`bin/`、`obj/` 及自动生成的 `*.Generated.cs`，不要修改其中内容。

## 框架入口

- `GoDo/Core/GoDoRuntime.tscn` 是框架唯一 Autoload 入口，`GoDoRuntime.cs` 负责框架初始化与退出清理。
- 不要在业务场景或测试场景中重复初始化框架；`TestScene.tscn` 仅用于测试。
- 新增需要全局生命周期的框架服务时，先在设计计划中确认依赖和顺序，再接入 GoDoRuntime；GoDoRuntime 不承载具体游戏流程。
- 不要自动修改其他项目的 Autoload 配置；未来由 EditorPlugin 提供显式的一键安装和卸载能力。

## 工作方式（重要）

- **每次只做一件具体的事**。如果我的需求里包含多个改动，先列出你打算做的步骤，等我确认再动手。
- **改动前先说明思路**，不要直接大段重写整个文件。优先用最小改动达成目标。
- **不确定 Godot API 的具体用法时，明确说“我不确定这个 API 的细节”**，不要凭记忆编造方法名、参数顺序或信号名。依次检查项目现有实现、Godot 官方文档和编译器反馈；仍无法确认时再询问我。
- 涉及节点树结构、场景文件（.tscn）改动时，先告诉我你的改动会如何影响场景树，再执行。
- 完成代码修改后，**用一句话总结你改了什么、为什么这么改**，不要写长篇大论。

## 代码规范

- 命名：类名/方法名用 PascalCase，私有字段用 _camelCase，常量用 PascalCase。
- 节点引用统一通过 `[Export]` 字段暴露，不要在代码里写死 `GetNode("路径/写死")`。
- 避免在 `_Process` / `_PhysicsProcess` 里做高开销操作（比如频繁 GetNode、字符串拼接、LINQ查询），有疑问就提出来问我，不要默认这样写。
- 异步操作优先用 Godot 的信号/协程方式（`await ToSignal(...)`），不要随意引入 `Task.Run` 除非我明确要求。

### 信号（Signal）写法 —— 必须严格遵守，这是最容易出错的地方

本项目是 **Godot 4.x**，信号系统和 Godot 3.x 完全不同。绝对不要用 Godot 3.x 的字符串写法。

**正确写法（C# 事件风格，优先用这个）：**
```csharp
[Signal]
public delegate void HealthChangedEventHandler(int newHealth);

// 订阅
someNode.HealthChanged += OnHealthChanged;

// 取消订阅
someNode.HealthChanged -= OnHealthChanged;

// 触发
EmitSignal(SignalName.HealthChanged, newHealth);

private void OnHealthChanged(int newHealth) { ... }
```

**正确写法（Callable方式，需要更细粒度控制时用）：**
```csharp
someNode.Connect(SignalName.HealthChanged, Callable.From<int>(OnHealthChanged));
```

**绝对禁止（Godot 3.x 旧语法，会直接报错或静默失效）：**
```csharp
// 禁止 —— 这是Godot 3.x写法
Connect("health_changed", this, "OnHealthChanged");
```

**生命周期规则（必须遵守，否则可能导致内存泄漏或“在已释放对象上调用方法”的报错）：**
- 对生命周期不同，或信号源可能比订阅者存活更久的订阅，订阅写在 `_EnterTree()` 或 `_Ready()`，并在对应的 `_ExitTree()` 对称取消。
- 如果项目已有 `EventChannel.Bind` 等自动绑定节点生命周期的机制，优先使用现有机制，不要再重复手动解绑。
- 手动取消 Godot 对象的订阅前，先用 `IsInstanceValid(目标对象)` 判断对象是否仍然存在，再执行 `-=`。
- 不要用匿名 lambda 订阅信号（`node.Signal += () => {...}`），这样无法手动取消订阅，容易内存泄漏。优先用具名方法。
- 信号命名用动词过去式或描述性短语（如 `HealthChanged`、`Died`），订阅方法用 `On` 前缀（如 `OnHealthChanged`）。

如果不确定某个内置信号（比如 Tween、AnimationPlayer 自带的信号）叫什么名字，先查项目现有实现和对应版本的 Godot 官方文档；仍无法确认时明确说明，不要凭记忆编造。

### 节点引用与空安全

- 优先用 `[Export]` 字段在编辑器里手动拖拽赋值，而不是代码里 `GetNode<T>("路径")` 硬编码路径——路径一旦改场景结构就会失效，且失效时往往没有编译期报错。
- 如果必须用 `GetNode`，要加判空处理或者用 `GetNodeOrNull<T>()`，不要假设节点一定存在。
- 涉及 `QueueFree()` 释放节点后，不要在同一帧内继续访问该节点的属性或方法；如果不确定时序，提出来问我。

### 性能相关的具体规则

- `_Process` / `_PhysicsProcess` 里禁止每帧 `new` 大对象（数组、List、字符串拼接），改为成员变量复用。
- 禁止在 `_Process` / `_PhysicsProcess` 里用 `GetNode` / `FindChild` 这类查找类API，应在 `_Ready()` 里缓存好引用。
- 大量重复创建/销毁的对象（子弹、特效粒子等）优先考虑对象池模式，不要默认每次 `Instantiate()` + `QueueFree()`。如果我没要求做对象池，先实现最简单版本，但可以提醒我后续可以优化。

## 禁止事项

- 不要修改 `.csproj`、`project.godot`、`export_presets.cfg` 这些工程配置文件，除非我明确要求。
- 不要删除或重命名现有的 public 方法/信号，除非我明确同意——这些可能被其他场景脚本引用。
- 不要引入新的 NuGet 包依赖，除非先问我。
- 不要自动执行 git commit / git push。

## 框架边界

- `GoDo.*` 只提供可跨游戏复用的机制，不得引用角色、血量、子弹、关卡规则等具体玩法概念；业务代码不得放进 `GoDo.*` 命名空间。
- Core 层模块之间禁止通过 `ServiceLocator` 或直接持有引用进行横向依赖；模块通信遵循 `ARCHITECTURE.md`，使用 `EventChannel`。`ErrorHub` 是文档明确规定的例外。
- 新增模块或公共 API 前，先检查现有模块是否已经提供同类能力，不要重复实现事件、日志、错误处理、对象池等基础设施。
- 不要删除、重命名或改变现有 public API 和信号语义，除非我明确同意；新增 public API 时说明它的职责、依赖方向和兼容性影响。
- Scene、Audio、UI、Config 等运行时模块加载 Godot Resource 时统一使用 `ResourceHub`，不要各自重复封装 `Godot.ResourceLoader`。
- ResourceHub 只包装 Godot 资源加载机制：公共 API 使用 `ResourceKey` 和 `T : Resource`，失败抛出 `ResourceLoadException`；禁止返回 null、重复上报后再 throw，或自行维护第二套引用计数与缓存。
- 远程下载、PCK/DLC、热更新、目录批量加载和自定义缓存属于未来独立扩展，不得混入 ResourceHub 首版核心。

## 测试与验证

- 代码修改后默认可以执行 `dotnet build` 和不依赖 Godot 编辑器交互的自动化测试；如果测试会修改项目数据、场景或外部状态，必须先询问我。
- 涉及输入、物理、节点生命周期、场景切换或编辑器配置时，提醒我在 Godot 编辑器中手动运行对应场景验证。
- 不得把“编译通过”描述成“功能验证通过”，也不得在没有实际执行时声称测试通过。
- 完成修改后简要报告：修改了哪些文件、是否影响 public API 或场景树、执行了什么验证、还需要我手动验证什么。

## 当你不确定时

- 缺少上下文（比如不知道某个节点在场景里怎么挂的）就直接问我，不要假设。
- 如果你判断某个需求实现方式有更好的做法，可以提出建议，但默认还是先做我要求的最小实现。

## 给本地小模型的额外提醒

如果你是本地运行的中小型模型（参数量低于30B），请特别注意：

- 你对 Godot 4.x 相对较新的API（4.2+引入的特性）记忆可能不准确或停留在 Godot 3.x，涉及具体类名、方法签名时，优先参考项目里已有的同类代码作为范例，而不是凭训练记忆直接写。
- 不要在一次回复里同时改动超过2个文件，除非任务明确要求跨文件改动（比如新增一个类同时要在场景里挂载）。
- 长任务（预计改动较多）请先分解成步骤列表展示给我，不要直接开始大段编辑。
- 如果生成的代码里出现了你自己也不确定是否存在的API（比如不常见的内置类、方法），用注释标注 `// TODO: 请确认此API是否存在`，不要假装自己很确定。

## 回复格式

- 默认用简体中文回复说明性文字，代码注释也用中文（除非项目已有英文注释习惯，保持一致）。
- 不要输出大段的"我理解了""好的我来帮你"这类开场白，直接进入实质内容。
- 改动完成后的总结控制在3句话以内：改了什么、为什么、有什么需要我注意的。
