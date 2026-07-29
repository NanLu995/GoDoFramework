using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using GoDo;
using GodotFileAccess = Godot.FileAccess;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>SaveService 的无交互回归验证入口。</summary>
public sealed partial class SaveServiceRegression : Node
{
    private readonly List<SaveSlot> _createdSlots = new();
    private ISaveService _saves = null!;
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            _saves = Services.Get<ISaveService>();
            Run("NotFound 正常结果", VerifyNotFound);
            Run("保存读取与元数据", VerifyRoundTrip);
            Run("正式档损坏后备份恢复", VerifyBackupRecovery);
            Run("正式档损坏且无备份", VerifyPrimaryCorruptionWithoutBackup);
            Run("正式档与备份双重损坏", VerifyPrimaryAndBackupCorruption);
            Run("健康备份不被损坏正式档覆盖", VerifyHealthyBackupProtection);
            Run("Codec 异常边界", VerifyCodecFailure);
            Run("删除正式档与备份", VerifyDelete);

            GD.Print($"[SaveServiceRegression] PASS ({_passed}/8)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            CleanupSlots();
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[SaveServiceRegression] PASS: {name}");
    }

    private void VerifyNotFound()
    {
        SaveSlot slot = CreateSlot();
        SaveLoadResult<TestData> result = _saves.Load(slot, TestCodec.Instance);

        AssertEqual(SaveLoadStatus.NotFound, result.Status, "空槽位状态错误");
        Assert(!result.HasValue, "NotFound 错误地包含值");
        AssertEqual(0, result.DataVersion, "NotFound 业务版本不为 0");
        Assert(result.SavedAtUtc is null, "NotFound 错误地包含保存时间");
        AssertThrows<InvalidOperationException>(
            () => _ = result.Value,
            "访问 NotFound.Value 没有失败");
    }

    private void VerifyRoundTrip()
    {
        SaveSlot slot = CreateSlot();
        var expected = new TestData(42, "round-trip");
        DateTimeOffset beforeSave = DateTimeOffset.UtcNow;

        _saves.Save(slot, expected, dataVersion: 3, TestCodec.Instance);
        SaveLoadResult<TestData> result = _saves.Load(slot, TestCodec.Instance);

        Assert(_saves.Exists(slot), "保存后 Exists 返回 false");
        AssertEqual(SaveLoadStatus.Loaded, result.Status, "正常读取状态错误");
        AssertEqual(expected, result.Value, "读取值与保存值不一致");
        AssertEqual(3, result.DataVersion, "业务版本没有保留");
        Assert(
            result.SavedAtUtc >= beforeSave.AddMilliseconds(-1),
            "保存 UTC 时间早于允许的毫秒精度误差");
        Assert(result.SavedAtUtc <= DateTimeOffset.UtcNow, "保存 UTC 时间晚于当前时间");
    }

    private void VerifyBackupRecovery()
    {
        SaveSlot slot = CreateSlot();
        var backupValue = new TestData(1, "backup");
        var primaryValue = new TestData(2, "primary");

        _saves.Save(slot, backupValue, dataVersion: 1, TestCodec.Instance);
        _saves.Save(slot, primaryValue, dataVersion: 2, TestCodec.Instance);
        CorruptPrimary(slot);

        SaveLoadResult<TestData> result = _saves.Load(slot, TestCodec.Instance);
        AssertEqual(
            SaveLoadStatus.RecoveredFromBackup,
            result.Status,
            "正式档损坏后没有从备份恢复");
        AssertEqual(backupValue, result.Value, "恢复值不是旧正式档形成的备份");
        AssertEqual(1, result.DataVersion, "备份业务版本错误");
    }

    private void VerifyCodecFailure()
    {
        SaveSlot encodeSlot = CreateSlot();
        SaveException encodeFailure = AssertThrows<SaveException>(
            () => _saves.Save(encodeSlot, new TestData(1, "encode"), 1, ThrowingCodec.EncodeFailure),
            "编码异常没有包装为 SaveException");
        AssertEqual(SaveOperation.Save, encodeFailure.Operation, "编码异常操作类型错误");

        SaveSlot decodeSlot = CreateSlot();
        _saves.Save(decodeSlot, new TestData(2, "decode"), 1, TestCodec.Instance);
        SaveException decodeFailure = AssertThrows<SaveException>(
            () => _saves.Load(decodeSlot, ThrowingCodec.DecodeFailure),
            "解码异常没有包装为 SaveException");
        AssertEqual(SaveOperation.Load, decodeFailure.Operation, "解码异常操作类型错误");
    }

    private void VerifyPrimaryCorruptionWithoutBackup()
    {
        SaveSlot slot = CreateSlot();
        _saves.Save(slot, new TestData(1, "primary-only"), 1, TestCodec.Instance);
        CorruptFile(slot, string.Empty);

        SaveException failure = AssertThrows<SaveException>(
            () => _saves.Load(slot, TestCodec.Instance),
            "损坏正式档且无备份时没有抛出 SaveException");
        AssertEqual(SaveOperation.Load, failure.Operation, "正式档损坏的操作类型错误");
    }

