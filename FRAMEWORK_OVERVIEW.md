# GoDoFramework —— Godot C# 游戏开发框架

GoDoFramework（简称 GoDo）是一个专门为 Godot 4.7 以上版本 C# 开发的游戏框架。

目标是解决 Godot C# 开发中反复出现的痛点，统一代码风格，提升开发效率和运行性能。
保证项目的鲁棒性，稳定性。
框架要稳定，要有可扩展性。
不是要替换 Godot，而是在引擎之上提供一套更好用的工具集。

**命名空间**：`GoDo`
**调用风格**：`GoDo.模块名.方法名(...)`

```csharp
// 典型调用示例
GoDo.EventChannel.Emit(new PlayerDiedEvent());
GoDo.Scene.Load<GameScene>();
GoDo.Pool.Get<BulletNode>();
GoDo.Audio.PlaySfx("explosion");
GoDo.Service.Get<ISaveService>().Save();
```

---

## 为什么要做这个框架

### Godot C# 开发的常见痛点

**1. 节点引用到处硬编码**
```csharp
// 路径一旦改变，全部崩掉
GetNode<ProgressBar>("../UI/Canvas/HUD/HPBar");
GetNode<AudioStreamPlayer>("/root/Game/AudioManager/SFX");
```

**2. 信号容易泄漏**
```csharp
// 连接了，忘记断开，节点销毁后还在触发
enemy.Connect("died", new Callable(this, nameof(OnEnemyDied)));
// _ExitTree 里经常忘了写对应的 Disconnect
```

**3. 频繁创建销毁对象**
```csharp
// 子弹、特效每次都 new，GC 压力大
var bullet = bulletScene.Instantiate<Bullet>();
AddChild(bullet);
// 用完
bullet.QueueFree();
```

**4. 跨场景通信没有规范**
```csharp
// 各种奇怪的全局访问方式，互相耦合
var gameManager = GetNode<GameManager>("/root/GameManager");
var player = GetTree().Root.GetNode<Player>("Game/Player");
```

**5. 错误日志散乱**
```csharp
// 每个脚本各自为政，格式不统一，Release 下无法收集
GD.PrintErr("something went wrong");
GD.Print("debug: " + someValue);
```

**痛点一：节点引用硬编码路径**
游戏项目一旦稍微复杂，场景结构会反复调整。节点改了名字、换了层级，所有引用它的路径字符串全部失效，而且编译不报错，只有运行时才崩。
```csharp
// 常见写法：路径写死在代码里
var hp = GetNode<ProgressBar>("UI/Canvas/HUD/PlayerStatus/HPBar");
var audio = GetNode<AudioStreamPlayer>("/root/Game/Managers/AudioManager/SFX");
var enemy = GetNode<Enemy>("../Enemies/Goblin_001");

// 问题：
// 1. 策划把 HPBar 移到 HUD/Left/HPBar，这行代码运行时 null
// 2. 路径字符串没有 IDE 提示，拼错了不知道
// 3. 同一个节点可能被十几个脚本引用，改一次要找遍整个项目
// 4. GetNode 失败只在运行时报错，调试成本极高
```

**痛点二：信号连接泄漏**
Godot 的 Signal 是很好的机制，但 C# 里手动管理连接断开非常容易出错。节点已经销毁了，信号还在触发，轻则逻辑错误，重则崩溃。
```csharp
public partial class EnemySpawner : Node
{
    public override void _Ready()
    {
        // 连接了敌人的死亡信号
        foreach (var enemy in GetChildren().Cast<Enemy>())
            enemy.Died += OnEnemyDied;

        // 连接了全局事件
        GameEvents.Instance.LevelCompleted += OnLevelCompleted;
        GameEvents.Instance.PlayerRespawned += OnPlayerRespawned;
        GameEvents.Instance.BossDefeated    += OnBossDefeated;
    }

    public override void _ExitTree()
    {
        // 问题：
        // 1. 忘了写这个方法，所有连接全部泄漏
        // 2. 写了但只断开了一部分，另一部分还在跑
        // 3. Enemy 列表是动态的，新加进来的 Enemy 没有被断开
        // 4. 某个 Enemy 已经 QueueFree 了，再 Disconnect 会报错
    }

    private void OnEnemyDied() { }
    private void OnLevelCompleted() { }
    private void OnPlayerRespawned() { }
    private void OnBossDefeated() { }
}
```

