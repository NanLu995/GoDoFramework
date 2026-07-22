# GoDoFramework —— Godot C# 游戏开发框架

> 本文件只讲"为什么做这个框架"和"设计原则"，即历史愿景与痛点。
> 模块状态与路线见 `AI/FRAMEWORK_DESIGN_PLAN.md`；当前架构与依赖见 `AI/ARCHITECTURE.md`；
> 各模块 API、失败语义、生命周期与性能细节见对应模块的 `USAGE.md`。
> 当前定位已升级为面向中大型、长期维护项目的工业级 Godot C# 框架；目标、质量标准和实施路线以 `AI/FRAMEWORK_DESIGN_PLAN.md` 为准。

GoDoFramework（简称 GoDo）是为 Godot 4.7.1 以上版本 C# 开发的游戏框架，目标是解决 Godot C# 开发中反复出现的痛点，统一代码风格，提升开发效率和运行性能，同时保证项目的鲁棒性与稳定性。

不是要替换 Godot，而是在引擎之上提供一套更好用的工具集。

**命名空间**：`GoDo`
**调用风格**：`GoDo.模块名.方法名(...)`

```csharp
GoDo.EventChannel.Emit(new PlayerDiedEvent());
GoDo.Scene.Load<GameScene>();
GoDo.Pool.Get<BulletNode>();
GoDo.Audio.PlaySfx("explosion");
GoDo.Service.Get<ISaveService>().Save();
```

---

## 为什么要做这个框架

Godot C# 项目在规模变大后，会反复遇到以下痛点：

**1. 节点引用硬编码路径**
场景结构一旦调整，写死的路径字符串全部失效，且编译不报错，只在运行时崩溃。
```csharp
GetNode<ProgressBar>("UI/Canvas/HUD/PlayerStatus/HPBar");
```

**2. 信号连接泄漏**
C# 里手动管理信号连接/断开极易出错，节点已销毁但信号仍在触发，轻则逻辑错误重则崩溃；动态列表、多个信号源的情况尤其容易漏掉。

**3. 频繁创建销毁节点**
子弹、特效、怪物等高频对象每次 `Instantiate` / `QueueFree` 都会造成 GC 压力，帧率出现周期性抖动，移动端更明显。

**4. 跨场景通信没有规范**
项目里同时存在"找全局节点""Autoload 单例""事件回调""父节点层层转发"等五六种通信方式，新人不知道该用哪种，排查 bug 要挨个方式检查。

**5. 错误日志散乱**
`GD.Print` / `GD.PrintErr` / `Console.WriteLine` 混用，格式不统一；异常被 `catch` 吞掉、调用栈丢失；Release 版本玩家看不到日志，问题无法复现。

**6. 场景切换没有统一管理**
简单切换没有加载进度，画面硬切；加了进度条后每个项目各写一套，过渡动画、传参方式、失败处理都没有规范。

**7. 音频管理散乱**
音效节点挂在各处，音量控制没有统一入口，同一音效无法同时播放多份，场景切换后 BGM 状态处理混乱。

**8. 存档没有规范**
自制存档格式不带版本号，加字段旧存档就报废；无加密、无多存档槽、无失败回滚。

等等不止这些开发中的痛点。

---

## 设计原则

1. **零摩擦**：用框架比不用更轻松，调用比原生更简洁，不是更繁琐。
2. **可逃脱**：框架是工具集，不是枷锁。任何时候都可以绕过框架直接用 Godot 原生 API，两者可以共存。
3. **渐进使用**：不需要一次性全部引入，可以只用 EventChannel，也可以只用 Pool，模块之间松耦合。
4. **性能优先**：框架不能成为性能瓶颈。struct 事件零 GC、对象池复用、条件编译消除 Debug 开销。
5. **类型安全**：消灭字符串硬编码。场景、事件、服务全部用泛型类型标识，编译期发现错误。

---

## 模块概览

按依赖层级分为 Core（核心层）、Gameplay（游戏功能层）、Tools（工具层），完整目录结构与模块间依赖关系见 `AI/ARCHITECTURE.md`。

| 模块 | 定位 |
|---|---|
| Core/ErrorHub | 统一错误捕获、格式化、上报 |
| Core/EventChannel | 事件总线，解耦跨节点/跨场景通信 |
| Core/ServiceLocator | 全局服务注册与获取 |
| Gameplay/Scene | 类型安全的场景切换与加载 |
| Gameplay/Pool | 对象池，节点复用 |
| Gameplay/Audio | 音频统一管理 |
| Gameplay/Save | 存档系统 |
| Gameplay/UI | UI 层级与栈管理 |
| Tools/Log | 日志系统 |
| Tools/Tick | 统一帧更新管理 |
| Runtime/Config | 配置资源校验与只读配置表 |
| Tools/Extensions | 常用扩展方法集 |

各模块当前完成状态、API 签名、使用示例见 `AI/FRAMEWORK_DESIGN_PLAN.md`（状态与路线）和对应模块的 `USAGE.md`（API 细节），此处不重复维护，避免和实际实现脱节。
