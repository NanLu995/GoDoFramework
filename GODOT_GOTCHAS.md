# Godot AI 常见坑位清单（持续更新）

> 用法：每次发现AI（尤其本地模型）在Godot相关代码上犯了同类错误，就在这里记一条。
> 攒到一定数量后，把高频的几条摘录进 AGENTS.md 的"代码规范"或"禁止事项"部分。

格式建议：

```
## [日期] 错误描述
- AI做了什么：
- 正确做法应该是：
- 已加入AGENTS.md：是/否
```

---

## 示例（仅供参考格式，可删除）

## 2026-XX-XX Godot 4.x 信号连接写法
- AI做了什么：用了 Godot 3.x 的 `Connect("signal_name", this, "MethodName")` 字符串写法
- 正确做法应该是：Godot 4.x C# 用强类型方式 `node.SignalName += OnSignalName;` 或 `node.Connect(SignalName.XXX, Callable.From(OnXXX));`
- 已加入AGENTS.md：是

---

## 你的记录从这里开始

##
-
-
-