**痛点三：频繁创建销毁节点**
射击游戏里子弹、格斗游戏里特效、塔防里的怪物，都是高频创建销毁的对象。每次 Instantiate 都在堆上分配内存，每次 QueueFree 都触发 GC，帧率曲线会出现明显的周期性抖动。
```csharp
public partial class Player : CharacterBody2D
{
    [Export] private PackedScene _bulletScene;

    public override void _Process(double delta)
    {
        if (Input.IsActionPressed("shoot"))
        {
            // 每次开枪都在堆上分配一个新节点
            var bullet = _bulletScene.Instantiate<Bullet>();
            GetParent().AddChild(bullet);
            bullet.GlobalPosition = _muzzle.GlobalPosition;
            bullet.Direction = _aimDirection;

            // 子弹飞出去打到墙上
            // Bullet.cs 里：
            // QueueFree(); ← 每次都触发节点销毁流程和 GC
        }
    }
}

// 问题：
// 射速 10 发/秒，每秒 10 次 Instantiate + 10 次 QueueFree
// GC 每隔几秒就要回收一批，帧率从 60 掉到 45 再恢复
// 多个武器同时开火，抖动更明显
// 手机平台上这个问题更严重
```

**痛点四：跨场景通信没有规范**
游戏功能一多，各个系统之间需要互相通信。没有规范的情况下，每个人用自己的方式，最后项目里同时存在五六种通信方式，维护起来极其混乱。
```csharp
// 常见的混乱现象，同一个项目里同时存在：

// 方式一：直接找全局节点
var gameManager = GetNode<GameManager>("/root/GameManager");
gameManager.AddScore(100);

// 方式二：用 Autoload 单例
GameManager.Instance.AddScore(100);

// 方式三：找场景树里的节点
var ui = GetTree().Root.GetNode<GameUI>("Game/UI");
ui.UpdateScore(100);

// 方式四：自定义事件
GameManager.OnScoreChanged?.Invoke(100);

// 方式五：通过父节点中转
((Game)GetParent().GetParent()).UI.UpdateScore(100);

// 问题：
// 1. 新人完全不知道该用哪种
// 2. GameManager 节点换了位置，方式一和三全崩
// 3. 单例 Instance 如果初始化顺序不对，方式二报空引用
// 4. 场景切换后 UI 节点不存在了，方式三崩溃
// 5. 五种方式混用，排查 bug 要把每种都检查一遍
```

**痛点五：错误日志散乱无法追踪**
开发期日志满天飞，发布后什么都看不到。玩家反馈游戏崩了，开发者完全不知道发生了什么。
```csharp
// 项目里常见的日志现状

// 各种格式混用，看不出来是哪个模块报的
GD.Print("connected");
GD.Print("player died");
GD.PrintErr("null reference");
GD.Print("[DEBUG] hp = " + hp);
Console.WriteLine("loading scene...");  // 有人用这个
GD.PushError("something wrong");        // 有人用这个

// 异常被吞掉，完全不知道发生了什么
try {
    LoadConfig();
} catch {
    // 什么都没写，或者只写了
    GD.Print("load failed");
    // 调用栈丢失，不知道是哪行代码出的错
}

// Release 版本里所有 GD.Print 还在跑
// 但玩家看不到控制台，这些日志完全浪费
// 玩家遇到 bug 只能截图告诉你"游戏崩了"
// 你什么都复现不了
```

**痛点六：场景切换没有统一管理**
场景切换看起来简单，但加上加载进度、过渡动画、参数传递、错误处理之后，每个游戏都会各自写一套，而且写法都不太一样。
```csharp
// 简单切换：没有加载进度，画面直接跳
GetTree().ChangeSceneToFile("res://scenes/game.tscn");

// 加载进度版：每个需要进度条的地方各自实现一遍
var loader = ResourceLoader.LoadThreadedRequest("res://scenes/game.tscn");
while (ResourceLoader.LoadThreadedGetStatus("res://scenes/game.tscn") 
       == ResourceLoader.ThreadLoadStatus.InProgress)
{
    var progress = new Godot.Collections.Array();
    ResourceLoader.LoadThreadedGetStatus("res://scenes/game.tscn", progress);
    loadingBar.Value = (float)progress[0] * 100;
    await ToSignal(GetTree(), "process_frame");
}
var scene = ResourceLoader.LoadThreadedGet("res://scenes/game.tscn") as PackedScene;
GetTree().ChangeSceneToPacked(scene);

// 问题：
// 1. 字符串路径到处复制粘贴，改了场景文件名就全崩
// 2. 进度条逻辑每个项目自己写一遍，每次写法还不太一样
// 3. 没有过渡动画，画面硬切体验差
// 4. 传参数给下一个场景没有规范，有人用全局变量，有人用 Autoload
// 5. 加载失败没有统一的错误处理
```

