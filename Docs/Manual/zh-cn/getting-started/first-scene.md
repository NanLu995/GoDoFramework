# 切换第一个主内容场景

本教程接着“创建第一个游戏流程”，让 `WelcomeProcedure` 使用 SceneService 加载并切换到一个新的主内容场景。运行后，原来的 `Boot` 场景会被替换，画面中央显示“主内容场景已加载”。

SceneService 只管理 `SceneTree.CurrentScene`。菜单弹窗、HUD 和暂停界面属于 UI，不应该通过主场景切换实现。

## 完成后的文件

在上一教程的文件基础上增加 `Main` 目录：

```text
res://
├─ Boot.cs
├─ Boot.tscn
├─ WelcomeProcedure.cs
└─ Main/
   └─ MainScene.tscn
```

## 1. 创建目标场景

在 Godot 中创建以下节点树：

```text
MainScene (Control)
└─ Message (Label)
```

将 `MainScene` 的布局设为铺满矩形，把 `Message` 放在画面中央，并将文字设为：

```text
主内容场景已加载
```

保存为 `res://Main/MainScene.tscn`。不要把它设为项目主场景，项目仍然从 `Boot.tscn` 启动。

## 2. 在 Procedure 中切换场景

将 `WelcomeProcedure.cs` 替换为：

```csharp
using System.Threading.Tasks;
using GoDo;

public sealed class WelcomeProcedure : IProcedure
{
    private static readonly ResourceKey MainSceneKey =
        ResourceKey.FromPath("res://Main/MainScene.tscn");

    public string Name => "Welcome";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        await scenes.ChangeAsync(MainSceneKey);
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
}
```

`ResourceKey` 会先检查定位串是否为规范的 `res://` 或 `uid://` 资源地址。SceneService 随后异步加载 `PackedScene`，成功实例化并挂到场景树后，才把它设为新的 CurrentScene。

教程直接使用路径，方便看清完整过程。项目扩大后，可以再使用 ResourceManifest 为资源定义稳定的业务 ID。

## 3. 修改启动代码

上一教程会在进入 Procedure 后更新 `Boot` 中的标签。现在 Procedure 会替换整个主场景，因此切换成功后，旧 `Boot` 已经排队等待释放，不能继续访问它的节点。

将 `Boot.cs` 替换为：

```csharp
using System;
using Godot;
using GoDo;

public partial class Boot : Control
{
    public override async void _Ready()
    {
        try
        {
            IProcedureService procedures = Services.Get<IProcedureService>();
            await procedures.ChangeAsync<WelcomeProcedure>();

            // 切换成功后 Boot 已进入释放流程，不要在这里访问自身或子节点。
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "GameBoot", nameof(_Ready));
        }
    }
}
```

`Boot.tscn` 中原有的 `StatusLabel` 可以删除，也可以保留为加载提示；无论哪种方式，都不要在 `await` 成功返回后再修改它。

## 4. 运行并确认结果

运行项目后确认：

- 画面中央显示“主内容场景已加载”。
- Remote 场景树的 CurrentScene 是 `MainScene`，旧 `Boot` 已被释放。
- `GoDoRuntime` 仍作为唯一 Autoload 存在，没有随主场景一起释放。

还可以故意把代码中的路径改为 `res://Main/Missing.tscn` 再运行一次。此时应该看到：

- Godot 输出面板出现由 ErrorHub 上报的启动错误。
- 目标场景没有提交，旧 `Boot` 场景保持不变。

验证完后将路径恢复为 `res://Main/MainScene.tscn`。

## 常见问题

### 报告找不到资源

确认文件名大小写和目录与 `ResourceKey` 完全一致。导出到区分大小写的平台后，`MainScene.tscn` 与 `mainscene.tscn` 不是同一个路径。

### 提示已有场景切换正在执行

同一时间只能执行一次 `ChangeAsync`。不要在 `_Process` 中调用，也不要让多个按钮同时直接发起切换。顶层切换由当前 Procedure 串行组织。

### 切换后出现已释放对象错误

检查是否在 `await scenes.ChangeAsync(...)` 返回后继续访问旧场景节点。需要操作新场景时，使用 `ChangeAsync` 返回的 `Node`，或让新场景在自己的 `_Ready()` 中完成初始化。

精确接口可查询 <xref:GoDo.ISceneService>、<xref:GoDo.ResourceKey> 和 <xref:GoDo.SceneChangeException>。

下一步可以把主菜单做成 UI，由 Procedure 协调主场景与界面，而不是继续向 `Boot.cs` 添加业务逻辑。
