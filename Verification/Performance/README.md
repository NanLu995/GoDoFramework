# 性能基准

性能场景记录特定机器与构建配置下的相对基线，不把耗时设为跨机器硬门槛。行为正确性和明确承诺的零分配热路径仍作为断言。

## Scheduler

`SchedulerBenchmark.tscn` 覆盖：

- 1,000 个等待任务下连续 10,000 次空闲 Process 推进，断言零托管分配；
- 10,000 次创建并立即取消，记录耗时和必要分配，并验证失效队列压缩；
- 1,000 个任务同轮到期，断言全部执行且派发阶段零托管分配。

Debug 运行：

```powershell
dotnet build GoDoFramework.sln
& <GodotConsole.exe> --headless --path <RepoRoot> res://Verification/Performance/SchedulerBenchmark.tscn
```

Release 数据必须使用不包含 `DEBUG` 条件符号的程序集运行同一场景，并在记录中注明具体方式。基准结束后应恢复普通 Debug 构建，避免 Godot 编辑器继续加载错误配置的程序集。

## 当前基线

记录日期：2026-07-16。环境：Godot 4.7 stable Mono、.NET 8、Windows、20 个逻辑处理器。每项为一次稳态样本，仅用于后续同机同配置回归，不代表跨机器性能保证。

| 构建 | 1,000 等待任务 / 10,000 次空闲推进 | 10,000 次创建取消 | 1,000 个任务同轮派发 |
|---|---:|---:|---:|
| Debug | 1.583 ms / 0 B | 3.080 ms / 1,125,304 B | 2.011 ms / 0 B |
| Release | 1.770 ms / 0 B | 2.719 ms / 1,125,304 B | 1.978 ms / 0 B |

两种构建的取消队列最终都保留 16 个失效项，低于 64 项压缩阈值。创建取消分配包含任务条目与优先队列项，是创建路径的预期成本；空闲推进和已有任务派发达到稳态零托管分配。

Release 样本通过 `dotnet build GoDoFramework.csproj -c Release` 构建，然后临时将 Release DLL 放到 Godot Headless 的 Debug 加载位置运行；完成后已重新执行默认 Debug 构建。该方法验证 Release IL/JIT 行为，但不是正式 ExportRelease 包体性能测试。
