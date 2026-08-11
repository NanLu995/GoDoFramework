# 用户文档维护

公开文档站只由两类内容组成：独立维护的用户手册，以及从 `GoDo.*` public API XML 注释生成的 API Reference。

`AI/**` 和任何 `USAGE.md` 都是内部技术资料，只用于确认实现事实，不会被复制到公开站点。生成脚本也不会把源码目录自动变成用户手册目录。

## 内容位置

```text
Docs/
├─ Manual/
│  ├─ zh-cn/                 # 中文用户手册，主要内容源
│  └─ en-us/                 # 英文翻译，保持相同相对路径
├─ navigation.zh-cn.json     # 中文导航顺序
├─ navigation.en-us.json     # 英文导航顺序
├─ coverage.json             # 技术契约到用户手册的维护状态
└─ build_docs.py
```

新增手册页面后，必须把它加入对应语言的 `navigation.<locale>.json`。未加入导航的孤立页面、导航中不存在的页面和重复页面都会使构建失败。API Reference 由脚本自动追加到导航。

## 功能覆盖清单

`coverage.json` 会登记所有 `addons/godo_framework/**/USAGE.md`，防止新功能只维护内部文档，却忘记考虑用户手册。

每项状态只能是：

- `documented`：当前公开契约的用户用法已经覆盖，必须列出存在的中文 `manual_pages`；未来能力、实验状态和目标平台人工验收不影响这个状态。
- `pending`：尚待迁移，必须说明 `reason`；已有部分页面时可用 `manual_pages` 记录当前覆盖范围。
- `reference-only`：确认只需要 API Reference，必须说明原因。

每项还记录 `reviewed_contract_hash`。`USAGE.md` 变化后，检查会失败；维护者必须复核用户文档与状态，再更新摘要。新增 `USAGE.md` 未登记、删除后留下失效条目也都会失败。

API Reference 另外使用两个字段记录渐进式审计状态：

- `api_reference_status: pending`：参数、泛型参数、返回值和最终展示尚未按模块复核，必须说明 `api_reference_reason`。审计会报告这些缺口，但不会让既有债务阻断所有后续开发。
- `api_reference_status: verified`：该模块已经完成复核；以后缺少 `<param>`、`<typeparam>` 或非 `void` 成员的 `<returns>` 会使审计失败。

所有模块无论状态如何，都必须有 `<summary>`。Godot C# 源生成器产生的 `MethodName`、`PropertyName` 和 `SignalName` 辅助类型不会进入公开 API Reference；若过滤失效，审计会直接失败。异常类型和失败语义不能只靠标签数量判断，仍需在模块批次中结合源码、回归测试和 `USAGE.md` 人工复核。

更新摘要只代表已完成复核，不能用来绕过正文维护。

## 常用命令

在仓库根目录运行：

```powershell
python Docs/build_docs.py lint
python Docs/build_docs.py api-audit
python Docs/build_docs.py check
python Docs/build_docs.py build
python Docs/build_docs.py serve
```

- `lint`：检查覆盖清单、导航、Markdown 结构和翻译状态。
- `api-audit`：生成中文 API metadata，检查 XML 摘要、参数、泛型参数、返回值、模块归属和 Godot 生成类型过滤，并写入 `.artifacts/docs/api-audit.json`；适合模块批次收尾，不生成完整站点。
- `check`：运行文档校验测试，生成中英文站点，将 DocFX 警告视为错误，并验证最终产物；提交前使用。
- `build`：生成静态站点到 `.artifacts/docs/site/`。
- `serve`：生成后启动本地预览；只有需要查看页面效果时使用。
- `prepare`：仅生成 `.artifacts/docs/work/`，便于检查实际进入公开站点的 Markdown。
- `clean`：删除 `.artifacts/docs/` 中的生成物。

脚本使用 Python 标准库。DocFX 版本固定在 `.config/dotnet-tools.json`；首次完整构建可能需要访问 NuGet。

## 开发与文档收尾节奏

开发 public API 时，直接同步维护准确的 XML 注释，并运行当前行为改动所需的最小编译或回归。此时不要求为了每次小改动立即重写中英文手册、导航或覆盖摘要。

一个模块的行为和 API 稳定后，再按模块完成文档批次：复核源码与测试事实，更新对应 `USAGE.md`，补齐中文任务路径与英文翻译，按需维护导航和 `reviewed_contract_hash`，运行 `api-audit`。只有审计报告中的自动检查项已经清零，并人工复核失败语义、线程、生命周期和展示效果后，才把该模块改为 `api_reference_status: verified`。

准备提交、发布或完成文档里程碑时运行完整 `check`。这样把便宜且紧贴实现的 XML 维护放在开发阶段，把跨语言手册和最终 API Reference 验收集中到明确批次，同时保留可追踪的未完成状态。

## 中英文维护

中文是主要内容源。英文页面必须位于相同相对路径，并包含：

```yaml
---
translation_of: Docs/Manual/zh-cn/path/to/page.md
translation_source_hash: sha256:<中文源文件摘要>
---
```

中文源文件变化后，检查会提示英文翻译过期。人工复核英文正文后再更新摘要。缺少对应英文页面时，不生成机器翻译占位页；语言切换会回到英文首页。

## GitHub Pages

`.github/workflows/docs.yml` 使用同一个 `python Docs/build_docs.py check`。仓库管理员只需首次在 GitHub 的 **Settings → Pages → Build and deployment** 中将 Source 设为 **GitHub Actions**；之后提交并 push 到触发分支即可自动部署，不提交 `.artifacts/docs/site/`。
