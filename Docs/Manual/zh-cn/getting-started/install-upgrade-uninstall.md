# 安装、升级与卸载框架

GoDoFramework 的分发单元是完整的 `addons/godo_framework/` 目录。编辑器插件负责检查环境并显式安装唯一的 `GoDoRuntime` Autoload；它不会接管目标项目的 `.csproj`、输入映射、导出预设或业务场景。

本页适用于把一个明确版本的框架接入已有 Godot C# 项目，以及后续安全升级或彻底移除。

## 安装前准备

目标项目需要：

- Godot 4.7.1 .NET 版本。
- 根目录恰好一份可用的 `.csproj`。
- 至少成功完成一次 Debug C# 编译。
- 运行目标使用 .NET 8；Android 构建按项目要求使用 .NET 9。

安装前提交或备份项目。不要把框架工作仓库的 `project.godot`、`.csproj`、解决方案、Demo、Verification、`.godot`、`bin` 或 `obj` 复制到游戏项目。

## 1. 复制完整框架目录

把版本包中的目录复制为：

```text
res://addons/godo_framework/
```

不要只挑选当前看起来会使用的 Runtime 子目录。Core、编辑器安装助手和模块之间存在明确的发布边界；拆分复制会让健康检查、编译或后续升级失去一致性。

可选第三方集成仍需要它自己的依赖，例如 GUIDE 或 Phantom Camera。先完成核心框架安装，再按对应功能指南安装可选后端：

- [输入系统与 GUIDE 后端](../guides/input/index.md)
- [主镜头与 Phantom Camera](../guides/camera/index.md)

## 2. 启用编辑器插件

在 Godot 中打开：

```text
项目设置 → 插件 → GoDo Framework → 启用
```

启用插件只增加顶部 `GoDo Framework` 菜单和编辑器工具，不会自动写入 Autoload。插件使用 GDScript，因此即使 C# 尚未编译，也应能打开设置窗口。

如果插件没有出现：

1. 确认目录精确为 `addons/godo_framework/`。
2. 检查 `addons/godo_framework/plugin.cfg` 是否存在。
3. 查看 Godot 编辑器输出中的 GDScript 加载错误。
4. 确认没有复制成双层目录，例如 `addons/godo_framework/godo_framework/`。

## 3. 完成 C# 编译

目标项目必须有自己的 C# 解决方案。使用 Godot 的 C# 项目生成能力创建后，完成一次 Debug 编译。

框架不会自动：

- 创建或修改 `.csproj` 和解决方案。
- 改变程序集名称或目标框架。
- 添加 NuGet 包。
- 复制工作仓库的构建配置。

设置窗口会把以下情况显示为错误：根目录缺少 `.csproj`、存在多份 `.csproj`、尚无 Debug 程序集，或框架源码比现有程序集更新。

## 4. 运行健康检查

打开：

```text
GoDo Framework → 配置 (Setup)...
```

检查窗口按顺序验证：

1. Godot 版本。
2. 框架 Runtime 场景是否完整。
3. C# 环境和编译结果。
4. 是否存在框架重复副本或重复 Runtime 路径。
5. `GoDoRuntime` Autoload 状态。

健康检查本身是只读操作。先修复所有错误，再安装 Runtime。警告应逐项理解，不要为了让按钮可用而手工绕过检查。

## 5. 显式安装 Runtime

检查通过后点击 **安装 Runtime**。插件会安装：

```text
名称：GoDoRuntime
路径：res://addons/godo_framework/Core/GoDoRuntime.tscn
```

安装前会重新检查实际状态，成功后也会再次读取项目配置确认结果。已经正确安装时不会重复写入。

不要同时在“项目设置 → 全局/Autoload”中手工添加另一个 Runtime。名称被其他 Autoload 占用，或其他名称已经指向同一 Runtime 场景时，插件只报告冲突，不会擅自覆盖或删除。

## 6. 验证安装结果

安装完成后确认：

- Setup 窗口全部核心检查通过。
- Autoload 列表只有一个 `GoDoRuntime`。
- C# 项目重新编译无错误。
- 游戏启动后能取得必需服务。

最小验证代码：

```csharp
using Godot;
using GoDo;

public partial class FrameworkProbe : Node
{
    public override void _Ready()
    {
        IProcedureService procedures = Services.Get<IProcedureService>();
        ISceneService scenes = Services.Get<ISceneService>();
        LogHub.Info("GoDo runtime is ready.", "Game.Boot");
    }
}
```

验证完成后删除临时 Probe。业务入口负责启动第一个 Procedure；不要在业务场景或测试场景中再次实例化 GoDoRuntime。

