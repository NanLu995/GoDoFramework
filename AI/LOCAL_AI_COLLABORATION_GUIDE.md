# GoDoFramework 本地 AI 协作指南

## 1. 目标

本地 AI 作为 Codex 的辅助分析员，承担重复、耗上下文但风险较低的只读工作：

- 指定范围的源码扫描
- public API 清单整理
- 生命周期和异常边界初查
- 编译及测试日志归纳
- Diff 二次审查
- 重复样板代码定位
- 文档与源码差异检查

本地 AI 不负责最终架构决策，也不直接修改 GoDoFramework。

```text
用户
  ↓
Codex：规划、设计、实现、验证、最终判断
  ↓
LocalAiWorker：受限任务分发
  ↓
LM Studio：运行本地模型
  ↓
本地 AI：扫描、归纳、初步审查
```

## 2. 本机推荐配置

硬件：

```text
GPU：RTX 4070 Super 12GB
内存：32GB
```

默认模型：

```text
Qwen2.5-Coder-14B-Instruct-GGUF
量化：Q4_K_M
```

LM Studio 参数：

```text
Context Length：16384
Temperature：0.1
Max Output Tokens：4096
GPU Offload：尽可能全部层
Flash Attention：开启
模型自动卸载：按需开启
```

显存不足时，先将 Context Length 降至 `8192`，再关闭其他占用显存的程序，最后才考虑减少 GPU Offload。

不建议日常使用 30B 模型。`Qwen3-Coder-30B Q4` 可以使用系统内存混合运行，但速度较慢，只适合偶尔进行深度审查。

## 3. LM Studio 服务配置

在 LM Studio 中进入：

```text
Developer → Start Server
```

默认地址：

```text
http://127.0.0.1:1234
```

OpenAI 兼容接口：

```text
http://127.0.0.1:1234/v1/chat/completions
```

安全设置：

- 只监听 `127.0.0.1`。
- 不开启 `Serve on Local Network`。
- 不允许其他局域网设备访问。
- 不向本地模型传递密码、Token 或私有密钥。

## 4. Codex MCP 配置

需要额外实现一个轻量的 `LocalAiWorker` MCP 服务。LM Studio 只是模型服务器，不会自动成为 Codex 的辅助工具。

Codex 项目级配置示例：

```toml
[mcp_servers.local_ai_worker]
command = "python"
args = ["D:/LocalAiWorker/server.py"]
cwd = "D:/LocalAiWorker"

enabled_tools = [
    "analyze_files",
    "review_diff",
    "summarize_test_log"
]

default_tools_approval_mode = "auto"
startup_timeout_sec = 20
tool_timeout_sec = 300
enabled = true
```

第一阶段只提供三个工具：

### `analyze_files`

分析明确指定的文件。

```json
{
  "goal": "检查 Procedure 与 Scene 的生命周期边界",
  "files": [
    "addons/godo_framework/Procedure/ProcedureHub.cs",
    "addons/godo_framework/Scene/SceneHub.cs"
  ],
  "constraints": [
    "只读分析",
    "所有结论必须提供源码证据"
  ]
}
```

### `review_diff`

审查 Codex 已生成的 Diff，重点定位：

- 生命周期遗漏
- 兼容性风险
- 异常不可见
- 无效清理
- 过度设计
- 测试缺口

### `summarize_test_log`

归纳编译或 Godot 回归日志：

- 是否真正执行到目标场景
- 首个有效错误
- 可能的根因
- 后续验证建议

## 5. 给本地 AI 的系统指令

以下内容作为 LocalAiWorker 调用模型时的固定 System Prompt。

```text
你是 GoDoFramework 的只读辅助分析员。

项目技术环境：
- Godot 4.7.1
- C# / .NET 8
- 框架根命名空间为 GoDo
- 框架源码位于 addons/godo_framework
- Templates 和 Demo3D 是框架 public API 的使用者

你的职责：
1. 根据明确提供的目标和文件进行源码分析。
2. 找出有直接源码证据的问题、风险、重复和遗漏。
3. 为 Codex 提供结构化的初步分析结果。
4. 明确区分事实、推断和未知信息。

强制规则：
- 你没有最终设计决定权。
- 不直接修改、创建、移动或删除文件。
- 不执行 Shell、Git、网络、包管理或系统命令。
- 不虚构不存在的类型、方法、Godot API、配置项或测试结果。
- 没有源码证据时，不得把推断描述成事实。
- 所有源码结论必须包含文件路径、行号和最小必要证据。
- 找不到证据时返回 unknown，不得补全或猜测。
- 不建议修改 project.godot、.csproj、Autoload 或第三方插件。
- 不建议删除、重命名或改变现有 public API；如任务涉及这些内容，必须明确标记兼容影响。
- 不把角色、血量、武器、关卡规则等具体玩法加入 GoDo 命名空间。
- 不因未来可能需要而增加抽象层、配置或扩展点。
- 源码和文档冲突时报告冲突，不自行决定哪一方需要修改。
- 编译通过、测试通过等结论只能来自提供的真实日志。

分析重点：
- public API 契约
- 生命周期进入、退出和清理
- 异步取消与场景销毁
- 失败语义和异常可见性
- Procedure、Scene、UI 的职责边界
- Godot Node 和 Resource 的有效性
- 信号订阅与对称取消
- 高频路径额外分配
- 向后兼容性
- 最小必要测试

输出必须是符合约定 Schema 的 JSON，不要附加 Markdown 或解释文字。
```

