#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

#nullable enable

namespace GoDo;

public sealed partial class DebuggerOverlay : CanvasLayer
{

    private void ScrollConsoleToBottom()
    {
        if (!IsInstanceValid(_debuggerLabel))
            return;

        ConsoleScrollToBottomCount++;
        ScheduleConsoleScrollToBottom();
    }

    private void ScheduleConsoleScrollToBottom()
    {
        if (_consoleScrollDeferred)
            return;

        _consoleScrollDeferred = true;
        Callable.From(ApplyDeferredConsoleScrollToBottom).CallDeferred();
    }

    private void ApplyDeferredConsoleScrollToBottom()
    {
        try
        {
            if (_consolePageOffset == 0 && _consoleFollowLatest)
                ApplyConsoleScrollToBottom();
        }
        finally
        {
            _consoleScrollDeferred = false;
        }
    }

    private void ApplyConsoleScrollToBottom()
    {
        if (!IsInstanceValid(_debuggerLabel) || !IsInstanceValid(_consoleScrollBar))
            return;

        _applyingConsoleScroll = true;
        try
        {
            _consoleScrollBar.Value = _consoleScrollBar.MaxValue;
        }
        finally
        {
            _applyingConsoleScroll = false;
        }
        UpdateLatestConsoleButtonState();
    }

    private void OnConsoleScrollValueChanged(double value)
    {
        if (_applyingConsoleScroll || _consolePageOffset != 0)
            return;

        ScheduleConsoleScrollEvaluation();
    }

    private void ScheduleConsoleScrollEvaluation()
    {
        if (_consoleScrollEvaluationDeferred)
            return;

        _consoleScrollEvaluationDeferred = true;
        Callable.From(ApplyDeferredConsoleScrollEvaluation).CallDeferred();
    }

    private void ApplyDeferredConsoleScrollEvaluation()
    {
        _consoleScrollEvaluationDeferred = false;
        if (_selectedPage?.IsConsole != true ||
            _consolePageOffset != 0 ||
            !IsInstanceValid(_consoleScrollBar))
            return;

        ConsoleScrollEvaluationCount++;
        _consoleFollowLatest = IsConsoleAtBottom();
        UpdateLatestConsoleButtonState();
    }

    private bool IsConsoleAtBottom()
    {
        if (!IsInstanceValid(_consoleScrollBar))
            return true;

        double bottom = Math.Max(
            _consoleScrollBar.MinValue,
            _consoleScrollBar.MaxValue - _consoleScrollBar.Page);
        return _consoleScrollBar.Value >= bottom - ConsoleBottomThreshold;
    }


    private void CacheConsoleFilterNodes()
    {
        _allConsoleFilterButton = GetConsoleFilterButton("All");
        _debugConsoleFilterButton = GetConsoleFilterButton("Debug");
        _infoConsoleFilterButton = GetConsoleFilterButton("Info");
        _warningConsoleFilterButton = GetConsoleFilterButton("Warning");
        _errorConsoleFilterButton = GetConsoleFilterButton("Error");
        ApplyConsoleFilterButtonStates();
    }

    private Button GetConsoleFilterButton(string path)
    {
        Button? button = _consoleFilters!.GetNodeOrNull<Button>(path);
        return IsInstanceValid(button)
            ? button
            : throw new InvalidOperationException($"DebuggerConsole 场景缺少节点：{path}");
    }


    private void OnConsoleSearchChanged(string text)
    {
        _consoleSearchQuery = text.Trim();
        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        if (_selectedPage?.IsConsole == true)
            RefreshDebugger(force: true);
    }

    private void OnConsoleSearchSubmitted(string text)
    {
        _consoleSearchQuery = text.Trim();
        if (IsInstanceValid(_consoleSearch))
            _consoleSearch.ReleaseFocus();
    }

    private void OnAllConsoleFilterPressed()
    {
        _consoleLevelFilter = ConsoleLevelFilter.All;
        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        ApplyConsoleFilterButtonStates();
        RefreshDebugger(force: true);
    }