**痛点七：音频管理散乱**
音效节点挂在各种奇怪的地方，音量控制没有统一入口，换个需求要改很多地方。
```csharp
// 子弹脚本里
[Export] private AudioStreamPlayer _hitSound;
public void OnHit() => _hitSound.Play();

// 玩家脚本里
[Export] private AudioStreamPlayer _jumpSound;
[Export] private AudioStreamPlayer _landSound;
[Export] private AudioStreamPlayer _runSound;
void Jump()  => _jumpSound.Play();
void Land()  => _landSound.Play();
void Run()   => _runSound.Play();

// 菜单脚本里
[Export] private AudioStreamPlayer _clickSound;
[Export] private AudioStreamPlayer _bgm;
void OnButtonClick() => _clickSound.Play();
void _Ready() => _bgm.Play();

// 问题：
// 1. 玩家想在设置里调音量，要找到所有 AudioStreamPlayer 逐一设置
// 2. 音效文件换了路径，要在所有用到的地方逐一更新 Export
// 3. 同一个音效同时触发多次（比如群体爆炸），只有一个在播
// 4. 场景切换后背景音乐重新开始，或者突然停掉
// 5. 某个节点销毁了，挂在上面的音效也跟着消失，声音戛然而止
```

**痛点八：存档没有规范**
每个项目自己做存档，格式不一，安全性不一，功能也残缺不全。
```csharp
// 常见的简陋存档实现
public void SaveGame()
{
    var file = FileAccess.Open("user://save.dat", FileAccess.ModeFlags.Write);
    file.StoreString(playerLevel.ToString());
    file.StoreString(playerHp.ToString());
    file.StoreString(gold.ToString());
    // 问题：字段顺序必须和读取完全一致，加一个字段旧存档全部失效
    file.Close();
}

public void LoadGame()
{
    var file = FileAccess.Open("user://save.dat", FileAccess.ModeFlags.Read);
    playerLevel = int.Parse(file.GetLine());
    playerHp    = int.Parse(file.GetLine());
    gold        = int.Parse(file.GetLine());
    // 没有版本号，没有错误处理，存档损坏直接崩溃
    file.Close();
}

// 问题：
// 1. 加了新字段，旧版本玩家的存档直接读取失败
// 2. 没有版本控制，无法做存档迁移
// 3. 存档是明文，玩家可以直接修改作弊
// 4. 只有一个存档槽，无法多存档
// 5. 存档损坏时没有回滚机制，玩家数据直接丢失
```
等等不止这些开发中的痛点

---

## 设计原则

**1. 零摩擦**：用框架比不用更轻松，调用比原生更简洁，不是更繁琐。

**2. 可逃脱**：框架是工具集，不是枷锁。任何时候都可以绕过框架直接用 Godot 原生 API，两者可以共存。

**3. 渐进使用**：不需要一次性全部引入。可以只用 EventChannel，也可以只用 Pool，模块之间松耦合。

**4. 性能优先**：框架不能成为性能瓶颈。struct 事件零 GC、对象池复用、条件编译消除 Debug 开销。

**5. 类型安全**：消灭字符串硬编码。场景、事件、服务全部用泛型类型标识，编译期发现错误。

**6. 待补充**


---

## 框架整体结构

```
GoDo/
├── Core/                        核心层（其他模块依赖）
│   ├── ErrorHandler/            错误捕获、格式化、上报
│   ├── EventChannel/            事件总线，解耦通信    ✅ 已完成
│   └── ServiceLocator/          全局服务注册与获取
│
├── Gameplay/                    游戏功能层
│   ├── Scene/                   场景管理，类型安全切换
│   ├── Pool/                    对象池，节点复用
│   ├── Audio/                   音频统一管理
│   ├── Save/                    存档系统
│   └── UI/                      UI 层级与栈管理
│
└── Tools/                       工具层
    ├── Log/                     日志系统
    ├── Tick/                    统一帧更新管理
    ├── Config/                  配置表读取
    └── Extensions/              扩展方法集
```

