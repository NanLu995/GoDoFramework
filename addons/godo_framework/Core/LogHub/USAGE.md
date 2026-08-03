# LogHub 使用指南

## 定位与边界

LogHub 是业务代码的统一日志调用入口：

- `Debug` / `Info` 记录仅供开发诊断的正常流程，例如流程进入、关键状态变化与资源命中。
- `Warn` / `Error` / `Fatal` 直接复用 ErrorHub 的结构化报告、Reporter 和后台队列，不建立第二套错误管线。

它不是玩家提示系统；玩家可见消息仍应由业务 UI 提供。

## 上手

```csharp
private static readonly LogChannel Log = LogHub.For("Gameplay");

Log.Debug("点击次数已更新", context: "score=3");
Log.Info("进入主菜单流程");
Log.Warn("配置缺失，已使用默认值", context: "key=audio.master");
```

`LogChannel` 是只保存模块字符串引用的只读值类型。推荐在类型中创建一个 `static readonly` 通道，避免每次调用重复填写模块名；创建通道和调用日志都不会为模块复制字符串。

一次性调用可以继续使用现有静态入口：

```csharp
LogHub.Info("进入主菜单流程", "Procedure");
LogHub.Error(exception, "Save", context: "slot=slot-1");
```

控制台格式统一为：

```text
[模块] [等级] (可选上下文) 消息
```

## 等级选择

| 等级 | 用途 | Release |
|---|---|---|
| `Debug` | 详细过程、缓存命中、排障细节 | 调用点移除 |
| `Info` | 启动完成、流程切换、场景提交等低频正常里程碑 | 调用点移除 |
| `Warning` | 当前操作可继续，但发生降级或使用备用结果 | 保留 |
| `Error` | 当前操作失败 | 保留 |
| `Fatal` | 无法安全继续的最高严重等级；不会自动退出 | 保留 |

Debug 与 Info 使用相同的输出端，但保留不同等级是为了在 Debugger 中把低频正常里程碑与详细排障信息分开筛选。不要用 Info 记录每帧或高频细节。

## Public API

| API | 用途 |
|---|---|
| `For(module)` | 创建绑定固定模块名的 `LogChannel` |
| `Debug(message, module, context)` | 开发期细节诊断 |
| `Info(message, module, context)` | 开发期低频正常流程里程碑 |
| `Warn(message, module, context)` | 委托 ErrorHub 上报 Warning |
| `Error(message/exception, module, context)` | 委托 ErrorHub 上报 Error |
| `Fatal(message/exception, module, context)` | 委托 ErrorHub 上报 Fatal |

- `For` 的 `module` 不得为空或全空白，否则抛出 `ArgumentException`；不要调用默认构造的 `LogChannel`。
- 消息与模块不得为空或全空白；异常重载的 `exception` 不得为 null。
- Debug / Info 只能在 Godot 主线程调用，并带 `Conditional("DEBUG")`；Release / ExportRelease 会在调用点移除，参数表达式不会求值。
- Warn / Error / Fatal 遵循 ErrorHub 线程语义：主线程立即分发，后台线程进入有界队列，并在 Release 中保留。
- Debug 构建同时写入 Godot 控制台、Debugger 内存历史和本地滚动文件；不上传远程端。
- Debug 构建会用预分配的 1000 条环形缓冲保留最近日志，写满后覆盖最早条目；连续且等级、模块、上下文、消息完全相同的日志聚合为一个条目并记录重复次数、首次时间与最后时间。Release 不保留日志历史。
- Godot 控制台每秒最多输出 100 条 LogHub 记录；重复日志只在第 1、2、4、8……次输出。被抑制的控制台文本仍完整计入 Debugger 内存历史，窗口轮换或退出时输出抑制数量摘要。
- Debugger 搜索与筛选会扫描全部 1000 条聚合历史，每页最多渲染 100 条普通日志。

## 与 ErrorHub 的分工

| 场景 | 使用 |
|---|---|
| 正常流程、开发诊断 | `LogHub.Debug` / `LogHub.Info` |
| 业务代码上报降级、失败或 Fatal | `LogHub.Warn` / `LogHub.Error` / `LogHub.Fatal` |
| 注册 Reporter、监听结构化报告、设置最低等级 | `ErrorHub` |

LogHub 的 Warning、Error 与 Fatal 只是便捷入口，产生的仍是同一份 `ErrorReport`。不要在 LogHub 上报后再次调用 ErrorHub，否则会形成重复报告。

## 滚动文件日志

- GoDoRuntime 优先写入 `user://logs/godo_framework.log`；该文件已被另一个进程占用时，自动回退到 `godo_framework.<进程号>.log`，不会停用文件日志。回退文件使用同一进程号命名自己的滚动历史。
- 单个文件达到 2 MiB 后滚动，最多保留 4 个历史文件；主文件使用 `godo_framework.1.log` 等名称，进程专属文件使用 `godo_framework.<进程号>.1.log` 等名称。
- Debug 构建记录 LogHub 的 Debug/Info 和 ErrorHub 的 Warning/Error/Fatal；Release 只记录 ErrorHub。
- 主线程只向最大 2048 条的有界队列非阻塞入队，实际目录创建、写入和轮转由单一后台线程完成。
- 后台线程在空闲后约 0.25 秒刷新；持续刷屏时最迟约 1 秒或累计 64 条刷新一次，使运行中的日志文件保持可读，同时避免每条日志都触发刷新。
- 队列满时丢弃并在主线程汇总 Warning；文件目录不可创建或磁盘写入失败时，本次运行停用文件日志并只提示一次。
- 退出时最多等待后台线程 2 秒刷新。单个日志文件只允许一个写入者，其他工具可以只读打开。
- 当前只完成 Windows 基线验证；其他平台需要确认 `user://` 全局路径、应用沙盒和退出刷新行为。

## 自动回归验证

`Verification/Automated/LogHubRegression.tscn` 验证 Debug、Info 的统一格式、模块绑定、Warning/Error/Fatal 委托、空参数拒绝、主线程控制台输出路径、连续重复聚合、环形历史、文件轮转、运行中刷新、退出刷新、状态快照、队列满和目录不可写降级。

```powershell
& $env:GODOT_PATH --headless --path . Verification/Automated/LogHubRegression.tscn
```

## 验证状态

- 已通过 Debug 与 ExportRelease 的 `dotnet build`。
- 已在 Windows Godot Debug 运行时手动验证控制台输出、Debugger 最近日志展示，以及主场景切换后的持续可见性。
- 已在 Windows 当前项目声明的 Godot Mono Headless 版本完成 `LogHubRegression` 12/12 项验证；运行时需允许 Godot 写入 AppData 与 `user://` 目录。

## 常见误用

| 应该 | 避免 |
|---|---|
| 用 Debug / Info 记录正常开发诊断 | 用 Debug / Info 记录异常或失败 |
| 用绑定稳定模块名的 LogChannel 减少重复参数 | 把所有日志都归入无意义的 General 模块 |
| 用 Warn / Error / Fatal 记录 Release 仍需可见的问题 | 依赖 Debug / Info 记录线上失败 |
| 一个失败只通过 LogHub 或 ErrorHub 上报一次 | 先 LogHub.Error 再 ErrorHub.Report |
