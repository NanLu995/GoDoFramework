# GoDoFramework 用户手册

GoDoFramework 为 Godot 4.x C# 项目提供可复用的游戏流程、场景、UI、音频、输入、数据与诊断能力。它不替代玩法代码或 Godot 原生节点；它解决的是这些基础能力在项目变大后容易出现的生命周期、失败处理与协作边界问题。

## 选择一条阅读路线

<div class="godo-doc-grid">
  <a class="godo-doc-card" href="getting-started/index.md">
    <strong>从零搭建一个游戏骨架</strong>
    <span>安装 Runtime，然后依次建立流程、场景、UI、交互循环、音频、存档与本地化。</span>
  </a>
  <a class="godo-doc-card" href="guides/index.md">
    <strong>按模块解决当前问题</strong>
    <span>已经接入框架时，从 Procedure、Scene、UI、Input、Save 等模块直接开始。</span>
  </a>
  <a class="godo-doc-card" href="integrations/index.md">
    <strong>接入第三方能力</strong>
    <span>了解 G.U.I.D.E-CSharp 与 Phantom Camera 的适配边界、安装方式和限制。</span>
  </a>
  <a class="godo-doc-card" href="troubleshooting/index.md">
    <strong>排查运行问题</strong>
    <span>按现象定位安装、资源、流程、UI、输入和诊断问题。</span>
  </a>
</div>

## 这套框架适合什么

- 使用 Godot C#，并希望统一顶层流程、主场景、屏幕 UI、音频、输入、存档与设置的项目。
- 需要明确异步失败、节点生命周期与 Godot 主线程边界的团队。

如果只想查询精确类型、成员、参数或异常，请使用 API Reference；本手册只讲何时使用、如何组合以及如何验证结果。
