using System;

namespace NovaTechPostProcessor.Infrastructure.Logging
{
    /// <summary>
    /// Logger abstraction following Dependency Inversion Principle.
    /// Enables testability and swappable logging implementations.
    /// </summary>
    public interface ILogger
    {
        void LogInformation(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, Exception? exception = null, params object[] args);
        void LogDebug(string message, params object[] args);
    }
}