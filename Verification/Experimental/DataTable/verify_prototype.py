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


PROFILE_PATH = SCRIPT_DIR / "prototype.datatable.schema.json"
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
        [sys.executable, "-X", "utf8", str(TOOL_PATH), *arguments],
        cwd=PROJECT_ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
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
    schema = root / ".datatable.schema.json"
    schema_value = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
    schema_value["source_directory"] = "source files"
    schema_value["output_directory"] = "config output"
    schema_value["csharp_output"] = "Config Generated.txt"
    schema.write_text(json.dumps(schema_value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    before_check = digest_files(root)

    check_result = run_compiler(
        "check",
        "--schema",
        str(schema),
    )
    assert_equal(before_check, digest_files(root), "check 模式写入了文件")
    if "CHECK PASS" not in check_result.stdout:
        raise RuntimeError("check 模式未输出成功标记。")
    print("[DataTablePrototype] PASS: CLI check 不写入")

    output = root / "config output"
    csharp = root / "Config Generated.txt"
    run_compiler("generate", "--schema", str(schema))
    baseline_output = ARTIFACT_ROOT / "scratch" / "cli-baseline"
    baseline_csharp = ARTIFACT_ROOT / "scratch" / "cli-baseline.Generated.txt"
    compile_tables(PROFILE_PATH, sources / "small", baseline_output, baseline_csharp)
    assert_equal(digest_files(baseline_output), digest_files(output), "CLI generate 产物不一致")
    assert_equal(baseline_csharp.read_bytes(), csharp.read_bytes(), "CLI generate C# 不一致")
    print("[DataTablePrototype] PASS: CLI generate 支持空格路径")

    before_config_check = digest_files(root)
    run_compiler("check", "--schema", str(schema))
    assert_equal(before_config_check, digest_files(root), "Schema check 写入了文件")
    run_compiler("generate", "--schema", str(schema))
    assert_equal(digest_files(baseline_output), digest_files(root / "config output"), "Schema 产物不一致")
    assert_equal(
        baseline_csharp.read_bytes(),
        (root / "Config Generated.txt").read_bytes(),
        "Schema C# 不一致",
    )
    print("[DataTablePrototype] PASS: Schema check/generate")

    config_csharp = root / "Config Generated.txt"
    csharp_timestamp = config_csharp.stat().st_mtime_ns
    run_compiler("generate", "--schema", str(schema))
    assert_equal(csharp_timestamp, config_csharp.stat().st_mtime_ns, "未变化的 C# 被重复改写")
    print("[DataTablePrototype] PASS: 未变化 C# 保留时间戳")

    run_compiler(
        "generate",
        "--schema",
        str(schema),
        "--table",
        "Item",
    )
    if (root / "config output" / "build-report.json").exists():
        raise RuntimeError("CLI --table 默认生成了不应存在的构建报告。")
    print("[DataTablePrototype] PASS: CLI Schema 单表生成")

    before_verify = digest_files(root)
    verify_result = run_compiler("verify-generated", "--schema", str(schema))
    assert_equal(before_verify, digest_files(root), "verify-generated 改写了文件")
    if "VERIFY GENERATED PASS" not in verify_result.stdout:
        raise RuntimeError("verify-generated 未输出成功标记。")
    print("[DataTablePrototype] PASS: CLI 只读验证单表生成后的有效产物")

    missing_config = root / "missing-field.datatable.schema.json"
    missing_config.write_text('{"format_version":2}\n', encoding="utf-8")
    missing_result = run_compiler(
        "check",
        "--schema",
        str(missing_config),
        expected_exit=1,
    )
    if "缺少字段" not in missing_result.stderr:
        raise RuntimeError("Schema 缺字段未产生明确错误。")
    print("[DataTablePrototype] PASS: Schema 缺字段拒绝")

    escaping_config = root / "escaping.datatable.schema.json"
    escaping_schema = dict(schema_value)
    escaping_schema["output_directory"] = "../escaped"
    escaping_config.write_text(
        json.dumps(escaping_schema, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    escaping_result = run_compiler(
        "generate",
        "--schema",
        str(escaping_config),
        expected_exit=1,
    )
    if "逃逸" not in escaping_result.stderr:
        raise RuntimeError("Schema 路径逃逸未产生明确错误。")
    print("[DataTablePrototype] PASS: Schema 路径逃逸拒绝")


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
    profile = root / ".datatable.schema.json"
    csharp = root / "DataTables.Generated.txt"
    shutil.copytree(sources / "small", source)
    profile_value = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
    profile_value["source_directory"] = "source"
    profile_value["output_directory"] = "output"
    profile_value["csharp_output"] = "DataTables.Generated.txt"
    profile.write_text(json.dumps(profile_value, ensure_ascii=False) + "\n", encoding="utf-8")
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
    if (output / "build-report.json").exists():
        raise RuntimeError("单表生成默认写入了构建报告。")
    print("[DataTablePrototype] PASS: 单表数据更新与未选表保留")

    profile_value = json.loads(profile.read_text(encoding="utf-8"))
    item_profile = next(table for table in profile_value["tables"] if table["id"] == "Item")
    display_field = next(field for field in item_profile["fields"] if field["name"] == "display_name")
    display_field["name"] = "title"
    item_profile["schema_version"] += 1
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


def verify_generated_detection(sources: Path) -> None:
    root = ARTIFACT_ROOT / "scratch" / "verify-generated"
    source = root / "source"
    output = root / "output"
    profile = root / ".datatable.schema.json"
    csharp = root / "DataTables.Generated.txt"
    shutil.copytree(sources / "small", source)
    profile_value = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
    profile_value["source_directory"] = "source"
    profile_value["output_directory"] = "output"
    profile_value["csharp_output"] = "DataTables.Generated.txt"
    profile.write_text(json.dumps(profile_value, ensure_ascii=False) + "\n", encoding="utf-8")
    compile_tables(profile, source, output, csharp)

    def assert_stale(expected_detail: str, message: str) -> None:
        before = digest_files(root)
        result = run_compiler(
            "verify-generated",
            "--schema",
            str(profile),
            expected_exit=1,
        )
        if "生成产物不是最新状态" not in result.stderr or expected_detail not in result.stderr:
            raise RuntimeError(f"{message}未产生明确差异：{result.stderr}")
        assert_equal(before, digest_files(root), f"{message}检查改写了文件")

    items = source / "Items.csv"
    original_items = items.read_bytes()
    items.write_text(
        items.read_text(encoding="utf-8").replace("测试物品 1", "源数据已变化", 1),
        encoding="utf-8",
    )
    assert_stale("内容过期", "数据过期")
    items.write_bytes(original_items)
    print("[DataTablePrototype] PASS: verify-generated 检出数据过期")

    profile_value = json.loads(profile.read_text(encoding="utf-8"))
    profile_value["tables"][1]["schema_version"] += 1
    profile.write_text(json.dumps(profile_value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    assert_stale("内容过期", "结构过期")
    profile_value["tables"][1]["schema_version"] -= 1
    profile.write_text(json.dumps(profile_value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("[DataTablePrototype] PASS: verify-generated 检出结构过期")

    missing_path = output / "Item.gdtb"
    missing_bytes = missing_path.read_bytes()
    missing_path.unlink()
    assert_stale("缺少", "文件缺失")
    missing_path.write_bytes(missing_bytes)
    print("[DataTablePrototype] PASS: verify-generated 检出文件缺失")

    extra_path = output / "obsolete.gdtb"
    extra_path.write_bytes(b"obsolete")
    assert_stale("额外文件", "额外文件")
    extra_path.unlink()
    print("[DataTablePrototype] PASS: verify-generated 检出额外文件")

    original_csharp = csharp.read_bytes()
    csharp.write_bytes(original_csharp + b"// stale\n")
    assert_stale(str(csharp), "C# 过期")
    csharp.write_bytes(original_csharp)
    print("[DataTablePrototype] PASS: verify-generated 检出 C# 过期")


def verify_export_target_artifacts(sources: Path) -> None:
    root = ARTIFACT_ROOT / "scratch" / "export-targets"
    source = root / ".datafiles"
    output = root / "output"
    profile = root / ".datatable.schema.json"
    csharp = root / "DataTables.Generated.txt"
    shutil.copytree(sources / "small", source)
    profile_value = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
    profile_value["source_directory"] = ".datafiles"
    profile_value["output_directory"] = "output"
    profile_value["csharp_output"] = "DataTables.Generated.txt"
    profile_value["tables"].extend(
        [
            {
                "id": "ClientSetting",
                "source": "ClientSettings.csv",
                "schema_version": 1,
                "audience": "ClientOnly",
                "primary_key": "id",
                "fields": [
                    {"name": "id", "type": "string", "required": True},
                    {"name": "value", "type": "string", "required": True},
                ],
            },
            {
                "id": "ServerSetting",
                "source": "ServerSettings.csv",
                "schema_version": 1,
                "audience": "ServerOnly",
                "primary_key": "id",
                "fields": [
                    {"name": "id", "type": "string", "required": True},
                    {"name": "value", "type": "string", "required": True},
                ],
            },
        ]
    )
    profile.write_text(json.dumps(profile_value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (source / "ClientSettings.csv").write_text("id,value\nclient,visible\n", encoding="utf-8")
    (source / "ServerSettings.csv").write_text("id,value\nserver,secret\n", encoding="utf-8")
    compile_tables(profile, source, output, csharp)

    client_manifest = json.loads((output / "manifest.client.json").read_text(encoding="utf-8"))
    server_manifest = json.loads((output / "manifest.server.json").read_text(encoding="utf-8"))
    client_ids = {table["id"] for table in client_manifest["tables"]}
    server_ids = {table["id"] for table in server_manifest["tables"]}
    if "ClientSetting" not in client_ids or "ServerSetting" in client_ids:
        raise RuntimeError(f"Client Manifest audience 过滤错误：{sorted(client_ids)}")
    if "ServerSetting" not in server_ids or "ClientSetting" in server_ids:
        raise RuntimeError(f"Server Manifest audience 过滤错误：{sorted(server_ids)}")
    if not {"Item", "ItemCategory"}.issubset(client_ids & server_ids):
        raise RuntimeError("Shared 表未同时进入 Client / Server Manifest。")
    for name in ["debug.json", "debug.client.json", "debug.server.json", "build-report.json", "normalized.ir.json"]:
        if (output / name).exists():
            raise RuntimeError(f"默认生成了不应存在的诊断产物：{name}")
    print("[DataTablePrototype] PASS: Client / Server 导出产物 audience 隔离")

    client_path = output / "manifest.client.json"
    server_path = output / "manifest.server.json"
    compatible = run_compiler(
        "compare-manifests",
        "--client",
        str(client_path),
        "--server",
        str(server_path),
    )
    if "MANIFEST COMPATIBLE" not in compatible.stdout:
        raise RuntimeError("兼容 Manifest 未输出成功摘要。")
    print("[DataTablePrototype] PASS: Client / Server Manifest 兼容")

    compatibility_cases = root / "compatibility-cases"
    compatibility_cases.mkdir()

    def assert_incompatible(
        case_name: str,
        expected_text: str,
        *,
        client_value: dict[str, object] | None = None,
        server_value: dict[str, object] | None = None,
        invalid_server_json: str | None = None,
    ) -> None:
        case_client = compatibility_cases / f"{case_name}.client.json"
        case_server = compatibility_cases / f"{case_name}.server.json"
        case_client.write_text(
            json.dumps(client_value or client_manifest, ensure_ascii=False),
            encoding="utf-8",
        )
        if invalid_server_json is None:
            case_server.write_text(
                json.dumps(server_value or server_manifest, ensure_ascii=False),
                encoding="utf-8",
            )
        else:
            case_server.write_text(invalid_server_json, encoding="utf-8")
        result = run_compiler(
            "compare-manifests",
            "--client",
            str(case_client),
            "--server",
            str(case_server),
            expected_exit=1,
        )
        if expected_text not in result.stdout + result.stderr:
            raise RuntimeError(
                f"Manifest 失败样例 {case_name} 缺少诊断 {expected_text!r}。"
            )
        print(f"[DataTablePrototype] PASS: Manifest {case_name} 拒绝")

    mismatched_data_set = dict(server_manifest)
    mismatched_data_set["data_set_id"] = "prototype.other"
    assert_incompatible(
        "data_set_id",
        "数据集 ID 不一致",
        server_value=mismatched_data_set,
    )
    mismatched_schema = dict(server_manifest)
    mismatched_schema["shared_schema_hash"] = "0" * 64
    assert_incompatible(
        "shared_schema",
        "共享结构摘要不一致",
        server_value=mismatched_schema,
    )
    mismatched_content = dict(server_manifest)
    mismatched_content["shared_content_hash"] = "0" * 64
    assert_incompatible(
        "shared_content",
        "共享内容摘要不一致",
        server_value=mismatched_content,
    )
    wrong_target = dict(server_manifest)
    wrong_target["target"] = "Client"
    assert_incompatible("target", "target 错误", server_value=wrong_target)
    missing_field = dict(server_manifest)
    del missing_field["protocol_version"]
    assert_incompatible(
        "missing_field",
        "protocol_version 必须是正整数",
        server_value=missing_field,
    )
    assert_incompatible(
        "invalid_json",
        "Manifest JSON 无效",
        invalid_server_json="{invalid",
    )

    generated = csharp.read_text(encoding="utf-8")
    if "GodotFileAccess.Open" not in generated or "File.ReadAllBytes" in generated:
        raise RuntimeError("生成读取器未切换到支持 res:// / PCK 的 Godot FileAccess。")
    print("[DataTablePrototype] PASS: 生成读取器使用 Godot FileAccess")


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
    verify_generated_detection(sources)
    verify_export_target_artifacts(sources)
    build_performance_artifacts(sources)
    build_corruption_artifacts()
    print("[DataTablePrototype] PASS (41/41)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[DataTablePrototype] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
