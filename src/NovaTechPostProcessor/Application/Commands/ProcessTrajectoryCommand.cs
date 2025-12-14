using NovaTechPostProcessor.Application.Common;
using NovaTechPostProcessor.Domain.Common;

namespace NovaTechPostProcessor.Application.Commands
{
    /// <summary>
    /// CQRS Command for processing robot trajectories.
    /// Encapsulates request data and validation rules at application layer.
    /// </summary>
    public class ProcessTrajectoryCommand : ICommand<ProcessTrajectoryResult>
    {
        public string RobotModel { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public double[] BaseFrame { get; set; } = Array.Empty<double>();
        public double[] ToolFrame { get; set; } = Array.Empty<double>();
        public List<TrajectoryPointDto> TrajectoryPoints { get; set; } = new();

        /// <summary>
        /// Command validation following CQRS patterns.
        /// Application-level validation before domain processing.
        /// </summary>
        public Result Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(RobotModel))
                errors.Add("Robot model is required");

            if (string.IsNullOrWhiteSpace(FirmwareVersion))
                errors.Add("Firmware version is required");

            if (BaseFrame == null || BaseFrame.Length != 6)
                errors.Add("Base frame must contain 6 coordinates");

            if (ToolFrame == null || ToolFrame.Length != 6)
                errors.Add("Tool frame must contain 6 coordinates");

            if (!TrajectoryPoints.Any())
                errors.Add("At least one trajectory point is required");

            return errors.Any() 
                ? Result.Failure(string.Join(Environment.NewLine, errors))
                : Result.Success();
        }
    }

    /// <summary>
    /// Data Transfer Object for trajectory point data.
    /// Keeps application layer decoupled from domain entities.
    /// </summary>
    public class TrajectoryPointDto
    {
        public string Type { get; set; } = string.Empty;
        public double[]? Position { get; set; }
        public double[]? Joints { get; set; }
        public int Speed { get; set; }
        public int? Acceleration { get; set; }
    }

    /// <summary>
    /// Command result with generated robot code and metadata.
    /// </summary>
    public class ProcessTrajectoryResult
    {
        public string GeneratedCode { get; set; } = string.Empty;
        public int ProcessedPoints { get; set; }
        public string RobotModel { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}