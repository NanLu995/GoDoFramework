# 自动回归验证

`run_all.py` 会先执行 `dotnet build`，再依次启动 8 个独立 Godot Headless 场景。每个场景使用独立进程和退出码，单项失败后仍继续执行剩余场景，最后返回整体结果。

```powershell
python Verification/Automated/run_all.py --godot "E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe"
```

也可以设置环境变量后省略参数：

```powershell
$env:GODOT_PATH = "E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe"
python Verification/Automated/run_all.py
```

已经完成编译时可使用 `--skip-build`。每个场景默认超时 60 秒，可通过 `--timeout` 调整。

SaveService runner 会在 `user://saves/` 创建随机 `godo-regression-*` 槽位，并在最外层 `finally` 清理；其他 runner 不写外部数据。验证目录不进入框架发布 ZIP。
