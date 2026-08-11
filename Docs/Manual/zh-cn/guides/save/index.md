# Save：保存游戏进度

`SaveService` 负责本地存档槽位、编解码、损坏恢复与结果状态。它适合玩家进度、局内恢复点和可版本化的业务数据；设置项应使用 Settings，而不是混入游戏进度 Payload。

## 最小使用边界

为每类数据定义稳定 `SaveSlot`，由项目自己的 Codec 控制数据格式与迁移。加载时区分“没有存档”“已从备份恢复”和真正失败，不能把所有结果当作同一种异常。

## 关键规则

- 存档格式和版本迁移属于业务责任；框架不猜测角色、背包或关卡数据。
- 保存频率和 Payload 大小应由触发点控制，避免在高频路径同步写盘。
- SaveService 提供完整性与备份恢复，不把本地存档当作加密或防篡改边界。

多槽位、Codec、恢复和发布检查见[Save、Settings 与 Localization 工作流](../save-settings-localization/index.md)。
