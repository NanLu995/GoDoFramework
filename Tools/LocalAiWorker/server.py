#!/usr/bin/env python3
"""Read-only MCP bridge from Codex to a local LM Studio model."""

from __future__ import annotations

import json
import os
import re
import sys
import time
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import HTTPRedirectHandler, Request, build_opener

sys.stdin.reconfigure(encoding="utf-8", newline="")
sys.stdout.reconfigure(encoding="utf-8", newline="\n")

SERVER_NAME = "godo-local-ai-worker"
SERVER_VERSION = "0.1.0"
PROTOCOL_VERSION = "2025-06-18"
PROJECT_ROOT = Path(__file__).resolve().parents[2]
SYSTEM_PROMPT_PATH = Path(__file__).with_name("system_prompt.txt")
LM_STUDIO_BASE_URL = "http://127.0.0.1:1234"
MODEL_NAME_CONTAINS = os.environ.get(
    "GODO_LOCAL_AI_MODEL", "qwen2.5-coder-14b"
).strip().casefold() or "qwen2.5-coder-14b"

MAX_FILES = 12
MAX_FILE_CHARS = 80_000
MAX_TOTAL_CHARS = 240_000
MAX_DIFF_CHARS = 180_000
MAX_LOG_CHARS = 180_000
MAX_RPC_CHARS = 300_000
MAX_GOAL_CHARS = 1_000
MAX_CONSTRAINTS = 8
MAX_CONSTRAINT_CHARS = 500
MAX_OUTPUT_TOKENS = 768
MAX_FINDINGS = 2
MODEL_TIMEOUT_SECONDS = 120.0
HEALTH_TIMEOUT_SECONDS = 1.0
HEALTH_SUCCESS_TTL_SECONDS = 30.0
HEALTH_FAILURE_TTL_SECONDS = 60.0

DENIED_DIRECTORY_NAMES = {
    ".git",
    ".godot",
    ".artifacts",
    "bin",
    "obj",
    "release",
}
DENIED_FILE_NAMES = {
    ".env",
    "secrets.json",
    "credentials.json",
}
DENIED_SUFFIXES = {
    ".key",
    ".pem",
    ".pfx",
    ".p12",
    ".keystore",
}
ALLOWED_SUFFIXES = {
    ".cs",
    ".csproj",
    ".diff",
    ".godot",
    ".json",
    ".log",
    ".md",
    ".patch",
    ".res",
    ".sln",
    ".toml",
    ".tres",
    ".tscn",
    ".txt",
    ".xml",
}

STATUS_VALUES = {"complete", "partial", "blocked"}
SEVERITY_VALUES = {"critical", "high", "medium", "low", "info"}
SENSITIVE_CONTENT_PATTERNS = (
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\bgithub_pat_[A-Za-z0-9_]{20,}\b"),
    re.compile(r"\bghp_[A-Za-z0-9]{20,}\b"),
    re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{20,}\b"),
    re.compile(r"\bsk-[A-Za-z0-9_-]{20,}\b"),
)
_health_cache: tuple[float, dict[str, Any]] | None = None


