# GoDoFramework 架构文档

> Godot C# 游戏开发框架
> 命名空间：`GoDo`
> 调用风格：Core 使用 `GoDo.EventChannel.Emit(...)` 等稳定入口；独立运行时模块按职责使用实例 API，例如 `NodePool<T>.Acquire(parent)`。

---

## 框架边界声明（必须遵守的红线）

`GoDo.*` 命名空间下的所有代码，**永远不感知任何具体游戏玩法**。框架不知道"血量"是什么、不知道有没有"子弹"、不知道游戏是横版还是回合制。框架只提供机制（事件总线、对象池、场景切换这种通用能力），具体怎么用是业务层的事。

判断一段代码该不该放进 `GoDo.*` 的简单测试：**如果这段代码里出现了任何只属于这个游戏、换个游戏类型就不成立的概念，它就不属于框架，应该放进业务层代码里。**

业务层代码（角色控制器、战斗系统、对话系统等）放在 `GoDo.*` 命名空间之外，使用框架提供的能力，但框架不会反过来引用业务层的任何东西。

---

## 分层架构与依赖规则

框架内部按依赖方向分为三层，业务层在框架之上：

```
业务层（游戏代码）
        ↓
Runtime Services（Scene / Audio / UI / Config 等）
        ↓
Runtime Foundation（Resources / Pool 等通用运行时能力）
        ↓
Core（ErrorHub / EventChannel / GoDoRuntime）
        ↓
Godot 4.7 C# / .NET
```

### 硬性规则（写代码前必须确认自己没违反）

1. **Core 层内部模块之间禁止互相持有引用、互相 Get**。如果 A 模块需要在某个时机通知 B 模块，走 EventChannel 发事件，不要直接调用对方的方法。
2. **唯一例外是 ErrorHub**。任何模块（包括 Core 层之间、业务层）都可以直接调用 `GoDo.ErrorHub.Report(...)` 报错，不需要绕事件系统。ErrorHub 只接收调用，不会反过来依赖任何其他模块——它的 `OnError` 用原始 C# event 委托实现，不经过 EventChannel，避免循环依赖。
3. **Services 注册表专门给业务层访问框架服务用**，Core 层内部模块之间不允许通过 Services 互相访问。
4. 任何新模块开工前，先确认：这个模块要用到的能力，是不是已经有别的模块提供了？不要重复造轮子（比如不要给自己模块单独写一套日志逻辑，统一走 Log）。
5. Scene、Audio、UI、Config 等模块统一通过 ResourceHub 加载 Godot Resource，不允许各自复制线程化加载、进度轮询和类型检查。
6. ResourceHub 依赖 Godot ResourceLoader 与 Core，不反向依赖任何上层 Runtime Service。

---

## Core 层模块清单与优先级

Core 层内部按"地基程度"排序，越底层的模块越早做，因为后面的模块大概率会依赖它。

### 已完成

#### EventChannel —— 事件系统（框架通信骨干）

**文件**
- `EventChannel.cs` —— 核心实现
- `EventScope.cs`   —— 纯 C# 类的批量生命周期管理
- `IGameEvent.cs`   —— 事件标记接口

具体游戏事件由业务层自行定义，不属于 `GoDo` 框架；示例项目的事件当前位于 `Script/GameEvents.cs`。

**已实现的特性**
- 泛型类型安全事件，struct 零 GC 分配
- `On` / `Off` / `Once` / `Emit` 基础 API
- `Bind` 绑定 Node 生命周期，退出树自动解绑
- `EventScope` 批量管理，支持链式注册，`IDisposable` 一键清除
- 优先级排序（priority 越小越先执行）
- 派发安全：派发深度 + 延迟列表，支持同类型事件重入，最外层派发结束后统一提交注册/注销
- 异常隔离：单个 handler 崩溃不影响其他 handler，打印完整调用栈
- `try/finally` 保证 `_isDispatching` 一定复位
- `#if GODOT_DEBUG` 条件编译，Release 零开销
- Debug 工具：`DumpRegistry` / `GetListenerCount`
- 重复注册检测（所有构建配置行为一致，覆盖派发中途场景）
- `Bind` 节点不在树中时提前 return，且只在监听注册成功后连接生命周期
- `ClearAll` 限制为 internal，防止业务误用

