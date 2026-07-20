using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using GoDoFramework.Verification.DataTablePrototype;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>DataTable 阶段 B 的 Godot Zstd 实验目标处理入口。</summary>
public sealed partial class DataTableCompressionTargetRunner : Node
{
    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            RunTarget();
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[DataTableCompressionTarget] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void RunTarget()
    {
        string root = ProjectSettings.GlobalizePath(
            "res://Verification/Experimental/DataTable");
        string artifactRoot = Path.Combine(root, "Artifacts");
        string inputDirectory = Path.Combine(artifactRoot, "output");
        string candidateDirectory = Path.Combine(artifactRoot, "compression");
        string selectedDirectory = Path.Combine(artifactRoot, "selected");
        Directory.CreateDirectory(candidateDirectory);
        Directory.CreateDirectory(selectedDirectory);

        var reports = new List<CompressionReportEntry>();
        CompressionCandidate? itemCandidate = null;
        string[] inputFiles = Directory.GetFiles(inputDirectory, "*.gdtb")
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (inputFiles.Length == 0)
            throw new InvalidOperationException("没有可压缩的 DataTable 产物。");

        foreach (string inputPath in inputFiles)
        {
            CompressionCandidate candidate = DataTableCompressionTarget.BuildCandidate(
                File.ReadAllBytes(inputPath));
            if (candidate.TableId == "Item")
                itemCandidate = candidate;
            const PrototypeCompressionMode mode = PrototypeCompressionMode.Auto;
            byte[] selected = DataTableCompressionTarget.Select(candidate, mode);
            string fileName = Path.GetFileName(inputPath);
            WriteAtomically(Path.Combine(candidateDirectory, fileName), candidate.ZstdFile);
            WriteAtomically(Path.Combine(selectedDirectory, fileName), selected);

            string recommendation = candidate.ZstdFile.Length < candidate.UncompressedFile.Length
                ? "Zstd"
                : "None";
            string selection = mode == PrototypeCompressionMode.Always ? "Zstd" : "None";
            reports.Add(new CompressionReportEntry(
                candidate.TableId,
                mode.ToString(),
                selection,
                recommendation,
                candidate.UncompressedFile.Length,
                candidate.ZstdFile.Length));
            GD.Print(
                $"[DataTableCompressionTarget] {candidate.TableId}: Mode={mode}; " +
                $"SourceBytes={candidate.UncompressedFile.Length}; " +
                $"ZstdBytes={candidate.ZstdFile.Length}; " +
                $"CompressMs={candidate.CompressionElapsed.TotalMilliseconds:F3}; " +
                $"DecompressMs={candidate.DecompressionElapsed.TotalMilliseconds:F3}");
        }

        if (itemCandidate is null)
            throw new InvalidOperationException("缺少 Item 压缩候选，无法生成损坏样例。");
        VerifyModeSelection(itemCandidate.Value);
        WriteCompressedCorruptionArtifacts(artifactRoot, itemCandidate.Value);

        var report = new CompressionReport(PrototypeCompressionMode.Auto.ToString(), reports);
        string reportJson = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions { WriteIndented = true });
        WriteAtomically(
            Path.Combine(artifactRoot, "compression-report.json"),
            System.Text.Encoding.UTF8.GetBytes(reportJson + "\n"));
        GD.Print($"[DataTableCompressionTarget] PASS ({reports.Count}/{reports.Count})");
    }

    private static void VerifyModeSelection(CompressionCandidate candidate)
    {
        AssertBytes(
            candidate.UncompressedFile,
            DataTableCompressionTarget.Select(candidate, PrototypeCompressionMode.Auto),
            "Auto 必须保守选择未压缩产物");
        AssertBytes(
            candidate.UncompressedFile,
            DataTableCompressionTarget.Select(candidate, PrototypeCompressionMode.Never),
            "Never 必须选择未压缩产物");
        AssertBytes(
            candidate.ZstdFile,
            DataTableCompressionTarget.Select(candidate, PrototypeCompressionMode.Always),
            "Always 必须选择 Zstd 产物");
    }

    private static void WriteCompressedCorruptionArtifacts(
        string artifactRoot,
        CompressionCandidate candidate)
    {
        string directory = Path.Combine(artifactRoot, "compression-corruption");
        Directory.CreateDirectory(directory);

        byte[] tampered = (byte[])candidate.ZstdFile.Clone();
        tampered[^1] ^= 0xFF;
        WriteAtomically(Path.Combine(directory, "tampered-zstd.gdtb"), tampered);

        int tableIdLength = BinaryPrimitives.ReadUInt16LittleEndian(
            candidate.ZstdFile.AsSpan(12, sizeof(ushort)));
        int uncompressedSizeOffset = 14 + tableIdLength + sizeof(uint) + sizeof(ushort);
        byte[] badSize = (byte[])candidate.ZstdFile.Clone();
        int uncompressedSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            badSize.AsSpan(uncompressedSizeOffset, sizeof(uint))));
        BinaryPrimitives.WriteUInt32LittleEndian(
            badSize.AsSpan(uncompressedSizeOffset, sizeof(uint)),
            checked((uint)(uncompressedSize - 1)));
        WriteAtomically(Path.Combine(directory, "bad-uncompressed-size.gdtb"), badSize);

        byte[] badHash = (byte[])candidate.ZstdFile.Clone();
        badHash.AsSpan(uncompressedSizeOffset + sizeof(uint), 32).Clear();
        WriteAtomically(Path.Combine(directory, "bad-payload-hash.gdtb"), badHash);
    }

    private static void WriteAtomically(string path, byte[] data)
    {
        string temporaryPath = path + ".tmp";
        File.WriteAllBytes(temporaryPath, data);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void AssertBytes(byte[] expected, byte[] actual, string message)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new InvalidOperationException(message);
    }

    private sealed record CompressionReport(
        [property: JsonPropertyName("default_mode")] string DefaultMode,
        [property: JsonPropertyName("tables")] IReadOnlyList<CompressionReportEntry> Tables);

    private sealed record CompressionReportEntry(
        [property: JsonPropertyName("table_id")] string TableId,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("selected")] string Selected,
        [property: JsonPropertyName("recommendation")] string Recommendation,
        [property: JsonPropertyName("uncompressed_bytes")] int UncompressedBytes,
        [property: JsonPropertyName("zstd_bytes")] int ZstdBytes);
}
