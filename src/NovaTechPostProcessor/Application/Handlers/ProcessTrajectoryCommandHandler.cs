using NovaTechPostProcessor.Application.Commands;
using NovaTechPostProcessor.Application.Common;
using NovaTechPostProcessor.Application.Services;
using NovaTechPostProcessor.Domain.Common;
using NovaTechPostProcessor.Domain.Entities;
using NovaTechPostProcessor.Infrastructure.Logging;
using System.Diagnostics;

namespace NovaTechPostProcessor.Application.Handlers
{
    /// <summary>
    /// CQRS Command Handler for trajectory processing.
    /// Implements the Application Service pattern with comprehensive error handling,
    /// logging, and fault tolerance using the Circuit Breaker pattern.
    /// </summary>
    public class ProcessTrajectoryCommandHandler : ICommandHandler<ProcessTrajectoryCommand, ProcessTrajectoryResult>
    {
        private readonly IRobotCodeGenerator _codeGenerator;
        private readonly ILogger _logger;
        private readonly ICircuitBreaker _circuitBreaker;

        public ProcessTrajectoryCommandHandler(
            IRobotCodeGenerator codeGenerator,
            ILogger logger,
            ICircuitBreaker circuitBreaker)
        {
            _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        }

        /// <summary>
        /// Handles trajectory processing command with comprehensive error handling.
        /// Implements fault tolerance patterns and detailed logging.
        /// </summary>
        public async Task<Result<ProcessTrajectoryResult>> HandleAsync(
            ProcessTrajectoryCommand command,
            CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("Starting trajectory processing. CorrelationId: {CorrelationId}, Robot: {Robot}", 
                correlationId, command.RobotModel);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Application-level validation (CQRS pattern)
                var validationResult = command.Validate();
                if (validationResult.IsFailure)
                {
                    _logger.LogWarning("Command validation failed. CorrelationId: {CorrelationId}, Errors: {Errors}", 
                        correlationId, validationResult.Error);
                    return Result.Failure<ProcessTrajectoryResult>(validationResult.Error);
                }

                // Circuit breaker pattern for fault tolerance
                return await _circuitBreaker.ExecuteAsync(async () =>
                {
                    // Domain aggregate creation with validation
                    var aggregateResult = await CreateTrajectoryAggregateAsync(command, correlationId, cancellationToken);
                    if (aggregateResult.IsFailure)
                        return Result.Failure<ProcessTrajectoryResult>(aggregateResult.Error);

                    // Robot code generation through domain service
                    var codeResult = await _codeGenerator.GenerateCodeAsync(aggregateResult.Value, cancellationToken);
                    if (codeResult.IsFailure)
                        return Result.Failure<ProcessTrajectoryResult>(codeResult.Error);

                    // Success result with metadata
                    var result = new ProcessTrajectoryResult
                    {
                        GeneratedCode = codeResult.Value,
                        ProcessedPoints = command.TrajectoryPoints.Count,
                        RobotModel = command.RobotModel,
                        FirmwareVersion = command.FirmwareVersion,
                        ProcessedAt = DateTime.UtcNow
                    };

                    _logger.LogInformation("Trajectory processing completed successfully. " +
                        "CorrelationId: {CorrelationId}, Points: {Points}, Duration: {Duration}ms",
                        correlationId, result.ProcessedPoints, stopwatch.ElapsedMilliseconds);

                    return Result.Success(result);
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Trajectory processing cancelled. CorrelationId: {CorrelationId}", correlationId);
                return Result.Failure<ProcessTrajectoryResult>("Processing was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error during trajectory processing. CorrelationId: {CorrelationId}", 
                    ex, correlationId);
                
                // Fail-safe: Return structured error instead of throwing
                return Result.Failure<ProcessTrajectoryResult>(
                    $"Processing failed due to unexpected error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug("Trajectory processing completed. CorrelationId: {CorrelationId}, " +
                    "Total Duration: {Duration}ms", correlationId, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Creates domain aggregate from command data with validation.
        /// Follows DDD patterns for domain model construction.
        /// </summary>
        private Task<Result<TrajectoryAggregate>> CreateTrajectoryAggregateAsync(
            ProcessTrajectoryCommand command,
            string correlationId,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating trajectory aggregate. CorrelationId: {CorrelationId}", correlationId);

            // Create aggregate root
            var aggregateResult = TrajectoryAggregate.Create(
                command.RobotModel,
                command.FirmwareVersion,
                command.BaseFrame,
                command.ToolFrame);

            if (aggregateResult.IsFailure)
                return Task.FromResult(aggregateResult);

            var aggregate = aggregateResult.Value;

            // Add trajectory points with domain validation
            foreach (var (point, index) in command.TrajectoryPoints.Select((p, i) => (p, i)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var addPointResult = aggregate.AddTrajectoryPoint(
                    point.Type,
                    point.Position,
                    point.Joints,
                    point.Speed,
                    point.Acceleration);

                if (addPointResult.IsFailure)
                {
                    _logger.LogWarning("Failed to add trajectory point {Index}. CorrelationId: {CorrelationId}, " +
                        "Error: {Error}", index, correlationId, addPointResult.Error);
                    return Task.FromResult(Result.Failure<TrajectoryAggregate>(
                        $"Point {index}: {addPointResult.Error}"));
                }
            }

            // Final aggregate validation
            var validationResult = aggregate.ValidateTrajectory();
            if (validationResult.IsFailure)
            {
                _logger.LogWarning("Trajectory validation failed. CorrelationId: {CorrelationId}, " +
                    "Error: {Error}", correlationId, validationResult.Error);
                return Task.FromResult(Result.Failure<TrajectoryAggregate>(validationResult.Error));
            }

            return Task.FromResult(Result.Success(aggregate));
        }
    }
}