## 6. 本地 AI 输出格式

LocalAiWorker 应强制使用 JSON Schema。示例：

```json
{
  "status": "complete",
  "summary": "发现两个有源码证据的生命周期风险。",
  "findings": [
    {
      "severity": "medium",
      "category": "lifecycle",
      "claim": "退出流程没有对订阅进行对称清理。",
      "file": "addons/godo_framework/Example.cs",
      "line": 42,
      "evidence": "EventSource.Changed += OnChanged;",
      "reasoning": "当前文件未发现对应的 -= OnChanged。",
      "suggestion": "由 Codex 检查退出生命周期并评估是否需要取消订阅。",
      "confidence": 0.92
    }
  ],
  "unknowns": [
    "未提供 EventSource 的生命周期实现，无法判断信号源是否先于订阅者释放。"
  ],
  "suggested_checks": [
    "检查 _ExitTree() 或 Dispose() 是否存在间接清理。"
  ]
}
```

状态值：

- `complete`：在提供范围内完成。
- `partial`：部分文件、日志或上下文缺失。
- `blocked`：无法完成。

严重程度：`critical`、`high`、`medium`、`low`、`info`。

`confidence` 只能表示本地模型对结论的确信程度，不能替代 Codex 的源码复核。

## 7. Codex 使用本地 AI 的规则

适合调用本地 AI 的情况：

- 文件较多，需要先缩小范围。
- 需要对 Diff 进行独立第二视角审查。
- 测试日志较长，需要提取首个有效错误。
- 需要整理重复 API 或样板代码。
- 需要检查文档和实现是否存在明显差异。

Codex 不得直接接受本地 AI 的结论。必须遵循：

```text
明确任务范围
→ 本地 AI 初步分析
→ Codex 检查引用文件和行号
→ Codex 判断结论是否成立
→ Codex 决定是否设计或修改
→ 真实编译和回归验证
```

以下工作不委托本地 AI 最终决定：

- public API 命名
- 兼容性修改
- 框架模块职责
- Godot 生命周期契约
- 异常传播策略
- 是否修改工程配置
- 测试是否通过
- 是否可以交付

## 8. 用户配合方法

用户不需要编写复杂提示，可以直接使用以下表达。

### 先让本地 AI 扫描

```text
先让本地 AI 只读扫描 Procedure、Scene 和 UI 的相关实现，
整理带文件和行号的事实，然后你复核并给我结论。
```

### 审查当前改动

```text
让本地 AI 对当前 Diff 做一次独立审查，
重点检查生命周期、异常可见性和兼容性。
你复核后只告诉我成立的问题。
```

### 分析测试日志

```text
让本地 AI 归纳这次测试日志，
然后你确认首个真实错误以及是否执行到了目标场景。
```

### 对比多个方案

```text
方案由你设计，本地 AI 只负责从现有源码中寻找
支持或反对各方案的证据。
```

### 禁止本地 AI 参与

```text
这次不要调用本地 AI，由你直接分析。
```

## 9. 推荐工作节奏

每个开发阶段采用：

1. Codex 明确本阶段完成标准。
2. 本地 AI 扫描限定范围。
3. Codex 复核并提出设计方案。
4. 用户确认涉及命名或兼容性的选择。
5. Codex 实现最小改动。
6. 本地 AI 审查 Diff。
7. Codex 过滤误报并修正成立的问题。
8. 执行真实编译和目标回归。
9. Codex 汇报改动与验证结果。

## 10. 安全边界

LocalAiWorker 必须做到：

- 只允许读取指定的项目根目录。
- 拒绝路径中包含 `..` 的越界访问。
- 排除 `.git`、`.godot`、`bin`、`obj` 和敏感配置。
- 限制单次文件数量和总字符数。
- 设置请求超时。
- 不记录完整源码到长期日志。
- 不将内容发送到 LM Studio 之外的地址。
- 不开放任意命令参数。
- 不向本地模型提供写文件工具。

第一阶段即使本地模型支持 MCP 和工具调用，也不要让它直接获得文件系统或 Shell MCP。

## 11. 验收标准

接入完成后至少验证：

- 能分析两个指定源码文件。
- 返回内容符合 JSON Schema。
- 每个 Finding 都包含文件和行号。
- 查询不存在的类型时返回未知，而不是编造。
- 无法访问范围外路径。
- 超时后能够返回明确错误。
- 本地模型不可写入项目。
- Codex 能调用工具并复核至少一个结论。
- 关闭 LM Studio 后，Codex 能看到明确的连接失败，而不是静默忽略。

## 12. 当前状态

本文件只定义协作协议。真正接通 Codex 与 LM Studio，还需要实现只读的 `LocalAiWorker` MCP 服务。该服务不应要求修改 GoDoFramework 的 `.csproj`、`project.godot` 或 Autoload。
