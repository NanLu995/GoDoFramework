using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using GodotFileAccess = Godot.FileAccess;

#nullable enable

namespace GoDo;

/// <summary>使用校验容器、临时文件和单份备份实现的多槽位存档服务。</summary>
public sealed class SaveService : ISaveService
{
    private const string SaveDirectory = "user://saves";
    private const string SaveExtension = ".gdsave";
    private const string BackupExtension = ".bak";
    private const string TemporaryExtension = ".tmp";
    private const ushort ContainerVersion = 1;
    private const int HashLength = 32;
    private const int MaxPayloadBytes = 64 * 1024 * 1024;
    private const int HeaderLength = 8 + sizeof(ushort) + sizeof(int) + sizeof(long) + sizeof(int) + HashLength;

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GODOSAVE");

    public void Save<T>(SaveSlot slot, T value, int dataVersion, ISaveCodec<T> codec)
    {
        VerifyAccess();
        ValidateSlot(slot);
        ArgumentNullException.ThrowIfNull(codec);

        if (dataVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(dataVersion), "业务存档版本必须大于 0。");

        try
        {
            byte[] payload = codec.Encode(value) ??
                throw new InvalidOperationException("ISaveCodec.Encode 返回了 null。");
            if (payload.Length > MaxPayloadBytes)
            {
                throw new InvalidOperationException(
                    $"存档 Payload 超过首版上限 {MaxPayloadBytes} bytes。");
            }

            DateTimeOffset savedAtUtc = DateTimeOffset.UtcNow;
            byte[] container = BuildContainer(payload, dataVersion, savedAtUtc);
            EnsureSaveDirectory();

            SavePaths paths = GetPaths(slot);
            WriteFile(paths.Temporary, container);

            // 写入完成后立刻按读取路径校验临时文件，不能把半写文件替换为正式档。
            ReadContainer(paths.Temporary);
            CommitTemporaryFile(paths);
        }
        catch (SaveException)
        {
            throw;
        }
        catch (Exception exception)
        {
            BestEffortRemove(GetPaths(slot).Temporary);
            throw new SaveException(
                slot,
                SaveOperation.Save,
                $"保存槽位失败: {slot.Value}",
                exception);
        }
    }

    public SaveLoadResult<T> Load<T>(SaveSlot slot, ISaveCodec<T> codec)
    {
        VerifyAccess();
        ValidateSlot(slot);
        ArgumentNullException.ThrowIfNull(codec);

        SavePaths paths = GetPaths(slot);
        bool hasPrimary = GodotFileAccess.FileExists(paths.Primary);
        bool hasBackup = GodotFileAccess.FileExists(paths.Backup);
        if (!hasPrimary && !hasBackup)
            return SaveLoadResult<T>.NotFound();

        Exception? primaryFailure = null;
        if (hasPrimary)
        {
            try
            {
                return ReadAndDecode(slot, paths.Primary, codec, recoveredFromBackup: false);
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
            }
        }

        if (hasBackup)
        {
            try
            {
                return ReadAndDecode(slot, paths.Backup, codec, recoveredFromBackup: true);
            }
            catch (Exception backupFailure)
            {
                var failures = new List<Exception>(2);
                if (primaryFailure != null)
                    failures.Add(primaryFailure);
                failures.Add(backupFailure);

                throw new SaveException(
                    slot,
                    SaveOperation.Load,
                    $"正式存档与备份均无法读取: {slot.Value}",
                    new AggregateException(failures));
            }
        }

        throw new SaveException(
            slot,
            SaveOperation.Load,
            $"正式存档无法读取且没有可用备份: {slot.Value}",
            primaryFailure);
    }

    public bool Exists(SaveSlot slot)
    {
        VerifyAccess();
        ValidateSlot(slot);
        SavePaths paths = GetPaths(slot);
        return GodotFileAccess.FileExists(paths.Primary) ||
               GodotFileAccess.FileExists(paths.Backup);
    }

    public bool Delete(SaveSlot slot)
    {
        VerifyAccess();
        ValidateSlot(slot);
        SavePaths paths = GetPaths(slot);
        string[] files = { paths.Primary, paths.Backup, paths.Temporary };
        bool removedAny = false;
        List<Exception>? failures = null;

        for (int i = 0; i < files.Length; i++)
        {
            if (!GodotFileAccess.FileExists(files[i]))
                continue;

            Error error = DirAccess.RemoveAbsolute(files[i]);
            if (error == Error.Ok)
            {
                removedAny = true;
                continue;
            }

            failures ??= new List<Exception>();
            failures.Add(new IOException($"删除文件失败，Error={error}: {files[i]}"));
        }

        if (failures != null)
        {
            throw new SaveException(
                slot,
                SaveOperation.Delete,
                $"删除槽位文件失败: {slot.Value}",
                new AggregateException(failures));
        }

        return removedAny;
    }

    private static SaveLoadResult<T> ReadAndDecode<T>(
        SaveSlot slot,
        string path,
        ISaveCodec<T> codec,
        bool recoveredFromBackup)
    {
        ContainerData container = ReadContainer(path);
        T value;
        try
        {
            value = codec.Decode(container.Payload, container.DataVersion);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"业务 Codec 解码失败: {slot.Value}, version={container.DataVersion}",
                exception);
        }

