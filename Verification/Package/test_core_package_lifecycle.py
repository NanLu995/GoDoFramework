from __future__ import annotations

import importlib.util
import tempfile
import unittest
import zipfile
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
LIFECYCLE_SCRIPT = (
    REPOSITORY_ROOT / "Verification" / "Package" / "verify_core_package_lifecycle.py"
)


def load_lifecycle_module():
    spec = importlib.util.spec_from_file_location(
        "godo_core_package_lifecycle", LIFECYCLE_SCRIPT
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载生命周期验证脚本：{LIFECYCLE_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class CorePackageLifecycleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.lifecycle = load_lifecycle_module()

    def test_project_config_keeps_install_states_explicit(self) -> None:
        clean = self.lifecycle.render_project_config()
        installed = self.lifecycle.render_project_config(
            self.lifecycle.RUNTIME_SCENE_PATH
        )
        removed = self.lifecycle.render_project_config(plugin_enabled=False)

        self.assertNotIn("[autoload]", clean)
        self.assertIn(self.lifecycle.PLUGIN_CONFIG_PATH, clean)
        self.assertIn(self.lifecycle.RUNTIME_SCENE_PATH, installed)
        self.assertNotIn("[editor_plugins]", removed)

    def test_archive_extraction_rejects_parent_traversal(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godo-lifecycle-test-") as temporary:
            root = Path(temporary)
            archive = root / "unsafe.zip"
            project = root / "project"
            project.mkdir()
            with zipfile.ZipFile(archive, mode="w") as package:
                package.writestr("../escaped.txt", "unsafe")

            with self.assertRaisesRegex(RuntimeError, "不安全路径"):
                self.lifecycle.extract_core_archive(archive, project)
            self.assertFalse((root / "escaped.txt").exists())

    def test_framework_removal_is_limited_to_exact_project_child(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godo-lifecycle-test-") as temporary:
            root = Path(temporary)
            framework = root / "addons" / "godo_framework"
            framework.mkdir(parents=True)
            marker = framework / "marker.txt"
            marker.write_text("fixture", encoding="utf-8")

            self.lifecycle.remove_framework_directory(root)

            self.assertFalse(framework.exists())
            self.assertTrue(root.exists())

    def test_replacement_removes_synthetic_obsolete_file(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godo-lifecycle-test-") as temporary:
            root = Path(temporary)
            project = root / "project"
            framework = project / "addons" / "godo_framework"
            framework.mkdir(parents=True)
            obsolete = framework / "LegacyOnly.cs"
            obsolete.write_text("old", encoding="utf-8")
            archive = root / "core.zip"
            with zipfile.ZipFile(archive, mode="w") as package:
                package.writestr(
                    "addons/godo_framework/plugin.cfg", "[plugin]\nname=\"GoDo\"\n"
                )

            self.lifecycle.replace_framework_directory(archive, project)

            self.assertFalse(obsolete.exists())
            self.assertTrue((framework / "plugin.cfg").is_file())


if __name__ == "__main__":
    unittest.main()
