# 发布工具

发布工具生成四个职责独立的归档，并自动排除其中的 Markdown 文档：

- `GoDoFramework-v<version>.zip`：不包含 `Integrations/` 的核心框架；无第三方插件依赖。
- `GoDoFramework-GuideInput-v<version>.zip`：GUIDE Input 适配层，需要先安装匹配版本的 GUIDE / G.U.I.D.E-CSharp。
- `GoDoFramework-PhantomCamera-v<version>.zip`：Phantom Camera 适配层，需要先安装匹配版本的 Phantom Camera。
- `GoDoFramework-FrifloEcs-v<version>.zip`：Friflo ECS 场景级适配层；目标项目需添加 `Friflo.Engine.ECS 3.6.0` NuGet 引用。

四个归档都保留 `addons/godo_framework/` 下的原始路径。目标项目必须先完整安装核心包；需要可选集成时，再把对应归档叠加到项目根目录。不要只复制核心包内部的局部 Runtime 子目录。Friflo 集成 ZIP 只包含适配源码和上游 MIT License，不复制 NuGet 程序集。

## 本地打包

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
