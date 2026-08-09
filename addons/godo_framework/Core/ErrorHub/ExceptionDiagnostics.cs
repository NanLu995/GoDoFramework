using System;
using System.Text;

#nullable enable

namespace GoDo;

/// <summary>为各错误输出端生成有界且不会反向抛出的异常诊断文本。</summary>
internal static class ExceptionDiagnostics
{
    internal const int MaxSummaryLength = 4096;
    internal const int MaxDetailsLength = 32768;

    private const int MaxChainDepth = 16;
    private const int MaxAggregateCauses = 16;
    private const string TruncatedSuffix = " ... [truncated]";

    internal static string FormatCauseSummary(Exception exception)
    {
        try
        {
            Exception cause = exception is AggregateException
                ? exception
                : exception.InnerException ?? exception;
            var builder = new StringBuilder(256);
            AppendSummary(builder, cause, 0);
            return Limit(builder.ToString(), MaxSummaryLength);
        }
        catch
        {
            return SafeTypeName(exception);
        }
    }

    internal static string FormatDetails(Exception exception)
    {
        try
        {
            return Limit(exception.ToString(), MaxDetailsLength);
        }
        catch
        {
            return FormatCauseSummary(exception);
        }
    }

    private static void AppendSummary(StringBuilder builder, Exception exception, int depth)
    {
        if (depth >= MaxChainDepth)
        {
            builder.Append("... [exception chain depth limit]");
            return;
        }

        if (exception is AggregateException aggregate)
        {
            AggregateException flattened = aggregate.Flatten();
            int count = Math.Min(flattened.InnerExceptions.Count, MaxAggregateCauses);
            builder.Append("AggregateException (")
                .Append(flattened.InnerExceptions.Count)
                .Append(" causes)");

            for (int index = 0; index < count; index++)
            {
                builder.Append(" [").Append(index).Append("] ");
                AppendSummary(builder, flattened.InnerExceptions[index], depth + 1);
            }

            if (flattened.InnerExceptions.Count > count)
                builder.Append(" ... [additional causes omitted]");
            return;
        }

        builder.Append(SafeTypeName(exception));
        string message = SafeMessage(exception);
        if (!string.IsNullOrWhiteSpace(message))
            builder.Append(": ").Append(SingleLine(message));

        if (exception.InnerException != null)
        {
            builder.Append(" -> ");
            AppendSummary(builder, exception.InnerException, depth + 1);
        }
    }

    private static string SafeTypeName(Exception exception)
    {
        try
        {
            return exception.GetType().Name;
        }
        catch
        {
            return "Exception";
        }
    }

    private static string SafeMessage(Exception exception)
    {
        try
        {
            return exception.Message ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SingleLine(string value) =>
        value.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

    private static string Limit(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        int contentLength = Math.Max(0, maxLength - TruncatedSuffix.Length);
        return value[..contentLength] + TruncatedSuffix;
    }
}
