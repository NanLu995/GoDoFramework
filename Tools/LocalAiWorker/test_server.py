import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from urllib.error import URLError

import server


class PathBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_directory.name)
        (self.root / "Source").mkdir()
        (self.root / "Source" / "Example.cs").write_text("class Example {}", encoding="utf-8")
        (self.root / ".git").mkdir()
        (self.root / ".git" / "config.txt").write_text("secret", encoding="utf-8")
        (self.root / ".env").write_text("TOKEN=secret", encoding="utf-8")
        (self.root / "image.png").write_bytes(b"png")

    def tearDown(self):
        self.temp_directory.cleanup()

    def test_accepts_normal_relative_source_file(self):
        resolved = server.resolve_project_file("Source/Example.cs", self.root)
        self.assertEqual(resolved, (self.root / "Source" / "Example.cs").resolve())

    def test_rejects_parent_traversal(self):
        with self.assertRaises(ValueError):
            server.resolve_project_file("../outside.cs", self.root)

    def test_rejects_absolute_path(self):
        with self.assertRaises(ValueError):
            server.resolve_project_file(str((self.root / "Source" / "Example.cs").resolve()), self.root)

    def test_rejects_denied_directory(self):
        with self.assertRaises(ValueError):
            server.resolve_project_file(".git/config.txt", self.root)

    def test_rejects_sensitive_file(self):
        with self.assertRaises(ValueError):
            server.resolve_project_file(".env", self.root)

    def test_rejects_non_allowlisted_extension(self):
        with self.assertRaises(ValueError):
            server.resolve_project_file("image.png", self.root)


