# GoDoFramework 开发计划

> Godot C# 游戏开发框架
> 命名空间：`GoDo`
> 调用风格：`GoDo.EventChannel.Emit(...)` / `GoDo.Scene.Load<T>()` / `GoDo.Pool.Get<T>()`

---

## 当前进度

### ✅ 已完成：EventChannel 事件系统

**文件**
- `EventChannel.cs` —— 核心实现
- `EventScope.cs`   —— 纯 C# 类的批量生命周期管理
- `IGameEvent.cs`   —— 事件标记接口
- `GameEvents.cs`   —— 游戏事件统一定义文件

**已实现的特性**
- 泛型类型安全事件，struct 零 GC 分配
- `On` / `Off` / `Once` / `Emit` 基础 API
- `Bind` 绑定 Node 生命周期，退出树自动解绑
- `EventScope` 批量管理，支持链式注册，`IDisposable` 一键清除
- 优先级排序（priority 越小越先执行）
- 派发安全：`_isDispatching` + 延迟列表，派发中途注册/注销不崩溃
- 异常隔离：单个 handler 崩溃不影响其他 handler，打印完整调用栈
- `try/finally` 保证 `_isDispatching` 一定复位
- `#if GODOT_DEBUG` 条件编译，Release 零开销
- Debug 工具：`DumpRegistry` / `GetListenerCount`
- 重复注册检测（Debug 模式，覆盖派发中途场景）
- `Bind` 节点不在树中时提前 return，防止监听泄漏
- `ClearAll` 限制为 internal，防止业务误用

**设计决策记录**
- 事件用 `struct` 不用 `class`：零堆分配，无 GC 压力
- `HandlerEntry` 用 `struct`：避免每次 On/Once 的堆分配
- `RemoveFromList` 反向遍历：零 lambda 分配，语义精确
- `typeof(T)` 缓存为局部变量：避免 Emit 中重复调用
- List 预设初始容量：`_handlers(4)` / 其余 `(2)`，避免首次扩容
- 所有延迟列表统一 `for` 索引循环，JIT 友好

---

## 开发计划

### 🔴 优先级 1（框架基础，其他模块依赖它）

#### 1.1 GoDo.ErrorHandler —— 框架级错误捕获器（建议下一个做）

**职责**
- 统一捕获框架内部所有异常，统一格式输出
- 捕获 Godot 未处理异常（`SceneTree` 异常钩子）
- 错误分级：Debug / Warning / Error / Fatal
- 错误上下文记录：发生在哪个模块、哪个事件、哪个节点
- Debug 模式：详细调用栈 + 模块信息
- Release 模式：精简日志，预留远程上报接口
- 后期扩展：错误数据序列化，网络上报到自建服务器或第三方（Sentry 等）

**计划 API**
```csharp
GoDo.ErrorHandler.Report(exception, context: "EventChannel.Dispatch");
GoDo.ErrorHandler.Warn("重复注册 handler", module: "EventChannel");
GoDo.ErrorHandler.OnError += (err) => { /* 上报逻辑 */ };
```

---

#### 1.2 GoDo.ServiceLocator —— 服务定位器

**职责**
- 全局服务注册与获取，替代到处 GetNode 的硬编码
- 接口与实现分离，方便测试时替换 Mock
- 框架自身各模块通过它互相访问

**计划 API**
```csharp
GoDo.Service.Register<IAudioService>(new AudioService());
GoDo.Service.Get<IAudioService>().PlaySfx("jump");
GoDo.Service.Unregister<IAudioService>();
```

---

### 🟡 优先级 2（核心游戏功能）

#### 2.1 GoDo.Scene —— 场景管理

**职责**
- 类型安全的场景切换，告别字符串路径
- 异步加载 + 进度回调
- 切换过渡动画（淡入淡出等）
- 场景栈管理（push/pop，适合 UI 弹窗场景）

**计划 API**
```csharp
await GoDo.Scene.Load<GameScene>();
GoDo.Scene.Push<PauseMenuScene>();
GoDo.Scene.Pop();
```

---

#### 2.2 GoDo.Pool —— 对象池

**职责**
- 封装 PackedScene 的池化，解决频繁 Instantiate/QueueFree 性能问题
- 支持预热（游戏开始时提前实例化）
- 节点取出/归还时自动重置状态

