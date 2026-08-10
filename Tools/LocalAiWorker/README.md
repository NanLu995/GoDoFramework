# LocalAiWorker

`LocalAiWorker` 是 GoDoFramework 专用的只读 MCP 服务。Codex 只在多文件扫描、Diff 二次审查或长日志归纳确有收益时调用它；LM Studio 不可用或输出不合格时，Codex 立即自行完成任务。

## 启动条件

1. 安装 Python 3.10 或更高版本，无需安装第三方包。
2. 在 LM Studio 中加载名称包含 `qwen2.5-coder-14b` 的 LLM。
3. 仅在 `127.0.0.1:1234` 启动 LM Studio API Server，不启用局域网访问。
4. 重启 Codex，使项目 `.codex/config.toml` 中的 MCP 配置生效。

Codex 桌面启动 MCP 时不依赖终端 PATH；`.codex/config.toml` 使用当前机器的 Python 绝对路径。Python 安装位置变化时需同步更新该路径。

模型名称匹配串由 `.codex/config.toml` 的 `GODO_LOCAL_AI_MODEL` 提供。它只选择本机模型，不能改变固定的 LM Studio 地址。

## 安全边界

- MCP 只公开 `analyze_files`、`review_diff` 和 `summarize_test_log`。
- 本地模型没有文件系统、Shell、Git、网络或写文件工具。
- 文件分析只接受明确的项目相对路径；拒绝绝对路径、`..`、链接、敏感目录和非白名单扩展名。
- 源码、Diff 和日志在发送前拦截常见私钥及 Token 特征；命中后整次请求失败关闭。
- Diff 和日志由 Codex 直接提供；Worker 不运行 Git、编译或测试命令。
- 每次模型请求前用 `GET /api/v1/models` 做无推理预检；成功缓存 30 秒，失败熔断 60 秒。
- 单次模型请求失败、超时或 Schema 不合格时不自动重试。
- 单文件默认最多读取 80,000 字符；更大的文件必须通过 `start_line` 和 `end_line` 按原始行号分段分析。行范围仅支持单文件调用，两个参数必须同时提供。
- 文件路径、大小和行范围等输入校验失败会返回具体原因，不请求模型，也不触发 60 秒熔断。
- 不写持久日志，不长期保存源码或模型输出。

大文件分段分析示例：

```json
{
  "goal": "检查指定范围内的生命周期和异常可见性",
  "files": ["addons/godo_framework/Debugger/DebuggerOverlay.cs"],
  "start_line": 350,
  "end_line": 1100,
  "constraints": ["只读分析", "所有结论提供原始文件行号"]
}
```

## 验证

```powershell
python -m unittest discover -s Tools/LocalAiWorker -p "test_*.py" -v
```

直接检查 MCP 握手时，可向 `server.py` 的标准输入逐行发送 JSON-RPC。协议消息必须各占一行，标准输出不会包含诊断日志。
