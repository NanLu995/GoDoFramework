# 发布工具

发布工具只打包 `addons/godo_framework/`，并自动排除其中的 Markdown 文档。ZIP 保留运行时、编辑器资源与 DataTable Python 编译前端的目录结构，复制到目标 Godot 项目根目录即可使用。

## 本地打包

```powershell
python release/release.py
```

输出目录为 `release/dist/`，不会纳入 Git。

脚本默认读取 `addons/godo_framework/plugin.cfg` 的版本号。显式传入版本时必须与插件版本一致：

```powershell
python release/release.py --version 0.1.0
```

## 发布 GitHub Release

发布前需要：

1. 提交并推送本次版本代码；
2. 创建并推送对应 Tag，例如 `v0.1.0`；
3. 安装 GitHub CLI，并执行 `gh auth login`；
4. 运行：

```powershell
python release/release.py --publish
```

`--publish` 使用 `--verify-tag`，Tag 不存在时拒绝发布，不会隐式创建或移动 Tag。脚本会使用 GitHub 自动生成的 Release Notes，并上传本次生成的 ZIP。