class HealthAndSchemaTests(unittest.TestCase):
    def setUp(self):
        server._health_cache = None

    def test_unavailable_service_returns_compact_fallback(self):
        with patch.object(server, "_http_json", side_effect=URLError("offline")):
            result = server.check_lm_studio(force=True)
        self.assertFalse(result["available"])
        self.assertNotIn("offline", result["reason"])

    def test_selects_only_expected_llm(self):
        response = {
            "models": [
                {"type": "embedding", "key": "qwen2.5-coder-14b-embed"},
                {"type": "llm", "key": "publisher/qwen2.5-coder-14b-instruct"},
            ]
        }
        self.assertEqual(
            server._select_model(response),
            "publisher/qwen2.5-coder-14b-instruct",
        )

    def test_drops_finding_with_unprovided_source(self):
        value = {
            "status": "complete",
            "summary": "done",
            "findings": [
                {
                    "severity": "high",
                    "category": "security",
                    "claim": "claim",
                    "file": "outside.txt",
                    "line": 1,
                    "evidence": "evidence",
                    "reasoning": "reasoning",
                    "suggestion": "suggestion",
                    "confidence": 0.9,
                }
            ],
            "unknowns": [],
            "suggested_checks": [],
        }
        result = server.validate_model_result(value, {"Source/Example.cs"})
        self.assertEqual(result["findings"], [])

    def test_rejects_obvious_secret_before_model_request(self):
        with self.assertRaises(ValueError):
            server._reject_sensitive_content("token = sk-123456789012345678901234567890")

    def test_limits_findings_to_token_efficient_maximum(self):
        finding = {
            "severity": "low",
            "category": "test",
            "claim": "claim",
            "file": "Source/Example.cs",
            "line": 1,
            "evidence": "evidence",
            "reasoning": "reasoning",
            "suggestion": "suggestion",
            "confidence": 0.8,
        }
        value = {
            "status": "complete",
            "summary": "done",
            "findings": [finding.copy() for _ in range(4)],
            "unknowns": [],
            "suggested_checks": [],
        }
        result = server.validate_model_result(value, {"Source/Example.cs"})
        self.assertEqual(len(result["findings"]), server.MAX_FINDINGS)

    def test_valid_mocked_model_response_completes_once(self):
        model_result = {
            "status": "complete",
            "summary": "未发现问题。",
            "findings": [],
            "unknowns": [],
            "suggested_checks": [],
        }
        response = {
            "choices": [
                {"message": {"content": json.dumps(model_result, ensure_ascii=False)}}
            ]
        }
        arguments = {
            "goal": "验证一次分析请求",
            "files": ["Tools/LocalAiWorker/README.md"],
        }
        with patch.object(
            server,
            "check_lm_studio",
            return_value={"available": True, "model": "qwen-test"},
        ), patch.object(server, "_http_json", return_value=response) as request:
            result = server.invoke_local_model("analyze_files", arguments)
        self.assertEqual(result["status"], "complete")
        self.assertEqual(request.call_count, 1)
        payload = request.call_args.args[2]
        self.assertEqual(payload["max_tokens"], 768)
        self.assertEqual(payload["response_format"]["type"], "json_schema")
        self.assertNotIn("store", payload)
        self.assertNotIn("context_length", payload)
        self.assertNotIn("integrations", payload)

    def test_token_limit_response_fails_closed(self):
        response = {
            "choices": [
                {
                    "finish_reason": "length",
                    "message": {"content": '{"status":"complete"'},
                }
            ]
        }
        arguments = {
            "goal": "验证截断失败关闭",
            "files": ["Tools/LocalAiWorker/README.md"],
        }
        with patch.object(
            server,
            "check_lm_studio",
            return_value={"available": True, "model": "qwen-test"},
        ), patch.object(server, "_http_json", return_value=response):
            result = server.invoke_local_model("analyze_files", arguments)
        self.assertEqual(result["status"], "blocked")

    def test_analyze_file_range_preserves_original_line_numbers(self):
        arguments = {
            "goal": "验证大文件分段分析",
            "files": ["addons/godo_framework/Debugger/DebuggerOverlay.cs"],
            "start_line": 421,
            "end_line": 424,
        }

        prompt, allowed_sources = server._build_prompt("analyze_files", arguments)

        self.assertEqual(
            allowed_sources,
            {"addons/godo_framework/Debugger/DebuggerOverlay.cs"},
        )
        self.assertIn("000421:             !IsInstanceValid(_titleLabel)", prompt)
        self.assertIn("000424:             !IsInstanceValid(_systemDashboard)", prompt)
        self.assertNotIn("000420:", prompt)
        self.assertNotIn("000425:", prompt)

    def test_input_validation_failure_keeps_precise_reason_without_circuit_breaker(self):
        arguments = {
            "goal": "验证非法行范围",
            "files": ["addons/godo_framework/Debugger/DebuggerOverlay.cs"],
            "start_line": 20,
            "end_line": 10,
        }

        with patch.object(server, "check_lm_studio") as health, patch.object(
            server,
            "_mark_generation_failure",
        ) as mark_failure:
            result = server.invoke_local_model("analyze_files", arguments)

        self.assertEqual(result["status"], "blocked")
        self.assertIn("end_line 不能小于 start_line", result["unknowns"][0])
        health.assert_not_called()
        mark_failure.assert_not_called()

    def test_oversized_file_requests_a_line_range_without_circuit_breaker(self):
        arguments = {
            "goal": "验证超大文件提示",
            "files": ["addons/godo_framework/Debugger/DebuggerOverlay.cs"],
        }

        with patch.object(server, "check_lm_studio") as health, patch.object(
            server,
            "_mark_generation_failure",
        ) as mark_failure:
            result = server.invoke_local_model("analyze_files", arguments)

        self.assertEqual(result["status"], "blocked")
        self.assertIn("单文件超过字符限制", result["unknowns"][0])
        health.assert_not_called()
        mark_failure.assert_not_called()


class McpProtocolTests(unittest.TestCase):
    def test_stdio_initialize_and_tool_list(self):
        script = Path(server.__file__)
        messages = [
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "test", "version": "1"}},
            },
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            {"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}},
        ]
        process = subprocess.run(
            [sys.executable, str(script)],
            input="\n".join(json.dumps(message) for message in messages) + "\n",
            text=True,
            encoding="utf-8",
            capture_output=True,
            timeout=5,
            check=True,
        )
        responses = [json.loads(line) for line in process.stdout.splitlines()]
        self.assertEqual(responses[0]["result"]["serverInfo"]["name"], server.SERVER_NAME)
        self.assertEqual(
            [tool["name"] for tool in responses[1]["result"]["tools"]],
            ["analyze_files", "review_diff", "summarize_test_log"],
        )
        analyze_schema = responses[1]["result"]["tools"][0]["inputSchema"]
        self.assertIn("start_line", analyze_schema["properties"])
        self.assertIn("end_line", analyze_schema["properties"])
        self.assertEqual(process.stderr, "")


if __name__ == "__main__":
    unittest.main()
