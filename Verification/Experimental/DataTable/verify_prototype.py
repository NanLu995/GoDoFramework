#!/usr/bin/env python3
"""Run deterministic generation and validation checks for the DataTable prototype."""

from __future__ import annotations

import hashlib
import json
import shutil
import struct
import subprocess
import sys
from unittest import mock
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[2]
TOOL_DIR = PROJECT_ROOT / "addons" / "godo_framework" / "Tools" / "DataTable"
TOOL_PATH = TOOL_DIR / "godo_datatable.py"
sys.path.insert(0, str(TOOL_DIR))

import godo_datatable
from godo_datatable import FORMAT_VERSION, CompileFailure, compile_tables
from generate_fixtures import write_invalid_sets, write_valid_set


PROFILE_PATH = SCRIPT_DIR / "profile.json"
GENERATED_CSHARP = SCRIPT_DIR / "Generated" / "DataTablePrototype.Generated.cs"
ARTIFACT_ROOT = SCRIPT_DIR / "Artifacts"
EXPECTED_DIAGNOSTICS = {
    "missing_column": "DT003",
    "duplicate_key": "DT114",
    "invalid_enum": "DT108",
    "out_of_range": "DT110",
    "invalid_foreign_key": "DT115",
    "short_row": "DT005",
}


def digest_files(directory: Path) -> dict[str, str]:
    return {
        path.relative_to(directory).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(directory.rglob("*"))
        if path.is_file()
    }


def assert_equal(expected: object, actual: object, message: str) -> None:
    if expected != actual:
        raise RuntimeError(f"{message}；期望 {expected!r}，实际 {actual!r}")


def generate_sources() -> Path:
    sources = ARTIFACT_ROOT / "sources"
    if sources.exists():
        shutil.rmtree(sources)
    write_valid_set(sources / "small", 12)
    write_valid_set(sources / "performance", 10_000)
    write_invalid_sets(sources / "invalid")
    return sources


def verify_determinism(sources: Path) -> None:
    scratch = ARTIFACT_ROOT / "scratch"
    first = scratch / "determinism-a"
    second = scratch / "determinism-b"
    generated_copy = scratch / "determinism.Generated.txt"
    compile_tables(PROFILE_PATH, sources / "small", first, GENERATED_CSHARP)
    first_csharp = GENERATED_CSHARP.read_bytes()
    compile_tables(PROFILE_PATH, sources / "small", second, generated_copy)
    assert_equal(digest_files(first), digest_files(second), "相同输入产生了不同产物")
    assert_equal(first_csharp, generated_copy.read_bytes(), "相同 Profile 产生了不同 C# 代码")
    print("[DataTablePrototype] PASS: 确定性产物")


def verify_invalid_cases(sources: Path) -> None:
    scratch = ARTIFACT_ROOT / "scratch"
    preservation = scratch / "preservation"
    temporary_csharp = scratch / "preservation.Generated.txt"
    compile_tables(PROFILE_PATH, sources / "small", preservation, temporary_csharp)
    expected_files = digest_files(preservation)
    expected_csharp = temporary_csharp.read_bytes()

    for case_name, expected_code in EXPECTED_DIAGNOSTICS.items():
        try:
            compile_tables(
                PROFILE_PATH,
                sources / "invalid" / case_name,
                preservation,
                temporary_csharp,
            )
        except CompileFailure as failure:
            codes = {diagnostic.code for diagnostic in failure.diagnostics}
            if expected_code not in codes:
                details = "; ".join(diagnostic.format() for diagnostic in failure.diagnostics)
                raise RuntimeError(
                    f"错误样例 {case_name} 未产生 {expected_code}：{details}"
                ) from failure
            print(f"[DataTablePrototype] PASS: {case_name} -> {expected_code}")
        else:
            raise RuntimeError(f"错误样例 {case_name} 未阻止生成。")

        assert_equal(expected_files, digest_files(preservation), "失败生成覆盖了已有产物")
        assert_equal(expected_csharp, temporary_csharp.read_bytes(), "失败生成覆盖了已有 C#")


def run_compiler(*arguments: str, expected_exit: int = 0) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        [sys.executable, str(TOOL_PATH), *arguments],
        cwd=PROJECT_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != expected_exit:
        raise RuntimeError(
            f"DataTable CLI 退出码错误；期望 {expected_exit}，实际 {result.returncode}。\n"
            f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )
    return result


