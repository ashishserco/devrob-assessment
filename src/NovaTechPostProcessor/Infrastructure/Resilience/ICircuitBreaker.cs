using NovaTechPostProcessor.Domain.Common;

namespace NovaTechPostProcessor.Application.Services
{
    /// <summary>
    /// Circuit Breaker interface for fault tolerance.
    /// Implements the Circuit Breaker pattern to prevent cascade failures
    /// and provide graceful degradation in distributed systems.
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// Executes operation with circuit breaker protection.
        /// Automatically opens circuit when failure threshold is exceeded,
        /// preventing further calls until recovery period elapses.
        /// </summary>
        Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> operation);

        /// <summary>
        /// Gets current circuit breaker state.
        /// </summary>
        CircuitBreakerState State { get; }

        /// <summary>
        /// Gets failure count in current time window.
        /// </summary>
        int FailureCount { get; }

        /// <summary>
        /// Gets success count in current time window.
        /// </summary>
        int SuccessCount { get; }
    }

    /// <summary>
    /// Circuit breaker state enumeration.
    /// </summary>
    public enum CircuitBreakerState
    {
        Closed,    // Normal operation
        Open,      // Circuit is open, calls are blocked
        HalfOpen   // Testing if service has recovered
    }
}