using System;
using System.IO;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Simple file logger to help diagnose data issues.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new();
        private static readonly string _logFile;

        static Logger()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            _logFile = Path.Combine(dir, "app.log");
        }

        public static void Log(string message)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            lock (_lock)
            {
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
        }
    }
}