**计划 API**
```csharp
GoDo.Pool.Register<BulletNode>("res://scenes/Bullet.tscn", preload: 20);
var bullet = GoDo.Pool.Get<BulletNode>();
GoDo.Pool.Return(bullet);
```

---

#### 2.3 GoDo.Audio —— 音频管理

**职责**
- 统一音效/背景音乐的播放、暂停、停止
- 音量分组管理（Master / BGM / SFX）
- 音效池化，支持同时播放多个同类音效

**计划 API**
```csharp
GoDo.Audio.PlayBgm("main_theme");
GoDo.Audio.PlaySfx("explosion");
GoDo.Audio.SetVolume(AudioGroup.SFX, 0.8f);
```

---

### 🟢 优先级 3（开发效率）

#### 3.1 GoDo.Save —— 存档系统

- 统一存档格式（JSON / 二进制可选）
- 支持多存档槽
- 自动序列化 C# 对象

#### 3.2 GoDo.UI —— UI 管理

- UI 层级管理（弹窗、HUD、Loading 分层）
- UI 栈，支持 Back 操作
- 简单的数据绑定（MVVM 轻量版）

#### 3.3 GoDo.Tick —— 统一更新管理

- 接管 `_Process` / `_PhysicsProcess`
- 支持优先级、分组暂停
- 避免各节点各自为政

#### 3.4 GoDo.Config —— 配置表系统

- CSV / JSON 转强类型 C# 对象
- 编辑器工具：一键导入配置

---

### 🔵 优先级 4（工具层）

#### 4.1 GoDo.Log —— 日志系统

- 日志分级，Release 可关闭
- 与 ErrorHandler 联动

#### 4.2 扩展方法集

- `Vector2 / Vector3` 扩展
- `Node` 扩展（类型安全的子节点查找等）
- `String` / `Math` 工具

#### 4.3 协程/异步封装

- Godot Signal 转 C# async/await
- 简化异步场景加载、延时执行

---

### 🔵 其他可能要做
状态机 - StateMachine - 行为管理
资源管理 - ResourceMgr - 统一加载
时间管理 - TimerMgr
等等

## 文件目录规划

```
GoDo/
├── Core/
│   ├── ErrorHandler/        ← 下一个
│   │   ├── ErrorHandler.cs
│   │   ├── ErrorLevel.cs
│   │   └── ErrorReport.cs
│   ├── EventChannel/        ← 已完成
│   │   ├── EventChannel.cs
│   │   ├── EventScope.cs
│   │   ├── IGameEvent.cs
│   │   └── GameEvents.cs
│   └── ServiceLocator/      ← 紧随其后
│       └── ServiceLocator.cs
│
├── Gameplay/
│   ├── Scene/
│   ├── Pool/
│   ├── Audio/
│   ├── Save/
│   └── UI/
│
├── Tools/
│   ├── Log/
│   ├── Tick/
│   ├── Config/
│   └── Extensions/
│
└── GoDo_Framework_Plan.md   ← 本文件
```

---

## 错误捕获器详细设计（下一步）

### 为什么要做

框架内部现在每个模块各自 `GD.PrintErr`，格式不统一，Release 下可能丢失关键信息，
且无法做错误聚合、过滤、上报。需要一个统一的错误出口。

### 分层设计

```
业务代码 / 框架模块
        ↓ 发生异常
  ErrorHandler.Report()
        ↓
  ErrorFormatter     —— 格式化：模块名 + 上下文 + 调用栈
        ↓
  ErrorDispatcher    —— 分发给所有注册的处理器
    ↙         ↘
GodotLogger    RemoteReporter（预留）
(GD.PrintErr)  (HTTP 上报 / Sentry / 自建服务器)
```

### 错误数据结构

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

### 网络上报预留接口

```csharp
// 框架使用者可以挂载自己的上报逻辑
GoDo.ErrorHandler.AddReporter(new SentryReporter());
GoDo.ErrorHandler.AddReporter(new MyServerReporter("https://errors.mygame.com"));
```

---

## 约定与规范

- 所有框架模块通过 `GoDo.ErrorHandler` 报告错误，不直接调用 `GD.PrintErr`
- 事件定义统一在 `GameEvents.cs`，不散落各处
- 框架服务全部注册到 `GoDo.Service`，不用 `GetNode` 全局访问
- Debug 专属代码一律用 `#if GODOT_DEBUG` 包裹
- 公开 API 必须有 XML 注释