---

## 各模块详细说明

---

### ✅ Core / EventChannel —— 事件系统（已完成）

**解决的问题**：跨节点、跨场景通信耦合问题。发送方和接收方完全不需要互相认识。

**核心文件**
- `EventChannel.cs` —— 核心实现
- `EventScope.cs` —— 纯 C# 类的批量生命周期管理
- `IGameEvent.cs` —— 事件标记接口
- `GameEvents.cs` —— 全项目事件统一定义

**已实现特性**
- 泛型类型安全，struct 事件零 GC 分配
- `On` / `Off` / `Once` / `Emit` 基础 API
- `Bind`：绑定节点生命周期，节点销毁自动解绑，不会泄漏
- `EventScope`：纯 C# 类用，链式注册，`Dispose()` 一键清除
- 优先级排序，数字越小越先执行
- 派发安全：派发过程中注册/注销不会崩溃
- 异常隔离：单个 handler 出错不影响其他 handler
- Debug 工具：`DumpRegistry()` / `GetListenerCount<T>()`
- `#if GODOT_DEBUG` 条件编译，Release 零开销

**使用示例**
```csharp
// 定义事件
public struct PlayerDiedEvent : IGameEvent {
    public int PlayerId;
    public Vector2 Position;
}

// 监听（Node 里用 Bind，自动解绑）
GoDo.EventChannel.Bind<PlayerDiedEvent>(this, OnPlayerDied);

// 发送
GoDo.EventChannel.Emit(new PlayerDiedEvent { PlayerId = 1, Position = GlobalPosition });

// 纯 C# 类用 EventScope
private readonly EventScope _scope = new();
_scope.On<PlayerDiedEvent>(OnPlayerDied)
      .On<GameOverEvent>(OnGameOver);
// 销毁时
_scope.Dispose();
```

---

### 🔴 Core / ErrorHandler —— 错误捕获器

**解决的问题**：框架内部各模块错误日志散乱、格式不统一、Release 下无法收集，无法做远程上报。

**核心职责**
- 统一所有框架模块的错误出口，格式一致
- 捕获 Godot 未处理异常
- 错误分级：`Debug / Warning / Error / Fatal`
- 每条错误记录：模块名、上下文、完整调用栈、时间戳
- Debug 模式：详细输出到 Godot 控制台
- Release 模式：精简输出 + 触发上报
- 预留远程上报接口（Sentry / 自建服务器）

**计划 API**
```csharp
// 框架模块内部使用
GoDo.ErrorHandler.Report(exception, module: "EventChannel", context: "Dispatch");
GoDo.ErrorHandler.Warn("重复注册 handler", module: "EventChannel");
GoDo.ErrorHandler.Fatal("ServiceLocator 未初始化", module: "ServiceLocator");

// 挂载自定义上报器（游戏项目启动时配置一次）
GoDo.ErrorHandler.AddReporter(new RemoteReporter("https://errors.mygame.com/report"));

// 订阅错误事件（可选，用于游戏内显示错误提示）
GoDo.ErrorHandler.OnError += (report) => { ShowErrorToast(report.Message); };
```

**错误数据结构**
```csharp
public struct ErrorReport {
    public ErrorLevel Level;      // Debug / Warning / Error / Fatal
    public string     Module;     // 来源模块
    public string     Message;    // 错误描述
    public string     Context;    // 额外上下文
    public Exception  Exception;  // 原始异常（可为 null）
    public string     StackTrace;
    public DateTime   Timestamp;
}
```

**架构**
```
框架模块 / 业务代码
       ↓
ErrorHandler.Report()
       ↓
ErrorFormatter（格式化）
       ↓
ErrorDispatcher（分发）
   ↙          ↘
GodotLogger   RemoteReporter（可插拔）
```

---

### 🔴 Core / ServiceLocator —— 服务定位器

**解决的问题**：全局服务访问没有规范，GetNode 路径硬编码，模块间直接依赖难以测试。

**核心职责**
- 全局服务注册与获取
- 接口与实现分离，测试时可替换 Mock
- 框架各模块通过它互相访问，不直接依赖

