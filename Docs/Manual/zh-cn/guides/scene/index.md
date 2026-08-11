# Scene：切换主内容场景

`SceneService` 只负责替换 `SceneTree.CurrentScene`。关卡、主菜单背景和主内容场景属于它；HUD、设置页和确认框属于 UI，不应通过切换主场景实现。

## 最小使用边界

在当前 Procedure 中获取 `ISceneService`，然后等待 `ChangeAsync(ResourceKey)` 完成。成功返回后，旧主场景已经进入释放流程，因此不要继续访问旧场景节点。

```csharp
ISceneService scenes = context.GetService<ISceneService>();
await scenes.ChangeAsync(GameScenes.Gameplay);
```

## 关键规则

- 同一时间只允许一个场景切换；由顶层流程串行协调，不在按钮或每帧回调中并发调用。
- 失败时不会提交半个新场景，调用方应处理 `SceneChangeException`。
- 需要显示进度或允许取消时使用带进度回调和取消标记的重载；一旦开始提交新场景，取消不会回滚提交。

资源清单、加载进度与场景切换的完整工作流见[资源与场景工作流](../resources-and-scenes/index.md)。
