# 自动回归验证

`run_all.py` 会先执行 `dotnet build`，再依次启动全部独立 Godot Headless 回归场景。每个场景使用独立进程和退出码，单项失败后仍继续执行剩余场景，最后返回整体结果。

`Verification/Package/verify_core_package.py` 则在系统临时目录创建干净的 Godot C# 项目，只复制 `addons/godo_framework/`，验证核心包不依赖 GUIDE、Phantom Camera 或任何 `godo_*` 适配包：

```powershell
python Verification/Package/verify_core_package.py --godot $env:GODOT_PATH
```

默认无论通过或失败都会清理临时项目；排查时添加 `--keep`，失败后会输出保留目录。

```powershell
python Verification/Automated/run_all.py --godot $env:GODOT_PATH
```

也可以设置环境变量后省略参数：

```powershell
$env:GODOT_PATH = "<Godot Mono Console 可执行文件>"
python Verification/Automated/run_all.py
```

引擎升级后先检查 csproj、CI、编辑器最低版本与文档是否一致：

```powershell
python Tools/update_godot_version.py --check
```

验证按 suite 分组：

- `--suite core`：只运行干净 Core 包验证，不需要任何可选插件。
- `--suite guide`：运行 GUIDE 集成验证，只要求 GUIDE / G.U.I.D.E-CSharp。
- `--suite phantom`：运行 Phantom 集成验证，只要求 Phantom Camera。
- `--suite demo`：运行 Demo3D 集成验证，需要 GUIDE / G.U.I.D.E-CSharp 与 Phantom Camera。
- `--suite all`：先验证 Core 包，再构建当前工作区并运行核心回归；已安装的可选依赖追加对应验证，缺少的套件会明确标记为跳过。

`GoDoFramework.csproj` 默认根据第三方插件目录是否存在决定是否编译可选适配包。可用下面的构建参数在 CI 或排查时强制验证缺失插件分支：

```powershell
dotnet build GoDoFramework.csproj -c CoreVerification -p:GoDoIncludeGuideInput=false -p:GoDoIncludePhantomCamera=false
```

必须使用独立的 `CoreVerification` 配置并直接构建 `.csproj`。不要用上述强制禁用参数构建默认 Debug：它会覆盖 Godot 编辑器当前加载的 Debug 程序集，导致已安装的 GUIDE / Phantom C# 插件脚本暂时无法实例化，直到重新执行默认 Debug 构建。

GitHub Actions 的 `Core Verification` 工作流从 `GoDoFramework.csproj` 读取 Godot Mono 版本，先以 `CoreVerification` 配置强制关闭可选适配，再运行 `--suite core`。相关框架、验证或工程文件推送到 `master` 时会自动触发，也可以在 Actions 页面手动运行；它只验证可分发核心包的编译与 9 项长期服务启动，不把本地工作台的 GUIDE、Phantom Camera 或 Demo 配置视为 CI 前置条件。

`SchedulerCoreRegression` 使用人工时间推进验证三种时钟、Process/Physics、重复/取消/暂停、异常隔离、DelayAsync、跨线程 Token 取消与 Shutdown，并通过真实 SceneTree 验证 Owner 入树约束、绑定清理和退出树自动取消；Debug 构建另验证只读快照。该场景不进行真实等待，也不依赖 Scheduler 已接入 GoDoRuntime。

`SchedulerRuntimeRegression` 使用 Autoload 注册的 `ISchedulerService` 验证真实 Process/Physics 采样、TimeScale、SceneTree 暂停、Owner 退出与服务退出清理。该场景包含短暂真实等待，外层 runner 超时仍是最终卡死保护。

已经完成编译时可使用 `--skip-build` 跳过集成工作区构建。每个场景默认超时 60 秒，可通过 `--timeout` 调整。

SaveService runner 会在 `user://saves/` 创建随机 `godo-regression-*` 槽位，并在最外层 `finally` 清理；其他 runner 不写外部数据。InputService 与 InputRuntime runner 使用内存假后端；GuideInputBackend runner 使用仓库内 Fixture；Demo3DInputProfile runner 验证模板的真实 Profile、WASD、鼠标视角缩放、跳跃与 Result Context 隔离。输入回归不读取真实设备或写入改键配置。验证目录不进入框架发布 ZIP。
