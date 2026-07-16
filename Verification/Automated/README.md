# 自动回归验证

`run_all.py` 会先执行 `dotnet build`，再依次启动全部独立 Godot Headless 回归场景。每个场景使用独立进程和退出码，单项失败后仍继续执行剩余场景，最后返回整体结果。

`Verification/Package/verify_core_package.py` 则在系统临时目录创建干净的 Godot C# 项目，只复制 `addons/godo_framework/`，验证核心包不依赖 GUIDE、Phantom Camera 或任何 `godo_*` 适配包：

```powershell
python Verification/Package/verify_core_package.py --godot "E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe"
```

默认无论通过或失败都会清理临时项目；排查时添加 `--keep`，失败后会输出保留目录。

```powershell
python Verification/Automated/run_all.py --godot "E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe"
```

也可以设置环境变量后省略参数：

```powershell
$env:GODOT_PATH = "E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe"
python Verification/Automated/run_all.py
```

验证按 suite 分组：

- `--suite core`：只运行干净 Core 包验证，不需要任何可选插件。
- `--suite guide`：运行 GUIDE 集成验证，只要求 GUIDE / G.U.I.D.E-CSharp。
- `--suite phantom`：运行 Phantom 集成验证，只要求 Phantom Camera。
- `--suite demo`：运行 Demo3D 集成验证，需要 GUIDE / G.U.I.D.E-CSharp 与 Phantom Camera。
- `--suite all`：先验证 Core 包，再构建当前工作区并运行核心回归；已安装的可选依赖追加对应验证，缺少的套件会明确标记为跳过。

`GoDoFramework.csproj` 默认根据第三方插件目录是否存在决定是否编译可选适配包。可用下面的构建参数在 CI 或排查时强制验证缺失插件分支：

```powershell
dotnet build GoDoFramework.sln -p:GoDoIncludeGuideInput=false -p:GoDoIncludePhantomCamera=false
```

已经完成编译时可使用 `--skip-build` 跳过集成工作区构建。每个场景默认超时 60 秒，可通过 `--timeout` 调整。

SaveService runner 会在 `user://saves/` 创建随机 `godo-regression-*` 槽位，并在最外层 `finally` 清理；其他 runner 不写外部数据。InputService 与 InputRuntime runner 使用内存假后端；GuideInputBackend runner 使用仓库内 Fixture；Demo3DInputProfile runner 验证模板的真实 Profile、WASD、鼠标视角缩放、跳跃与 Result Context 隔离。输入回归不读取真实设备或写入改键配置。验证目录不进入框架发布 ZIP。
