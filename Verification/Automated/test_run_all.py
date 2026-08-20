from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
RUN_ALL_SCRIPT = REPOSITORY_ROOT / "Verification" / "Automated" / "run_all.py"


def load_run_all_module():
    spec = importlib.util.spec_from_file_location("godo_run_all", RUN_ALL_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载自动回归脚本：{RUN_ALL_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RunAllIsolationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.run_all = load_run_all_module()

    def test_isolated_editor_project_copies_only_declared_inputs(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godo-run-all-test-") as temporary:
            root = Path(temporary)
            source = root / "source"
            target = root / "target"
            for relative_path in self.run_all.ISOLATED_EDITOR_PROJECT_ITEMS:
                path = source / relative_path
                if relative_path.suffix:
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text(relative_path.as_posix(), encoding="utf-8")
                else:
                    path.mkdir(parents=True, exist_ok=True)
                    (path / "fixture.txt").write_text("fixture", encoding="utf-8")
            editor_layout = source / ".godot" / "editor" / "editor_layout.cfg"
            editor_layout.parent.mkdir(parents=True)
            editor_layout.write_text("open_scenes=fixture", encoding="utf-8")

            self.run_all.create_isolated_editor_project(target, source)

            for relative_path in self.run_all.ISOLATED_EDITOR_PROJECT_ITEMS:
                self.assertTrue((target / relative_path).exists())
            self.assertFalse((target / ".godot").exists())
            project_config = (target / "project.godot").read_text(encoding="utf-8")
            self.assertIn("[phantom_camera]", project_config)
            self.assertIn("updater/updater_mode=0", project_config)

    def test_isolated_editor_project_rejects_missing_required_file(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godo-run-all-test-") as temporary:
            root = Path(temporary)
            with self.assertRaisesRegex(RuntimeError, "缺少必需文件"):
                self.run_all.create_isolated_editor_project(
                    root / "target",
                    root / "empty-source",
                )


if __name__ == "__main__":
    unittest.main()
