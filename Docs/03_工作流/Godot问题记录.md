# Godot/C# 问题记录

> 文档类型：工作流
> 状态：当前
> 权威范围：项目已经实际遇到并确认的 Godot/C# 坑位
> 最近核对：2026-09-28
> 相关证据：[工程配置](../../GoDoFramework.csproj)、[自动回归说明](../../Verification/Automated/README.md)

每次确认一个可复现、可能重复出现的问题时在这里记录；高频且具有普遍约束价值的规则再同步到根目录 `AGENTS.md`。

格式建议：

```text
## [日期] 错误描述
- AI做了什么：
- 正确做法应该是：
- 已加入AGENTS.md：是/否
```

---

## 2026-07-16 条件排除第三方 C# 源码后覆盖 Debug 程序集
- AI 做了什么：使用 `-p:GoDoIncludeGuideInput=false -p:GoDoIncludePhantomCamera=false` 构建默认 Debug，覆盖了 Godot 当前工作区的 Debug DLL；随后启动完整项目时，GUIDE Autoload 与 EditorPlugin 脚本无法从程序集实例化，表现为插件偶发被禁用或提示脚本存在错误。
- 正确做法应该是：强制无插件验证直接构建 `GoDoFramework.csproj`，并使用独立 `CoreVerification` 配置；完整工作区继续使用默认 Debug。若已发生，重新执行默认 `dotnet build GoDoFramework.sln` 恢复程序集。
- 已加入 AGENTS.md：否；当前已记录在自动化验证说明中。