## 安装可选集成

核心安装完成后再启用需要的集成。每项集成都有独立健康检查，不应通过修改第三方源码强行适配。

| 集成 | 安装入口 | 核心是否依赖 |
|---|---|---:|
| GUIDE Input | `GoDo Framework → 编辑器扩展 → 输入映射配置 (GUIDE Input Settings)...` | 否 |
| Phantom Camera | `GoDo Framework → 编辑器扩展 → 幻影相机配置 (Phantom Camera Settings)...` | 否 |
| DataTable | `GoDo Framework → 数据表 → 数据表配置 (DataTable Configuration)...` | 否，开发期工具 |

禁用或不复制可选集成不会改变 GoDoRuntime 的核心初始化。对应业务代码必须避免引用未安装集成的类型。

## 升级到新版本

升级采用“完整替换目录”，不要只覆盖同名文件。增量覆盖会留下新版本已经删除的旧源码，这些文件仍可能被 Godot 扫描或参与 C# 编译。

推荐步骤：

1. 阅读目标版本说明，确认 Godot/.NET、public API 和迁移要求。
2. 提交或备份当前项目，确保框架目录没有业务私改。
3. 关闭 Godot 编辑器和仍在运行的游戏实例。
4. 移除旧的 `addons/godo_framework/`，复制新版本完整目录到同一路径。
5. 重新打开 Godot，等待文件扫描和 C# 项目更新。
6. 完成 Debug 编译。
7. 打开 Setup，处理全部错误和警告。
8. 重新生成 DataTable 等生成物，并验证可选集成。
9. 运行游戏项目的自动测试和关键场景人工验收。

只要名称和路径仍匹配，升级不会要求重新安装 GoDoRuntime。插件不会静默修改业务代码或迁移 public API 调用。

### 如果业务修改过框架目录

不要直接覆盖并期待自动合并。先把业务改动迁移到游戏代码、适配层或独立扩展，再恢复纯净框架目录。直接维护框架私有分支会增加每次升级的合并和回归成本。

### 升级失败时回滚

1. 关闭 Godot。
2. 恢复升级前提交或备份中的完整框架目录及业务迁移修改。
3. 删除由新版本产生、但不属于旧版本的生成物。
4. 重新打开项目并编译。
5. 运行旧版本 Setup，确认 Runtime 路径和唯一性。

不要用混合版本继续开发；源码、场景和生成代码必须来自同一框架版本。

## 暂时禁用和彻底卸载的区别

在插件列表中禁用 **GoDo Framework** 只会移除编辑器菜单和窗口，不会卸载 `GoDoRuntime`。游戏运行时仍会初始化框架。

彻底卸载按以下顺序执行：

1. 打开 `GoDo Framework → 配置 (Setup)...`。
2. 点击 **卸载 Runtime** 并确认。
3. 确认 Autoload 中已没有精确匹配的 `GoDoRuntime`。
4. 在插件列表禁用 GoDo Framework。
5. 关闭 Godot。
6. 删除完整 `addons/godo_framework/` 目录。
7. 删除或迁移游戏代码中所有 `GoDo.*`、生成代码和可选集成引用。
8. 重新打开项目并编译，清理游戏项目自己维护的剩余配置。

卸载按钮只移除名称为 `GoDoRuntime` 且精确指向框架 Runtime 场景的 Autoload。路径不匹配、同一路径存在其他名称或项目配置不可读时会拒绝操作，避免误删其他项目设置。

插件不会删除框架文件、业务场景、资源清单、存档、输入映射或导出预设。删除哪些游戏数据由项目自己决定。

## 常见错误

- 安装按钮不可用：查看 Setup 中最靠前的错误，常见原因是未完成 Debug 编译或 `.csproj` 数量不正确。
- Runtime 名称冲突：已有其他 Autoload 使用 `GoDoRuntime`；先确认归属，不要直接覆盖。
- 提示重复 Runtime 路径：其他名称指向同一场景；保留唯一注册。
- 更新后仍编译旧类型：增量覆盖留下了已删除文件；恢复备份后重新进行完整目录替换。
- 禁用插件后框架仍运行：禁用编辑器插件不会卸载 Autoload。
- 删除目录后项目无法编译：业务代码仍引用 GoDo 类型，或 Autoload 尚未卸载。
- 可选插件设置健康但运行失败：仍需完成 C# 编译和对应真实场景验证，健康检查不是完整功能测试。

安装后可继续阅读 [获取长期服务与发送业务事件](../guides/services-and-events/index.md)，再按快速开始完成第一个 Procedure、场景和 UI。
