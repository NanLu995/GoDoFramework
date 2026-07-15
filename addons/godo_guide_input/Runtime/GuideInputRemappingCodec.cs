using System;
using System.IO;
using Godot;
using Godot.Collections;
using GuideCs;
using GodotFileAccess = Godot.FileAccess;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>使用 GUIDE 原生 Resource 格式编解码重绑定配置。</summary>
internal sealed class GuideInputRemappingCodec : ISaveCodec<GuideRemappingConfig>
{
    internal const int DataVersion = 1;
    private const string TemporaryDirectory = "user://godo-input-codec";

    /// <inheritdoc />
    public byte[] Encode(GuideRemappingConfig value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Resource resource = value.BaseGuideObject as Resource ??
            throw new InvalidOperationException("GUIDE 重绑定配置不是可保存的 Resource。");
        string path = CreateTemporaryPath();
        try
        {
            EnsureTemporaryDirectory();
            Error error = ResourceSaver.Save(resource, path);
            if (error != Error.Ok)
                throw new IOException($"保存 GUIDE 临时配置失败，Error={error}。");

            byte[] payload = GodotFileAccess.GetFileAsBytes(path);
            if (payload.Length == 0)
                throw new InvalidDataException("GUIDE 临时配置为空。");

            return payload;
        }
        finally
        {
            BestEffortRemove(path);
        }
    }

    /// <inheritdoc />
    public GuideRemappingConfig Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion != DataVersion)
            throw new InvalidDataException($"不支持的 GUIDE 重绑定配置版本: {dataVersion}");
        if (payload.IsEmpty)
            throw new InvalidDataException("GUIDE 重绑定配置 Payload 为空。");

        string path = CreateTemporaryPath();
        try
        {
            EnsureTemporaryDirectory();
            using GodotFileAccess file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Write) ??
                throw new IOException($"无法创建 GUIDE 临时配置: {path}");
            file.StoreBuffer(payload.ToArray());
            Error writeError = file.GetError();
            if (writeError != Error.Ok)
                throw new IOException($"写入 GUIDE 临时配置失败，Error={writeError}。");
            file.Close();

            Resource resource = ResourceLoader.Load<Resource>(path) ??
                throw new InvalidDataException("无法解析 GUIDE 重绑定配置 Resource。");
            if (!resource.HasMethod("_get_bound_input_or_null"))
                throw new InvalidDataException("配置 Resource 不是 GUIDERemappingConfig。");

            var configuration = new GuideRemappingConfig(resource);
            ClearRuntimeInputMetadata(configuration);
            return configuration;
        }
        finally
        {
            BestEffortRemove(path);
        }
    }

    private static string CreateTemporaryPath() =>
        $"{TemporaryDirectory}/{Guid.NewGuid():N}.tres";

    private static void EnsureTemporaryDirectory()
    {
        if (DirAccess.DirExistsAbsolute(TemporaryDirectory))
            return;

        Error error = DirAccess.MakeDirRecursiveAbsolute(TemporaryDirectory);
        if (error != Error.Ok)
            throw new IOException($"创建 GUIDE 配置临时目录失败，Error={error}。");
    }

    private static void BestEffortRemove(string path)
    {
        if (GodotFileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(path);
    }

    private static void ClearRuntimeInputMetadata(GuideRemappingConfig configuration)
    {
        Dictionary contexts = configuration.GetRemappedInputs();
        foreach (Variant contextValue in contexts.Values)
        {
            Dictionary actions = contextValue.AsGodotDictionary();
            foreach (Variant actionValue in actions.Values)
            {
                Dictionary mappings = actionValue.AsGodotDictionary();
                foreach (Variant inputValue in mappings.Values)
                {
                    if (inputValue.VariantType == Variant.Type.Nil)
                        continue;

                    GodotObject? input = inputValue.As<GodotObject>();
                    input?.RemoveMeta("__guide_in_use");
                }
            }
        }
    }
}
