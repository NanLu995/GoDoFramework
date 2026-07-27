#!/usr/bin/env python3
"""Export and run an isolated Windows DataTable ExportRelease probe."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[2]
EXPORT_WRAPPER = (
    PROJECT_ROOT
    / "addons"
    / "godo_framework"
    / "Tools"
    / "DataTable"
    / "godo_datatable_export.py"
)


def run(command: list[str], *, cwd: Path) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if result.stdout:
        print(result.stdout, end="" if result.stdout.endswith("\n") else "\n")
    if result.stderr:
        print(result.stderr, end="" if result.stderr.endswith("\n") else "\n", file=sys.stderr)
    if result.returncode != 0:
        raise RuntimeError(f"命令失败（{result.returncode}）：{' '.join(command)}")
    return result


def copy_probe_project(project: Path) -> None:
    shutil.copytree(
        PROJECT_ROOT / "addons" / "godo_framework",
        project / "addons" / "godo_framework",
    )
    shutil.copytree(PROJECT_ROOT / "DataTables" / "Base", project / "DataTables" / "Base")

    automated = project / "Verification" / "Automated"
    automated.mkdir(parents=True)
    for name in (
        "DataTableServiceRegression.cs",
        "DataTableServiceRegression.cs.uid",
        "DataTableServiceRegression.tscn",
    ):
        shutil.copy2(PROJECT_ROOT / "Verification" / "Automated" / name, automated / name)
    shutil.copytree(
        PROJECT_ROOT / "Verification" / "Automated" / "Fixtures" / "DataTableService",
        automated / "Fixtures" / "DataTableService",
    )

    (project / "project.godot").write_text(
        """config_version=5

[application]

config/name="DataTable ExportRelease Verification"
run/main_scene="res://Verification/Automated/DataTableServiceRegression.tscn"
config/features=PackedStringArray("4.7", "C#", "GL Compatibility")

[autoload]

GoDoRuntime="*res://addons/godo_framework/Core/GoDoRuntime.tscn"

[dotnet]

project/assembly_name="DataTableExportReleaseVerification"

[editor_plugins]

enabled=PackedStringArray("res://addons/godo_framework/plugin.cfg")

[rendering]

renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"
""",
        encoding="utf-8",
    )
    (project / "DataTableExportReleaseVerification.csproj").write_text(
        """<Project Sdk="Godot.NET.Sdk/4.7.1">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="addons/godo_framework/Integrations/**/*.cs" />
  </ItemGroup>
</Project>
""",
        encoding="utf-8",
    )
    (project / "DataTableExportReleaseVerification.sln").write_text(
        """Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DataTableExportReleaseVerification", "DataTableExportReleaseVerification.csproj", "{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		ExportDebug|Any CPU = ExportDebug|Any CPU
		ExportRelease|Any CPU = ExportRelease|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}.ExportDebug|Any CPU.ActiveCfg = ExportDebug|Any CPU
		{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}.ExportDebug|Any CPU.Build.0 = ExportDebug|Any CPU
		{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}.ExportRelease|Any CPU.ActiveCfg = ExportRelease|Any CPU
		{6EC0C2D5-B0F4-4FC8-9B2C-70BDCCF79A2B}.ExportRelease|Any CPU.Build.0 = ExportRelease|Any CPU
	EndGlobalSection
EndGlobal
""",
        encoding="utf-8",
    )
    (project / "export_presets.cfg").write_text(
        """[preset.0]

name="DataTable ExportRelease Verification"
platform="Windows Desktop"
runnable=true
advanced_options=false
dedicated_server=false
custom_features=""
export_filter="all_resources"
include_filter="*.gdtb,*.json"
exclude_filter=""
export_path=""
script_export_mode=2

[preset.0.options]

binary_format/embed_pck=false
""",
        encoding="utf-8",
    )


def write_manual_instructions(output_root: Path) -> None:
    (output_root / "MANUAL_EXPORT.md").write_text(
        """# DataTable ExportRelease 手动验收

1. 使用 Godot 4.7.1 Mono 打开 `project/project.godot`。
2. 确认已安装 Godot 4.7.1 Mono Windows 导出模板。
3. 打开“项目 → 导出”，选择 `DataTable ExportRelease Verification`。
4. 导出到 `distribution/DataTableExportReleaseVerification.exe`。
5. 在终端运行：

   ```powershell
   .\\distribution\\DataTableExportReleaseVerification.exe --headless
   ```

成功标记：

```text
[DataTableServiceRegression] PASS (10/10)
```

发布门禁已在导出前校验 Base DataTable 生成产物；失败时不要跳过校验。
""",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="验证 Windows ExportRelease DataTable 全链路。")
    parser.add_argument("--godot", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--prepare-only", action="store_true")
    arguments = parser.parse_args()

    godot = arguments.godot.expanduser().resolve()
    if not godot.is_file():
        raise RuntimeError(f"Godot 控制台不存在：{godot}")
    output_root = arguments.output_root.expanduser().resolve()
    if output_root.exists():
        raise RuntimeError(f"输出目录已存在，拒绝覆盖：{output_root}")

    project = output_root / "project"
    distribution = output_root / "distribution"
    project.mkdir(parents=True)
    distribution.mkdir()
    copy_probe_project(project)
    write_manual_instructions(output_root)

    run([str(godot), "--headless", "--editor", "--path", str(project), "--quit"], cwd=project)
    if arguments.prepare_only:
        print(f"[DataTableExportReleaseVerification] PREPARED: {project}")
        return 0

    executable = distribution / "DataTableExportReleaseVerification.exe"
    run(
        [
            sys.executable,
            "-X",
            "utf8",
            str(EXPORT_WRAPPER),
            "--godot",
            str(godot),
            "--project",
            str(project),
            "--preset",
            "DataTable ExportRelease Verification",
            "--output",
            str(executable),
            "--mode",
            "release",
        ],
        cwd=PROJECT_ROOT,
    )
    if not executable.is_file():
        raise RuntimeError(f"ExportRelease 可执行文件不存在：{executable}")

    result = run([str(executable), "--headless"], cwd=distribution)
    if "[DataTableServiceRegression] PASS (10/10)" not in result.stdout:
        raise RuntimeError("导出包未输出 DataTableServiceRegression 成功标记。")

    print(f"[DataTableExportReleaseVerification] PASS: {executable}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[DataTableExportReleaseVerification] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
