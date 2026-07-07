using System.Collections.Generic;
using System.Text;

#nullable enable

namespace GoDo.Editor;

internal enum GoDoHealthLevel
{
    Normal,
    Warning,
    Error
}

internal readonly struct GoDoHealthItem
{
    public GoDoHealthLevel Level { get; }
    public string Name { get; }
    public string Message { get; }

    public GoDoHealthItem(GoDoHealthLevel level, string name, string message)
    {
        Level = level;
        Name = name;
        Message = message;
    }
}

internal sealed class GoDoHealthReport
{
    private readonly List<GoDoHealthItem> _items = new();

    public IReadOnlyList<GoDoHealthItem> Items => _items;
    public GoDoHealthLevel Level { get; private set; }
    public bool RuntimeSceneValid { get; set; }
    public bool VersionSupported { get; set; }
    public bool AutoloadMissing { get; set; }
    public bool AutoloadHealthy { get; set; }
    public bool HasNameConflict { get; set; }
    public bool HasDuplicate { get; set; }

    public bool CanInstall =>
        RuntimeSceneValid &&
        VersionSupported &&
        AutoloadMissing &&
        !HasNameConflict &&
        !HasDuplicate;

    public void Add(GoDoHealthLevel level, string name, string message)
    {
        _items.Add(new GoDoHealthItem(level, name, message));
        if (level > Level)
            Level = level;
    }

    public string Format()
    {
        var builder = new StringBuilder(512);
        builder.Append("总体状态：").AppendLine(Level switch
        {
            GoDoHealthLevel.Normal => "正常",
            GoDoHealthLevel.Warning => "警告",
            _ => "错误"
        });
        builder.AppendLine();

        for (int i = 0; i < _items.Count; i++)
        {
            GoDoHealthItem item = _items[i];
            builder.Append('[').Append(item.Level switch
                {
                    GoDoHealthLevel.Normal => "正常",
                    GoDoHealthLevel.Warning => "警告",
                    _ => "错误"
                })
                .Append("] ").Append(item.Name).Append("：")
                .AppendLine(item.Message);
        }

        return builder.ToString();
    }
}
