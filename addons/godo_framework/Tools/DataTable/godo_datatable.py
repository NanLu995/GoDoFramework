#!/usr/bin/env python3
"""Validate CSV sources and emit deterministic GoDo DataTable artifacts."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import shutil
import struct
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any


FORMAT_VERSION = 2
BUILD_CONFIG_VERSION = 1
MAX_DIAGNOSTICS = 100
SUPPORTED_TYPES = {"string", "bool", "int32", "float64", "enum"}
BUILD_CONFIG_FIELDS = {"format_version", "profile", "source", "output", "csharp"}


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


def resolve_build_path(config_path: Path, field_name: str, raw_value: Any) -> Path:
    if not isinstance(raw_value, str) or not raw_value.strip():
        raise ValueError(f"Build Config 的 {field_name} 必须是非空相对路径。")
    if "\\" in raw_value:
        raise ValueError(f"Build Config 的 {field_name} 必须使用正斜杠。")
    relative = PurePosixPath(raw_value)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError(f"Build Config 的 {field_name} 不能是绝对路径或逃逸配置目录。")
    root = config_path.parent.resolve()
    resolved = root.joinpath(*relative.parts).resolve()
    if not resolved.is_relative_to(root):
        raise ValueError(f"Build Config 的 {field_name} 逃逸了配置目录。")
    return resolved


def load_build_config(path: Path) -> tuple[Path, Path, Path, Path]:
    with path.open("r", encoding="utf-8-sig") as stream:
        config = json.load(stream)
    if not isinstance(config, dict):
        raise ValueError("Build Config 根节点必须是对象。")
    if config.get("format_version") != BUILD_CONFIG_VERSION:
        raise ValueError(f"Build Config format_version 必须为 {BUILD_CONFIG_VERSION}。")
    missing = BUILD_CONFIG_FIELDS - config.keys()
    unknown = config.keys() - BUILD_CONFIG_FIELDS
    if missing:
        raise ValueError(f"Build Config 缺少字段：{', '.join(sorted(missing))}。")
    if unknown:
        raise ValueError(f"Build Config 包含未知字段：{', '.join(sorted(unknown))}。")
    profile = resolve_build_path(path, "profile", config["profile"])
    source = resolve_build_path(path, "source", config["source"])
    output = resolve_build_path(path, "output", config["output"])
    csharp = resolve_build_path(path, "csharp", config["csharp"])
    validate_output_paths(profile, source, output, csharp)
    return profile, source, output, csharp


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
    header.extend(struct.pack("<IHI", len(rows), len(fields), len(payload)))
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
using Godot;

#nullable enable

namespace {namespace};

{joined_declarations}

internal static class DataTablePrototypeLoader
{{
    private const uint CompressionZstdFlag = 1u;

{joined_loaders}

    private static ReaderContext Open(string path, string tableId, ushort schemaVersion, ushort fieldCount)
    {{
        byte[] data = File.ReadAllBytes(path);
        using var headerStream = new MemoryStream(data, writable: false);
        using var headerReader = new BinaryReader(headerStream, Encoding.UTF8, leaveOpen: true);
        if (!headerReader.ReadBytes(4).AsSpan().SequenceEqual("GDTB"u8))
            throw new InvalidDataException("DataTable magic 不匹配。");
        if (headerReader.ReadUInt16() != {FORMAT_VERSION})
            throw new InvalidDataException("DataTable 格式版本不兼容。");
        if (headerReader.ReadUInt16() != schemaVersion)
            throw new InvalidDataException("DataTable schema 版本不兼容。");
        uint flags = headerReader.ReadUInt32();
        if ((flags & ~CompressionZstdFlag) != 0)
            throw new InvalidDataException("DataTable 包含未知 flags。");
        ushort tableIdLength = headerReader.ReadUInt16();
        string actualTableId = Encoding.UTF8.GetString(headerReader.ReadBytes(tableIdLength));
        if (!StringComparer.Ordinal.Equals(actualTableId, tableId))
            throw new InvalidDataException($"DataTable ID 不匹配：{{actualTableId}}。");
        int rowCount = checked((int)headerReader.ReadUInt32());
        if (headerReader.ReadUInt16() != fieldCount)
            throw new InvalidDataException("DataTable 字段数量不匹配。");
        int uncompressedSize = checked((int)headerReader.ReadUInt32());
        byte[] expectedHash = headerReader.ReadBytes(32);
        int storedPayloadOffset = checked((int)headerStream.Position);

        byte[] payloadData;
        int payloadOffset;
        if ((flags & CompressionZstdFlag) == 0)
        {{
            if (data.Length - storedPayloadOffset != uncompressedSize)
                throw new InvalidDataException("DataTable 未压缩 payload 大小不匹配。");
            payloadData = data;
            payloadOffset = storedPayloadOffset;
        }}
        else
        {{
            byte[] storedPayload = data.AsSpan(storedPayloadOffset).ToArray();
            try
            {{
                payloadData = storedPayload.Decompress(
                    uncompressedSize,
                    Godot.FileAccess.CompressionMode.Zstd);
            }}
            catch (Exception exception)
            {{
                throw new InvalidDataException("DataTable Zstd 解压失败。", exception);
            }}
            if (payloadData.Length != uncompressedSize)
                throw new InvalidDataException("DataTable Zstd 解压大小不匹配。");
            payloadOffset = 0;
        }}

        byte[] actualHash = SHA256.HashData(
            payloadData.AsSpan(payloadOffset, uncompressedSize));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new InvalidDataException("DataTable payload 摘要不匹配。");
        var stream = new MemoryStream(
            payloadData,
            payloadOffset,
            uncompressedSize,
            writable: false,
            publiclyVisible: false);
        var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
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


def json_bytes(value: Any) -> bytes:
    return canonical_bytes(value) + b"\n"


def commit_files(files: dict[Path, bytes]) -> None:
    changed = {
        path: content
        for path, content in files.items()
        if not path.exists() or path.read_bytes() != content
    }
    staged_paths = {path: path.with_suffix(path.suffix + ".tmp") for path in changed}
    backup_paths = {path: path.with_suffix(path.suffix + ".previous") for path in changed}
    for path, content in changed.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        staged_paths[path].unlink(missing_ok=True)
        backup_paths[path].unlink(missing_ok=True)
        staged_paths[path].write_bytes(content)

    backed_up: list[Path] = []
    committed: list[Path] = []
    try:
        for path in changed:
            if path.exists():
                os.replace(path, backup_paths[path])
                backed_up.append(path)
            os.replace(staged_paths[path], path)
            committed.append(path)
    except Exception:
        for path in reversed(committed):
            path.unlink(missing_ok=True)
        for path in reversed(backed_up):
            if backup_paths[path].exists():
                os.replace(backup_paths[path], path)
        raise
    finally:
        for staged_path in staged_paths.values():
            staged_path.unlink(missing_ok=True)

    for backup_path in backup_paths.values():
        backup_path.unlink(missing_ok=True)


def commit_outputs(staged: Path, output: Path, csharp_path: Path, csharp_text: str) -> None:
    output_backup = output.with_name(f"{output.name}.previous")
    csharp_backup = csharp_path.with_suffix(csharp_path.suffix + ".previous")
    csharp_staged = csharp_path.with_suffix(csharp_path.suffix + ".tmp")
    encoded_csharp = csharp_text.encode("utf-8")
    csharp_changed = not csharp_path.exists() or csharp_path.read_bytes() != encoded_csharp
    if output_backup.exists():
        shutil.rmtree(output_backup)
    csharp_backup.unlink(missing_ok=True)
    csharp_staged.unlink(missing_ok=True)
    if csharp_changed:
        csharp_path.parent.mkdir(parents=True, exist_ok=True)
        csharp_staged.write_bytes(encoded_csharp)

    output_backed_up = False
    csharp_backed_up = False
    output_committed = False
    csharp_committed = False
    try:
        if output.exists():
            os.replace(output, output_backup)
            output_backed_up = True
        if csharp_changed and csharp_path.exists():
            os.replace(csharp_path, csharp_backup)
            csharp_backed_up = True
        os.replace(staged, output)
        output_committed = True
        if csharp_changed:
            os.replace(csharp_staged, csharp_path)
            csharp_committed = True
    except Exception:
        if output_committed and output.exists():
            shutil.rmtree(output)
        if output_backed_up and output_backup.exists():
            os.replace(output_backup, output)
        if csharp_committed:
            csharp_path.unlink(missing_ok=True)
        if csharp_backed_up and csharp_backup.exists():
            os.replace(csharp_backup, csharp_path)
        raise
    finally:
        csharp_staged.unlink(missing_ok=True)

    if output_backup.exists():
        shutil.rmtree(output_backup)
    csharp_backup.unlink(missing_ok=True)


def validate_output_paths(
    profile_path: Path,
    source_dir: Path,
    output: Path,
    csharp_path: Path,
) -> None:
    protected_paths = (profile_path, source_dir)
    for protected_path in protected_paths:
        if protected_path == output or protected_path.is_relative_to(output):
            raise ValueError("输出目录不能是 Profile、源目录或它们的祖先目录。")
    if csharp_path == profile_path:
        raise ValueError("生成的 C# 文件不能覆盖 Profile。")
    if csharp_path.is_relative_to(output):
        raise ValueError("生成的 C# 文件必须位于数据输出目录之外。")


def read_json_object(path: Path, description: str) -> dict[str, Any]:
    if not path.is_file():
        raise ValueError(f"单表生成要求已有完整产物；缺少 {description}：{path}。请先生成全部。")
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"无法读取已有 {description}：{path}。请先生成全部。") from error
    if not isinstance(value, dict):
        raise ValueError(f"已有 {description} 根节点无效：{path}。请先生成全部。")
    return value


def validate_single_table_baseline(
    output: Path,
    ir: dict[str, Any],
    manifest: dict[str, Any],
    binaries: dict[str, bytes],
    selected_table: str,
) -> None:
    previous_ir = read_json_object(output / "normalized.ir.json", "normalized.ir.json")
    previous_manifest = read_json_object(output / "manifest.json", "manifest.json")
    current_ids = [table["id"] for table in ir["tables"]]
    previous_tables = previous_ir.get("tables")
    manifest_tables = previous_manifest.get("tables")
    if not isinstance(previous_tables, list) or not isinstance(manifest_tables, list):
        raise ValueError("已有 IR 或 Manifest 缺少 tables 数组。请先生成全部。")
    try:
        previous_ids = [table["id"] for table in previous_tables]
        manifest_ids = [table["id"] for table in manifest_tables]
    except (KeyError, TypeError) as error:
        raise ValueError("已有 IR 或 Manifest 的数据表记录无效。请先生成全部。") from error
    if set(previous_ids) != set(current_ids) or set(manifest_ids) != set(current_ids):
        raise ValueError("数据表集合已发生变化，不能安全地单表生成。请先生成全部。")

    expected_artifacts = {f"{table_id}.gdtb" for table_id in current_ids}
    actual_artifacts = {path.name for path in output.glob("*.gdtb") if path.is_file()}
    if actual_artifacts != expected_artifacts:
        raise ValueError("已有二进制数据表集合不完整或包含过期文件。请先生成全部。")

    previous_by_id = {table["id"]: table for table in previous_tables}
    current_by_id = {table["id"]: table for table in ir["tables"]}
    for table_id in current_ids:
        if table_id == selected_table:
            continue
        previous_schema = {
            key: value for key, value in previous_by_id[table_id].items() if key != "rows"
        }
        current_schema = {
            key: value for key, value in current_by_id[table_id].items() if key != "rows"
        }
        if previous_schema != current_schema:
            raise ValueError(
                f"未选数据表 {table_id} 的结构已变化，不能安全地单表生成。请先生成全部。"
            )
        artifact = output / f"{table_id}.gdtb"
        if not artifact.is_file() or artifact.read_bytes() != binaries[table_id]:
            raise ValueError(
                f"未选数据表 {table_id} 的现有产物缺失或过期。请先生成全部。"
            )

    if manifest["data_set_id"] != previous_manifest.get("data_set_id"):
        raise ValueError("数据集标识已发生变化，不能安全地单表生成。请先生成全部。")


def compile_tables(
    profile_path: Path,
    source_dir: Path,
    output: Path | None = None,
    csharp_path: Path | None = None,
    *,
    check_only: bool = False,
    selected_table: str | None = None,
) -> None:
    if check_only and selected_table is not None:
        raise ValueError("check 模式不支持 --table；检查始终覆盖全部数据表。")
    if check_only:
        if output is not None or csharp_path is not None:
            raise ValueError("check 模式不能指定输出目录或 C# 文件。")
    elif output is None or csharp_path is None:
        raise ValueError("generate 模式必须指定输出目录和 C# 文件。")
    else:
        validate_output_paths(profile_path, source_dir, output, csharp_path)

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
    table_ids = [table["id"] for table in profile["tables"]]
    if selected_table is not None and selected_table not in table_ids:
        raise ValueError(
            f"Profile 中不存在数据表 {selected_table!r}；可用值：{', '.join(table_ids)}。"
        )
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

    binaries: dict[str, bytes] = {}
    binary_sizes: dict[str, int] = {}
    for table_profile, table_ir in zip(profile["tables"], ir["tables"], strict=True):
        table_id = table_profile["id"]
        binary = build_binary(table_profile, table_ir["rows"])
        binaries[table_id] = binary
        binary_sizes[table_id] = len(binary)
        manifest["tables"].append(
            {
                "id": table_id,
                "audience": table_profile["audience"],
                "schema_version": table_profile["schema_version"],
                "row_count": len(table_ir["rows"]),
                "content_hash": sha256_hex(table_ir),
                "artifact": f"{table_id}.gdtb",
            }
        )
    report: dict[str, Any] = {
        "status": "success",
        "scope": "single" if selected_table is not None else "all",
        "compression": "None",
        "source_bytes": {
            table["source"]: (source_dir / table["source"]).stat().st_size
            for table in profile["tables"]
        },
        "binary_bytes": binary_sizes,
        "diagnostic_count": 0,
    }
    if selected_table is not None:
        report["selected_table"] = selected_table

    if check_only:
        canonical_bytes(ir)
        canonical_bytes({table["id"]: table["rows"] for table in ir["tables"]})
        canonical_bytes(manifest)
        canonical_bytes(report)
        generate_csharp(profile)
        return

    assert output is not None
    assert csharp_path is not None
    csharp_text = generate_csharp(profile)
    if selected_table is not None:
        validate_single_table_baseline(output, ir, manifest, binaries, selected_table)
        files = {
            output / "normalized.ir.json": json_bytes(ir),
            output / "debug.json": json_bytes(
                {table["id"]: table["rows"] for table in ir["tables"]}
            ),
            output / "manifest.json": json_bytes(manifest),
            output / "build-report.json": json_bytes(report),
            output / f"{selected_table}.gdtb": binaries[selected_table],
            csharp_path: csharp_text.encode("utf-8"),
        }
        commit_files(files)
        return

    output.parent.mkdir(parents=True, exist_ok=True)
    staged = Path(tempfile.mkdtemp(prefix=f"{output.name}.", dir=output.parent))
    try:
        write_json(staged / "normalized.ir.json", ir)
        write_json(staged / "debug.json", {table["id"]: table["rows"] for table in ir["tables"]})
        for table_id, binary in binaries.items():
            (staged / f"{table_id}.gdtb").write_bytes(binary)
        write_json(staged / "manifest.json", manifest)
        write_json(staged / "build-report.json", report)
        commit_outputs(staged, output, csharp_path, csharp_text)
    finally:
        if staged.exists():
            shutil.rmtree(staged)


def add_input_arguments(parser: argparse.ArgumentParser, *, generate: bool) -> None:
    parser.add_argument("--build-config", type=Path, help="可移植的 DataTable Build Config。")
    parser.add_argument("--profile", type=Path, help="DataTable Profile 路径。")
    parser.add_argument("--source", type=Path, help="CSV 源目录。")
    if generate:
        parser.add_argument("--output", type=Path, help="数据产物目录。")
        parser.add_argument("--csharp", type=Path, help="生成的 C# 文件。")
        parser.add_argument("--table", help="仅提交指定表；仍会校验全部数据表。")


def resolve_command_paths(
    arguments: argparse.Namespace,
    *,
    generate: bool,
) -> tuple[Path, Path, Path | None, Path | None]:
    explicit_values = [arguments.profile, arguments.source]
    if generate:
        explicit_values.extend([arguments.output, arguments.csharp])
    if arguments.build_config is not None:
        if any(value is not None for value in explicit_values):
            raise ValueError("--build-config 不能与显式路径参数同时使用。")
        return load_build_config(arguments.build_config.resolve())
    if any(value is None for value in explicit_values):
        required = "--profile、--source、--output 和 --csharp" if generate else "--profile 和 --source"
        raise ValueError(f"未使用 --build-config 时必须提供 {required}。")
    profile = arguments.profile.resolve()
    source = arguments.source.resolve()
    if not generate:
        return profile, source, None, None
    return profile, source, arguments.output.resolve(), arguments.csharp.resolve()


def main() -> int:
    parser = argparse.ArgumentParser(description="GoDo DataTable 编译前端。")
    commands = parser.add_subparsers(dest="command", required=True)
    generate_parser = commands.add_parser("generate", help="校验并生成全部产物。")
    add_input_arguments(generate_parser, generate=True)
    check_parser = commands.add_parser("check", help="完整校验和构建，但不写入产物。")
    add_input_arguments(check_parser, generate=False)
    arguments = parser.parse_args()
    resolved_output: Path | None = None
    try:
        profile, source, output, csharp = resolve_command_paths(
            arguments,
            generate=arguments.command == "generate",
        )
        if arguments.command == "check":
            compile_tables(profile, source, check_only=True)
        else:
            assert output is not None
            assert csharp is not None
            resolved_output = output
            compile_tables(profile, source, output, csharp, selected_table=arguments.table)
    except CompileFailure as failure:
        for diagnostic in failure.diagnostics:
            print(diagnostic.format())
        print(f"[DataTableCompiler] FAIL ({len(failure.diagnostics)} diagnostics)")
        return 1
    except (KeyError, OSError, RuntimeError, ValueError) as error:
        print(f"[DataTableCompiler] FAIL: {error}", file=sys.stderr)
        return 1
    if arguments.command == "check":
        print("[DataTableCompiler] CHECK PASS")
    else:
        print(f"[DataTableCompiler] GENERATE PASS: {resolved_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