**计划 API**
```csharp
// 注册（游戏启动时）
GoDo.Service.Register<IAudioService>(new AudioService());
GoDo.Service.Register<ISaveService>(new JsonSaveService());

// 获取
var audio = GoDo.Service.Get<IAudioService>();
audio.PlaySfx("jump");

// 注销
GoDo.Service.Unregister<IAudioService>();

// 检查是否注册
bool exists = GoDo.Service.Has<IAudioService>();
```

---

### 🟡 Gameplay / Scene —— 场景管理

**解决的问题**：场景切换用字符串路径，易错；异步加载没有统一封装；过渡动画各自实现。

**核心职责**
- 类型安全的场景切换，告别字符串路径
- 异步加载 + 进度回调
- 统一过渡动画（淡入淡出等）
- 场景栈（适合暂停菜单、弹窗类场景）

**计划 API**
```csharp
// 切换场景（异步，带过渡动画）
await GoDo.Scene.Load<GameScene>();
await GoDo.Scene.Load<GameScene>(transition: Transition.Fade);

// 场景栈
GoDo.Scene.Push<PauseMenuScene>();   // 叠加，不销毁当前场景
GoDo.Scene.Pop();                    // 返回上一个

// 进度回调
GoDo.Scene.Load<GameScene>(onProgress: p => loadingBar.Value = p);
```

---

### 🟡 Gameplay / Pool —— 对象池

**解决的问题**：子弹、特效、敌人等频繁 Instantiate/QueueFree，GC 压力大，帧率不稳。

**核心职责**
- 封装 PackedScene 池化
- 支持预热（游戏开始时提前实例化 N 个）
- 取出/归还时自动调用重置接口
- 池容量管理，超出上限时策略可配置

**计划 API**
```csharp
// 注册（启动时）
GoDo.Pool.Register<BulletNode>("res://scenes/Bullet.tscn", preload: 20, maxSize: 100);

// 使用
var bullet = GoDo.Pool.Get<BulletNode>();
bullet.GlobalPosition = muzzle.GlobalPosition;

// 归还（代替 QueueFree）
GoDo.Pool.Return(bullet);
```

**节点实现接口**
```csharp
// 节点脚本实现此接口，Pool 自动调用
public interface IPoolable {
    void OnGet();      // 从池取出时调用，做初始化
    void OnReturn();   // 归还时调用，做清理
}
```

---

### 🟡 Gameplay / Audio —— 音频管理

**解决的问题**：音效节点散落各处，音量无法统一控制，同类音效无法同时播放多个。

**核心职责**
- 统一播放接口
- 音量分组管理（Master / BGM / SFX / Voice）
- 音效池化（同一音效可同时播放多个实例）
- 淡入淡出切换背景音乐

**计划 API**
```csharp
GoDo.Audio.PlayBgm("main_theme", fadeIn: 1.0f);
GoDo.Audio.StopBgm(fadeOut: 0.5f);
GoDo.Audio.PlaySfx("explosion");
GoDo.Audio.PlaySfx("footstep", volume: 0.5f, pitch: 1.2f);
GoDo.Audio.SetVolume(AudioGroup.SFX, 0.8f);
GoDo.Audio.SetVolume(AudioGroup.Master, 0f);  // 静音
```

---

### 🟢 Gameplay / Save —— 存档系统

**解决的问题**：存档格式各游戏自己实现，序列化/反序列化重复造轮子。

**核心职责**
- 统一存档/读档接口
- 支持 JSON（可读）和二进制（加密）两种格式
- 多存档槽
- 自动序列化 C# 对象（标记 `[Saveable]`）

**计划 API**
```csharp
GoDo.Save.Save(slot: 0, data: new GameSaveData { Level = 5, Gold = 100 });
var data = GoDo.Save.Load<GameSaveData>(slot: 0);
GoDo.Save.Delete(slot: 0);
bool exists = GoDo.Save.Exists(slot: 0);
```

---

### 🟢 Gameplay / UI —— UI 管理

**解决的问题**：UI 层级混乱，弹窗管理没有栈，界面间数据传递耦合。

**核心职责**
- UI 层级分层（HUD / Popup / Loading / Toast）
- UI 栈，支持 Back/返回
- 打开/关闭界面带过渡动画
- 轻量数据绑定

