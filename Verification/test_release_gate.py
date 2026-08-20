from __future__ import annotations

import importlib.util
import io
import subprocess
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest.mock import Mock


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
RELEASE_GATE_SCRIPT = REPOSITORY_ROOT / "Verification" / "release_gate.py"


def load_release_gate_module():
    spec = importlib.util.spec_from_file_location("godo_release_gate", RELEASE_GATE_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载发布门禁脚本：{RELEASE_GATE_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ReleaseGateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.gate = load_release_gate_module()

    def test_default_selection_uses_canonical_order(self) -> None:
        self.assertEqual(self.gate.STAGE_ORDER, self.gate.select_stages(None))

    def test_explicit_selection_is_ordered_and_deduplicated(self) -> None:
        selected = self.gate.select_stages(
            ["docs", "debug-build", "docs", "release-tests"]
        )
        self.assertEqual(("release-tests", "debug-build", "docs"), selected)

    def test_commands_delegate_to_existing_entry_points(self) -> None:
        godot = Path("C:/tools/godot.exe")
        output = Path("C:/temp/datatable-export")
        commands = self.gate.create_stage_commands(godot, 75, output)

        self.assertIn("Verification\\Automated\\run_all.py", commands["regressions"][1])
        self.assertIn("Docs\\build_docs.py", commands["docs"][1])
        self.assertIn(
            "Verification\\ApiCompatibility\\api_compatibility.py",
            commands["api-compatibility"][1],
        )
        self.assertIn(
            "Verification\\ApiCompatibility\\test_api_compatibility.py",
            commands["api-compatibility-tests"][1],
        )
        self.assertIn("release\\release.py", commands["packages"][1])
        self.assertIn(
            "Verification\\Package\\verify_core_package_lifecycle.py",
            commands["package-lifecycle"][1],
        )
        self.assertEqual("unittest", commands["release-tests"][2])
        self.assertTrue(commands["debug-build"][2].endswith("GoDoFramework.csproj"))
        self.assertTrue(commands["release-build"][2].endswith("GoDoFramework.csproj"))
        self.assertEqual("Debug", commands["debug-build"][-1])
        self.assertEqual("Release", commands["release-build"][-1])
        self.assertEqual("75", commands["regressions"][-1])
        self.assertEqual("75", commands["package-lifecycle"][-1])
        self.assertEqual(str(output), commands["datatable-export-release"][-1])

    def test_failure_stops_later_stages(self) -> None:
        runner = Mock(
            side_effect=[
                subprocess.CompletedProcess([], 0),
                subprocess.CompletedProcess([], 7),
            ]
        )
        commands = {
            "release-tests": ["first"],
            "datatable-generated": ["second"],
            "debug-build": ["third"],
        }

        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            result = self.gate.run_stages(
                ("release-tests", "datatable-generated", "debug-build"),
                commands,
                runner,
            )

        self.assertEqual(7, result)
        self.assertEqual(2, runner.call_count)


if __name__ == "__main__":
    unittest.main()
