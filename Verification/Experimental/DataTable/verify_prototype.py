#!/usr/bin/env python3
"""Run deterministic generation and validation checks for the DataTable prototype."""

from __future__ import annotations

import hashlib
import shutil
import struct
import sys
from pathlib import Path

from compile_tables import FORMAT_VERSION, CompileFailure, compile_tables
from generate_fixtures import write_invalid_sets, write_valid_set


SCRIPT_DIR = Path(__file__).resolve().parent
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
    build_performance_artifacts(sources)
    build_corruption_artifacts()
    print("[DataTablePrototype] PASS (8/8)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[DataTablePrototype] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
