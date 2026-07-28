# LogHub 使用指南

## 定位与边界

LogHub 用于输出仅供开发诊断的正常流程日志，例如流程进入、关键状态变化与资源命中。它不是玩家提示系统，也不记录异常、降级或操作失败；这些情况统一使用 ErrorHub。

## 上手

```csharp
LogHub.Debug("点击次数已更新", "Gameplay", context: "score=3");
LogHub.Info("进入主菜单流程", "Procedure");
```

控制台格式统一为：

```text
[模块] [等级] (可选上下文) 消息
```

## API 与构建行为

| API | 用途 |
|---|---|
| `Debug(message, module, context)` | 开发期细节诊断 |
| `Info(message, module, context)` | 开发期正常流程诊断 |

- API 只能在 Godot 主线程调用。
- `message` 与 `module` 不得为空或全空白，否则抛出 `ArgumentException`。
- 两个 API 都带 `Conditional("DEBUG")`；Release / ExportRelease 会在调用点移除，参数表达式不会求值。
- 当前仅写入 Godot 控制台与 Debugger 内存历史；不写文件、不上传远程端。
- Debug 构建会用预分配的 1000 条环形缓冲保留最近日志，写满后覆盖最早条目；连续且等级、模块、上下文、消息完全相同的日志聚合为一个条目并记录重复次数、首次时间与最后时间。Release 不保留日志历史。
- Godot 控制台每秒最多输出 100 条 LogHub 记录；重复日志只在第 1、2、4、8……次输出。被抑制的控制台文本仍完整计入 Debugger 内存历史，窗口轮换或退出时输出抑制数量摘要。
- Debugger 搜索与筛选会扫描全部 1000 条聚合历史，每页最多渲染 100 条普通日志。

## 与 ErrorHub 的分工

| 场景 | 使用 |
|---|---|
| 正常流程、开发诊断 | `LogHub.Debug` / `LogHub.Info` |
| 可恢复的异常或降级 | `ErrorHub.Warn` |
| 当前操作失败、异常对象 | `ErrorHub.Report` / `ErrorHub.Error` |
| 无法继续的严重错误 | `ErrorHub.Fatal` |

Release 默认不输出 LogHub；ErrorHub 仍按自身最低等级策略输出 Warning、Error 与 Fatal。

新代码不应使用 ErrorHub 记录正常流程；普通 Debug / Info 日志统一使用 LogHub。

## 自动回归验证

`Verification/Automated/LogHubRegression.tscn` 验证 Debug、Info 的统一格式、空消息和空模块拒绝、主线程控制台输出路径、连续重复聚合，以及环形历史的容量与淘汰顺序。

```powershell
& $env:GODOT_PATH --headless --path . Verification/Automated/LogHubRegression.tscn
```

## 验证状态

- 已通过 Debug 与 ExportRelease 的 `dotnet build`。
- 已在 Windows Godot Debug 运行时手动验证控制台输出、Debugger 最近日志展示，以及主场景切换后的持续可见性。
- 已在 Windows 当前项目声明的 Godot Mono Headless 版本完成 `LogHubRegression` 6/6 项验证；运行时需允许 Godot 写入 AppData 与 `user://` 目录。

## 常见误用

| 应该 | 避免 |
|---|---|
| 用 LogHub 记录正常开发诊断 | 用 LogHub 上报异常或失败 |
| 填写稳定模块名与必要上下文 | 在消息中手工拼接等级或模块前缀 |
| 在 Release 需要异常可见性时使用 ErrorHub | 依赖 LogHub 记录线上诊断 |
