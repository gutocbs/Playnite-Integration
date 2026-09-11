using System.Globalization;
using System.Text;

namespace Launcher.Logging;

public sealed class AppLogger : IAppLogger
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly string _executionId;
    private readonly string _logPath;

    public AppLogger(string executionId, string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        _executionId = executionId;
        _logPath = Path.GetFullPath(logPath);
    }

    public void Write(
        LogLevel level,
        string component,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string? line = null;

        try
        {
            line = FormatLine(level, component, message, data);
            WriteLine(_logPath, line);
        }
        catch
        {
            if (line is not null)
            {
                TryWriteFallback(line);
            }
        }
    }

    private string FormatLine(
        LogLevel level,
        string component,
        string message,
        IReadOnlyDictionary<string, object?>? data)
    {
        var timestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-dd HH:mm:ss.fff zzz",
            CultureInfo.InvariantCulture);

        var line = new StringBuilder()
            .Append('[').Append(timestamp).Append("] ")
            .Append('[').Append(level.ToString().ToUpperInvariant()).Append("] ")
            .Append('[').Append(EscapeLineBreaks(_executionId)).Append("] ")
            .Append('[').Append(EscapeLineBreaks(component)).Append("] ")
            .Append(EscapeLineBreaks(message));

        if (data is { Count: > 0 })
        {
            line.Append(" | ");
            line.AppendJoin("; ", data.Select(pair =>
                $"{pair.Key}={FormatValue(pair.Value)}"));
        }

        return line.ToString();
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        var text = value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        } ?? string.Empty;

        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal);
    }

    private static string EscapeLineBreaks(string value) => value
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void WriteLine(string path, string line)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = Utf8WithoutBom.GetBytes(line + Environment.NewLine);
        using var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 1,
            FileOptions.WriteThrough);

        stream.Write(bytes);
    }

    private void TryWriteFallback(string line)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath) ?? AppContext.BaseDirectory;
            var baseName = Path.GetFileNameWithoutExtension(_logPath);
            var extension = Path.GetExtension(_logPath);
            var fallbackPath = Path.Combine(
                directory,
                $"{baseName}.{Environment.ProcessId}{extension}");

            WriteLine(fallbackPath, line);
        }
        catch
        {
            // Logging is auxiliary infrastructure and must never block launching.
        }
    }
}
