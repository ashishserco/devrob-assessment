using NovaTechPostProcessor.Domain.Common;
using NovaTechPostProcessor.Domain.Entities;

namespace NovaTechPostProcessor.Application.Services
{
    /// <summary>
    /// Domain service interface for robot code generation.
    /// Follows DDD patterns and Dependency Inversion Principle.
    /// </summary>
    public interface IRobotCodeGenerator
    {
        /// <summary>
        /// Generates robot programming language code from trajectory aggregate.
        /// Returns Result pattern for functional error handling.
        /// </summary>
        Task<Result<string>> GenerateCodeAsync(
            TrajectoryAggregate trajectory, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if the generator supports the specified robot model.
        /// Enables multi-vendor support with proper error handling.
        /// </summary>
        bool SupportsRobot(string robotModel);

        /// <summary>
        /// Gets supported firmware versions for a robot model.
        /// Enables firmware compatibility checking.
        /// </summary>
        IEnumerable<string> GetSupportedFirmwareVersions(string robotModel);
    }
}