**计划 API**
```csharp
GoDo.UI.Open<PauseMenuPanel>();
GoDo.UI.Open<ShopPanel>(data: new ShopData { Items = shopItems });
GoDo.UI.Close<PauseMenuPanel>();
GoDo.UI.Back();          // 关闭最顶层界面
GoDo.UI.CloseAll();
GoDo.UI.Toast("获得道具 x3", duration: 2f);
```

---

### 🟢 Tools / Tick —— 统一帧更新管理

**解决的问题**：`_Process` 分散在各节点脚本，执行顺序不可控，无法统一暂停。

**核心职责**
- 统一接管帧更新
- 支持执行优先级
- 分组暂停（比如只暂停游戏逻辑，不暂停 UI）

**计划 API**
```csharp
// 节点实现接口，由 TickManager 统一驱动
public interface IUpdatable {
    void OnUpdate(float delta);
}
public interface IFixedUpdatable {
    void OnFixedUpdate(float delta);
}

// 注册/注销
GoDo.Tick.Register(this, priority: 0);
GoDo.Tick.Unregister(this);

// 分组暂停
GoDo.Tick.PauseGroup(TickGroup.Gameplay);
GoDo.Tick.ResumeGroup(TickGroup.Gameplay);
```

---

### 🟢 Tools / Config —— 配置表系统

**解决的问题**：游戏数值配置（怪物属性、道具数据）散落在代码里，策划无法修改。

**核心职责**
- 读取 CSV / JSON 配置文件
- 自动映射到强类型 C# 对象
- 支持热重载（开发期修改配置即时生效）

**计划 API**
```csharp
// 定义配置结构
public class EnemyConfig {
    public int    Id;
    public string Name;
    public int    Hp;
    public float  Speed;
}

// 加载
GoDo.Config.Load<EnemyConfig>("res://configs/enemies.csv");

// 获取
var goblin = GoDo.Config.Get<EnemyConfig>(id: 1001);
var allEnemies = GoDo.Config.GetAll<EnemyConfig>();
```

---

### 🔵 Tools / Extensions —— 扩展方法集

常用扩展，减少重复代码：

```csharp
// Vector 扩展
Vector2 dir = position.DirectionTo(target);
float dist = position.DistanceTo(target);
Vector2 clamped = velocity.ClampLength(maxSpeed);

// Node 扩展
T child = node.GetChildOfType<T>();
bool found = node.TryGetChild<T>(out var result);
void node.DestroyChildren();

// 数字扩展
float mapped = value.Remap(0, 100, 0f, 1f);
bool inRange = value.Between(min, max);

// 异步扩展
await signal.ToTask();                  // Signal 转 Task
await GoDo.Async.Delay(1.5f);          // 等待 1.5 秒
await GoDo.Async.NextFrame();           // 等待下一帧
await GoDo.Async.NextPhysicsFrame();    // 等待下一物理帧
```

---

## 开发顺序

```
阶段一（核心基础）
  ✅ EventChannel     事件系统        已完成
  🔴 ErrorHandler     错误捕获器      下一个
  🔴 ServiceLocator   服务定位器

阶段二（游戏功能）
  🟡 Pool             对象池
  🟡 Scene            场景管理
  🟡 Audio            音频管理

阶段三（开发效率）
  🟢 Save             存档系统
  🟢 UI               界面管理
  🟢 Tick             帧更新管理
  🟢 Config           配置表

阶段四（工具完善）
  🔵 Log              日志系统
  🔵 Extensions       扩展方法集
  🔵 Async            异步封装
```

---

## 开发规范

**命名**
- 命名空间统一 `GoDo`
- 模块入口类用静态类，调用风格 `GoDo.模块.方法()`
- 接口以 `I` 开头：`IAudioService`、`IPoolable`、`ISaveService`

**错误处理**
- 所有模块错误统一走 `GoDo.ErrorHandler`，不直接调用 `GD.PrintErr`
- Debug 专属代码用 `#if GODOT_DEBUG` 包裹，Release 零开销

**性能**
- 事件/数据传递优先用 `struct`，避免堆分配
- 频繁调用的路径用 `for` 代替 `foreach`，缓存 `typeof(T)`
- 集合类预设合理初始容量

**API 设计**
- 每个公开方法必须有 XML 注释
- 参数非法时抛出 `ArgumentNullException`，不静默失败
- 支持链式调用的地方返回 `this`

---