def verify_cli_modes(sources: Path) -> None:
    root = ARTIFACT_ROOT / "scratch" / "path with spaces"
    source_copy = root / "source files"
    shutil.copytree(sources / "small", source_copy)
    before_check = digest_files(root)

    check_result = run_compiler(
        "check",
        "--profile",
        str(PROFILE_PATH),
        "--source",
        str(source_copy),
    )
    assert_equal(before_check, digest_files(root), "check 模式写入了文件")
    if "CHECK PASS" not in check_result.stdout:
        raise RuntimeError("check 模式未输出成功标记。")
    print("[DataTablePrototype] PASS: CLI check 不写入")

    invalid_result = run_compiler(
        "check",
        "--profile",
        str(PROFILE_PATH),
        "--source",
        str(sources / "invalid" / "invalid_enum"),
        expected_exit=1,
    )
    if "DT108" not in invalid_result.stdout:
        raise RuntimeError("CLI check 未返回预期的 DT108 诊断。")
    assert_equal(before_check, digest_files(root), "失败的 check 模式写入了文件")
    print("[DataTablePrototype] PASS: CLI check 错误诊断")

    unsafe_result = run_compiler(
        "generate",
        "--profile",
        str(PROFILE_PATH),
        "--source",
        str(source_copy),
        "--output",
        str(root),
        "--csharp",
        str(root / "Unsafe.Generated.txt"),
        expected_exit=1,
    )
    if "输出目录" not in unsafe_result.stderr:
        raise RuntimeError("CLI generate 未报告危险输出目录。")
    assert_equal(before_check, digest_files(root), "危险输出目录校验后写入了文件")
    print("[DataTablePrototype] PASS: CLI generate 拒绝危险输出目录")

    output = root / "generated output"
    csharp = root / "Generated Code.txt"
    run_compiler(
        "generate",
        "--profile",
        str(PROFILE_PATH),
        "--source",
        str(source_copy),
        "--output",
        str(output),
        "--csharp",
        str(csharp),
    )
    baseline_output = ARTIFACT_ROOT / "scratch" / "cli-baseline"
    baseline_csharp = ARTIFACT_ROOT / "scratch" / "cli-baseline.Generated.txt"
    compile_tables(PROFILE_PATH, sources / "small", baseline_output, baseline_csharp)
    assert_equal(digest_files(baseline_output), digest_files(output), "CLI generate 产物不一致")
    assert_equal(baseline_csharp.read_bytes(), csharp.read_bytes(), "CLI generate C# 不一致")
    print("[DataTablePrototype] PASS: CLI generate 支持空格路径")

    config_profile = root / "profile.json"
    shutil.copy2(PROFILE_PATH, config_profile)
    build_config = root / "datatable.build.json"
    build_config.write_text(
        json.dumps(
            {
                "format_version": 1,
                "profile": "profile.json",
                "source": "source files",
                "output": "config output",
                "csharp": "Config Generated.txt",
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    before_config_check = digest_files(root)
    run_compiler("check", "--build-config", str(build_config))
    assert_equal(before_config_check, digest_files(root), "Build Config check 写入了文件")
    run_compiler("generate", "--build-config", str(build_config))
    assert_equal(digest_files(baseline_output), digest_files(root / "config output"), "Build Config 产物不一致")
    assert_equal(
        baseline_csharp.read_bytes(),
        (root / "Config Generated.txt").read_bytes(),
        "Build Config C# 不一致",
    )
    print("[DataTablePrototype] PASS: Build Config check/generate")

    config_csharp = root / "Config Generated.txt"
    csharp_timestamp = config_csharp.stat().st_mtime_ns
    run_compiler("generate", "--build-config", str(build_config))
    assert_equal(csharp_timestamp, config_csharp.stat().st_mtime_ns, "未变化的 C# 被重复改写")
    print("[DataTablePrototype] PASS: 未变化 C# 保留时间戳")

    run_compiler(
        "generate",
        "--build-config",
        str(build_config),
        "--table",
        "Item",
    )
    single_report = json.loads(
        (root / "config output" / "build-report.json").read_text(encoding="utf-8")
    )
    assert_equal("single", single_report.get("scope"), "CLI --table 未进入单表生成")
    assert_equal("Item", single_report.get("selected_table"), "CLI --table 未传递目标表")
    print("[DataTablePrototype] PASS: CLI Build Config 单表生成")

    missing_config = root / "missing-field.build.json"
    missing_config.write_text('{"format_version":1}\n', encoding="utf-8")
    missing_result = run_compiler(
        "check",
        "--build-config",
        str(missing_config),
        expected_exit=1,
    )
    if "缺少字段" not in missing_result.stderr:
        raise RuntimeError("Build Config 缺字段未产生明确错误。")
    print("[DataTablePrototype] PASS: Build Config 缺字段拒绝")

    escaping_config = root / "escaping.build.json"
    escaping_config.write_text(
        '{"format_version":1,"profile":"profile.json","source":"source files",'
        '"output":"../escaped","csharp":"Config Generated.txt"}\n',
        encoding="utf-8",
    )
    escaping_result = run_compiler(
        "generate",
        "--build-config",
        str(escaping_config),
        expected_exit=1,
    )
    if "逃逸" not in escaping_result.stderr:
        raise RuntimeError("Build Config 路径逃逸未产生明确错误。")
    print("[DataTablePrototype] PASS: Build Config 路径逃逸拒绝")


def verify_output_rollback() -> None:
    root = ARTIFACT_ROOT / "scratch" / "rollback"
    output = root / "output"
    staged = root / "staged"
    csharp = root / "DataTables.Generated.txt"
    output.mkdir(parents=True)
    staged.mkdir()
    (output / "state.txt").write_text("old-data", encoding="utf-8")
    (staged / "state.txt").write_text("new-data", encoding="utf-8")
    csharp.write_text("old-csharp", encoding="utf-8")
    csharp_staged = csharp.with_suffix(csharp.suffix + ".tmp")
    real_replace = godo_datatable.os.replace

    def fail_csharp_commit(source: object, destination: object) -> None:
        if Path(source) == csharp_staged:
            raise OSError("injected C# commit failure")
        real_replace(source, destination)

    try:
        with mock.patch.object(godo_datatable.os, "replace", side_effect=fail_csharp_commit):
            godo_datatable.commit_outputs(staged, output, csharp, "new-csharp")
    except OSError as error:
        if "injected" not in str(error):
            raise
    else:
        raise RuntimeError("故障注入未阻止双产物提交。")

    assert_equal("old-data", (output / "state.txt").read_text(encoding="utf-8"), "数据目录未回滚")
    assert_equal("old-csharp", csharp.read_text(encoding="utf-8"), "C# 文件未回滚")
    print("[DataTablePrototype] PASS: 双产物提交失败回滚")


def verify_single_table_generation(sources: Path) -> None:
    root = ARTIFACT_ROOT / "scratch" / "single-table"
    source = root / "source"
    output = root / "output"
    profile = root / "profile.json"
    csharp = root / "DataTables.Generated.txt"
    shutil.copytree(sources / "small", source)
    shutil.copy2(PROFILE_PATH, profile)
    compile_tables(profile, source, output, csharp)

    category = output / "ItemCategory.gdtb"
    item = output / "Item.gdtb"
    category_bytes = category.read_bytes()
    category_timestamp = category.stat().st_mtime_ns
    item_bytes = item.read_bytes()
    csharp_timestamp = csharp.stat().st_mtime_ns
    items_path = source / "Items.csv"
    items_text = items_path.read_text(encoding="utf-8")
    items_path.write_text(items_text.replace("测试物品 1", "单表更新物品", 1), encoding="utf-8")
    compile_tables(profile, source, output, csharp, selected_table="Item")
    if item.read_bytes() == item_bytes:
        raise RuntimeError("单表数据变化未更新目标二进制。")
    assert_equal(category_bytes, category.read_bytes(), "单表生成改写了未选表内容")
    assert_equal(category_timestamp, category.stat().st_mtime_ns, "单表生成改写了未选表时间戳")
    assert_equal(csharp_timestamp, csharp.stat().st_mtime_ns, "纯数据变化改写了 C# 时间戳")
    report = json.loads((output / "build-report.json").read_text(encoding="utf-8"))
    assert_equal("single", report.get("scope"), "单表报告未记录生成范围")
    assert_equal("Item", report.get("selected_table"), "单表报告未记录目标表")
    print("[DataTablePrototype] PASS: 单表数据更新与未选表保留")

    profile_value = json.loads(profile.read_text(encoding="utf-8"))
    item_profile = next(table for table in profile_value["tables"] if table["id"] == "Item")
    display_field = next(field for field in item_profile["fields"] if field["name"] == "display_name")
    display_field["name"] = "title"
    profile.write_text(json.dumps(profile_value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    items_path.write_text(
        items_path.read_text(encoding="utf-8").replace("display_name", "title", 1),
        encoding="utf-8",
    )
    previous_csharp = csharp.read_bytes()
    compile_tables(profile, source, output, csharp, selected_table="Item")
    if csharp.read_bytes() == previous_csharp:
        raise RuntimeError("单表结构变化未更新聚合 C#。")
    assert_equal(category_bytes, category.read_bytes(), "目标表结构变化改写了未选表")
    print("[DataTablePrototype] PASS: 单表结构变化更新聚合 C#")

    stable_files = digest_files(output)
    stable_csharp = csharp.read_bytes()
    categories_path = source / "ItemCategories.csv"
    categories_path.write_text(
        categories_path.read_text(encoding="utf-8").replace("消耗品", "过期类别", 1),
        encoding="utf-8",
    )
    try:
        compile_tables(profile, source, output, csharp, selected_table="Item")
    except ValueError as error:
        if "ItemCategory" not in str(error) or "过期" not in str(error):
            raise RuntimeError(f"未选表过期错误不明确：{error}") from error
    else:
        raise RuntimeError("单表生成未拒绝过期的未选表。")
    assert_equal(stable_files, digest_files(output), "过期拒绝后改写了数据产物")
    assert_equal(stable_csharp, csharp.read_bytes(), "过期拒绝后改写了 C#")
    print("[DataTablePrototype] PASS: 单表生成拒绝过期未选表")

    categories_path.write_text(
        categories_path.read_text(encoding="utf-8").replace("过期类别", "消耗品", 1),
        encoding="utf-8",
    )
    category.unlink()
    try:
        compile_tables(profile, source, output, csharp, selected_table="Item")
    except ValueError as error:
        if "二进制数据表集合" not in str(error):
            raise RuntimeError(f"未选表缺失错误不明确：{error}") from error
    else:
        raise RuntimeError("单表生成未拒绝缺失的未选表。")
    print("[DataTablePrototype] PASS: 单表生成拒绝缺失未选表")

    compile_tables(profile, source, output, csharp)
    expanded_profile = json.loads(profile.read_text(encoding="utf-8"))
    expanded_profile["tables"].append(
        {
            "id": "Extra",
            "source": "Extra.csv",
            "schema_version": 1,
            "audience": "ClientOnly",
            "primary_key": "id",
            "fields": [{"name": "id", "type": "string", "required": True}],
        }
    )
    profile.write_text(json.dumps(expanded_profile, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (source / "Extra.csv").write_text("id\nextra\n", encoding="utf-8")
    try:
        compile_tables(profile, source, output, csharp, selected_table="Item")
    except ValueError as error:
        if "集合已发生变化" not in str(error):
            raise RuntimeError(f"数据表集合变化错误不明确：{error}") from error
    else:
        raise RuntimeError("单表生成未拒绝数据表集合变化。")
    print("[DataTablePrototype] PASS: 单表生成拒绝数据表集合变化")

    try:
        compile_tables(profile, source, output, csharp, selected_table="Missing")
    except ValueError as error:
        if "不存在数据表" not in str(error):
            raise RuntimeError(f"未知表 ID 错误不明确：{error}") from error
    else:
        raise RuntimeError("单表生成未拒绝未知表 ID。")
    print("[DataTablePrototype] PASS: 单表生成拒绝未知表 ID")

    invalid_source = sources / "invalid" / "invalid_foreign_key"
    try:
        compile_tables(PROFILE_PATH, invalid_source, output, csharp, selected_table="Item")
    except CompileFailure as failure:
        if "DT115" not in {diagnostic.code for diagnostic in failure.diagnostics}:
            raise RuntimeError("单表生成外键错误未产生 DT115。") from failure
    else:
        raise RuntimeError("单表生成未执行全量外键校验。")
    print("[DataTablePrototype] PASS: 单表生成执行全量外键校验")


def verify_file_commit_rollback() -> None:
    root = ARTIFACT_ROOT / "scratch" / "file-rollback"
    first = root / "first.txt"
    second = root / "second.txt"
    root.mkdir(parents=True)
    first.write_text("old-first", encoding="utf-8")
    second.write_text("old-second", encoding="utf-8")
    second_staged = second.with_suffix(second.suffix + ".tmp")
    real_replace = godo_datatable.os.replace

    def fail_second_commit(source: object, destination: object) -> None:
        if Path(source) == second_staged:
            raise OSError("injected file commit failure")
        real_replace(source, destination)

    try:
        with mock.patch.object(godo_datatable.os, "replace", side_effect=fail_second_commit):
            godo_datatable.commit_files({first: b"new-first", second: b"new-second"})
    except OSError as error:
        if "injected" not in str(error):
            raise
    else:
        raise RuntimeError("故障注入未阻止多文件提交。")
    assert_equal("old-first", first.read_text(encoding="utf-8"), "首文件未回滚")
    assert_equal("old-second", second.read_text(encoding="utf-8"), "次文件未回滚")
    print("[DataTablePrototype] PASS: 多文件提交失败回滚")


def build_performance_artifacts(sources: Path) -> None:
    output = ARTIFACT_ROOT / "output"
    compile_tables(PROFILE_PATH, sources / "performance", output, GENERATED_CSHARP)
    print(f"[DataTablePrototype] PASS: 性能产物 {output}")


def binary_layout(data: bytearray) -> tuple[int, int, int, int]:
    table_id_length = struct.unpack_from("<H", data, 12)[0]
    row_count_offset = 14 + table_id_length
    row_count = struct.unpack_from("<I", data, row_count_offset)[0]
    hash_offset = row_count_offset + 10
    payload_offset = hash_offset + 32
    string_count = struct.unpack_from("<I", data, payload_offset)[0]
    return row_count, hash_offset, payload_offset, string_count


def refresh_payload_hash(data: bytearray, hash_offset: int, payload_offset: int) -> None:
    data[hash_offset:payload_offset] = hashlib.sha256(data[payload_offset:]).digest()


def build_corruption_artifacts() -> None:
    valid_path = ARTIFACT_ROOT / "output" / "Item.gdtb"
    valid = bytearray(valid_path.read_bytes())
    corruption = ARTIFACT_ROOT / "corruption"
    if corruption.exists():
        shutil.rmtree(corruption)
    corruption.mkdir(parents=True)

    variants: dict[str, bytearray] = {}
    bad_magic = bytearray(valid)
    bad_magic[0:4] = b"BAD!"
    variants["bad-magic.gdtb"] = bad_magic

    bad_format = bytearray(valid)
    struct.pack_into("<H", bad_format, 4, FORMAT_VERSION + 1)
    variants["bad-format-version.gdtb"] = bad_format

    bad_schema = bytearray(valid)
    struct.pack_into("<H", bad_schema, 6, 2)
    variants["bad-schema-version.gdtb"] = bad_schema

    bad_flags = bytearray(valid)
    struct.pack_into("<I", bad_flags, 8, 2)
    variants["bad-flags.gdtb"] = bad_flags

    tampered = bytearray(valid)
    tampered[-1] ^= 0xFF
    variants["tampered-payload.gdtb"] = tampered
    variants["truncated.gdtb"] = bytearray(valid[:-1])

    row_count, hash_offset, payload_offset, string_count = binary_layout(valid)
    bad_string_index = bytearray(valid)
    first_row_offset = payload_offset + 4
    for _ in range(string_count):
        byte_count = struct.unpack_from("<I", bad_string_index, first_row_offset)[0]
        first_row_offset += 4 + byte_count
    first_string_index_offset = first_row_offset + 1
    struct.pack_into("<I", bad_string_index, first_string_index_offset, string_count)
    refresh_payload_hash(bad_string_index, hash_offset, payload_offset)
    variants["bad-string-index.gdtb"] = bad_string_index

    bad_primary_index = bytearray(valid)
    struct.pack_into("<I", bad_primary_index, len(bad_primary_index) - 4, row_count)
    refresh_payload_hash(bad_primary_index, hash_offset, payload_offset)
    variants["bad-primary-index.gdtb"] = bad_primary_index

    for name, data in variants.items():
        (corruption / name).write_bytes(data)
    print(f"[DataTablePrototype] 生成损坏样例：{corruption}")


def main() -> int:
    scratch = ARTIFACT_ROOT / "scratch"
    if scratch.exists():
        shutil.rmtree(scratch)
    sources = generate_sources()
    verify_determinism(sources)
    verify_invalid_cases(sources)
    verify_cli_modes(sources)
    verify_output_rollback()
    verify_single_table_generation(sources)
    verify_file_commit_rollback()
    build_performance_artifacts(sources)
    build_corruption_artifacts()
    print("[DataTablePrototype] PASS (26/26)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[DataTablePrototype] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
