using System.IO;

namespace UpdateSkriptApp.Services
{
    public class FileLoggerService : ILoggerService
    {
        private readonly string _logFilePath;
        public event Action<string, string>? OnLogLineReceived;

        public FileLoggerService(string? logDirectory = null)
        {
            try
            {
                logDirectory ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);
                _logFilePath = Path.Combine(logDirectory, "UpdateSkript_GUI.log");
            }
            catch
            {
                // Fallback to Public folder if app dir is read-only
                var fallback = @"C:\Users\Public\UpdateSkript\Logs";
                if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                _logFilePath = Path.Combine(fallback, "UpdateSkript_GUI.log");
            }
        }

        public void Log(string message, string color = "White")
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logLine = $"[{timestamp}] {message}";

            // Write to file
            try
            {
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
            }
            catch { /* Ignore logging errors to prevent crash */ }

            // Notify UI
            OnLogLineReceived?.Invoke(logLine, color);
        }

        public void Clear()
        {
            if (File.Exists(_logFilePath)) File.Delete(_logFilePath);
        }
    }
}
