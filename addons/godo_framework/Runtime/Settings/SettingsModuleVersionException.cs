using System;

namespace GoDo;

/// <summary>模块无法读取某个持久化数据版本时抛出的异常。</summary>
public sealed class SettingsModuleVersionException : Exception
{
    /// <summary>创建版本不兼容异常。</summary>
    /// <param name="dataVersion">存档记录的数据版本。</param>
    /// <param name="currentVersion">当前模块写入的数据版本。</param>
    public SettingsModuleVersionException(int dataVersion, int currentVersion)
        : base($"设置模块不支持数据版本 {dataVersion}；当前版本为 {currentVersion}。")
    {
        DataVersion = dataVersion;
        CurrentVersion = currentVersion;
    }

    /// <summary>无法读取的数据版本。</summary>
    public int DataVersion { get; }

    /// <summary>当前模块写入的数据版本。</summary>
    public int CurrentVersion { get; }
}
