# Add Save Data Field

本文档说明如何给业务存档新增字段。

## 步骤

1. 修改存档数据类型。

```csharp
public sealed class GameSaveData
{
    public int BestScore { get; set; }
    public int GamesPlayed { get; set; }
    public int Coins { get; set; }
}
```

2. 修改 Save Codec 的读写逻辑。

新增字段必须考虑旧存档缺字段的情况。旧字段读取失败时应给出明确默认值，而不是让存档整体不可用。

示例原则：

```csharp
data.Coins = container.TryGetInt("coins", out int coins) ? coins : 0;
```

具体 API 以当前项目的 `StarterSaveCodec` 或业务 Codec 为准，不确定时先读源码。

3. 更新写入版本号。

如果 Codec 使用版本常量，新增字段后提高数据版本：

```csharp
public const int CurrentDataVersion = 2;
```

4. 修改使用点。

只在需要展示、累计、消费或奖励该字段的业务流程里更新，不要在框架模块中引入玩法字段。

## 验证

- `dotnet build GoDoFramework.sln`
- 用没有新字段的旧存档验证可以读取。
- 新增字段写入后重启游戏验证仍可读取。
- 删除或损坏存档时，业务界面能走默认值或明确错误路径。

## 常见错误

- 只改 `SaveData`，忘记改 Codec。
- 新字段没有默认值，旧存档读取失败。
- 把业务存档字段放进 GoDo 框架命名空间。
- 存档失败后静默吞掉异常，导致用户以为保存成功。

