from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify_export_release.py")


def load_module():
    spec = importlib.util.spec_from_file_location("verify_export_release", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载验证脚本：{SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ExportReleaseProbeTests(unittest.TestCase):
    def test_probe_matches_core_package_boundary(self) -> None:
        module = load_module()
        with tempfile.TemporaryDirectory(prefix="godo-datatable-export-test-") as directory:
            project = Path(directory) / "project"
            project.mkdir()
            module.copy_probe_project(project)

            integrations = project / "addons" / "godo_framework" / "Integrations"
            self.assertFalse(integrations.exists())
            project_file = project / "DataTableExportReleaseVerification.csproj"
            self.assertNotIn("Integrations", project_file.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