**设计决策记录**
- 事件用 `struct` 不用 `class`：零堆分配，无 GC 压力
- `HandlerEntry` 用 `struct`：避免每次 On/Once 的堆分配
- `RemoveFromList` 反向遍历：零 lambda 分配，语义精确
- `typeof(T)` 缓存为局部变量：避免 Emit 中重复调用
- List 预设初始容量：`_handlers(4)` / 其余 `(2)`，避免首次扩容
- 所有延迟列表统一 `for` 索引循环，JIT 友好
- EventChannel 仅限 Godot 主线程调用，不提供线程安全保证

**这是整个框架的设计哲学基准**：性能优先、零GC意识、Debug/Release分离、防御性编程（异常隔离、重复注册检测）。后续所有 Core 模块的实现严谨程度都应该向这个看齐。

---

### 优先级 1（框架地基）

#### ErrorHub —— 框架级错误中心

**状态：本地稳定基线完成；正式远程上报待后续实现。**

**职责**
- 为框架和业务层提供统一的显式错误上报入口与格式化输出
- 通过 `GoDoRuntime` 安装 `AppDomain.UnhandledException` 作为进程级兜底；不承诺捕获所有被 Godot 引擎处理的脚本回调异常
- 错误分级：Debug / Warning / Error / Fatal
- 错误上下文记录：发生在哪个模块、哪个事件、哪个节点
- Debug 模式：详细调用栈 + 模块信息
- Release 模式：精简日志，预留远程上报接口
- 后期扩展：错误数据序列化，网络上报到自建服务器或第三方（Sentry 等）
- `Fatal` 只表示最高严重等级，不主动终止游戏，退出策略由调用方决定
- 后台线程报告进入有界队列，由 `GoDoRuntime` 在主线程按帧预算分发，避免业务监听者跨线程操作场景树
- `GoDoRuntime` 退出时调用 ErrorHub Shutdown，清理监听者和 Reporter；实现 `IDisposable` 的 Reporter 可在此刷新和释放资源
- 已通过监听者异常隔离、Reporter 异常隔离、递归保护、后台队列和等级过滤运行时验证

**架构约束（重要）**
- ErrorHub **不依赖 EventChannel**，也不依赖任何其他 Core 模块。它的 `OnError` 用原始 C# `event` 委托实现。这是为了避免"EventChannel 出错要用 ErrorHub 报错，ErrorHub 又用 EventChannel 分发"这种循环依赖。
- 所有模块（包括业务层）都可以直接调用，不需要通过 ServiceLocator 或 EventChannel。

**计划 API**
```csharp
GoDo.ErrorHub.Report(exception, context: "EventChannel.Dispatch");
GoDo.ErrorHub.Warn("重复注册 handler", module: "EventChannel");
GoDo.ErrorHub.OnError += (err) => { /* 上报逻辑 */ };
```

**错误数据结构**
```csharp
public struct ErrorReport
{
    public ErrorLevel Level;       // Debug / Warning / Error / Fatal
    public string     Module;      // 来自哪个框架模块
    public string     Message;     // 错误描述
    public string     Context;     // 额外上下文（节点名、事件类型等）
    public Exception  Exception;   // 原始异常（可为 null）
    public DateTime   Timestamp;
    public string     StackTrace;
}
```

**分层设计**
```
业务代码 / 框架模块
        ↓ 发生异常
  ErrorHub.Report()
        ↓
  ErrorFormatter     —— 格式化：模块名 + 上下文 + 调用栈
        ↓
  ErrorDispatcher    —— 分发给所有注册的处理器
    ↙         ↘
GodotLogger    RemoteReporter（预留）
(GD.PrintErr)  (HTTP 上报 / Sentry / 自建服务器)
```

**网络上报预留接口**
```csharp
GoDo.ErrorHub.AddReporter(new SentryReporter());
GoDo.ErrorHub.AddReporter(new MyServerReporter("https://errors.mygame.com"));
```

---

#### Log —— 日志系统

**职责**
- 日志分级，Release 可关闭
- 与 ErrorHub 联动（ErrorHub 内部用 Log 输出格式化后的内容，但 Log 本身不反向依赖 ErrorHub）

**架构说明**：Log 和 ErrorHub 是近亲，建议一起实现。Log 是更通用的"输出"能力，ErrorHub 是更专门的"异常处理"能力，ErrorHub 内部会调用 Log 来实际打印，但 Log 不需要知道 ErrorHub 的存在。

