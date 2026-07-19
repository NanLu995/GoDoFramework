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

更新摘要只代表已完成复核，不能用来绕过正文维护。

## 常用命令

在仓库根目录运行：

```powershell
python Docs/build_docs.py lint
python Docs/build_docs.py check
python Docs/build_docs.py build
python Docs/build_docs.py serve
```

- `lint`：检查覆盖清单、导航、Markdown 结构和翻译状态。
- `check`：运行文档校验测试，生成中英文站点，将 DocFX 警告视为错误，并验证最终产物；提交前使用。
- `build`：生成静态站点到 `.artifacts/docs/site/`。
- `serve`：生成后启动本地预览；只有需要查看页面效果时使用。
- `prepare`：仅生成 `.artifacts/docs/work/`，便于检查实际进入公开站点的 Markdown。
- `clean`：删除 `.artifacts/docs/` 中的生成物。

脚本使用 Python 标准库。DocFX 版本固定在 `.config/dotnet-tools.json`；首次完整构建可能需要访问 NuGet。

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
