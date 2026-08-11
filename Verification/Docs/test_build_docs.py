from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "Docs"))

import build_docs  # noqa: E402


class CoverageValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.contract = self.root / "addons" / "godo_framework" / "Runtime" / "Sample" / "USAGE.md"
        self.contract.parent.mkdir(parents=True)
        self.contract.write_text("# Sample\n", encoding="utf-8")
        self.manual_root = self.root / "Docs" / "Manual"
        (self.manual_root / "zh-cn").mkdir(parents=True)
        self.coverage_path = self.root / "Docs" / "coverage.json"

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write_coverage(self, entries: dict[str, object]) -> None:
        self.coverage_path.write_text(
            json.dumps({"allow_pending": True, "entries": entries}),
            encoding="utf-8",
        )

    def pending_entry(self) -> dict[str, object]:
        digest = hashlib.sha256(self.contract.read_bytes()).hexdigest()
        return {
            "contract": "addons/godo_framework/Runtime/Sample/USAGE.md",
            "status": "pending",
            "reason": "等待用户手册。",
            "api_reference_status": "pending",
            "api_reference_reason": "等待 API Reference 审计。",
            "reviewed_contract_hash": f"sha256:{digest}",
        }

    def validate(self) -> None:
        build_docs.validate_coverage(
            repository_root=self.root,
            coverage_path=self.coverage_path,
            manual_root=self.manual_root,
        )

    def test_accepts_registered_contract_with_current_hash(self) -> None:
        self.write_coverage({"sample": self.pending_entry()})

        self.validate()

    def test_rejects_unregistered_contract(self) -> None:
        self.write_coverage({})

        with self.assertRaisesRegex(RuntimeError, "新增技术契约尚未登记"):
            self.validate()

    def test_rejects_changed_contract_hash(self) -> None:
        entry = self.pending_entry()
        self.contract.write_text("# Changed\n", encoding="utf-8")
        self.write_coverage({"sample": entry})

        with self.assertRaisesRegex(RuntimeError, "技术契约已变化"):
            self.validate()

    def test_rejects_missing_partial_manual_page(self) -> None:
        entry = self.pending_entry()
        entry["manual_pages"] = ["missing.md"]
        self.write_coverage({"sample": entry})

        with self.assertRaisesRegex(RuntimeError, "中文用户手册不存在"):
            self.validate()

    def test_rejects_missing_api_reference_status(self) -> None:
        entry = self.pending_entry()
        del entry["api_reference_status"]
        self.write_coverage({"sample": entry})

        with self.assertRaisesRegex(RuntimeError, "api_reference_status"):
            self.validate()

    def test_rejects_pending_api_reference_without_reason(self) -> None:
        entry = self.pending_entry()
        del entry["api_reference_reason"]
        self.write_coverage({"sample": entry})

        with self.assertRaisesRegex(RuntimeError, "api_reference_reason"):
            self.validate()


class ApiReferenceAuditTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.api_root = Path(self.temporary_directory.name)
        self.coverage_entry = {
            "contract": "addons/godo_framework/Runtime/Sample/USAGE.md",
            "api_reference_status": "pending",
        }

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write_api(self, uid: str = "GoDo.Sample.TryRead(System.String)") -> None:
        (self.api_root / "GoDo.Sample.yml").write_text(
            f"""items:
- uid: {uid}
  type: Method
  source:
    remote:
      path: addons/godo_framework/Runtime/Sample/Sample.cs
  summary: 尝试读取值。
  syntax:
    content: public bool TryRead(string key)
    parameters:
    - id: key
      type: System.String
    return:
      type: System.Boolean
""",
            encoding="utf-8",
        )

    def collect(self) -> list[build_docs.ApiReferenceIssue]:
        issues, _ = build_docs.collect_api_reference_issues(
            self.api_root,
            {"sample": self.coverage_entry},
        )
        return issues

    def test_pending_module_reports_details_without_blocking(self) -> None:
        self.write_api()

        issues = self.collect()

        self.assertEqual(
            {"missing-param", "missing-returns"},
            {issue.rule for issue in issues},
        )
        self.assertEqual([], build_docs.blocking_api_reference_issues(issues))

    def test_verified_module_blocks_on_missing_details(self) -> None:
        self.coverage_entry["api_reference_status"] = "verified"
        self.write_api()

        issues = self.collect()

        self.assertEqual(issues, build_docs.blocking_api_reference_issues(issues))

    def test_generated_godot_name_type_is_always_blocking(self) -> None:
        self.write_api("GoDo.Sample.MethodName")

        issues = self.collect()

        self.assertEqual(["generated-godot-api"], [issue.rule for issue in issues])
        self.assertEqual(issues, build_docs.blocking_api_reference_issues(issues))


class NavigationValidationTests(unittest.TestCase):
    def test_rejects_manual_page_missing_from_navigation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "index.md"
            source.write_text("# Home\n", encoding="utf-8")
            navigation_path = root / "navigation.json"
            navigation_path.write_text(
                json.dumps({"home": "missing.md", "sections": []}),
                encoding="utf-8",
            )
            page = build_docs.Page(
                logical_id="index",
                locale="zh-cn",
                title="Home",
                source=source,
                destination=Path("index.md"),
                group="Manual",
            )

            with self.assertRaisesRegex(RuntimeError, "导航配置与用户手册不一致"):
                build_docs.load_navigation(
                    "zh-cn", [page], navigation_path=navigation_path
                )


if __name__ == "__main__":
    unittest.main()