---

#### Tick —— 统一更新管理

**职责**
- 接管 `_Process` / `_PhysicsProcess`，避免各节点各自为政
- 支持优先级、分组暂停
- 后续的 Pool（对象池状态更新）、Audio（淡入淡出效果）等模块大概率需要依赖它

**为什么提到这么靠前**：如果 Tick 在 Pool、Audio 这些模块做完之后才补，那些模块只能各自写自己的 `_Process`，等 Tick 做出来还得回头重构接入，属于早做省事、晚做返工的典型情况。

---

#### Services —— 服务注册表（仅供业务层使用）

**状态：首版稳定基线完成。**

**职责**
- 业务层按接口访问少量长期框架服务，替代业务代码里到处 GetNode 的硬编码
- 接口与实现分离，方便测试时替换 Mock

**架构约束（重要）**
- **Core 层内部模块之间禁止通过 Services 互相访问**，模块间通信走 EventChannel。
- 只允许按接口注册；重复注册和缺失查询明确失败，注销时必须匹配原注册实例。
- Services 不负责自动构造、依赖注入、生命周期推断或任意具体类型的全局化。

**当前 API**
```csharp
GoDo.Services.Register<IAudioService>(audioService);
GoDo.Services.Get<IAudioService>().PlaySfx("jump");
GoDo.Services.Unregister<IAudioService>(audioService);
```

已通过缺失查询、重复注册、接口约束、错误实例注销、正常注销和 `TryGet` 运行时验证。

---

### 优先级 2（核心游戏功能）

#### ResourceHub —— 统一资源加载

**状态：首版稳定基线完成，首次线程加载、类型检查、并发合并、进度、主线程完成和 Shutdown 已通过 Godot 运行时验证。**

**职责**
- `ResourceKey` 统一验证和规范化 `res://` 资源路径，为未来 Resource UID 留出 API 边界
- `Load<T>` 同步加载与类型检查，`T` 必须继承 Godot `Resource`
- `LoadAsync<T>` 包装 Godot 线程化加载，跨帧提供状态、进度和 Completion
- 相同 ResourceKey 的并发请求合并；同路径不同类型冲突会明确失败
- 由 GoDoRuntime 在主线程 Update 和 Shutdown，完成回调不跨线程操作业务场景树
- 复用 Godot CacheMode.Reuse，不建立自定义引用计数、递归释放或第二套缓存

**当前 API**
```csharp
ResourceKey key = ResourceKey.Create("res://Scenes/Level01.tscn");
PackedScene syncScene = ResourceHub.Load<PackedScene>(key);
ResourceLoadOperation<PackedScene> operation = ResourceHub.LoadAsync<PackedScene>(key);
PackedScene asyncScene = await operation.Completion;
```

**明确边界**
- 失败抛出 `ResourceLoadException`，由 Scene、Audio 等业务边界捕获并交给 ErrorHub 补充上下文
- 首版不支持远程 URL、PCK/DLC、热更新、目录批量加载、手动 Unload、LRU 或下载重试
- Shutdown 只停止 GoDo 调用方等待；Godot 已启动的底层线程加载可能继续完成

---

#### Scene —— 场景管理

**状态：首版稳定基线完成。**

**职责**
- 使用 `ResourceKey` 与 ResourceHub 异步加载 `PackedScene`
- 加载和实例化成功后替换主内容场景，失败保留旧场景
- 提供切换状态与加载进度，切换期间拒绝第二个请求
- SceneService 由 GoDoRuntime Autoload 持有，并以 `ISceneService` 注册到 Services
- 服务离树或重新入树时取消尚未提交的切换，事件订阅对称解绑
- UI 弹窗、返回栈、过渡动画和统一错误场景不属于首版

**当前 API**
```csharp
ResourceKey levelKey = ResourceKey.Create("res://Scenes/Level01.tscn");
Node scene = await Services.Get<ISceneService>().ChangeAsync(levelKey);
```

已通过加载失败回滚、并发拒绝、服务离树取消、Autoload 注册、100 次缓存场景切换和旧节点释放验证；本次 Debug 结果为 608 ms、当前线程累计分配 136864 bytes、残留旧场景 0。

