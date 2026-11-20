using System;
using System.IO;
using System.Text;

namespace LiveWallpaper
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _logDirectory;
        private static readonly string _logFilePath;

        static Logger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _logFilePath = Path.Combine(_logDirectory, "app.log");

            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] Cannot create log directory: {ex.Message}");
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            string detail = ex == null ? message : $"{message} | Exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}";
            Write("ERROR", detail);
        }

        private static void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{Environment.CurrentManagedThreadId,2}] [{level}] {message}";

            // Console output
            Console.WriteLine(line);

            // File output
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Avoid crashing on log write failure
                }
            }
        }

        public static string LogFilePath => _logFilePath;
    }
}
