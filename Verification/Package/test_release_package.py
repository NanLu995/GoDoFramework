from __future__ import annotations

import importlib.util
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
RELEASE_SCRIPT = REPOSITORY_ROOT / "release" / "release.py"


def load_release_module():
    spec = importlib.util.spec_from_file_location("godo_release", RELEASE_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载发布脚本：{RELEASE_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ReleasePackageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.release = load_release_module()

    def test_package_file_sets_are_isolated(self) -> None:
        framework_license = "addons/godo_framework/LICENSE"
        core = self._relative_paths(self.release.collect_release_files("core"))
        guide = self._relative_paths(self.release.collect_release_files("guide-input"))
        phantom = self._relative_paths(self.release.collect_release_files("phantom-camera"))
        friflo = self._relative_paths(self.release.collect_release_files("friflo-ecs"))

        self.assertTrue(core)
        self.assertTrue(guide)
        self.assertTrue(phantom)
        self.assertTrue(friflo)
        self.assertIn(framework_license, core)
        self.assertIn(framework_license, guide)
        self.assertIn(framework_license, phantom)
        self.assertIn(framework_license, friflo)
        self.assertFalse(any("/Integrations/" in path for path in core))
        self.assertTrue(
            all(path == framework_license or "/Integrations/GuideInput/" in path for path in guide)
        )
        self.assertTrue(
            all(path == framework_license or "/Integrations/PhantomCamera/" in path for path in phantom)
        )
        self.assertTrue(
            all(path == framework_license or "/Integrations/FrifloEcs/" in path for path in friflo)
        )
        self.assertIn("addons/godo_framework/Integrations/FrifloEcs/LICENSE", friflo)
        self.assertEqual({framework_license}, core & guide)
        self.assertEqual({framework_license}, core & phantom)
        self.assertEqual({framework_license}, guide & phantom)
        self.assertEqual({framework_license}, core & friflo)
        self.assertEqual({framework_license}, guide & friflo)
        self.assertEqual({framework_license}, phantom & friflo)

    def test_framework_license_copy_matches_repository_license(self) -> None:
        self.assertEqual(
            (REPOSITORY_ROOT / "LICENSE").read_bytes(),
            (REPOSITORY_ROOT / "addons" / "godo_framework" / "LICENSE").read_bytes(),
        )

    def test_build_archives_preserves_overlay_paths(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godo-release-test-") as temporary_directory:
            archives = self.release.build_archives("9.8.7", Path(temporary_directory))

            self.assertEqual(
                [
                    "GoDoFramework-v9.8.7.zip",
                    "GoDoFramework-GuideInput-v9.8.7.zip",
                    "GoDoFramework-PhantomCamera-v9.8.7.zip",
                    "GoDoFramework-FrifloEcs-v9.8.7.zip",
                ],
                [archive.name for archive in archives],
            )

            for archive in archives:
                with zipfile.ZipFile(archive, mode="r") as package:
                    names = package.namelist()
                    self.assertTrue(names)
                    self.assertTrue(
                        all(
                            name.startswith("addons/godo_framework/")
                            for name in names
                        )
                    )
                    self.assertIn("addons/godo_framework/LICENSE", names)
                    self.assertIsNone(package.testzip())

            with zipfile.ZipFile(archives[0], mode="r") as core:
                core_names = set(core.namelist())
                self.assertFalse(any("/Integrations/" in name for name in core_names))
                self.assertTrue(
                    {
                        "addons/godo_framework/Runtime/StateMachine/IState.cs",
                        "addons/godo_framework/Runtime/StateMachine/IUpdatableState.cs",
                        "addons/godo_framework/Runtime/StateMachine/StateChangeResult.cs",
                        "addons/godo_framework/Runtime/StateMachine/StateMachine.cs",
                        "addons/godo_framework/Runtime/StateMachine/StateMachineTransitionLimitException.cs",
                    }.issubset(core_names)
                )

    def test_publish_uploads_every_archive(self) -> None:
        archives = [
            Path("core.zip"),
            Path("guide.zip"),
            Path("phantom.zip"),
            Path("friflo.zip"),
        ]
        with (
            patch.object(self.release.shutil, "which", return_value="gh") as which,
            patch.object(self.release.subprocess, "run") as run,
        ):
            self.release.publish_release("9.8.7", archives)

        which.assert_called_once_with("gh")
        command = run.call_args.args[0]
        self.assertEqual("v9.8.7", command[3])
        self.assertEqual([str(path) for path in archives], command[4:8])
        self.assertIn("--verify-tag", command)

    @staticmethod
    def _relative_paths(paths: list[Path]) -> set[str]:
        return {path.relative_to(REPOSITORY_ROOT).as_posix() for path in paths}


if __name__ == "__main__":
    unittest.main()
