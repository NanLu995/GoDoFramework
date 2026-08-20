from __future__ import annotations

import importlib.util
import io
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parent / "api_compatibility.py"


def load_module():
    spec = importlib.util.spec_from_file_location("godo_api_compatibility", SCRIPT_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载 API 兼容脚本：{SCRIPT_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class ApiCompatibilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.compat = load_module()

    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory(prefix="godo-api-compat-")
        self.root = Path(self.temporary_directory.name)

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write_api(self, blocks: str) -> Path:
        api_root = self.root / "api"
        api_root.mkdir(exist_ok=True)
        (api_root / "GoDo.Sample.yml").write_text(
            "### YamlMime:ManagedReference\nitems:\n" + blocks,
            encoding="utf-8",
        )
        return api_root

    def test_collects_plain_quoted_and_folded_signatures(self) -> None:
        api_root = self.write_api(
            """- uid: GoDo
  type: Namespace
- uid: GoDo.Sample
  type: Class
  syntax:
    content: >-
      [ScriptPath(\"res://sample.cs\")]

      public sealed class Sample
- uid: GoDo.Sample.Name
  type: Property
  syntax:
    content: public string Name { get; }
- uid: GoDo.Sample.Read``1(System.String)
  type: Method
  syntax:
    content: 'T Read<T>(string key) where T : class'
"""
        )

        items = self.compat.collect_api_items(api_root)

        self.assertEqual(3, len(items))
        self.assertEqual("public sealed class Sample", items[0].signature)
        self.assertEqual("public string Name { get; }", items[1].signature)
        self.assertEqual("T Read<T>(string key) where T : class", items[2].signature)

    def test_addition_is_compatible(self) -> None:
        baseline = (self.compat.ApiItem("GoDo.Sample", "Class", "public class Sample"),)
        current = baseline + (
            self.compat.ApiItem("GoDo.Sample.Value", "Property", "public int Value { get; }"),
        )

        report = self.compat.compare_api(baseline, current)

        self.assertTrue(report.is_compatible)
        self.assertEqual((current[1],), report.added)

    def test_removal_and_signature_change_are_breaking(self) -> None:
        baseline = (
            self.compat.ApiItem("GoDo.Sample", "Class", "public class Sample"),
            self.compat.ApiItem("GoDo.Sample.Read", "Method", "public int Read()"),
        )
        current = (
            self.compat.ApiItem("GoDo.Sample.Read", "Method", "public string Read()"),
        )

        report = self.compat.compare_api(baseline, current)

        self.assertFalse(report.is_compatible)
        self.assertEqual((baseline[0],), report.removed)
        self.assertEqual("GoDo.Sample.Read", report.changed[0].uid)

    def test_baseline_round_trip_is_deterministic(self) -> None:
        baseline_path = self.root / "baseline.json"
        items = (
            self.compat.ApiItem("GoDo.B", "Class", "public class B"),
            self.compat.ApiItem("GoDo.A", "Struct", "public struct A"),
        )

        self.compat.write_baseline(baseline_path, items, "1.2.3")
        version, loaded = self.compat.read_baseline(baseline_path)

        self.assertEqual("1.2.3", version)
        self.assertEqual(tuple(sorted(items, key=lambda item: item.uid)), loaded)
        first = baseline_path.read_bytes()
        self.compat.write_baseline(baseline_path, items, "1.2.3")
        self.assertEqual(first, baseline_path.read_bytes())

    def test_check_command_allows_additions_and_rejects_signature_changes(self) -> None:
        baseline_path = self.root / "baseline.json"
        report_path = self.root / "report.json"
        original = self.compat.ApiItem("GoDo.Sample", "Class", "public class Sample")
        self.compat.write_baseline(baseline_path, (original,), "1.0.0")
        api_root = self.write_api(
            """- uid: GoDo.Sample
  type: Class
  syntax:
    content: public class Sample
"""
        )

        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            exact_result = self.compat.main(
                [
                    "check",
                    "--api-root",
                    str(api_root),
                    "--baseline",
                    str(baseline_path),
                    "--report",
                    str(report_path),
                ]
            )
        self.assertEqual(0, exact_result)

        self.write_api(
            """- uid: GoDo.Sample
  type: Class
  syntax:
    content: public class Sample
- uid: GoDo.Sample.Value
  type: Property
  syntax:
    content: public int Value { get; }
"""
        )
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            addition_result = self.compat.main(
                [
                    "check",
                    "--api-root",
                    str(api_root),
                    "--baseline",
                    str(baseline_path),
                    "--report",
                    str(report_path),
                ]
            )
        self.assertEqual(0, addition_result)

        self.write_api(
            """- uid: GoDo.Sample
  type: Class
  syntax:
    content: public sealed class Sample
"""
        )
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            breaking_result = self.compat.main(
                [
                    "check",
                    "--api-root",
                    str(api_root),
                    "--baseline",
                    str(baseline_path),
                    "--report",
                    str(report_path),
                ]
            )
        self.assertEqual(1, breaking_result)


if __name__ == "__main__":
    unittest.main()