---

#### Pool —— 对象池

**状态：首版稳定基线完成。**

**职责**
- `NodePool<T>` 封装 PackedScene 的实例级池化，解决频繁 Instantiate/QueueFree 性能问题
- 支持初始容量（游戏开始时提前实例化）
- 通过 `IPoolable.OnAcquire/OnRelease` 由节点显式重置自身状态
- 空闲节点保持在场景树外，激活时加入指定父节点，释放时从场景树移除
- `idleCapacity` 表示空闲区容量，不限制同时活动的节点数量
- Pool 拥有它创建的节点；Dispose 会强制清理仍然活动的节点，业务代码不得绕过 Pool 直接 QueueFree
- `_Ready()` 默认不会因节点复用而再次调用，每次激活与清理逻辑必须放在 `OnAcquire/OnRelease`
- 已通过基础行为、异常路径、Dispose 和 1 万次 Acquire/Release 压力验证；本次 Debug 测试结果为 68 ms、当前线程分配 152 bytes

**当前 API**
```csharp
var pool = new NodePool<BulletNode>(bulletScene, initialSize: 20, idleCapacity: 100);
var bullet = pool.Acquire(projectileRoot);
pool.Release(bullet);
```

首版不支持纯 C# 对象池、多线程调用、自动字段重置、全局静态入口和复杂淘汰策略。所有操作限定在创建池的 Godot 主线程。

---

#### Audio —— 音频管理

**状态：首版稳定基线完成。**

**职责**
- 通过 ResourceHub 异步加载 AudioStream
- 单路 BGM 播放、暂停、恢复、停止和重复请求控制
- Master / BGM / SFX 线性音量管理，缺失 Bus 时运行时补齐
- 通过 NodePool 复用非空间 SFX Voice，自然结束自动回收
- 默认最大 32 路 SFX，容量满返回 false，不抢占已有声音
- 由 GoDoRuntime 持有并以 `IAudioService` 注册到 Services

**当前 API**
```csharp
IAudioService audio = Services.Get<IAudioService>();
await audio.PlayBgmAsync(bgmKey);
bool played = await audio.PlaySfxAsync(sfxKey);
audio.SetVolume(AudioGroup.Sfx, 0.8f);
```

加载失败抛出 `AudioPlaybackException`，主动 Stop 或服务退出使未完成请求收到 `OperationCanceledException`。首版不包含淡入淡出、播放列表、跨 BGM 混音和空间音频。

已通过 Bus、失败语义、BGM 并发与取消、SFX 自然回收、32 路上限、StopAll、100 次压力循环和服务离树清理验证。

---

### 优先级 3（开发效率）

#### Save —— 存档系统

**状态：首版稳定基线完成。**

**职责**
- 使用安全 `SaveSlot` 管理 `user://saves` 下的多槽位文件
- 统一二进制容器：魔数、容器版本、业务版本、UTC 时间、Payload 长度和 SHA-256
- 临时文件完整校验后提交，保留一份健康备份；损坏正式档不会覆盖健康备份
- 正式档失败时尝试备份，并通过 `SaveLoadStatus` 明确来源
- 业务层通过 `ISaveCodec<T>` 负责具体数据编码、解码与版本迁移
- 由 GoDoRuntime 创建并以 `ISaveService` 注册到 Services

**当前 API**
```csharp
ISaveService saves = Services.Get<ISaveService>();
saves.Save(slot, gameSave, dataVersion: 3, codec);
SaveLoadResult<GameSave> result = saves.Load(slot, codec);
```

首版为同步主线程 API，Payload 上限 64 MiB；不包含自动 JSON、压缩、加密、云存档、自动存档调度或槽位 UI。Debug 与 Release 使用相同权威格式。

已通过真实数据、版本、NotFound、备份恢复、健康备份保护、双重损坏、Codec 异常、删除和 100 次保存/读取验证。

#### Settings —— 用户设置

**状态：Windows 首版稳定基线完成；移动端真机验证后置。**

设置修改立即应用，调用 `Save()` 才通过 SaveService 的独立固定槽位写盘。SettingsService 通过显式构造依赖使用 AudioService、SaveService 和平台适配器，不在框架内部通过 Services 横向查找。

