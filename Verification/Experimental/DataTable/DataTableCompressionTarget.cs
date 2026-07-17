using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Godot;

#nullable enable

namespace GoDoFramework.Verification.DataTablePrototype;

internal enum PrototypeCompressionMode
{
    Auto,
    Never,
    Always,
}

internal readonly record struct CompressionCandidate(
    string TableId,
    byte[] UncompressedFile,
    byte[] ZstdFile,
    TimeSpan CompressionElapsed,
    TimeSpan DecompressionElapsed);

internal static class DataTableCompressionTarget
{
    private const ushort FormatVersion = 2;
    private const uint CompressionZstdFlag = 1u;
    private const int FlagsOffset = 8;

    internal static CompressionCandidate BuildCandidate(byte[] uncompressedFile)
    {
        Header header = ReadUncompressedHeader(uncompressedFile);
        ReadOnlySpan<byte> payload = uncompressedFile.AsSpan(header.PayloadOffset);
        byte[] actualHash = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(header.PayloadHash, actualHash))
            throw new InvalidDataException("DataTable 未压缩候选的 payload 摘要不匹配。");

        long compressionStarted = Stopwatch.GetTimestamp();
        byte[] compressedPayload = payload.ToArray().Compress(
            Godot.FileAccess.CompressionMode.Zstd);
        TimeSpan compressionElapsed = Stopwatch.GetElapsedTime(compressionStarted);
        if (compressedPayload.Length == 0)
            throw new InvalidDataException("Godot Zstd 未生成压缩 payload。");

        long decompressionStarted = Stopwatch.GetTimestamp();
        byte[] roundTrip = compressedPayload.Decompress(
            header.UncompressedSize,
            Godot.FileAccess.CompressionMode.Zstd);
        TimeSpan decompressionElapsed = Stopwatch.GetElapsedTime(decompressionStarted);
        if (!payload.SequenceEqual(roundTrip))
            throw new InvalidDataException("Godot Zstd round-trip 数据不一致。");

        var compressedFile = new byte[header.PayloadOffset + compressedPayload.Length];
        uncompressedFile.AsSpan(0, header.PayloadOffset).CopyTo(compressedFile);
        BinaryPrimitives.WriteUInt32LittleEndian(
            compressedFile.AsSpan(FlagsOffset, sizeof(uint)),
            CompressionZstdFlag);
        compressedPayload.CopyTo(compressedFile.AsSpan(header.PayloadOffset));
        return new CompressionCandidate(
            header.TableId,
            uncompressedFile,
            compressedFile,
            compressionElapsed,
            decompressionElapsed);
    }

    internal static byte[] Select(
        CompressionCandidate candidate,
        PrototypeCompressionMode mode) =>
        mode == PrototypeCompressionMode.Always
            ? candidate.ZstdFile
            : candidate.UncompressedFile;

    private static Header ReadUncompressedHeader(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual("GDTB"u8))
            throw new InvalidDataException("DataTable magic 不匹配。");
        if (reader.ReadUInt16() != FormatVersion)
            throw new InvalidDataException("DataTable 格式版本不兼容。");
        _ = reader.ReadUInt16();
        if (reader.ReadUInt32() != 0)
            throw new InvalidDataException("压缩目标只接受未压缩 DataTable 输入。");
        ushort tableIdLength = reader.ReadUInt16();
        string tableId = Encoding.UTF8.GetString(reader.ReadBytes(tableIdLength));
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt16();
        int uncompressedSize = checked((int)reader.ReadUInt32());
        byte[] payloadHash = reader.ReadBytes(32);
        int payloadOffset = checked((int)stream.Position);
        if (data.Length - payloadOffset != uncompressedSize)
            throw new InvalidDataException("DataTable 未压缩 payload 大小不匹配。");
        return new Header(tableId, uncompressedSize, payloadHash, payloadOffset);
    }

    private readonly record struct Header(
        string TableId,
        int UncompressedSize,
        byte[] PayloadHash,
        int PayloadOffset);
}