    private void VerifyPrimaryAndBackupCorruption()
    {
        SaveSlot slot = CreateSlot();
        _saves.Save(slot, new TestData(1, "backup"), 1, TestCodec.Instance);
        _saves.Save(slot, new TestData(2, "primary"), 2, TestCodec.Instance);
        CorruptFile(slot, string.Empty);
        CorruptFile(slot, ".bak");

        SaveException failure = AssertThrows<SaveException>(
            () => _saves.Load(slot, TestCodec.Instance),
            "正式档与备份双重损坏时没有抛出 SaveException");
        AssertEqual(SaveOperation.Load, failure.Operation, "双重损坏的操作类型错误");
        Assert(
            failure.InnerException is AggregateException aggregate &&
            aggregate.InnerExceptions.Count == 2,
            "双重损坏没有保留两条底层失败原因");
    }

    private void VerifyHealthyBackupProtection()
    {
        SaveSlot slot = CreateSlot();
        var protectedBackup = new TestData(1, "protected-backup");
        _saves.Save(slot, protectedBackup, 1, TestCodec.Instance);
        _saves.Save(slot, new TestData(2, "corrupt-primary"), 2, TestCodec.Instance);
        CorruptFile(slot, string.Empty);

        _saves.Save(slot, new TestData(3, "new-primary"), 3, TestCodec.Instance);
        CorruptFile(slot, string.Empty);

        SaveLoadResult<TestData> result = _saves.Load(slot, TestCodec.Instance);
        AssertEqual(SaveLoadStatus.RecoveredFromBackup, result.Status, "未从受保护备份恢复");
        AssertEqual(protectedBackup, result.Value, "健康备份被损坏正式档覆盖");
    }

    private void VerifyDelete()
    {
        SaveSlot slot = CreateSlot();
        _saves.Save(slot, new TestData(1, "first"), 1, TestCodec.Instance);
        _saves.Save(slot, new TestData(2, "second"), 2, TestCodec.Instance);

        Assert(_saves.Delete(slot), "存在正式档和备份时 Delete 返回 false");
        Assert(!_saves.Exists(slot), "Delete 后槽位仍存在");
        Assert(!_saves.Delete(slot), "重复 Delete 返回 true");
    }

    private SaveSlot CreateSlot()
    {
        SaveSlot slot = SaveSlot.Create($"godo-regression-{Guid.NewGuid():N}");
        _createdSlots.Add(slot);
        return slot;
    }

    private static void CorruptPrimary(SaveSlot slot)
        => CorruptFile(slot, string.Empty);

    private static void CorruptFile(SaveSlot slot, string suffix)
    {
        string path = $"user://saves/{slot.Value}.gdsave{suffix}";
        using GodotFileAccess? file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Write);
        if (file is null)
            throw new InvalidOperationException($"无法打开测试正式档进行损坏验证：{path}");

        file.StoreBuffer(new byte[] { 1, 2, 3, 4 });
        file.Flush();
    }

    private void CleanupSlots()
    {
        for (int i = 0; i < _createdSlots.Count; i++)
        {
            try
            {
                _saves?.Delete(_createdSlots[i]);
            }
            catch (Exception exception)
            {
                GD.PushWarning(
                    $"[SaveServiceRegression] 清理测试槽位失败：{_createdSlots[i]}；{exception.Message}");
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}；期望 {expected}，实际 {actual}");
        }
    }

    private static TException AssertThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private sealed record TestData(int Score, string Label);

    private sealed class TestCodec : ISaveCodec<TestData>
    {
        public static TestCodec Instance { get; } = new();

        public byte[] Encode(TestData value) =>
            Encoding.UTF8.GetBytes($"{value.Score}|{value.Label}");

        public TestData Decode(ReadOnlySpan<byte> payload, int dataVersion)
        {
            string text = Encoding.UTF8.GetString(payload);
            string[] parts = text.Split('|', count: 2);
            return new TestData(int.Parse(parts[0]), parts[1]);
        }
    }

    private sealed class ThrowingCodec : ISaveCodec<TestData>
    {
        public static ThrowingCodec EncodeFailure { get; } = new(throwOnEncode: true);
        public static ThrowingCodec DecodeFailure { get; } = new(throwOnEncode: false);

        private readonly bool _throwOnEncode;

        private ThrowingCodec(bool throwOnEncode)
        {
            _throwOnEncode = throwOnEncode;
        }

        public byte[] Encode(TestData value)
        {
            if (_throwOnEncode)
                throw new InvalidOperationException("expected encode failure");
            return TestCodec.Instance.Encode(value);
        }

        public TestData Decode(ReadOnlySpan<byte> payload, int dataVersion)
        {
            if (!_throwOnEncode)
                throw new InvalidOperationException("expected decode failure");
            return TestCodec.Instance.Decode(payload, dataVersion);
        }
    }
}