首版管理 Master / BGM / SFX 音量、Locale、窗口模式、分辨率和 VSync；不包含键位映射、画质预设、云同步或平台账户设置。不支持的平台能力返回 `Unsupported`。

已通过默认值、非法参数、能力降级、真实 SaveService 持久化、Windows 显示/Locale/音量实际应用和 100 次修改/保存/加载循环验证；本次 Debug 结果为 0 ms、当前线程累计分配 104840 bytes。

#### UI —— UI 管理
- UI 层级管理（弹窗、HUD、Loading 分层）
- UI 栈，支持 Back 操作
- 简单的数据绑定（MVVM 轻量版）

#### Config —— 配置表系统

**状态：首版稳定基线完成。**

- 具体游戏定义继承 Godot `Resource` 并实现 `IConfigResource` 的强类型配置资产
- ConfigHub 通过 ResourceHub 同步加载，并把内容校验失败包装为 `ConfigValidationException`
- ConfigTable 在构建时拒绝空项、空键和重复键，提供 `Get` / `TryGet` 只读查询
- 不接入 GoDoRuntime 或 Services，不建立第二套资源缓存
- CSV/JSON、目录扫描、热重载、代码生成和编辑器导入器不属于首版

已在 Godot 运行时通过有效/无效 Resource、缺失资源、正常/缺失键查询和重复键验证。

---

### 优先级 4（工具层，相对独立，随时可以插空做）

#### 扩展方法集
- `Vector2` / `Vector3` 扩展
- `Node` 扩展（类型安全的子节点查找等）
- `String` / `Math` 工具

#### 协程/异步封装
- Godot Signal 转 C# async/await
- 简化异步场景加载、延时执行

---

### 暂未排期

- StateMachine —— 通用状态机（行为管理，注意：框架只提供通用状态机机制，具体状态如"Idle/Walking/Jumping"属于业务层）
- TimerMgr —— 时间管理
- InputMgr —— 输入映射
- i18n —— 本地化系统(多语言)

---

## 文件目录规划

```
GoDo/
├── Core/
│   ├── EventChannel/        ← 已完成
│   │   ├── EventChannel.cs
│   │   ├── EventScope.cs
│   │   └── IGameEvent.cs
│   ├── ErrorHub/        ← 本地稳定基线完成
│   │   ├── ErrorHub.cs
│   │   ├── ErrorLevel.cs
│   │   └── ErrorReport.cs
│   ├── Log/
│   ├── Tick/
│   ├── GoDoRuntime.cs       ← 框架运行时与生命周期入口
│   ├── GoDoRuntime.tscn     ← Autoload 场景
│   └── Services/            ← 首版稳定基线完成
│
├── Runtime/
│   ├── Resources/           ← 首版稳定基线完成
│   │   ├── ResourceHub.cs
│   │   ├── ResourceKey.cs
│   │   ├── ResourceLoadOperation.cs
│   │   ├── ResourceLoadStatus.cs
│   │   └── ResourceLoadException.cs
│   ├── Pool/                ← 首版稳定基线完成
│   │   ├── NodePool.cs
│   │   └── IPoolable.cs
│   ├── Scene/               ← 首版稳定基线完成
│   ├── Audio/               ← 首版稳定基线完成
│   ├── Save/                ← 首版稳定基线完成
│   └── UI/
│
├── Tools/
│   ├── Config/
│   └── Extensions/
│
└── ARCHITECTURE.md   ← 本文件
```

**目录调整说明**：Log 和 Tick 从原来的 Tools 挪进了 Core，因为它们是框架地基级别的能力，跟 ErrorHub、EventChannel 是同一性质。Tools 现在只保留相对独立、随时可插空做的内容。

---

## 约定与规范

- 所有框架模块通过 `GoDo.ErrorHub` 报告错误，不直接调用 `GD.PrintErr`
- 事件定义统一在 `GameEvents.cs`，不散落各处
- Core 层模块之间禁止互相 Get，通信走 EventChannel（ErrorHub 除外，见上方架构约束）
- Services 只用于业务层按接口访问长期框架服务，不用于框架内部模块互访
- Debug 专属代码一律用 `#if GODOT_DEBUG` 包裹
- 公开 API 必须有 XML 注释
- 新模块开工前，先检查待补充设计决策是否已经想清楚——没想清楚的部分，先讨论再写代码，不要让 AI 自己拍板架构性决定