        return SaveLoadResult<T>.Loaded(
            value,
            container.DataVersion,
            container.SavedAtUtc,
            recoveredFromBackup);
    }

    private static byte[] BuildContainer(
        byte[] payload,
        int dataVersion,
        DateTimeOffset savedAtUtc)
    {
        byte[] hash = SHA256.HashData(payload);
        using var stream = new MemoryStream(HeaderLength + payload.Length);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(ContainerVersion);
        writer.Write(dataVersion);
        writer.Write(savedAtUtc.ToUnixTimeMilliseconds());
        writer.Write(payload.Length);
        writer.Write(hash);
        writer.Write(payload);
        writer.Flush();
        return stream.ToArray();
    }

    private static ContainerData ReadContainer(string path)
    {
        byte[] bytes = ReadFile(path);
        if (bytes.Length < HeaderLength)
            throw new InvalidDataException($"存档文件短于最小容器长度: {path}");

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        byte[] magic = reader.ReadBytes(Magic.Length);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException($"存档魔数不匹配: {path}");

        ushort containerVersion = reader.ReadUInt16();
        if (containerVersion != ContainerVersion)
        {
            throw new InvalidDataException(
                $"不支持的存档容器版本 {containerVersion}: {path}");
        }

        int dataVersion = reader.ReadInt32();
        if (dataVersion <= 0)
            throw new InvalidDataException($"业务存档版本无效: {path}");

        long savedAtUnixMilliseconds = reader.ReadInt64();
        DateTimeOffset savedAtUtc;
        try
        {
            savedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(savedAtUnixMilliseconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException($"存档 UTC 时间无效: {path}", exception);
        }

        int payloadLength = reader.ReadInt32();
        if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
            throw new InvalidDataException($"存档 Payload 长度无效: {path}");

        byte[] expectedHash = reader.ReadBytes(HashLength);
        if (expectedHash.Length != HashLength)
            throw new InvalidDataException($"存档 SHA-256 不完整: {path}");

        if (stream.Length - stream.Position != payloadLength)
            throw new InvalidDataException($"存档 Payload 长度与文件不匹配: {path}");

        byte[] payload = reader.ReadBytes(payloadLength);
        byte[] actualHash = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new InvalidDataException($"存档 SHA-256 校验失败: {path}");

        return new ContainerData(payload, dataVersion, savedAtUtc);
    }

    private static byte[] ReadFile(string path)
    {
        using GodotFileAccess? file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new IOException(
                $"无法打开存档文件，Error={GodotFileAccess.GetOpenError()}: {path}");
        }

        ulong length = file.GetLength();
        if (length > (ulong)(HeaderLength + MaxPayloadBytes))
            throw new InvalidDataException($"存档文件长度超出首版限制: {path}");

        return file.GetBuffer(checked((long)length));
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        using GodotFileAccess? file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new IOException(
                $"无法创建临时存档，Error={GodotFileAccess.GetOpenError()}: {path}");
        }

        file.StoreBuffer(bytes);
        file.Flush();
    }

    private static void CommitTemporaryFile(SavePaths paths)
    {
        bool movedPrimary = false;

        if (GodotFileAccess.FileExists(paths.Primary))
        {
            bool primaryIsValid;
            try
            {
                ReadContainer(paths.Primary);
                primaryIsValid = true;
            }
            catch
            {
                primaryIsValid = false;
            }

            if (primaryIsValid)
            {
                if (GodotFileAccess.FileExists(paths.Backup))
                    EnsureFileRemoved(paths.Backup);

                Error backupError = DirAccess.RenameAbsolute(paths.Primary, paths.Backup);
                if (backupError != Error.Ok)
                {
                    throw new IOException(
                        $"正式存档转为备份失败，Error={backupError}: {paths.Primary}");
                }

                movedPrimary = true;
            }
            else
            {
                // 损坏的正式档不能覆盖仍可能健康的备份。新临时档已经完整校验，
                // 删除损坏正式档后提交；若提交失败，原备份仍保留供恢复。
                EnsureFileRemoved(paths.Primary);
            }
        }

        Error commitError = DirAccess.RenameAbsolute(paths.Temporary, paths.Primary);
        if (commitError == Error.Ok)
            return;

        if (movedPrimary && !GodotFileAccess.FileExists(paths.Primary))
        {
            Error restoreError = DirAccess.RenameAbsolute(paths.Backup, paths.Primary);
            if (restoreError != Error.Ok)
            {
                throw new IOException(
                    $"提交新存档失败且旧存档恢复失败，commit={commitError}, restore={restoreError}");
            }
        }

        throw new IOException($"临时存档提交失败，Error={commitError}: {paths.Primary}");
    }

    private static void EnsureSaveDirectory()
    {
        if (DirAccess.DirExistsAbsolute(SaveDirectory))
            return;

        Error error = DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);
        if (error != Error.Ok)
            throw new IOException($"创建存档目录失败，Error={error}: {SaveDirectory}");
    }

    private static void EnsureFileRemoved(string path)
    {
        Error error = DirAccess.RemoveAbsolute(path);
        if (error != Error.Ok)
            throw new IOException($"移除旧备份失败，Error={error}: {path}");
    }

    private static void BestEffortRemove(string path)
    {
        if (GodotFileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(path);
    }

    private static void ValidateSlot(SaveSlot slot)
    {
        if (!slot.IsValid)
            throw new ArgumentException("SaveSlot 未初始化。", nameof(slot));
    }

    private static void VerifyAccess()
    {
        MainThreadGuard.VerifyAccess();
    }

    private static SavePaths GetPaths(SaveSlot slot)
    {
        string primary = $"{SaveDirectory}/{slot.Value}{SaveExtension}";
        return new SavePaths(
            primary,
            primary + BackupExtension,
            primary + TemporaryExtension);
    }

    private readonly record struct SavePaths(string Primary, string Backup, string Temporary);

    private readonly record struct ContainerData(
        byte[] Payload,
        int DataVersion,
        DateTimeOffset SavedAtUtc);
}
