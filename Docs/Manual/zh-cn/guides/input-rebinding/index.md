# 制作运行时改键界面

InputService 的重绑定能力让设置页面查询当前按键、捕获新输入、显示冲突并保存结果，同时让业务代码继续使用稳定 Action 和 Binding ID。当前 GUIDE 后端实现了改键、文本提示和 SaveService 持久化。

## 1. 在 Profile 公开可重绑定槽位

为每个允许玩家修改的 GUIDE Mapping 条目创建 `GuideInputBindingDefinition`：

```text
BindingId   = gameplay.jump.primary
ContextId   = gameplay
ActionId    = gameplay.jump
MappingIndex = 0
```

Binding ID 是游戏协议，不能来自显示文字、键名或数组位置。一个 GUIDE 槽位只能对应一个 Binding ID；目标必须存在且被 GUIDE 标记为可重绑定。

```csharp
public static readonly InputBindingId JumpPrimary =
    InputBindingId.Create("gameplay.jump.primary");
```

## 2. 检查后端能力

设置页面打开时：

```csharp
IInputService input = Services.Get<IInputService>();
if (!input.TryGetRebinding(out IInputRebinding? rebinding))
{
    rebindingPanel.Visible = false;
    return;
}
```

重绑定、提示查询和持久化都是可选能力。不要仅根据平台或插件名称猜测，始终用 `TryGet...` 检测。

列出一个 Context 的公开槽位：

```csharp
IReadOnlyList<InputBindingInfo> bindings =
    rebinding.GetBindings(GameInput.Gameplay);
```

`InputBindingInfo` 提供稳定 Binding、Action、设备类别、当前与默认显示文字。设置 UI 可以按 Profile 顺序生成列表。

## 3. 捕获玩家输入

```csharp
private async Task CaptureAsync(InputBindingId binding)
{
    _captureOverlay.Visible = true;
    try
    {
        InputBindingCandidate? candidate =
            await _rebinding.CaptureAsync(binding);

        if (candidate == null)
            return;

        await ResolveAndApplyAsync(binding, candidate);
    }
    catch (InputOperationException exception)
    {
        ErrorHub.Report(exception, "Game.Input", binding.Value);
        ShowCaptureFailed();
    }
    finally
    {
        _captureOverlay.Visible = false;
    }
}
```

GUIDE 捕获会忽略开始后的 0.2 秒，轴幅度至少 0.5，Esc 表示取消。主动关闭设置页时调用 `CancelCapture()`；任务返回 null。

同一后端只能有一个捕获任务。捕获期间禁用其他改键按钮，避免重复请求抛出 `InputOperationException`。

## 4. 让玩家决定冲突

```csharp
IReadOnlyList<InputBindingInfo> conflicts =
    _rebinding.FindConflicts(binding, candidate);

if (conflicts.Count == 0)
{
    _rebinding.Apply(binding, candidate);
    return;
}

ShowConflictDialog(binding, candidate, conflicts);
```

`FindConflicts` 只报告事实，`Apply` 不会自动解绑或覆盖其他槽位。推荐提供“取消”与“返回重新输入”；只有游戏规则明确支持交换或清除时，才由业务层设计对应操作。

Candidate 只能交回创建它的同一后端，不持久化、不跨会话保存，也不尝试读取内部 GUIDE 对象。

## 5. 应用与恢复默认

```csharp
_rebinding.Apply(binding, candidate);
RefreshBindingRows();
```

恢复单项默认：

```csharp
_rebinding.RestoreDefault(GameBindings.JumpPrimary);
```

应用和恢复会低频重建 GUIDE Context。不要在 `_Process()`、滑块更新或连续循环中调用。

操作成功后发布 `InputBindingsChangedEvent`；失败会回滚旧绑定且不发布事件。提示 UI 可以同时监听它和 `InputDeviceChangedEvent`，按需刷新，不要每帧查询。

## 6. 保存和加载

Installer 配置有效 `PersistenceSlot` 后：

```csharp
if (input.TryGetRebindingPersistence(
        out IInputRebindingPersistence? persistence))
{
    InputBindingLoadStatus status = persistence.LoadAndApply();
}
```

这一步放在 Boot 的 Installer 完成后、第一个 Procedure 启动前。没有存档时应用默认绑定；正式文件损坏时 SaveService 尝试备份。

设置页让玩家点击“应用”或“确定”时保存：

```csharp
try
{
    _persistence?.Save();
}
catch (SaveException exception)
{
    ErrorHub.Report(exception, "Game.Input", "Save bindings");
    ShowBindingsNotSaved();
}
```

Apply 只改变当前运行状态，不自动写盘。Save 失败也不会撤销本次会话已应用的绑定，应明确提示“当前有效，但重启后可能恢复旧值”。

不同本地玩家档案需要不同 PersistenceSlot；默认 `godo-input-bindings` 适合单个本地玩家。

## 7. 显示当前设备提示

```csharp
if (input.TryGetPromptQuery(out IInputPromptQuery? prompts))
{
    IReadOnlyList<InputPromptInfo> infos = prompts.GetPrompts(
        GameInput.Gameplay,
        GameInput.Jump,
        input.ActiveDevice);
}
```

GUIDE 后端返回稳定顺序的回退文字。键帽图标、手柄品牌、本地化和布局由游戏 UI 负责。解绑槽位可能返回空文本，应显示“未绑定”等本地化状态。

## 常见错误

- 设置页没有改键入口：后端未声明 Rebinding，或 Profile 没有 Bindings。
- 捕获刚开始就得到鼠标输入：不要自行绕过 GUIDE 捕获器的启动延迟。
- 第二次捕获失败：上一项仍在进行；禁用按钮或先 CancelCapture。
- Apply 报候选无效：Candidate 来自旧后端或另一实例，应重新捕获。
- 冲突后两个动作仍同键：Apply 不自动解决冲突，业务 UI 必须先让玩家决策。
- 改键有效但重启丢失：没有调用 persistence.Save，或保存失败未提示。
- 提示没有刷新：没有监听 InputBindingsChangedEvent / InputDeviceChangedEvent。
- 加载损坏配置：处理 RecoveredFromBackup 或 SaveException，不要删除健康备份。

精确接口可查询 <xref:GoDo.IInputRebinding>、<xref:GoDo.IInputRebindingPersistence>、<xref:GoDo.IInputPromptQuery>、<xref:GoDo.InputBindingInfo> 和 <xref:GoDo.InputBindingCandidate>。
