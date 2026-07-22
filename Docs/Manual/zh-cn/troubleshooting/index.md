# 故障排查

本页按运行时现象组织。先找到最接近的问题，再依次确认原因；不要一开始就修改框架源码或吞掉异常。

## Setup 无法安装 Runtime

**现象：**“安装 Runtime”不可用，或安装后仍显示错误。

**确认：**

- 项目使用快速开始页声明的 Godot .NET 版本。
- 根目录恰好一份 `.csproj`，并已完成 Debug 编译。
- `addons/godo_framework/Core/GoDoRuntime.tscn` 存在。
- Autoload 中没有其他项目占用 `GoDoRuntime`，也没有其他名称指向同一路径。

**解决：**先修复 Setup 中最靠前的错误并刷新。不要手工添加第二个 Runtime。完整流程见[安装、升级与卸载](../getting-started/install-upgrade-uninstall.md)。

## `Services.Get<T>()` 提示服务未注册

**常见原因：**GoDoRuntime 未安装、调用发生在 Runtime 初始化前、业务场景重复创建框架，或查询了不存在的接口。

**解决：**确认唯一 Autoload 正常，业务入口在场景 `_Ready()` 或 Procedure 中查询服务。必需服务缺失不应改成 `TryGet` 静默跳过。参见[服务与事件](../guides/services-and-events/index.md)。

## 资源或场景无法加载

**现象：**`ResourceLoadException`、`SceneChangeException`，或资源移动后失效。

**确认：**

- Key 使用规范 `res://` 或有效 `uid://`，大小写与实际文件一致。
- ResourceManifest 已在 Boot 加载，语义 ID 存在。
- 请求的泛型类型与资源实际类型一致。
- 同一路径没有正在以另一类型异步加载。

场景切换失败发生在提交前时，旧场景应仍然存在。记录异常的 `Key` 与 `InnerException`，不要立即再次重复请求。参见[资源与场景](../guides/resources-and-scenes/index.md)。

## 场景切换后访问节点报错

SceneService 成功提交后会对旧 CurrentScene 调用 `QueueFree()`。`await ChangeAsync()` 返回后，只使用返回的新场景，不再访问旧节点或保存的子节点引用。

加载 UI 不要放在即将被替换的旧主场景中，应放在长期 UI 层。连续点击切换入口时禁用按钮，并由 Procedure 串行协调。

## UI 返回顺序错误或界面残留

**确认：**

- 受管理界面是否被直接 `QueueFree()` 或 `RemoveChild()`。
- 是否尝试关闭非顶部 View/Modal。
- View 是否由旧 Procedure 打开，却没有在 Exit 中关闭。
- 是否有多个节点同时处理返回 Action。

统一使用 `Close()` / `TryGoBack()`，并让打开者负责关闭。Scene UI 随场景切换清理；View 和 Modal 默认保留。参见[UI 与音频](../guides/ui-and-audio/index.md)。

## Modal 打开后角色仍响应输入

Modal 只阻止鼠标事件落到底层 Control，不会自动暂停 SceneTree，也不会阻止键盘、手柄或 `_UnhandledInput`。

由暂停协调器切换 InputService Context，并按游戏设计设置 SceneTree Pause；关闭 Modal 时按相反顺序恢复。

## 输入服务 `IsReady == false`

**确认：**

- GUIDE、GuideCs、GoDoRuntime Autoload 顺序正确。
- `GuideInputBackendInstaller` 位于只进入一次的 Boot 场景。
- Profile 中 Action/Context ID 与代码完全一致。
- Godot 已完成文件扫描并成功编译。

不要在 Gameplay 或菜单场景重复安装后端。Frame 过期、Axis 类型错误和 Context Pop 顺序问题参见[输入系统](../guides/input/index.md)。

## BGM 或 SFX 没有播放

