# SaveService 使用指南

## 定位与边界

SaveService 负责安全槽位、容器版本、完整性校验、临时文件、备份和恢复。具体游戏存档结构、字段含义及版本迁移由业务层 `ISaveCodec<T>` 负责。

框架不承诺任意 C# 对象自动序列化，避免隐藏 Godot 类型、AOT、字段改名和历史版本兼容问题。

## 首版契约

```csharp
public sealed class GameSaveCodec : ISaveCodec<GameSave>
{
    public byte[] Encode(GameSave value)
    {
        // 业务选择并维护唯一编码格式。
    }

    public GameSave Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        // 根据 dataVersion 解码并迁移。
    }
}

SaveSlot slot = SaveSlot.Create("slot_1");
ISaveService saves = Services.Get<ISaveService>();
saves.Save(slot, gameSave, dataVersion: 3, codec);

SaveLoadResult<GameSave> result = saves.Load(slot, codec);
if (result.HasValue)
    ApplySave(result.Value);
```

## 已确定语义

- 槽位只允许 ASCII 字母、数字、下划线和连字符，最长 64 字符。
- NotFound 是正常结果，不抛异常。
- 主文件损坏但备份可用时返回 `RecoveredFromBackup`。
- 保存、读取校验或 Codec 失败抛出带槽位和操作信息的 `SaveException`。
- 首版使用同步主线程 API，适合常规小型存档；不使用 Task.Run 包装同步文件操作。
- 首版不包含云存档、加密、压缩、自动存档调度、槽位 UI 或游戏设置。

## 文件容器与恢复

- 目录：`user://saves/`。
- 正式文件：`{slot}.gdsave`；备份：`.bak`；临时文件：`.tmp`。
- 容器记录魔数、容器版本、业务版本、UTC 保存时间、Payload 长度和 SHA-256。
- Payload 首版上限 64 MiB，文件长度和哈希必须完整匹配。
- 保存时先写并重新读取校验 `.tmp`，再把旧正式文件转为 `.bak`，最后提交新文件。
- 只有通过容器校验的旧正式文件才能提升为备份；损坏正式档不会覆盖健康 `.bak`。
- 提交中断后至少保留正式文件或备份；Load 会自动尝试备份，但不会静默改写正式文件。

## 生命周期与线程

- SaveService 是由 GoDoRuntime 创建并注册的纯 C# 长期服务，不需要 Node。
- 所有 API 只能在 Godot 主线程调用。
- 首版同步写入；Codec 应保持轻量，常规存档不应携带大型资源二进制。

## 验证基线

- 已验证槽位约束、NotFound、真实数据、业务版本、UTC 时间和 Codec 异常边界。
- 已验证正式档损坏回退、健康备份保护、正式档与备份双重损坏以及完整删除。
- 100 次小型 JSON Payload 保存/读取 Debug 验证为 594 ms、当前线程累计分配 439176 bytes。
- 性能数据包含 JSON Codec、SHA-256、文件读写、备份重命名和测试断言，不代表所有平台固定耗时。

## 后续可选扩展

- Debug 可由业务 Codec 额外输出可读 `.debug.json` 镜像；镜像只用于诊断，SaveService 的 Load 永远不读取它。
- Debug 与 Release 必须使用完全相同的正式容器与 Codec 路径，禁止按构建模式切换权威存档格式。
- 压缩未来通过容器 Flag 显式记录；建议仅在 Payload 超过 4 KiB 且压缩后确实更小时保存压缩结果。
- 压缩不是加密，也不提供防作弊或隐私保护；加密若有真实需求，应作为独立策略设计。
