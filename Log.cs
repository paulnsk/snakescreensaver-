using System.IO;

namespace SnakeScreensaver;

internal static class Log
{
    private static readonly string _path = Path.Combine(Path.GetTempPath(), "SnakeScreensaver.log");
    private static readonly object _gate = new();

    public static void Line(string message)
    {
        var line = $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
                   $"[tid={System.Threading.Thread.CurrentThread.ManagedThreadId}] " +
                   $"{message}{System.Environment.NewLine}";
        lock (_gate)
        {
            try { File.AppendAllText(_path, line); }
            catch { /* best-effort logging, never throw */ }
        }
    }

    public static void Exc(string label, System.Exception? ex)
    {
        if (ex == null) { Line($"{label}: <null exception>"); return; }
        Line($"{label}: {ex.GetType().FullName}: {ex.Message}");
        Line($"STACK: {ex.StackTrace ?? "<no stack>"}");
        if (ex is AggregateException aex)
        {
            foreach (var ix in aex.InnerExceptions)
                Line($"  inner: {ix?.GetType().FullName}: {ix?.Message}");
        }
        else if (ex.InnerException != null)
        {
            Exc($"{label}.Inner", ex.InnerException);
        }
    }
}
