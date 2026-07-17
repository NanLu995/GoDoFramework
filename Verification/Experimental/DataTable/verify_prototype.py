#!/usr/bin/env python3
"""Run deterministic generation and validation checks for the DataTable prototype."""

from __future__ import annotations

import hashlib
import shutil
import sys
from pathlib import Path

from compile_tables import CompileFailure, compile_tables
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


def main() -> int:
    scratch = ARTIFACT_ROOT / "scratch"
    if scratch.exists():
        shutil.rmtree(scratch)
    sources = generate_sources()
    verify_determinism(sources)
    verify_invalid_cases(sources)
    build_performance_artifacts(sources)
    print("[DataTablePrototype] PASS (8/8)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"[DataTablePrototype] FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
