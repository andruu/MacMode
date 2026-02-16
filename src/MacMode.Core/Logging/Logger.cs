namespace MacMode.Core.Logging;

/// <summary>
/// Simple rolling daily file logger. Writes to %LocalAppData%\MacMode\logs.
/// Thread-safe via lock.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static string? _logDir;
    private static string? _currentDate;
    private static StreamWriter? _writer;
    private static bool _debugEnabled;

    public static void Initialize(bool enableDebug = false)
    {
        _debugEnabled = enableDebug;
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacMode", "logs");

        Directory.CreateDirectory(_logDir);
        EnsureWriter();
        Info("Logger initialized.");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Debug(string message)
    {
        if (_debugEnabled)
            Write("DEBUG", message);
    }

    public static void SetDebugEnabled(bool enabled) => _debugEnabled = enabled;

    private static void Write(string level, string message)
    {
        if (_logDir == null) return;

        lock (_lock)
        {
            try
            {
                EnsureWriter();
                _writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
                _writer?.Flush();
            }
            catch
            {
                // Swallow logging errors to never crash the hook
            }
        }
    }

    private static void EnsureWriter()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_currentDate == today && _writer != null) return;

        _writer?.Dispose();
        _currentDate = today;
        string path = Path.Combine(_logDir!, $"macmode-{today}.log");
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
