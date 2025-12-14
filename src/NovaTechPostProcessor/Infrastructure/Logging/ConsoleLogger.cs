using System;

namespace NovaTechPostProcessor.Infrastructure.Logging
{
    /// <summary>
    /// Console implementation of ILogger.
    /// Demonstrates Adapter pattern for logging infrastructure.
    /// In production, this would be replaced with Serilog, NLog, etc.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void LogInformation(string message, params object[] args)
        {
            WriteLog("INFO", ConsoleColor.White, message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            WriteLog("WARN", ConsoleColor.Yellow, message, args);
        }

        public void LogError(string message, Exception? exception = null, params object[] args)
        {
            WriteLog("ERROR", ConsoleColor.Red, message, args);
            if (exception != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Exception: {exception.Message}");
                Console.WriteLine($"StackTrace: {exception.StackTrace}");
                Console.ResetColor();
            }
        }

        public void LogDebug(string message, params object[] args)
        {
            WriteLog("DEBUG", ConsoleColor.Gray, message, args);
        }

        private void WriteLog(string level, ConsoleColor color, string message, params object[] args)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.ForegroundColor = color;
            Console.WriteLine($"[{timestamp}] [{level}] {string.Format(message, args)}");
            Console.ResetColor();
        }
    }
}