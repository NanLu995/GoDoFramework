# 发布工具

发布工具生成四个职责独立的归档，并自动排除其中的 Markdown 文档：

- `GoDoFramework-v<version>.zip`：不包含 `Integrations/` 的核心框架；无第三方插件依赖。
- `GoDoFramework-GuideInput-v<version>.zip`：GUIDE Input 适配层，需要先安装匹配版本的 GUIDE / G.U.I.D.E-CSharp。
- `GoDoFramework-PhantomCamera-v<version>.zip`：Phantom Camera 适配层，需要先安装匹配版本的 Phantom Camera。
- `GoDoFramework-FrifloEcs-v<version>.zip`：Friflo ECS 场景级适配层；目标项目需添加 `Friflo.Engine.ECS 3.6.0` NuGet 引用。

四个归档都保留 `addons/godo_framework/` 下的原始路径，并包含 `addons/godo_framework/LICENSE`，不会在叠加安装时占用或覆盖消费项目根目录的同名许可证。目标项目必须先完整安装核心包；需要可选集成时，再把对应归档叠加到项目根目录。不要只复制核心包内部的局部 Runtime 子目录。Friflo 集成 ZIP 还保留上游 MIT `LICENSE`，不复制 NuGet 程序集。

## 本地打包

正式打包前先运行统一发布门禁。它按固定顺序复用现有的发布脚本单测、public API 兼容检查、DataTable 生成产物校验、Debug / Release 构建、Headless 回归、DataTable 导出过滤与 Windows ExportRelease、文档检查、发布包生成，以及核心 ZIP 的安装、替换升级与安全移除回归：

```powershell
python Verification/release_gate.py --godot $env:GODOT_PATH
```

默认执行全部阶段，任一阶段失败后立即停止并返回非零退出码。排查单项失败时可重复传入 `--stage`；阶段名称和执行顺序可通过 `python Verification/release_gate.py --list-stages` 查看：

```powershell
python Verification/release_gate.py --stage docs
python Verification/release_gate.py --godot $env:GODOT_PATH --stage regressions --stage datatable-export-release
python Verification/release_gate.py --godot $env:GODOT_PATH --stage package-lifecycle
```

DataTable ExportRelease 与 `package-lifecycle` 使用门禁创建的临时目录，成功后自动清理；生命周期回归只删除其临时项目内精确匹配的框架目录，不修改当前工作区的 `project.godot`、Autoload 或插件状态。`packages` 阶段生成的正式归档仍写入 `release/dist/`。门禁只负责本地验证和打包，不创建 Tag、不提交代码，也不发布 GitHub Release。

`package-lifecycle` 从当前源码生成真实核心 ZIP，在无预置 Autoload 的临时 Godot C# 项目中通过插件界面安装 Runtime，验证 9 项长期服务启动；随后放置合成旧文件并完整替换框架目录，确认残留被清除、Autoload 保留且运行时仍健康。移除阶段还会确认错误路径的同名 Autoload 不会被卸载，再执行精确卸载、禁用插件、删除框架目录，并验证不再引用 `GoDo.*` 的中性宿主仍可编译运行。合成旧文件只验证完整替换卫生，不代表任意历史版本的 API 或数据迁移兼容性。

### public API 兼容基线

`docs` 阶段先生成并审计 DocFX metadata，随后 `api-compatibility` 将当前 `GoDo.*` 的 UID、成员类型和规范化 C# 声明与 `Verification/ApiCompatibility/public-api-baseline.json` 比较。删除 API，或者改变返回类型、参数、默认值、访问器、继承声明等签名信息时门禁失败；纯新增 API 只在报告中列出，不阻止构建。比较结果写入 `.artifacts/docs/api-compatibility.json`。

日常开发和 CI 只能执行只读检查：

```powershell
python Docs/build_docs.py api-audit
python Verification/ApiCompatibility/api_compatibility.py check
```

只有完成兼容影响评审、版本决策、迁移说明和回归后，才显式接受当前 API 为新基线：

```powershell
python Verification/ApiCompatibility/api_compatibility.py update
```

`update` 不会由发布门禁自动调用。不能为了让检查通过而跳过兼容评审或无说明地更新基线。

### 托管 CI 边界

`Core Verification` 与 `Documentation` GitHub Actions 同时响应 `master` Push 和 Pull Request。Core CI 复用发布门禁中的发布脚本单测、API 兼容工具自测和 DataTable 已生成产物校验，再执行 Linux 干净核心包验证；Docs CI 在完整文档和 API metadata 检查后执行 public API 基线比较。

Windows DataTable ExportRelease、完整可选集成与正式发布归档仍由本地完整门禁负责，不在 Linux Pull Request CI 中伪装成跨平台验证。CI 不执行 `api_compatibility.py update`，基线变化始终需要代码评审。

```powershell
python release/release.py
```

输出目录为 `release/dist/`，不会纳入 Git。

脚本默认读取 `addons/godo_framework/plugin.cfg` 的框架版本、最低 Godot 版本和最高已验证 Godot 版本。三个值都必须使用 `major.minor.patch`；Godot 最低版本不得高于已验证版本，也不得跨 major。显式传入框架版本时必须与插件版本一致：

```powershell
python release/release.py --version 0.1.0
```

打包成功后会打印本次框架版本和 Godot 已验证范围。高于已验证版本的同 major Godot 不会被声明为已验证，使用方应升级框架或执行完整项目回归。

## 发布 GitHub Release

发布前需要：

1. 提交并推送本次版本代码；
2. 创建并推送对应 Tag，例如 `v0.1.0`；
3. 安装 GitHub CLI，并执行 `gh auth login`；
4. 运行：

```powershell
python release/release.py --publish
```

`--publish` 使用 `--verify-tag`，Tag 不存在时拒绝发布，不会隐式创建或移动 Tag。脚本会使用 GitHub 自动生成的 Release Notes，并上传核心、GUIDE Input、Phantom Camera 和 Friflo ECS 四个 ZIP。