- BGM 请求被拒绝：已有 BGM 仍在加载，等待上一请求完成。
- SFX 返回 `false`：并发 Voice 已满，不是资源异常。
- 循环 SFX 不归还：循环流不会自然 Finished，使用 `StopAllSfx()` 或业务播放器。
- 空间声音没有定位：AudioService 只管理非空间音频。
- Bus 每次启动出现 Warning：在项目 Audio Bus Layout 中正式创建 BGM/SFX。

## 存档无法读取或从备份恢复

`NotFound` 是空槽位，不是异常。`RecoveredFromBackup` 表示正式档损坏但 `.bak` 可用，应提示玩家可能丢失最近进度，并在下一安全点保存。

`SaveException` 中检查槽位、操作和 InnerException。正式档与备份都损坏时，不要自动删除；提供重试、选择其他槽位或经确认删除。版本不支持应由业务 Codec 明确迁移或拒绝。

## 设置无法应用

- 分辨率、窗口模式或 VSync 返回 `Unsupported`：当前平台不支持，先用 `Supports` 决定是否显示控件。
- 重启后恢复默认：修改后没有显式 `Save()`。
- Locale 被拒绝：翻译资源未加载，或 Locale 不能匹配 AvailableLocales。
- 音量参数异常：值不是有限的 0–1。

## 切换语言后文本没有更新

普通 Control 可使用 Godot 自动翻译；运行时拼接、缓存文字和非 Control 内容必须在 `LocaleChangedEvent` 后刷新。

如果显示方框，检查 Theme 字体 fallback；如果缺失键显示为键本身，检查翻译资源是否被项目和导出包包含。RTL 仍需检查布局方向、焦点、图标和自定义绘制。

## Procedure 切换后没有当前流程

新流程 `EnterAsync` 失败时，旧流程已经退出，`Current` 为 null，这是明确语义。进入 RecoveryProcedure 或安全标题流程，不要假设自动回滚。

旧流程 `ExitAsync` 失败时，`Current` 仍是旧流程，新流程不会进入。Enter/Exit 内需要后续切换时使用 `RequestChange`，不要递归 `ChangeAsync`。参见[Procedure 恢复](../guides/procedure-recovery/index.md)。

## Scheduler 回调没有按预期执行

- 暂停时停止：GameTime 和 UnscaledGameTime 都受 SceneTree Pause 影响；选择 RealTime。
- 慢动作时变慢：默认 GameTime 受 TimeScale 影响。
- 卡顿后只回调一次：遗漏重复周期会合并，这是设计行为。
- 场景切换后仍执行：任务没有绑定 Owner，也没有保存并取消 Handle/Token。
- `DelayAsync` 被取消：Owner 离树、Token 或框架关闭触发了正常取消。

## NodePool 复用后状态异常

Godot `_Ready()` 不会因复用再次调用。把每次初始化和清理放进 `OnAcquire` / `OnRelease`，包括信号、Tween、Scheduler、碰撞和外部引用。

Release 返回 false 通常表示重复归还、错误 Pool 或节点已被外部 QueueFree。活动节点属于 Pool，不要自行释放。

## DataTable 生成或导出失败

- 先运行 `check`，修正 CSV 编码、字段类型、主键、范围和外键。
- 修改源数据后运行 `generate`，再用 `verify-generated` 检查过期文件。
- 单表模式要求已有健康全量基线；表集合或其他表变化时生成全部。
- 正式发布使用 `godo_datatable_export.py` 包装脚本；当前支持的 Godot 4.x 直接导出不能可靠中止错误包，升级引擎后需重新验证该限制。

DataTable 仍为实验功能，升级后必须重新生成和编译。

## 仍无法定位时

收集最小信息：Godot/.NET 与框架版本、目标平台、完整异常类型和 InnerException、发生阶段、ResourceKey/槽位/流程名，以及能稳定复现的最小步骤。先查看 GoDo Debugger 和 Godot 输出，再在 API Reference 查询异常和成员契约。

不要提交存档内容、访问令牌、用户路径或其他敏感数据。
