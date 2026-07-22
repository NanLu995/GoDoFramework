#!/usr/bin/env python3
"""Build and optionally publish a GoDoFramework release archive."""

from __future__ import annotations

import argparse
import configparser
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
ADDON_ROOT = REPOSITORY_ROOT / "addons" / "godo_framework"
PLUGIN_CONFIG = ADDON_ROOT / "plugin.cfg"
DIST_ROOT = Path(__file__).resolve().parent / "dist"
INCLUDED_SUFFIXES = {".cfg", ".cs", ".gd", ".py", ".tscn", ".uid"}
EXCLUDED_NAMES = {".DS_Store", "Thumbs.db"}


def read_plugin_metadata() -> tuple[str, str, str]:
    config = configparser.ConfigParser()
    if not config.read(PLUGIN_CONFIG, encoding="utf-8"):
        raise RuntimeError(f"无法读取插件配置：{PLUGIN_CONFIG}")

    values = tuple(
        config.get("plugin", key, fallback="").strip().strip('"')
        for key in ("version", "min_godot_version", "tested_godot_version")
    )
    framework_version, minimum_version, tested_version = values
    if re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?", framework_version) is None:
        raise RuntimeError("plugin.cfg 的框架版本不是有效的语义化版本。")
    if any(re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", value) is None for value in (minimum_version, tested_version)):
        raise RuntimeError("plugin.cfg 的 Godot 兼容版本不是 major.minor.patch。")

    minimum_parts = tuple(map(int, minimum_version.split(".")))
    tested_parts = tuple(map(int, tested_version.split(".")))
    if minimum_parts > tested_parts or minimum_parts[0] != tested_parts[0]:
        raise RuntimeError("plugin.cfg 的 Godot 兼容范围无效。")
    return values


def collect_release_files() -> list[Path]:
    files = [
        path
        for path in ADDON_ROOT.rglob("*")
        if path.is_file()
        and path.suffix.lower() in INCLUDED_SUFFIXES
        and path.name not in EXCLUDED_NAMES
    ]
    if not files:
        raise RuntimeError("发布包没有可打包文件。")
    return sorted(files)


def build_archive(version: str) -> Path:
    DIST_ROOT.mkdir(parents=True, exist_ok=True)
    archive_path = DIST_ROOT / f"GoDoFramework-v{version}.zip"
    files = collect_release_files()

    with zipfile.ZipFile(
        archive_path,
        mode="w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        for source_path in files:
            relative_path = source_path.relative_to(REPOSITORY_ROOT)
            archive.write(source_path, relative_path.as_posix())

    with zipfile.ZipFile(archive_path, mode="r") as archive:
        invalid_entry = archive.testzip()
        if invalid_entry is not None:
            raise RuntimeError(f"ZIP 完整性校验失败：{invalid_entry}")
        if any(Path(name).suffix.lower() not in INCLUDED_SUFFIXES for name in archive.namelist()):
            raise RuntimeError("ZIP 中包含不在发布白名单内的文件。")

    print(f"已生成：{archive_path}")
    print(f"文件数：{len(files)}")
    return archive_path


def publish_release(version: str, archive_path: Path) -> None:
    gh_path = shutil.which("gh")
    if gh_path is None:
        raise RuntimeError("未找到 GitHub CLI（gh）；请先安装并执行 gh auth login。")

    tag = f"v{version}"
    command = [
        gh_path,
        "release",
        "create",
        tag,
        str(archive_path),
        "--title",
        tag,
        "--generate-notes",
        "--verify-tag",
    ]
    subprocess.run(command, cwd=REPOSITORY_ROOT, check=True)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="打包 GoDoFramework；可选择发布到已有 Git Tag 对应的 GitHub Release。"
    )
    parser.add_argument(
        "--version",
        help="发布版本；默认读取 addons/godo_framework/plugin.cfg。",
    )
    parser.add_argument(
        "--publish",
        action="store_true",
        help="打包后使用 GitHub CLI 发布；要求对应 v版本 Tag 已存在。",
    )
    return parser.parse_args()


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")

    arguments = parse_arguments()
    plugin_version, minimum_version, tested_version = read_plugin_metadata()
    version = arguments.version or plugin_version

    if version != plugin_version:
        raise RuntimeError(
            f"发布版本 {version} 与 plugin.cfg 版本 {plugin_version} 不一致。"
        )

    archive_path = build_archive(version)
    print(f"GoDoFramework：{plugin_version}")
    print(f"Godot 已验证范围：{minimum_version}～{tested_version}")
    print(f"更高的 Godot {tested_version.split('.')[0]}.x 版本需要项目回归验证。")
    if arguments.publish:
        publish_release(version, archive_path)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"发布失败：{error}", file=sys.stderr)
        raise SystemExit(1)
