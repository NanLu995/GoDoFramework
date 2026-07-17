#!/usr/bin/env python3
"""Validate CSV sources and emit deterministic DataTable prototype artifacts."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import shutil
import struct
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any


FORMAT_VERSION = 1
MAX_DIAGNOSTICS = 100
SUPPORTED_TYPES = {"string", "bool", "int32", "float64", "enum"}


@dataclass(frozen=True)
class Diagnostic:
    code: str
    source: str
    line: int
    column: str
    message: str

    def format(self) -> str:
        location = f"{self.source}:{self.line}" if self.line else self.source
        column = f" {self.column}" if self.column else ""
        return f"Error {self.code} {location}{column}: {self.message}"


class CompileFailure(Exception):
    def __init__(self, diagnostics: list[Diagnostic]):
        super().__init__("DataTable validation failed")
        self.diagnostics = diagnostics


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_hex(value: Any) -> str:
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def pascal_case(value: str) -> str:
    return "".join(part[:1].upper() + part[1:] for part in value.split("_"))


def load_profile(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as stream:
        profile = json.load(stream)
    if profile.get("format_version") != FORMAT_VERSION:
        raise RuntimeError(f"Profile format_version 必须为 {FORMAT_VERSION}。")
    if not profile.get("tables"):
        raise RuntimeError("Profile 至少需要一张表。")
    table_ids = [table["id"] for table in profile["tables"]]
    source_names = [table["source"] for table in profile["tables"]]
    if len(table_ids) != len(set(table_ids)):
        raise RuntimeError("Profile 的 Table ID 不能重复。")
    if len(source_names) != len(set(source_names)):
        raise RuntimeError("Profile 的 CSV source 不能重复。")
    for table in profile["tables"]:
        names = [field["name"] for field in table["fields"]]
        if len(names) != len(set(names)):
            raise RuntimeError(f"表 {table['id']} 的 Profile 字段名重复。")
        if table["primary_key"] not in names:
            raise RuntimeError(f"表 {table['id']} 的主键字段不存在。")
        primary_key_field = next(
            field for field in table["fields"] if field["name"] == table["primary_key"]
        )
        if primary_key_field["type"] != "string":
            raise RuntimeError(f"阶段 A 只支持字符串主键：{table['id']}。")
        for field in table["fields"]:
            if field["type"] not in SUPPORTED_TYPES:
                raise RuntimeError(
                    f"字段 {table['id']}.{field['name']} 使用不支持的类型 {field['type']}。"
                )
            if field["type"] == "enum":
                values = field.get("values", [])
                if not values or len(values) != len(set(values)):
                    raise RuntimeError(
                        f"字段 {table['id']}.{field['name']} 的 enum values 必须非空且唯一。"
                    )
    return profile


def add_diagnostic(
    diagnostics: list[Diagnostic],
    code: str,
    source: str,
    line: int,
    column: str,
    message: str,
) -> None:
    if len(diagnostics) < MAX_DIAGNOSTICS:
        diagnostics.append(Diagnostic(code, source, line, column, message))


def parse_bool(raw: str) -> bool | None:
    normalized = raw.strip().lower()
    if normalized == "true":
        return True
    if normalized == "false":
        return False
    return None


def parse_value(
    raw: str,
    field: dict[str, Any],
    source: str,
    line: int,
    diagnostics: list[Diagnostic],
) -> Any:
    name = field["name"]
    null_token = field.get("null_token")
    if null_token is not None and raw == null_token:
        if field.get("required", False):
            add_diagnostic(
                diagnostics, "DT101", source, line, name, "必填字段不能为 null。"
            )
        return None

    if raw == "":
        if "default" in field:
            return field["default"]
        if field["type"] == "string" and field.get("allow_empty", False):
            return ""
        if not field.get("required", False):
            return None
        add_diagnostic(diagnostics, "DT102", source, line, name, "必填字段不能为空。")
        return None

    field_type = field["type"]
    value: Any = raw
    if field_type == "bool":
        value = parse_bool(raw)
        if value is None:
            add_diagnostic(
                diagnostics, "DT103", source, line, name, "只能使用 true 或 false。"
            )
            return None
    elif field_type == "int32":
        try:
            value = int(raw, 10)
        except ValueError:
            add_diagnostic(
                diagnostics, "DT104", source, line, name, f"无法将 {raw[:80]!r} 转换为 int32。"
            )
            return None
        if not -(2**31) <= value < 2**31:
            add_diagnostic(diagnostics, "DT105", source, line, name, "超出 int32 范围。")
            return None
    elif field_type == "float64":
        try:
            value = float(raw)
        except ValueError:
            add_diagnostic(
                diagnostics, "DT106", source, line, name, f"无法将 {raw[:80]!r} 转换为 float64。"
            )
            return None
        if not math.isfinite(value):
            add_diagnostic(diagnostics, "DT107", source, line, name, "浮点值必须有限。")
            return None
    elif field_type == "enum":
        if raw not in field["values"]:
            choices = ", ".join(field["values"])
            add_diagnostic(
                diagnostics,
                "DT108",
                source,
                line,
                name,
                f"未知枚举值 {raw[:80]!r}；允许值：{choices}。",
            )
            return None

    if value is not None and "min" in field and value < field["min"]:
        add_diagnostic(
            diagnostics, "DT109", source, line, name, f"值必须大于等于 {field['min']}。"
        )
    if value is not None and "max" in field and value > field["max"]:
        add_diagnostic(
            diagnostics, "DT110", source, line, name, f"值必须小于等于 {field['max']}。"
        )
    if isinstance(value, str) and "min_length" in field and len(value) < field["min_length"]:
        add_diagnostic(
            diagnostics,
            "DT111",
            source,
            line,
            name,
            f"字符串长度必须大于等于 {field['min_length']}。",
        )
    if isinstance(value, str) and "max_length" in field and len(value) > field["max_length"]:
        add_diagnostic(
            diagnostics,
            "DT112",
            source,
            line,
            name,
            f"字符串长度必须小于等于 {field['max_length']}。",
        )
    return value


def read_table(
    source_dir: Path,
    table: dict[str, Any],
    diagnostics: list[Diagnostic],
) -> list[dict[str, Any]]:
    source_name = table["source"]
    source_path = source_dir / source_name
    expected_fields = [field["name"] for field in table["fields"]]
    try:
        stream = source_path.open("r", encoding="utf-8-sig", newline="")
    except (OSError, UnicodeError) as error:
        add_diagnostic(diagnostics, "DT001", source_name, 0, "", str(error))
        return []

    rows: list[dict[str, Any]] = []
    with stream:
        try:
            reader = csv.DictReader(stream, strict=True)
            headers = reader.fieldnames or []
            if len(headers) != len(set(headers)):
                add_diagnostic(diagnostics, "DT002", source_name, 1, "", "存在重复列名。")
            missing = [field for field in expected_fields if field not in headers]
            unknown = [field for field in headers if field not in expected_fields]
            for field in missing:
                add_diagnostic(diagnostics, "DT003", source_name, 1, field, "缺少 Profile 字段列。")
            for field in unknown:
                add_diagnostic(diagnostics, "DT004", source_name, 1, field, "列未在 Profile 中声明。")
            if missing or unknown or len(headers) != len(set(headers)):
                return []

            keys: dict[Any, int] = {}
            primary_key = table["primary_key"]
            for line, raw_row in enumerate(reader, start=2):
                if None in raw_row or any(value is None for value in raw_row.values()):
                    add_diagnostic(
                        diagnostics,
                        "DT005",
                        source_name,
                        line,
                        "",
                        "数据列数与表头不一致。",
                    )
                    continue
                parsed = {
                    field["name"]: parse_value(
                        raw_row[field["name"]], field, source_name, line, diagnostics
                    )
                    for field in table["fields"]
                }
                key = parsed[primary_key]
                if key is None or key == "":
                    add_diagnostic(diagnostics, "DT113", source_name, line, primary_key, "主键不能为空。")
                elif key in keys:
                    add_diagnostic(
                        diagnostics,
                        "DT114",
                        source_name,
                        line,
                        primary_key,
                        f"主键 {str(key)[:80]!r} 与第 {keys[key]} 行重复。",
                    )
                else:
                    keys[key] = line
                rows.append(parsed)
        except (csv.Error, UnicodeError) as error:
            add_diagnostic(
                diagnostics,
                "DT006",
                source_name,
                getattr(reader, "line_num", 0),
                "",
                f"CSV 解析失败：{error}",
            )
    return rows


def validate_foreign_keys(
    profile: dict[str, Any],
    tables: dict[str, list[dict[str, Any]]],
    diagnostics: list[Diagnostic],
) -> None:
    table_profiles = {table["id"]: table for table in profile["tables"]}
    key_sets = {
        table_id: {row[table_profiles[table_id]["primary_key"]] for row in rows}
        for table_id, rows in tables.items()
    }
    for table in profile["tables"]:
        for field in table["fields"]:
            foreign_key = field.get("foreign_key")
            if foreign_key is None:
                continue
            target_table, target_field = foreign_key.split(".", 1)
            target_profile = table_profiles.get(target_table)
            if target_profile is None or target_profile["primary_key"] != target_field:
                raise RuntimeError(f"无效 Profile 外键：{table['id']}.{field['name']} -> {foreign_key}")
            for index, row in enumerate(tables[table["id"]], start=2):
                value = row[field["name"]]
                if value is not None and value not in key_sets[target_table]:
                    add_diagnostic(
                        diagnostics,
                        "DT115",
                        table["source"],
                        index,
                        field["name"],
                        f"引用的 {foreign_key} 数据项 {str(value)[:80]!r} 不存在。",
                    )


def build_ir(profile: dict[str, Any], tables: dict[str, list[dict[str, Any]]]) -> dict[str, Any]:
    normalized_tables = []
    for table in profile["tables"]:
        primary_key = table["primary_key"]
        normalized_tables.append(
            {
                "id": table["id"],
                "schema_version": table["schema_version"],
                "audience": table["audience"],
                "primary_key": primary_key,
                "fields": table["fields"],
                "rows": sorted(tables[table["id"]], key=lambda row: row[primary_key]),
            }
        )
    return {
        "format_version": FORMAT_VERSION,
        "data_set_id": profile["data_set_id"],
        "protocol_version": profile["protocol_version"],
        "tables": normalized_tables,
    }


def build_binary(table_profile: dict[str, Any], rows: list[dict[str, Any]]) -> bytes:
    fields = table_profile["fields"]
    strings = sorted(
        {
            value
            for row in rows
            for field in fields
            if field["type"] == "string"
            for value in [row[field["name"]]]
            if value is not None
        }
    )
    string_indices = {value: index for index, value in enumerate(strings)}
    payload = bytearray()
    payload.extend(struct.pack("<I", len(strings)))
    for value in strings:
        encoded = value.encode("utf-8")
        payload.extend(struct.pack("<I", len(encoded)))
        payload.extend(encoded)

    bitmap_size = (len(fields) + 7) // 8
    for row in rows:
        bitmap = bytearray(bitmap_size)
        for index, field in enumerate(fields):
            if row[field["name"]] is not None:
                bitmap[index // 8] |= 1 << (index % 8)
        payload.extend(bitmap)
        for field in fields:
            value = row[field["name"]]
            if value is None:
                continue
            field_type = field["type"]
            if field_type == "string":
                payload.extend(struct.pack("<I", string_indices[value]))
            elif field_type == "bool":
                payload.extend(struct.pack("<B", 1 if value else 0))
            elif field_type == "int32":
                payload.extend(struct.pack("<i", value))
            elif field_type == "float64":
                payload.extend(struct.pack("<d", value))
            elif field_type == "enum":
                payload.extend(struct.pack("<H", field["values"].index(value)))

    primary_key = table_profile["primary_key"]
    sorted_index = sorted(
        ((row[primary_key], index) for index, row in enumerate(rows)), key=lambda pair: pair[0]
    )
    payload.extend(struct.pack("<I", len(sorted_index)))
    for key, row_index in sorted_index:
        payload.extend(struct.pack("<II", string_indices[key], row_index))

    table_id = table_profile["id"].encode("utf-8")
    header = bytearray(b"GDTB")
    header.extend(struct.pack("<HHI", FORMAT_VERSION, table_profile["schema_version"], 0))
    header.extend(struct.pack("<H", len(table_id)))
    header.extend(table_id)
    header.extend(struct.pack("<IH", len(rows), len(fields)))
    header.extend(hashlib.sha256(payload).digest())
    return bytes(header + payload)


def csharp_type(table: dict[str, Any], field: dict[str, Any]) -> str:
    field_type = field["type"]
    if field_type == "string":
        result = "string"
    elif field_type == "bool":
        result = "bool"
    elif field_type == "int32":
        result = "int"
    elif field_type == "float64":
        result = "double"
    else:
        result = f"{table['id']}{pascal_case(field['name'])}"
    if not field.get("required", False) and "default" not in field:
        result += "?"
    return result


def read_expression(table: dict[str, Any], field: dict[str, Any], index: int) -> str:
    required = field.get("required", False) or "default" in field
    suffix = "Required" if required else "Optional"
    field_type = field["type"]
    if field_type == "string":
        return f"ReadString{suffix}(context.Reader, context.Strings, bitmap, {index}, \"{field['name']}\")"
    if field_type == "bool":
        return f"ReadBool{suffix}(context.Reader, bitmap, {index}, \"{field['name']}\")"
    if field_type == "int32":
        return f"ReadInt32{suffix}(context.Reader, bitmap, {index}, \"{field['name']}\")"
    if field_type == "float64":
        return f"ReadDouble{suffix}(context.Reader, bitmap, {index}, \"{field['name']}\")"
    enum_type = f"{table['id']}{pascal_case(field['name'])}"
    return f"ReadEnum{suffix}<{enum_type}>(context.Reader, bitmap, {index}, {len(field['values'])}, \"{field['name']}\")"


def generate_csharp(profile: dict[str, Any]) -> str:
    namespace = profile["namespace"]
    declarations: list[str] = []
    loaders: list[str] = []
    for table in profile["tables"]:
        for field in table["fields"]:
            if field["type"] == "enum":
                enum_name = f"{table['id']}{pascal_case(field['name'])}"
                values = ",\n".join(
                    f"    {value} = {index}" for index, value in enumerate(field["values"])
                )
                declarations.append(f"internal enum {enum_name}\n{{\n{values}\n}}")

        row_name = f"{table['id']}Row"
        table_name = f"{table['id']}Table"
        parameters = ",\n".join(
            f"    {csharp_type(table, field)} {pascal_case(field['name'])}"
            for field in table["fields"]
        )
        declarations.append(f"internal readonly record struct {row_name}(\n{parameters});")
        declarations.append(
            f"""internal sealed class {table_name} : IReadOnlyCollection<{row_name}>
{{
    private readonly {row_name}[] _rows;
    private readonly Dictionary<string, int> _indices;

    internal {table_name}({row_name}[] rows, Dictionary<string, int> indices)
    {{
        _rows = rows;
        _indices = indices;
    }}

    public int Count => _rows.Length;

    public {row_name} Get(string id) => _rows[_indices[id]];

    public bool TryGet(string id, out {row_name} row)
    {{
        if (_indices.TryGetValue(id, out int index))
        {{
            row = _rows[index];
            return true;
        }}
        row = default;
        return false;
    }}

    public IEnumerator<{row_name}> GetEnumerator() => ((IEnumerable<{row_name}>)_rows).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _rows.GetEnumerator();
}}"""
        )
        arguments = ",\n                ".join(
            read_expression(table, field, index) for index, field in enumerate(table["fields"])
        )
        loaders.append(
            f"""    internal static {table_name} Load{table['id']}(string path)
    {{
        using ReaderContext context = Open(path, "{table['id']}", {table['schema_version']}, {len(table['fields'])});
        var rows = new {row_name}[context.RowCount];
        var bitmap = new byte[{(len(table['fields']) + 7) // 8}];
        for (int index = 0; index < rows.Length; index++)
        {{
            ReadBitmap(context.Reader, bitmap);
            rows[index] = new {row_name}(
                {arguments});
        }}
        Dictionary<string, int> indices = ReadIndex(context, rows.Length);
        EnsureEnd(context);
        return new {table_name}(rows, indices);
    }}"""
        )

    joined_declarations = "\n\n".join(declarations)
    joined_loaders = "\n\n".join(loaders)
    return f"""// <auto-generated />
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

#nullable enable

namespace {namespace};

{joined_declarations}

internal static class DataTablePrototypeLoader
{{
{joined_loaders}

    private static ReaderContext Open(string path, string tableId, ushort schemaVersion, ushort fieldCount)
    {{
        byte[] data = File.ReadAllBytes(path);
        var stream = new MemoryStream(data, writable: false);
        var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual("GDTB"u8))
            throw new InvalidDataException("DataTable magic 不匹配。");
        if (reader.ReadUInt16() != {FORMAT_VERSION})
            throw new InvalidDataException("DataTable 格式版本不兼容。");
        if (reader.ReadUInt16() != schemaVersion)
            throw new InvalidDataException("DataTable schema 版本不兼容。");
        if (reader.ReadUInt32() != 0)
            throw new InvalidDataException("阶段 A 不支持压缩或其他 flags。");
        ushort tableIdLength = reader.ReadUInt16();
        string actualTableId = Encoding.UTF8.GetString(reader.ReadBytes(tableIdLength));
        if (!StringComparer.Ordinal.Equals(actualTableId, tableId))
            throw new InvalidDataException($"DataTable ID 不匹配：{{actualTableId}}。");
        int rowCount = checked((int)reader.ReadUInt32());
        if (reader.ReadUInt16() != fieldCount)
            throw new InvalidDataException("DataTable 字段数量不匹配。");
        byte[] expectedHash = reader.ReadBytes(32);
        long payloadOffset = stream.Position;
        byte[] actualHash = SHA256.HashData(data.AsSpan(checked((int)payloadOffset)));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new InvalidDataException("DataTable payload 摘要不匹配。");
        int stringCount = checked((int)reader.ReadUInt32());
        var strings = new string[stringCount];
        for (int index = 0; index < strings.Length; index++)
        {{
            int byteCount = checked((int)reader.ReadUInt32());
            strings[index] = Encoding.UTF8.GetString(reader.ReadBytes(byteCount));
        }}
        return new ReaderContext(stream, reader, rowCount, strings);
    }}

    private static Dictionary<string, int> ReadIndex(ReaderContext context, int rowCount)
    {{
        int count = checked((int)context.Reader.ReadUInt32());
        if (count != rowCount)
            throw new InvalidDataException("DataTable 主键索引数量不匹配。");
        var indices = new Dictionary<string, int>(count, StringComparer.Ordinal);
        for (int index = 0; index < count; index++)
        {{
            string key = GetString(context.Strings, context.Reader.ReadUInt32());
            int rowIndex = checked((int)context.Reader.ReadUInt32());
            if (rowIndex < 0 || rowIndex >= rowCount || !indices.TryAdd(key, rowIndex))
                throw new InvalidDataException("DataTable 主键索引无效。");
        }}
        return indices;
    }}

    private static void EnsureEnd(ReaderContext context)
    {{
        if (context.Stream.Position != context.Stream.Length)
            throw new InvalidDataException("DataTable 文件包含未识别的尾部数据。");
    }}

    private static bool IsPresent(byte[] bitmap, int index) =>
        (bitmap[index / 8] & (1 << (index % 8))) != 0;

    private static void ReadBitmap(BinaryReader reader, byte[] bitmap)
    {{
        if (reader.Read(bitmap) != bitmap.Length)
            throw new EndOfStreamException("DataTable 行位图不完整。");
    }}

    private static string GetString(string[] strings, uint index)
    {{
        if (index >= strings.Length)
            throw new InvalidDataException("DataTable 字符串池索引越界。");
        return strings[index];
    }}

    private static string ReadStringRequired(BinaryReader reader, string[] strings, byte[] bitmap, int index, string name)
    {{
        RequirePresent(bitmap, index, name);
        return GetString(strings, reader.ReadUInt32());
    }}

    private static string? ReadStringOptional(BinaryReader reader, string[] strings, byte[] bitmap, int index, string name) =>
        IsPresent(bitmap, index) ? GetString(strings, reader.ReadUInt32()) : null;

    private static bool ReadBoolRequired(BinaryReader reader, byte[] bitmap, int index, string name)
    {{
        RequirePresent(bitmap, index, name);
        byte value = reader.ReadByte();
        if (value > 1)
            throw new InvalidDataException($"字段 {{name}} 的 bool 编码无效。");
        return value == 1;
    }}

    private static bool? ReadBoolOptional(BinaryReader reader, byte[] bitmap, int index, string name) =>
        IsPresent(bitmap, index) ? ReadBoolRequired(reader, bitmap, index, name) : null;

    private static int ReadInt32Required(BinaryReader reader, byte[] bitmap, int index, string name)
    {{
        RequirePresent(bitmap, index, name);
        return reader.ReadInt32();
    }}

    private static int? ReadInt32Optional(BinaryReader reader, byte[] bitmap, int index, string name) =>
        IsPresent(bitmap, index) ? reader.ReadInt32() : null;

    private static double ReadDoubleRequired(BinaryReader reader, byte[] bitmap, int index, string name)
    {{
        RequirePresent(bitmap, index, name);
        double value = reader.ReadDouble();
        if (!double.IsFinite(value))
            throw new InvalidDataException($"字段 {{name}} 不是有限浮点值。");
        return value;
    }}

    private static double? ReadDoubleOptional(BinaryReader reader, byte[] bitmap, int index, string name) =>
        IsPresent(bitmap, index) ? reader.ReadDouble() : null;

    private static TEnum ReadEnumRequired<TEnum>(BinaryReader reader, byte[] bitmap, int index, int valueCount, string name)
        where TEnum : struct, Enum
    {{
        RequirePresent(bitmap, index, name);
        ushort value = reader.ReadUInt16();
        if (value >= valueCount)
            throw new InvalidDataException($"字段 {{name}} 的 enum 编码越界。");
        return (TEnum)Enum.ToObject(typeof(TEnum), value);
    }}

    private static TEnum? ReadEnumOptional<TEnum>(BinaryReader reader, byte[] bitmap, int index, int valueCount, string name)
        where TEnum : struct, Enum =>
        IsPresent(bitmap, index) ? ReadEnumRequired<TEnum>(reader, bitmap, index, valueCount, name) : null;

    private static void RequirePresent(byte[] bitmap, int index, string name)
    {{
        if (!IsPresent(bitmap, index))
            throw new InvalidDataException($"必填字段 {{name}} 缺失。");
    }}

    private sealed class ReaderContext : IDisposable
    {{
        internal ReaderContext(MemoryStream stream, BinaryReader reader, int rowCount, string[] strings)
        {{
            Stream = stream;
            Reader = reader;
            RowCount = rowCount;
            Strings = strings;
        }}

        internal MemoryStream Stream {{ get; }}
        internal BinaryReader Reader {{ get; }}
        internal int RowCount {{ get; }}
        internal string[] Strings {{ get; }}
        public void Dispose() => Reader.Dispose();
    }}
}}
"""


def write_json(path: Path, value: Any) -> None:
    path.write_bytes(canonical_bytes(value) + b"\n")


def replace_directory(staged: Path, output: Path) -> None:
    backup = output.with_name(f"{output.name}.previous")
    if backup.exists():
        shutil.rmtree(backup)
    if output.exists():
        os.replace(output, backup)
    try:
        os.replace(staged, output)
    except Exception:
        if backup.exists() and not output.exists():
            os.replace(backup, output)
        raise
    if backup.exists():
        shutil.rmtree(backup)


def atomic_write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(text, encoding="utf-8", newline="\n")
    os.replace(temporary, path)


def compile_tables(profile_path: Path, source_dir: Path, output: Path, csharp_path: Path) -> None:
    profile = load_profile(profile_path)
    diagnostics: list[Diagnostic] = []
    tables = {
        table["id"]: read_table(source_dir, table, diagnostics)
        for table in profile["tables"]
    }
    validate_foreign_keys(profile, tables, diagnostics)
    if diagnostics:
        raise CompileFailure(diagnostics)

    ir = build_ir(profile, tables)
    full_schema = [
        {key: value for key, value in table.items() if key != "rows"}
        for table in ir["tables"]
    ]
    shared_tables = [table for table in ir["tables"] if table["audience"] == "Shared"]
    manifest: dict[str, Any] = {
        "format_version": FORMAT_VERSION,
        "data_set_id": profile["data_set_id"],
        "protocol_version": profile["protocol_version"],
        "full_schema_hash": sha256_hex(full_schema),
        "full_content_hash": sha256_hex(ir["tables"]),
        "shared_schema_hash": sha256_hex(
            [{key: value for key, value in table.items() if key != "rows"} for table in shared_tables]
        ),
        "shared_content_hash": sha256_hex(shared_tables),
        "tables": [],
    }

    output.parent.mkdir(parents=True, exist_ok=True)
    staged = Path(tempfile.mkdtemp(prefix=f"{output.name}.", dir=output.parent))
    try:
        write_json(staged / "normalized.ir.json", ir)
        write_json(staged / "debug.json", {table["id"]: table["rows"] for table in ir["tables"]})
        binary_sizes: dict[str, int] = {}
        for table_profile, table_ir in zip(profile["tables"], ir["tables"], strict=True):
            binary = build_binary(table_profile, table_ir["rows"])
            file_name = f"{table_profile['id']}.gdtb"
            (staged / file_name).write_bytes(binary)
            binary_sizes[table_profile["id"]] = len(binary)
            manifest["tables"].append(
                {
                    "id": table_profile["id"],
                    "audience": table_profile["audience"],
                    "schema_version": table_profile["schema_version"],
                    "row_count": len(table_ir["rows"]),
                    "content_hash": sha256_hex(table_ir),
                    "artifact": file_name,
                }
            )
        write_json(staged / "manifest.json", manifest)
        report = {
            "status": "success",
            "compression": "None",
            "source_bytes": {
                table["source"]: (source_dir / table["source"]).stat().st_size
                for table in profile["tables"]
            },
            "binary_bytes": binary_sizes,
            "diagnostic_count": 0,
        }
        write_json(staged / "build-report.json", report)
        replace_directory(staged, output)
        atomic_write_text(csharp_path, generate_csharp(profile))
    finally:
        if staged.exists():
            shutil.rmtree(staged)


def main() -> int:
    parser = argparse.ArgumentParser(description="编译 DataTable 阶段 A 原型。")
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--csharp", type=Path, required=True)
    arguments = parser.parse_args()
    try:
        compile_tables(
            arguments.profile.resolve(),
            arguments.source.resolve(),
            arguments.output.resolve(),
            arguments.csharp.resolve(),
        )
    except CompileFailure as failure:
        for diagnostic in failure.diagnostics:
            print(diagnostic.format())
        print(f"[DataTableCompiler] FAIL ({len(failure.diagnostics)} diagnostics)")
        return 1
    print(f"[DataTableCompiler] PASS: {arguments.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
