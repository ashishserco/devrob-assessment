using NovaTechPostProcessor.Application.Services;
using NovaTechPostProcessor.Domain.Common;
using NovaTechPostProcessor.Infrastructure.Logging;
using System.Diagnostics;

namespace NovaTechPostProcessor.Infrastructure.Resilience
{
    /// <summary>
    /// Circuit Breaker implementation for fault tolerance.
    /// Implements the Circuit Breaker pattern with configurable thresholds,
    /// automatic state transitions, and comprehensive monitoring.
    /// Follows the Fail-Fast principle for system resilience.
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker
    {
        private readonly CircuitBreakerOptions _options;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private int _failureCount;
        private int _successCount;
        private DateTime _lastFailureTime;
        private DateTime _stateChangedTime;

        public CircuitBreakerState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        public int FailureCount
        {
            get
            {
                lock (_lock)
                {
                    return _failureCount;
                }
            }
        }

        public int SuccessCount
        {
            get
            {
                lock (_lock)
                {
                    return _successCount;
                }
            }
        }

        public CircuitBreaker(CircuitBreakerOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateChangedTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Executes operation with circuit breaker protection.
        /// Implements state machine logic with automatic transitions.
        /// </summary>
        public async Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> operation)
        {
            var currentState = GetCurrentState();

            if (currentState == CircuitBreakerState.Open)
            {
                _logger.LogWarning("Circuit breaker is OPEN. Rejecting call. " +
                    "Failures: {FailureCount}, Last failure: {LastFailure}", 
                    _failureCount, _lastFailureTime);
                
                return Result.Failure<T>("Service temporarily unavailable due to circuit breaker");
            }

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var result = await operation();
                stopwatch.Stop();

                if (result.IsSuccess)
                {
                    OnSuccess(stopwatch.ElapsedMilliseconds);
                    return result;
                }
                else
                {
                    OnFailure(result.Error, stopwatch.ElapsedMilliseconds);
                    return result;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                OnFailure(ex.Message, stopwatch.ElapsedMilliseconds);
                
                _logger.LogError("Operation failed with exception in circuit breaker", ex);
                return Result.Failure<T>($"Operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets current state with automatic state transitions.
        /// Implements the circuit breaker state machine logic.
        /// </summary>
        private CircuitBreakerState GetCurrentState()
        {
            lock (_lock)
            {
                switch (_state)
                {
                    case CircuitBreakerState.Closed:
                        // Check if we should trip to Open
                        if (_failureCount >= _options.FailureThreshold)
                        {
                            TransitionTo(CircuitBreakerState.Open);
                        }
                        break;

                    case CircuitBreakerState.Open:
                        // Check if we should try Half-Open
                        if (DateTime.UtcNow - _stateChangedTime >= _options.OpenTimeout)
                        {
                            TransitionTo(CircuitBreakerState.HalfOpen);
                        }
                        break;

                    case CircuitBreakerState.HalfOpen:
                        // Half-Open state transitions are handled in OnSuccess/OnFailure
                        break;
                }

                return _state;
            }
        }

        /// <summary>
        /// Handles successful operation execution.
        /// Updates metrics and manages state transitions.
        /// </summary>
        private void OnSuccess(long elapsedMs)
        {
            lock (_lock)
            {
                _successCount++;
                
                _logger.LogDebug("Circuit breaker: Operation succeeded. " +
                    "Duration: {Duration}ms, State: {State}, Successes: {Successes}", 
                    elapsedMs, _state, _successCount);

                switch (_state)
                {
                    case CircuitBreakerState.HalfOpen:
                        // Success in Half-Open means service has recovered
                        if (_successCount >= _options.SuccessThreshold)
                        {
                            TransitionTo(CircuitBreakerState.Closed);
                            ResetCounters();
                        }
                        break;

                    case CircuitBreakerState.Closed:
                        // Reset failure count on success in normal operation
                        if (_failureCount > 0)
                        {
                            _logger.LogInformation("Circuit breaker: Resetting failure count after success");
                            _failureCount = 0;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Handles failed operation execution.
        /// Updates metrics and manages state transitions.
        /// </summary>
        private void OnFailure(string error, long elapsedMs)
        {
            lock (_lock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                _logger.LogWarning("Circuit breaker: Operation failed. " +
                    "Duration: {Duration}ms, State: {State}, Failures: {Failures}, Error: {Error}", 
                    elapsedMs, _state, _failureCount, error);

                switch (_state)
                {
                    case CircuitBreakerState.HalfOpen:
                        // Failure in Half-Open means service is still unhealthy
                        TransitionTo(CircuitBreakerState.Open);
                        break;

                    case CircuitBreakerState.Closed:
                        // Will be checked in GetCurrentState for threshold
                        break;
                }
            }
        }

        /// <summary>
        /// Transitions circuit breaker to new state with logging.
        /// </summary>
        private void TransitionTo(CircuitBreakerState newState)
        {
            var oldState = _state;
            _state = newState;
            _stateChangedTime = DateTime.UtcNow;

            _logger.LogInformation("Circuit breaker state transition: {OldState} -> {NewState}. " +
                "Failures: {Failures}, Successes: {Successes}", 
                oldState, newState, _failureCount, _successCount);
        }

        /// <summary>
        /// Resets internal counters when circuit closes.
        /// </summary>
        private void ResetCounters()
        {
            _failureCount = 0;
            _successCount = 0;
            _logger.LogInformation("Circuit breaker: Counters reset after successful recovery");
        }
    }

    /// <summary>
    /// Circuit breaker configuration options.
    /// Enables customization of failure thresholds and timeouts.
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// Number of failures required to trip the circuit breaker.
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Number of successes required to close the circuit breaker from half-open state.
        /// </summary>
        public int SuccessThreshold { get; set; } = 2;

        /// <summary>
        /// Time to wait before transitioning from Open to Half-Open.
        /// </summary>
        public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Validates configuration options.
        /// </summary>
        public Result Validate()
        {
            var errors = new List<string>();

            if (FailureThreshold <= 0)
                errors.Add("FailureThreshold must be greater than 0");

            if (SuccessThreshold <= 0)
                errors.Add("SuccessThreshold must be greater than 0");

            if (OpenTimeout <= TimeSpan.Zero)
                errors.Add("OpenTimeout must be greater than zero");

            return errors.Any() 
                ? Result.Failure(string.Join(Environment.NewLine, errors))
                : Result.Success();
        }
    }
}