using System.IO;
using System.Text;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Categories for classifying logged actions
/// </summary>
public enum LogCategory
{
    Application,
    Input,
    Selection,
    Component,
    Transform,
    FileOperation,
    Edit,
    View,
    Layer,
    Property,
    Error
}

/// <summary>
/// Provides comprehensive session-based action logging for troubleshooting.
/// Writes timestamped entries to a log file under %LOCALAPPDATA%\ElectricalComponentSandbox\Logs\.
/// </summary>
public sealed class ActionLogService : IDisposable
{
    private static readonly Lazy<ActionLogService> _instance = new(() => new ActionLogService());
    public static ActionLogService Instance => _instance.Value;

    private readonly string _logFilePath;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Full path to the current session log file
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// Directory containing all session log files
    /// </summary>
    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ElectricalComponentSandbox", "Logs");

    private ActionLogService()
    {
        Directory.CreateDirectory(LogDirectory);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logFilePath = Path.Combine(LogDirectory, $"session-{timestamp}.log");

        _writer = new StreamWriter(_logFilePath, append: false, encoding: Encoding.UTF8)
        {
            AutoFlush = false
        };

        WriteHeader();
    }

    private void WriteHeader()
    {
        _writer.WriteLine("================================================================");
        _writer.WriteLine("       Electrical Component Sandbox - Session Action Log         ");
        _writer.WriteLine("================================================================");
        _writer.WriteLine($"  Session Started : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        _writer.WriteLine($"  Machine         : {Environment.MachineName}");
        _writer.WriteLine($"  User            : {Environment.UserName}");
        _writer.WriteLine($"  OS              : {Environment.OSVersion}");
        _writer.WriteLine($"  .NET Runtime    : {Environment.Version}");
        _writer.WriteLine($"  Log File        : {_logFilePath}");
        _writer.WriteLine(new string('-', 80));
        _writer.WriteLine();
    }

    /// <summary>
    /// Logs an action with category, message, and optional details
    /// </summary>
    public void Log(LogCategory category, string message, string? details = null)
    {
        if (_disposed) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var cat = category.ToString().ToUpperInvariant().PadRight(14);
        var entry = details != null
            ? $"[{timestamp}] [{cat}] {message} | {details}"
            : $"[{timestamp}] [{cat}] {message}";

        lock (_lock)
        {
            _writer.WriteLine(entry);
        }
    }

    /// <summary>
    /// Logs an error with full exception details
    /// </summary>
    public void LogError(LogCategory category, string message, Exception ex)
    {
        if (_disposed) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var cat = "ERROR".PadRight(14);

        lock (_lock)
        {
            _writer.WriteLine($"[{timestamp}] [{cat}] {message}");
            _writer.WriteLine($"                Category  : {category}");
            _writer.WriteLine($"                Exception : {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                foreach (var line in ex.StackTrace.Split('\n').Take(5))
                    _writer.WriteLine($"                  {line.Trim()}");
            }
            if (ex.InnerException != null)
            {
                _writer.WriteLine($"                Inner     : {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            _writer.WriteLine();
        }
    }

    /// <summary>
    /// Writes a visual separator with an optional label for grouping
    /// </summary>
    public void LogSeparator(string? label = null)
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (label != null)
                _writer.WriteLine($"---- {label} {new string('-', Math.Max(0, 74 - label.Length))}");
            else
                _writer.WriteLine(new string('-', 80));
        }
    }

    /// <summary>
    /// Ends the session and writes final summary
    /// </summary>
    public void EndSession()
    {
        if (_disposed) return;

        Log(LogCategory.Application, "Session ending");

        lock (_lock)
        {
            _writer.WriteLine();
            _writer.WriteLine(new string('-', 80));
            _writer.WriteLine($"  Session Ended : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _writer.WriteLine(new string('=', 80));
            _writer.Flush();
        }
    }

    /// <summary>
    /// Flushes any buffered log entries to disk
    /// </summary>
    public void Flush()
    {
        if (_disposed) return;
        lock (_lock)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