    private void OnDebugConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Debug);

    private void OnInfoConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Info);

    private void OnWarningConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Warning);

    private void OnErrorConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Error);

    private void ToggleConsoleFilter(ConsoleLevelFilter filter)
    {
        if (_consoleLevelFilter == ConsoleLevelFilter.All)
        {
            _consoleLevelFilter = filter;
        }
        else if ((_consoleLevelFilter & filter) != 0)
        {
            ConsoleLevelFilter remaining = _consoleLevelFilter & ~filter;
            _consoleLevelFilter = remaining == ConsoleLevelFilter.None
                ? ConsoleLevelFilter.All
                : remaining;
        }
        else
        {
            _consoleLevelFilter |= filter;
        }

        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        ApplyConsoleFilterButtonStates();
        RefreshDebugger(force: true);
    }

    private void OnOlderConsolePagePressed()
    {
        _consolePageOffset++;
        _consoleFollowLatest = false;
        RefreshDebugger(force: true);
    }

    private void OnNewerConsolePagePressed()
    {
        if (_consolePageOffset == 0)
            return;

        _consolePageOffset--;
        if (_consolePageOffset == 0)
            _consoleFollowLatest = true;
        RefreshDebugger(force: true);
    }

    private void OnLatestConsolePagePressed()
    {
        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        RefreshDebugger(force: true);
    }

    private void ApplyConsoleFilterButtonStates()
    {
        if (!IsInstanceValid(_allConsoleFilterButton) ||
            !IsInstanceValid(_debugConsoleFilterButton) ||
            !IsInstanceValid(_infoConsoleFilterButton) ||
            !IsInstanceValid(_warningConsoleFilterButton) ||
            !IsInstanceValid(_errorConsoleFilterButton))
            return;

        bool showAll = _consoleLevelFilter == ConsoleLevelFilter.All;
        _allConsoleFilterButton.ButtonPressed = showAll;
        _debugConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Debug) != 0;
        _infoConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Info) != 0;
        _warningConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Warning) != 0;
        _errorConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Error) != 0;
    }

    private void OnPauseConsolePressed()
    {
        _consoleRefreshPaused = !_consoleRefreshPaused;
        if (IsInstanceValid(_pauseConsoleButton))
            _pauseConsoleButton.Text = _consoleRefreshPaused ? "继续" : "暂停";
        if (!_consoleRefreshPaused)
            RefreshDebugger(force: true);
    }

    private void OnCopyConsolePressed()
    {
        if (!IsInstanceValid(_debuggerLabel))
            return;

        DisplayServer.ClipboardSet(_debuggerLabel.GetParsedText());
    }

    private void OnConsoleFileLinkPressed()
    {
        if (string.IsNullOrWhiteSpace(_consoleFilePath))
            return;

        Error error = OS.ShellShowInFileManager(_consoleFilePath);
        if (error != Error.Ok)
        {
            ErrorHub.Warn(
                "无法在文件管理器中定位日志文件",
                nameof(DebuggerOverlay),
                $"path={_consoleFilePath}; error={error}");
        }
    }


    private void AppendConsole()
    {
        ConsoleRenderCount++;
        LogEntry[] logs = LogHub.GetDebugSnapshot();
        int errorCount = 0;
        foreach (DebuggerErrorEntry error in _recentWarnings)
            _consoleErrorSnapshot[errorCount++] = error;
        FileLogDebugSnapshot fileLog = LogHub.GetFileLogDebugSnapshot();
        UpdateConsoleFilterLabels(logs);
        AppendConsoleEntries(logs, _consoleErrorSnapshot, errorCount);
        UpdateConsoleFileLogStatus(fileLog);
    }

    private void UpdateConsoleFilterLabels(LogEntry[] logs)
    {
        if (!IsInstanceValid(_allConsoleFilterButton) ||
            !IsInstanceValid(_debugConsoleFilterButton) ||
            !IsInstanceValid(_infoConsoleFilterButton) ||
            !IsInstanceValid(_warningConsoleFilterButton) ||
            !IsInstanceValid(_errorConsoleFilterButton))
            return;

        long debugCount = 0;
        long infoCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (logs[index].Level == LogLevel.Debug)
                debugCount += logs[index].RepeatCount;
            else
                infoCount += logs[index].RepeatCount;
        }

        int warningCount = 0;
        int errorCount = 0;
        foreach (DebuggerErrorEntry entry in _recentWarnings)
        {
            if (entry.Level >= ErrorLevel.Error)
                errorCount++;
            else
                warningCount++;
        }

        _allConsoleFilterButton.Text = $"All ({debugCount + infoCount + warningCount + errorCount})";
        _debugConsoleFilterButton.Text = $"Debug ({debugCount})";
        _infoConsoleFilterButton.Text = $"Info ({infoCount})";
        _warningConsoleFilterButton.Text = $"Warning ({warningCount})";
        _errorConsoleFilterButton.Text = $"Error ({errorCount})";
    }


    private void AppendConsoleEntries(
        LogEntry[] logs,
        DebuggerErrorEntry[] errors,
        int errorCount)
    {
        int matchingCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (MatchesConsoleLevel(logs[index]) &&
                MatchesConsoleSearch(logs[index]))
                matchingCount++;
        }
        for (int index = 0; index < errorCount; index++)
        {
            if (MatchesConsoleLevel(errors[index]) &&
                MatchesConsoleSearch(errors[index]))
                matchingCount++;
        }

        int pageCount = Math.Max(1, (matchingCount + ConsoleLogsPerPage - 1) / ConsoleLogsPerPage);
        _consolePageOffset = Math.Clamp(_consolePageOffset, 0, pageCount - 1);
        int pageEnd = Math.Max(0, matchingCount - _consolePageOffset * ConsoleLogsPerPage);
        int pageStart = Math.Max(0, pageEnd - ConsoleLogsPerPage);
        UpdateConsolePagination(matchingCount, pageStart, pageEnd, pageCount);
        if (matchingCount == 0)
            return;

        int matchingIndex = 0;
        int logIndex = 0;
        int errorIndex = 0;
        while (logIndex < logs.Length || errorIndex < errorCount)
        {
            bool useLog = errorIndex >= errorCount ||
                (logIndex < logs.Length &&
                 logs[logIndex].TimestampUtc <= errors[errorIndex].TimestampUtc);
            if (useLog)
            {
                LogEntry log = logs[logIndex++];
                if (!MatchesConsoleLevel(log) || !MatchesConsoleSearch(log))
                    continue;
                if (matchingIndex >= pageStart && matchingIndex < pageEnd)
                    AppendConsoleLog(log);
                matchingIndex++;
            }
            else
            {
                DebuggerErrorEntry error = errors[errorIndex++];
                if (!MatchesConsoleLevel(error) || !MatchesConsoleSearch(error))
                    continue;
                if (matchingIndex >= pageStart && matchingIndex < pageEnd)
                    AppendConsoleError(error);
                matchingIndex++;
            }

            if (matchingIndex >= pageEnd)
                break;
        }
    }

    private void AppendConsoleLog(LogEntry log)
    {
        DateTime lastTimestamp = log.TimestampUtc.ToLocalTime();
        if (log.RepeatCount > 1)
        {
            _textBuilder.Append(log.FirstTimestampUtc.ToLocalTime().ToString("HH:mm:ss"))
                .Append('–').Append(lastTimestamp.ToString("HH:mm:ss"));
        }
        else
        {
            _textBuilder.Append(lastTimestamp.ToString("HH:mm:ss"));
        }

        _textBuilder.Append(' ').Append('[').Append(log.Level).Append("] ")
            .Append(log.Module).Append(": ");

        if (!string.IsNullOrWhiteSpace(log.Context))
            _textBuilder.Append('(').Append(log.Context).Append(") ");

        _textBuilder.Append(log.Message);
        if (log.RepeatCount > 1)
            _textBuilder.Append(" ×").Append(log.RepeatCount);
        _textBuilder.AppendLine();
    }

    private void AppendConsoleError(DebuggerErrorEntry error)
    {
        _textBuilder.Append(error.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"))
            .Append(' ').Append('[').Append(error.Level).Append("] ")
            .Append(error.Module).Append(": ");

        if (!string.IsNullOrWhiteSpace(error.Context))
            _textBuilder.Append('(').Append(error.Context).Append(") ");

        _textBuilder.Append(error.Message);
        if (!string.IsNullOrWhiteSpace(error.Cause))
            _textBuilder.Append(" | Cause: ").Append(error.Cause);
        _textBuilder.AppendLine();
    }

    private string BuildConsoleMarkup(string text)
    {
        _consoleMarkupBuilder.Clear();
        int lineStart = 0;
        while (lineStart < text.Length)
        {
            int newline = text.IndexOf('\n', lineStart);
            int lineEnd = newline >= 0 ? newline : text.Length;
            ReadOnlySpan<char> line = text.AsSpan(lineStart, lineEnd - lineStart);
            string? color = GetConsoleLineColor(line);
            if (color != null)
                _consoleMarkupBuilder.Append("[color=").Append(color).Append(']');

            AppendEscapedBbcode(_consoleMarkupBuilder, line);
            if (color != null)
                _consoleMarkupBuilder.Append("[/color]");
            if (newline < 0)
                break;

            _consoleMarkupBuilder.Append('\n');
            lineStart = newline + 1;
        }

        return _consoleMarkupBuilder.ToString();
    }

    private static string? GetConsoleLineColor(ReadOnlySpan<char> line)
    {
        if (line.Length <= 9 || line[8] != ' ')
            return null;

        ReadOnlySpan<char> level = line[9..];
        if (level.StartsWith("[Warning]"))
            return ConsoleWarningColor;
        if (level.StartsWith("[Error]") || level.StartsWith("[Fatal]"))
            return ConsoleErrorColor;
        return null;
    }

    private static void AppendEscapedBbcode(
        StringBuilder builder,
        ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '[')
                builder.Append("[lb]");
            else
                builder.Append(text[index]);
        }
    }

    private void UpdateConsolePagination(
        int matchingCount,
        int pageStart,
        int pageEnd,
        int pageCount)
    {
        if (!IsInstanceValid(_olderConsolePageButton) ||
            !IsInstanceValid(_newerConsolePageButton) ||
            !IsInstanceValid(_latestConsolePageButton) ||
            !IsInstanceValid(_consolePageStatus))
            return;

        _olderConsolePageButton.Disabled = pageStart == 0;
        _newerConsolePageButton.Disabled = _consolePageOffset == 0;
        UpdateLatestConsoleButtonState();
        _consolePageStatus.Text = matchingCount == 0
            ? "日志 0"
            : $"日志 {matchingCount} · {pageStart + 1}–{pageEnd} · " +
               $"{pageCount - _consolePageOffset}/{pageCount}";
    }

    private void UpdateConsoleFileLogStatus(FileLogDebugSnapshot snapshot)
    {
        if (!IsInstanceValid(_consoleFileLink))
            return;

        if (!snapshot.IsEnabled)
        {
            _consoleFilePath = string.Empty;
            _consoleFileLink.Text = "文件日志未启用";
            _consoleFileLink.TooltipText = string.Empty;
            _consoleFileLink.Disabled = true;
            return;
        }

        _consoleFilePath = snapshot.Path;
        string status = snapshot.HasFailed
            ? "已停用"
            : snapshot.IsReady ? "正常" : "启动中";
        string fileName = System.IO.Path.GetFileName(snapshot.Path);
        _consoleFileLink.Text = $"{fileName} ({status})";
        _consoleFileLink.Disabled = !snapshot.IsReady;
        _consoleFileLink.TooltipText =
            $"{snapshot.Path}\n" +
            $"状态：{status} · 已刷新：{FormatBytes(snapshot.CurrentFileBytes)} · " +
            $"丢弃：{snapshot.DroppedLineCount.ToString(CultureInfo.InvariantCulture)}" +
            (snapshot.HasFailed
                ? $"\n原因：{snapshot.FailureDetail ?? "未知写入错误"}"
                : "\n点击在文件管理器中定位");
    }

    private void UpdateLatestConsoleButtonState()
    {
        if (IsInstanceValid(_latestConsolePageButton))
            _latestConsolePageButton.Disabled =
                _consolePageOffset == 0 && _consoleFollowLatest;
    }

    private bool MatchesConsoleLevel(LogEntry entry) =>
        _consoleLevelFilter == ConsoleLevelFilter.All ||
        entry.Level switch
        {
            LogLevel.Debug => (_consoleLevelFilter & ConsoleLevelFilter.Debug) != 0,
            LogLevel.Info => (_consoleLevelFilter & ConsoleLevelFilter.Info) != 0,
            _ => false,
        };

    private bool MatchesConsoleLevel(DebuggerErrorEntry entry) =>
        _consoleLevelFilter == ConsoleLevelFilter.All ||
        (entry.Level == ErrorLevel.Warning
            ? (_consoleLevelFilter & ConsoleLevelFilter.Warning) != 0
            : (_consoleLevelFilter & ConsoleLevelFilter.Error) != 0);

    private bool MatchesConsoleSearch(LogEntry entry)
    {
        string query = _consoleSearchQuery;
        return query.Length == 0 ||
            entry.Level.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Module.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Context?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
            entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesConsoleSearch(DebuggerErrorEntry entry)
    {
        string query = _consoleSearchQuery;
        return query.Length == 0 ||
            entry.Level.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Module.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Context?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
            entry.Cause?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
            entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OnErrorReported(ErrorReport report)
    {
        if (report.Level < ErrorLevel.Warning)
            return;

        if (_recentWarnings.Count >= MaxStoredWarnings)
            _recentWarnings.Dequeue();

        _recentWarnings.Enqueue(new DebuggerErrorEntry(
            report.Timestamp,
            report.Level,
            report.Module,
            report.Message,
            report.Context,
            report.Exception == null
                ? null
                : ExceptionDiagnostics.FormatCauseSummary(report.Exception)));
        unchecked
        {
            _consoleErrorVersion++;
        }
    }

}
#endif
