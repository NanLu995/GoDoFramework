# 可删除的示例业务内容

此目录只演示 GoDo 的 Save Slot / Codec 边界，不包含任何玩法数据。`ExampleSaveData` 记录写入次数和 UTC 写入时间；`ExampleSaveStore` 使用固定的 `example-content` 槽位处理 `NotFound`、读取和写入。

要移除示例，请同时删除本目录，并从 `GameplayHud` 移除“Write Example Save”按钮及其 `ExampleSaveSelectedEvent` 发送；再从 `GameplayProcedure` 移除对应事件订阅和处理方法。实际游戏的存档数据、Codec 与槽位命名应放入自己的功能目录或 `Shared/`，不要继续沿用示例类型。
