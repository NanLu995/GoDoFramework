# 文档站维护

文档站使用现有 Markdown 与 `GoDo.*` public API 的 XML 注释生成，不维护另一份生成后正文。Python 脚本只使用标准库；DocFX 版本固定在仓库的 `.config/dotnet-tools.json`。

写作与翻译要求见 [STYLE_GUIDE.md](STYLE_GUIDE.md)，新增模块和 Recipe 分别从 [MODULE_TEMPLATE.md](Templates/MODULE_TEMPLATE.md) 与 [RECIPE_TEMPLATE.md](Templates/RECIPE_TEMPLATE.md) 开始。

## 常用命令

在仓库根目录运行：

```powershell
python Docs/build_docs.py lint
python Docs/build_docs.py check
python Docs/build_docs.py build
python Docs/build_docs.py serve
```

- `lint`：检查 Markdown 结构、代码块和翻译状态，不运行 DocFX。
- `check`：重新发现内容并生成中英文站点，将 DocFX 警告视为错误，并检查最终 Pages 产物；提交前使用。
- `build`：生成静态站点到 `.artifacts/docs/site/`。
- `serve`：生成站点并在 `http://127.0.0.1:8080/` 启动本地预览；按 `Ctrl+C` 停止。
- `prepare`：只生成 DocFX 临时工作区，排查导航或配置时使用。
- `clean`：删除 `.artifacts/docs/` 下的文档生成物。

脚本会自动执行 `dotnet tool restore` 与项目还原。第一次运行需要访问 NuGet，之后可复用本机缓存。

## 生成结构

```text
.artifacts/docs/site/
├─ index.html
├─ zh-cn/
│  ├─ index.html
│  ├─ modules/
│  ├─ recipes/
│  └─ api/
└─ en-us/
   ├─ index.html
   └─ api/
```

根页面提供语言选择。每个完整 HTML 页面右下角提供语言切换：目标语言存在同一路径时保持当前页面，否则回到目标语言首页。两个站点有独立导航、搜索索引和 sitemap。

当前英文站先提供首页和快速开始。API 页面在两种语言路径下生成，但内容复用当前中文 XML 注释，直到项目单独决定 public API 注释语言策略。

## 内容发现规则

中文内容自动发现：

- `addons/**/USAGE.md`：加入 Core、Runtime、Tools、Debugger 或可选集成。
- `AI/Recipes/*.md`：加入“教程与配方”；`.en.md` 文件除外。
- `GoDo.*` public API：由 DocFX 从 `GoDoFramework.csproj` 和 XML 注释生成。

英文翻译自动发现：

- `USAGE.md` 对应同目录的 `USAGE.en.md`。
- `Name.md` Recipe 对应 `Name.en.md`。
- 独立页面位于 `Docs/i18n/en-us/`，并在 `Docs/build_docs.py` 的顶层页面清单中登记。

新增 Markdown 必须包含一个 `# 一级标题`。模块分组内按稳定输出路径排序，因此新增模块通常不需要修改脚本。

首页、安装、游戏开发指南、项目结构、故障排查、版本记录和架构属于顶层策划内容，在 `CURATED_PAGES` 中显式维护。设计计划、内部协作规则和模块设计草稿不会进入用户站点。

## 翻译状态

英文翻译必须包含：

```yaml
---
translation_of: path/to/chinese-source.md
translation_source_hash: sha256:<source-hash>
---
```

中文源文件发生变化后，`lint` 和 `check` 会报告翻译过期，并给出新的摘要。更新摘要表示已经人工复核对应英文内容，不能只更新摘要跳过翻译检查。

## GitHub Pages 发布

`.github/workflows/docs.yml` 与本地使用同一个 `check` 命令。`master` 分支的文档或 public API 变化通过检查后，工作流发布整个 `.artifacts/docs/site/`。

仓库管理员只需首次在 GitHub 的 **Settings → Pages → Build and deployment** 中将 Source 设为 **GitHub Actions**。之后正常提交并 push 即可自动部署，也可以在 Actions 页面手动运行 Documentation 工作流。

文档改动已经提交到本地 `master` 后，发布只需一条命令：

```powershell
git push origin master
```

该 push 会触发 Documentation 工作流；不需要再选择 Jekyll、Static HTML 或手动上传生成目录。