class NoRedirectHandler(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # noqa: ANN001
        return None


HTTP_OPENER = build_opener(NoRedirectHandler())


def _json_response(request_id: Any, result: dict[str, Any]) -> dict[str, Any]:
    return {"jsonrpc": "2.0", "id": request_id, "result": result}


def _json_error(request_id: Any, code: int, message: str) -> dict[str, Any]:
    return {
        "jsonrpc": "2.0",
        "id": request_id,
        "error": {"code": code, "message": message},
    }


def _tool_result(payload: dict[str, Any], is_error: bool = False) -> dict[str, Any]:
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    return {
        "content": [{"type": "text", "text": encoded}],
        "structuredContent": payload,
        "isError": is_error,
    }


def _unavailable(reason: str) -> dict[str, Any]:
    return {
        "status": "blocked",
        "summary": "本地助手不可用，Codex 应立即自行完成当前任务。",
        "findings": [],
        "unknowns": [reason[:300]],
        "suggested_checks": [],
    }


def _is_reparse_point(path: Path) -> bool:
    try:
        stat_result = path.lstat()
    except OSError:
        return True
    attributes = getattr(stat_result, "st_file_attributes", 0)
    reparse_flag = getattr(__import__("stat"), "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return path.is_symlink() or bool(attributes & reparse_flag)


def resolve_project_file(relative_path: str, project_root: Path = PROJECT_ROOT) -> Path:
    if not isinstance(relative_path, str) or not relative_path.strip():
        raise ValueError("文件路径不能为空。")
    if "\x00" in relative_path:
        raise ValueError("文件路径包含非法字符。")

    requested = Path(relative_path)
    if requested.is_absolute() or requested.drive or requested.anchor:
        raise ValueError("只接受项目相对路径。")
    if ".." in requested.parts:
        raise ValueError("文件路径不能包含 '..'。")

    root = project_root.resolve(strict=True)
    current = root
    for part in requested.parts:
        if part in {"", "."}:
            continue
        if part.casefold() in DENIED_DIRECTORY_NAMES:
            raise ValueError(f"禁止访问目录：{part}")
        current = current / part
        if current.exists() and _is_reparse_point(current):
            raise ValueError("禁止通过符号链接或 reparse point 访问文件。")

    resolved = current.resolve(strict=True)
    try:
        resolved.relative_to(root)
    except ValueError as exc:
        raise ValueError("文件路径超出项目根目录。") from exc

    if not resolved.is_file():
        raise ValueError("目标不是普通文件。")
    name = resolved.name.casefold()
    if name in DENIED_FILE_NAMES or name.startswith(".env") or name.startswith("secrets."):
        raise ValueError("禁止访问敏感配置文件。")
    if resolved.suffix.casefold() in DENIED_SUFFIXES:
        raise ValueError("禁止访问密钥或证书文件。")
    if resolved.suffix.casefold() not in ALLOWED_SUFFIXES:
        raise ValueError("文件类型不在只读分析白名单中。")
    return resolved


def _require_text(arguments: dict[str, Any], name: str, maximum: int) -> str:
    value = arguments.get(name)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{name} 必须是非空字符串。")
    if len(value) > maximum:
        raise ValueError(f"{name} 超过长度限制。")
    return value.strip()


def _optional_constraints(arguments: dict[str, Any]) -> list[str]:
    values = arguments.get("constraints", [])
    if not isinstance(values, list) or len(values) > MAX_CONSTRAINTS:
        raise ValueError("constraints 必须是受限长度的字符串数组。")
    result: list[str] = []
    for value in values:
        if not isinstance(value, str) or not value.strip() or len(value) > MAX_CONSTRAINT_CHARS:
            raise ValueError("constraint 必须是非空且受限长度的字符串。")
        result.append(value.strip())
    return result


def _number_lines(content: str, start_line: int = 1) -> str:
    return "\n".join(
        f"{index:06d}: {line}"
        for index, line in enumerate(content.splitlines(), start_line)
    )


def _reject_sensitive_content(content: str) -> None:
    if any(pattern.search(content) for pattern in SENSITIVE_CONTENT_PATTERNS):
        raise ValueError("输入包含疑似密钥或 Token，已拒绝发送给本地模型。")


def _read_files(arguments: dict[str, Any]) -> tuple[list[dict[str, str]], set[str]]:
    values = arguments.get("files")
    if not isinstance(values, list) or not 1 <= len(values) <= MAX_FILES:
        raise ValueError(f"files 必须包含 1 到 {MAX_FILES} 个项目相对路径。")
    if len(values) != len(set(values)):
        raise ValueError("files 不能包含重复路径。")

    start_line = arguments.get("start_line")
    end_line = arguments.get("end_line")
    if (start_line is None) != (end_line is None):
        raise ValueError("start_line 和 end_line 必须同时提供。")
    if start_line is not None:
        if len(values) != 1:
            raise ValueError("行范围分析只支持单个文件。")
        if (
            not isinstance(start_line, int)
            or isinstance(start_line, bool)
            or start_line < 1
        ):
            raise ValueError("start_line 必须是大于等于 1 的整数。")
        if not isinstance(end_line, int) or isinstance(end_line, bool) or end_line < 1:
            raise ValueError("end_line 必须是大于等于 1 的整数。")
        if end_line < start_line:
            raise ValueError("end_line 不能小于 start_line。")

    total = 0
    documents: list[dict[str, str]] = []
    allowed_sources: set[str] = set()
    for relative in values:
        path = resolve_project_file(relative)
        content = path.read_text(encoding="utf-8-sig")
        content_start_line = 1
        if start_line is not None:
            lines = content.splitlines()
            if start_line > len(lines):
                raise ValueError(f"start_line 超出文件行数：{relative}")
            content = "\n".join(lines[start_line - 1 : end_line])
            content_start_line = start_line
        _reject_sensitive_content(content)
        if len(content) > MAX_FILE_CHARS:
            raise ValueError(f"单文件超过字符限制：{relative}")
        total += len(content)
        if total > MAX_TOTAL_CHARS:
            raise ValueError("文件总字符数超过限制。")
        normalized = path.relative_to(PROJECT_ROOT).as_posix()
        allowed_sources.add(normalized)
        documents.append(
            {"path": normalized, "content": _number_lines(content, content_start_line)}
        )
    return documents, allowed_sources


def _http_json(method: str, url: str, payload: dict[str, Any] | None, timeout: float) -> dict[str, Any]:
    data = None
    headers = {"Accept": "application/json"}
    if payload is not None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = Request(url, data=data, headers=headers, method=method)
    with HTTP_OPENER.open(request, timeout=timeout) as response:
        if response.geturl() != url:
            raise RuntimeError("本地服务返回了重定向。")
        body = response.read(MAX_TOTAL_CHARS + 1)
        if len(body) > MAX_TOTAL_CHARS:
            raise RuntimeError("本地服务响应过大。")
        value = json.loads(body.decode("utf-8"))
        if not isinstance(value, dict):
            raise RuntimeError("本地服务响应不是 JSON 对象。")
        return value


def _select_model(response: dict[str, Any]) -> str | None:
    models = response.get("models")
    if not isinstance(models, list):
        return None
    for model in models:
        if not isinstance(model, dict) or model.get("type") != "llm":
            continue
        key = model.get("key")
        display_name = model.get("display_name", "")
        if not isinstance(key, str):
            continue
        haystack = f"{key} {display_name}".casefold()
        if MODEL_NAME_CONTAINS in haystack:
            return key
    return None


def check_lm_studio(force: bool = False) -> dict[str, Any]:
    global _health_cache
    now = time.monotonic()
    if not force and _health_cache is not None and now < _health_cache[0]:
        return _health_cache[1]

    try:
        response = _http_json(
            "GET",
            f"{LM_STUDIO_BASE_URL}/api/v1/models",
            None,
            HEALTH_TIMEOUT_SECONDS,
        )
        model = _select_model(response)
        if model is None:
            result = {
                "available": False,
                "reason": f"未找到名称包含 '{MODEL_NAME_CONTAINS}' 的本地 LLM。",
            }
        else:
            result = {"available": True, "model": model}
    except (HTTPError, URLError, TimeoutError, OSError, ValueError, RuntimeError, json.JSONDecodeError) as exc:
        result = {"available": False, "reason": f"LM Studio 连接失败：{type(exc).__name__}"}

    ttl = HEALTH_SUCCESS_TTL_SECONDS if result["available"] else HEALTH_FAILURE_TTL_SECONDS
    _health_cache = (now + ttl, result)
    return result


def _build_prompt(tool_name: str, arguments: dict[str, Any]) -> tuple[str, set[str]]:
    goal = _require_text(arguments, "goal", MAX_GOAL_CHARS)
    constraints = _optional_constraints(arguments)
    sections = [
        f"任务类型：{tool_name}",
        f"目标：{goal}",
        "约束：" + ("；".join(constraints) if constraints else "只读分析；所有结论提供证据"),
    ]
    allowed_sources: set[str]

    if tool_name == "analyze_files":
        documents, allowed_sources = _read_files(arguments)
        for document in documents:
            sections.append(
                f"<document path={json.dumps(document['path'], ensure_ascii=False)}>\n"
                f"{document['content']}\n</document>"
            )
    elif tool_name == "review_diff":
        diff = _require_text(arguments, "diff", MAX_DIFF_CHARS)
        _reject_sensitive_content(diff)
        allowed_sources = {
            line[6:].strip()
            for line in diff.splitlines()
            if line.startswith("+++ b/") and line[6:].strip() != "/dev/null"
        }
        sections.append(f"<diff>\n{_number_lines(diff)}\n</diff>")
    elif tool_name == "summarize_test_log":
        log = _require_text(arguments, "log", MAX_LOG_CHARS)
        _reject_sensitive_content(log)
        allowed_sources = {"<test-log>"}
        sections.append(f"<test-log>\n{_number_lines(log)}\n</test-log>")
    else:
        raise ValueError("未知工具。")

    return "\n\n".join(sections), allowed_sources


def _extract_json(text: str) -> dict[str, Any]:
    stripped = text.strip()
    if stripped.startswith("```") and stripped.endswith("```"):
        first_newline = stripped.find("\n")
        stripped = stripped[first_newline + 1 : -3].strip()
    value = json.loads(stripped)
    if not isinstance(value, dict):
        raise ValueError("模型输出必须是 JSON 对象。")
    return value


def _bounded_strings(value: Any, maximum: int) -> list[str]:
    if not isinstance(value, list):
        raise ValueError("预期字符串数组。")
    result = []
    for item in value[:maximum]:
        if isinstance(item, str) and item.strip():
            result.append(item.strip()[:500])
    return result


def validate_model_result(value: dict[str, Any], allowed_sources: set[str]) -> dict[str, Any]:
    status = value.get("status")
    summary = value.get("summary")
    findings = value.get("findings")
    if status not in STATUS_VALUES or not isinstance(summary, str) or not isinstance(findings, list):
        raise ValueError("模型输出缺少必要字段。")

    validated_findings: list[dict[str, Any]] = []
    for finding in findings[:MAX_FINDINGS]:
        if not isinstance(finding, dict):
            continue
        severity = finding.get("severity")
        file_name = finding.get("file")
        line = finding.get("line")
        confidence = finding.get("confidence")
        required_text = ["category", "claim", "evidence", "reasoning", "suggestion"]
        if severity not in SEVERITY_VALUES or file_name not in allowed_sources:
            continue
        if not isinstance(line, int) or line < 1:
            continue
        if not isinstance(confidence, (int, float)) or isinstance(confidence, bool):
            continue
        if not 0 <= float(confidence) <= 1:
            continue
        if any(not isinstance(finding.get(key), str) or not finding[key].strip() for key in required_text):
            continue
        validated_findings.append(
            {
                "severity": severity,
                "category": finding["category"].strip()[:100],
                "claim": finding["claim"].strip()[:500],
                "file": file_name,
                "line": line,
                "evidence": finding["evidence"].strip()[:800],
                "reasoning": finding["reasoning"].strip()[:800],
                "suggestion": finding["suggestion"].strip()[:500],
                "confidence": round(float(confidence), 3),
            }
        )

    return {
        "status": status,
        "summary": summary.strip()[:800],
        "findings": validated_findings,
        "unknowns": _bounded_strings(value.get("unknowns", []), 8),
        "suggested_checks": _bounded_strings(value.get("suggested_checks", []), 8),
    }


def _mark_generation_failure() -> None:
    global _health_cache
    _health_cache = (
        time.monotonic() + HEALTH_FAILURE_TTL_SECONDS,
        {"available": False, "reason": "上一次本地模型请求失败，已临时熔断。"},
    )


def invoke_local_model(tool_name: str, arguments: dict[str, Any]) -> dict[str, Any]:
    try:
        prompt, allowed_sources = _build_prompt(tool_name, arguments)
    except ValueError as exc:
        return _unavailable(str(exc))

    health = check_lm_studio()
    if not health.get("available"):
        return _unavailable(str(health.get("reason", "未知连接错误。")))

    try:
        system_prompt = SYSTEM_PROMPT_PATH.read_text(encoding="utf-8-sig")
        response = _http_json(
            "POST",
            f"{LM_STUDIO_BASE_URL}/v1/chat/completions",
            {
                "model": health["model"],
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": prompt},
                ],
                "response_format": {
                    "type": "json_schema",
                    "json_schema": {
                        "name": "godo_local_analysis",
                        "strict": True,
                        "schema": OUTPUT_SCHEMA,
                    },
                },
                "stream": False,
                "temperature": 0.1,
                "max_tokens": MAX_OUTPUT_TOKENS,
            },
            MODEL_TIMEOUT_SECONDS,
        )
        choices = response.get("choices")
        if not isinstance(choices, list) or not choices:
            raise ValueError("本地模型响应缺少 choices。")
        if isinstance(choices[0], dict) and choices[0].get("finish_reason") == "length":
            raise ValueError("本地模型输出达到 Token 上限，JSON 可能不完整。")
        message = choices[0].get("message") if isinstance(choices[0], dict) else None
        text = message.get("content") if isinstance(message, dict) else None
        if not isinstance(text, str) or not text.strip():
            raise ValueError("本地模型没有返回文本结果。")
        return validate_model_result(_extract_json(text), allowed_sources)
    except json.JSONDecodeError:
        _mark_generation_failure()
        return _unavailable("本地模型响应或输出不是有效 JSON。")
    except ValueError as exc:
        _mark_generation_failure()
        return _unavailable(f"本地模型输出校验失败：{exc}")
    except (HTTPError, URLError, TimeoutError, OSError, RuntimeError) as exc:
        _mark_generation_failure()
        return _unavailable(f"本地模型请求失败：{type(exc).__name__}")


OUTPUT_SCHEMA = {
    "type": "object",
    "properties": {
        "status": {"type": "string", "enum": sorted(STATUS_VALUES)},
        "summary": {"type": "string"},
        "findings": {
            "type": "array",
            "maxItems": MAX_FINDINGS,
            "items": {
                "type": "object",
                "properties": {
                    "severity": {"type": "string", "enum": sorted(SEVERITY_VALUES)},
                    "category": {"type": "string"},
                    "claim": {"type": "string"},
                    "file": {"type": "string"},
                    "line": {"type": "integer", "minimum": 1},
                    "evidence": {"type": "string"},
                    "reasoning": {"type": "string"},
                    "suggestion": {"type": "string"},
                    "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                },
                "required": [
                    "severity",
                    "category",
                    "claim",
                    "file",
                    "line",
                    "evidence",
                    "reasoning",
                    "suggestion",
                    "confidence",
                ],
                "additionalProperties": False,
            },
        },
        "unknowns": {
            "type": "array",
            "maxItems": 8,
            "items": {"type": "string"},
        },
        "suggested_checks": {
            "type": "array",
            "maxItems": 8,
            "items": {"type": "string"},
        },
    },
    "required": ["status", "summary", "findings", "unknowns", "suggested_checks"],
    "additionalProperties": False,
}


def _tool_definitions() -> list[dict[str, Any]]:
    common = {
        "goal": {"type": "string", "maxLength": MAX_GOAL_CHARS},
        "constraints": {
            "type": "array",
            "maxItems": MAX_CONSTRAINTS,
            "items": {"type": "string", "maxLength": MAX_CONSTRAINT_CHARS},
        },
    }
    return [
        {
            "name": "analyze_files",
            "description": "只读分析明确指定的项目文件；仅在多文件扫描能节省主上下文时使用。失败时立即由 Codex 自行分析。",
            "inputSchema": {
                "type": "object",
                "properties": {
                    **common,
                    "files": {
                        "type": "array",
                        "minItems": 1,
                        "maxItems": MAX_FILES,
                        "items": {"type": "string"},
                    },
                    "start_line": {"type": "integer", "minimum": 1},
                    "end_line": {"type": "integer", "minimum": 1},
                },
                "required": ["goal", "files"],
                "additionalProperties": False,
            },
            "outputSchema": OUTPUT_SCHEMA,
        },
        {
            "name": "review_diff",
            "description": "只读审查调用方提供的 Diff，不运行 Git，也不读取额外文件。失败时立即由 Codex 自行审查。",
            "inputSchema": {
                "type": "object",
                "properties": {
                    **common,
                    "diff": {"type": "string", "maxLength": MAX_DIFF_CHARS},
                },
                "required": ["goal", "diff"],
                "additionalProperties": False,
            },
            "outputSchema": OUTPUT_SCHEMA,
        },
        {
            "name": "summarize_test_log",
            "description": "只读归纳调用方提供的编译或测试日志，不执行测试。失败时立即由 Codex 自行归纳。",
            "inputSchema": {
                "type": "object",
                "properties": {
                    **common,
                    "log": {"type": "string", "maxLength": MAX_LOG_CHARS},
                },
                "required": ["goal", "log"],
                "additionalProperties": False,
            },
            "outputSchema": OUTPUT_SCHEMA,
        },
    ]


def handle_request(message: dict[str, Any]) -> dict[str, Any] | None:
    request_id = message.get("id")
    method = message.get("method")
    if request_id is None:
        return None
    if method == "initialize":
        requested_version = message.get("params", {}).get("protocolVersion")
        version = requested_version if isinstance(requested_version, str) else PROTOCOL_VERSION
        return _json_response(
            request_id,
            {
                "protocolVersion": version,
                "capabilities": {"tools": {"listChanged": False}},
                "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
                "instructions": "只读本地分析；结果必须由 Codex 复核。服务不可用时立即回退。",
            },
        )
    if method == "ping":
        return _json_response(request_id, {})
    if method == "tools/list":
        return _json_response(request_id, {"tools": _tool_definitions()})
    if method == "tools/call":
        params = message.get("params")
        if not isinstance(params, dict):
            return _json_error(request_id, -32602, "Invalid params")
        name = params.get("name")
        if name not in {"analyze_files", "review_diff", "summarize_test_log"}:
            return _json_error(request_id, -32602, "Unknown tool")
        arguments = params.get("arguments", {})
        if not isinstance(arguments, dict):
            return _json_response(request_id, _tool_result(_unavailable("工具参数不是 JSON 对象。"), True))
        try:
            payload = invoke_local_model(name, arguments)
            return _json_response(request_id, _tool_result(payload, payload["status"] == "blocked"))
        except ValueError as exc:
            return _json_response(request_id, _tool_result(_unavailable(str(exc)), True))
    return _json_error(request_id, -32601, "Method not found")


def main() -> int:
    for raw_line in sys.stdin:
        try:
            if len(raw_line) > MAX_RPC_CHARS:
                raise ValueError("JSON-RPC message exceeds the size limit")
            message = json.loads(raw_line)
            if not isinstance(message, dict):
                raise ValueError("JSON-RPC message must be an object")
            response = handle_request(message)
        except (json.JSONDecodeError, ValueError) as exc:
            response = _json_error(None, -32700, str(exc)[:200])
        if response is not None:
            sys.stdout.write(json.dumps(response, ensure_ascii=False, separators=(",", ":")) + "\n")
            sys.stdout.flush()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
