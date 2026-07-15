# G.U.I.D.E-CSharp InputLab

这是 G.U.I.D.E-CSharp `0.3.7` / G.U.I.D.E `0.13.0` 作为 GoDo 输入后端候选的隔离验证，不是正式 InputService，也不进入永久自动回归 runner。

运行：

```text
res://Verification/Exploratory/InputLab/GuideInputLab.tscn
```

验证范围：

- Gameplay/UI 映射上下文互斥。
- 鼠标相对位移与虚拟手柄右摇杆统一输出 Axis2D。
- 手柄摇杆死区 modifier。
- 运行时重绑定冲突查询。
- 重绑定配置保存、重新加载与绑定恢复。
- C# 高频读取的线程分配观测。

测试会临时写入 `user://godo-guide-input-lab-remapping.tres`，并在退出前清理。
