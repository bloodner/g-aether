using System.Collections.Concurrent;
using System.Diagnostics;

public static class Logger
{
    public static string appPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\GHelper";
    public static string logFile = appPath + "\\log.txt";

    private static readonly ConcurrentQueue<string> _queue = new();
    private static readonly System.Threading.Timer _flushTimer;
    private static readonly object _flushLock = new();
    private static readonly Random _random = new();

    static Logger()
    {
        _flushTimer = new System.Threading.Timer(_ => Flush(), null,
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    public static void WriteLine(string logMessage)
    {
        var line = $"{DateTime.Now}: {logMessage}";
        Debug.WriteLine(line);
        _queue.Enqueue(line);

        // Force a flush if the queue grows too large between ticks
        if (_queue.Count >= 200) Flush();
    }

    private static void Flush()
    {
        if (_queue.IsEmpty) return;
        if (!Monitor.TryEnter(_flushLock)) return;

        try
        {
            if (!Directory.Exists(appPath)) Directory.CreateDirectory(appPath);

            var lines = new List<string>(_queue.Count);
            while (_queue.TryDequeue(out var line))
                lines.Add(line);

            if (lines.Count == 0) return;

            File.AppendAllLines(logFile, lines);

            if (_random.Next(200) == 1) Cleanup();
        }
        catch { }
        finally
        {
            Monitor.Exit(_flushLock);
        }
    }

    public static void Cleanup()
    {
        try
        {
            var file = File.ReadAllLines(logFile);
            int skip = Math.Max(0, file.Length - 2000);
            File.WriteAllLines(logFile, file.Skip(skip).ToArray());
        }
        catch { }
    }

    /// <summary>
    /// Flush any queued writes. Call on shutdown so the tail of the log isn't lost.
    /// </summary>
    public static void Shutdown()
    {
        try { _flushTimer.Dispose(); } catch { }
        Flush();
    }
}
