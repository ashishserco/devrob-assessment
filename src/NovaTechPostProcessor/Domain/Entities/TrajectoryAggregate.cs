using NovaTechPostProcessor.Domain.Common;
using NovaTechPostProcessor.Domain.ValueObjects;
using System.Collections.Generic;
using System.Linq;

namespace NovaTechPostProcessor.Domain.Entities
{
    /// <summary>
    /// Trajectory Aggregate Root following DDD principles.
    /// Encapsulates business rules and maintains invariants for robot trajectories.
    /// </summary>
    public class TrajectoryAggregate
    {
        private readonly List<TrajectoryPoint> _points = new();
        
        public RobotId RobotId { get; private set; }
        public FirmwareVersion FirmwareVersion { get; private set; }
        public CoordinateFrame BaseFrame { get; private set; }
        public CoordinateFrame ToolFrame { get; private set; }
        public IReadOnlyList<TrajectoryPoint> Points => _points.AsReadOnly();

        private TrajectoryAggregate() 
        { 
            // Required for EF Core if using - initialize to avoid nullable warnings
            RobotId = null!;
            FirmwareVersion = null!;
            BaseFrame = null!;
            ToolFrame = null!;
        }

        private TrajectoryAggregate(
            RobotId robotId,
            FirmwareVersion firmwareVersion,
            CoordinateFrame baseFrame,
            CoordinateFrame toolFrame)
        {
            RobotId = robotId;
            FirmwareVersion = firmwareVersion;
            BaseFrame = baseFrame;
            ToolFrame = toolFrame;
        }

        /// <summary>
        /// Factory method following DDD patterns.
        /// Ensures aggregate is created in valid state.
        /// </summary>
        public static Result<TrajectoryAggregate> Create(
            string robotModel,
            string firmwareVersion,
            double[] baseFrame,
            double[] toolFrame)
        {
            var robotIdResult = RobotId.Create(robotModel);
            if (robotIdResult.IsFailure)
                return Result.Failure<TrajectoryAggregate>(robotIdResult.Error);

            var firmwareResult = FirmwareVersion.Create(firmwareVersion);
            if (firmwareResult.IsFailure)
                return Result.Failure<TrajectoryAggregate>(firmwareResult.Error);

            var baseFrameResult = CoordinateFrame.Create(baseFrame);
            if (baseFrameResult.IsFailure)
                return Result.Failure<TrajectoryAggregate>($"Invalid base frame: {baseFrameResult.Error}");

            var toolFrameResult = CoordinateFrame.Create(toolFrame);
            if (toolFrameResult.IsFailure)
                return Result.Failure<TrajectoryAggregate>($"Invalid tool frame: {toolFrameResult.Error}");

            return Result.Success(new TrajectoryAggregate(
                robotIdResult.Value,
                firmwareResult.Value,
                baseFrameResult.Value,
                toolFrameResult.Value));
        }

        /// <summary>
        /// Adds a trajectory point with business rule validation.
        /// Maintains aggregate invariants.
        /// </summary>
        public Result AddTrajectoryPoint(
            string movementType,
            double[]? position,
            double[]? joints,
            int speed,
            int? acceleration = null)
        {
            var pointResult = TrajectoryPoint.Create(movementType, position, joints, speed, acceleration);
            if (pointResult.IsFailure)
                return Result.Failure(pointResult.Error);

            // Business rule: Validate point compatibility with robot
            var validationResult = ValidatePointForRobot(pointResult.Value);
            if (validationResult.IsFailure)
                return validationResult;

            _points.Add(pointResult.Value);
            return Result.Success();
        }

        /// <summary>
        /// Business rule: Validates trajectory point against robot capabilities.
        /// Encapsulates domain knowledge about robot constraints.
        /// </summary>
        private Result ValidatePointForRobot(TrajectoryPoint point)
        {
            // Joint 6 extended range validation for NovaTech RT-500
            if (RobotId.Model == "NovaTech RT-500" && point.IsJointMovement)
            {
                var joint6Angle = point.Joints![5]; // Joint 6 is at index 5
                if (joint6Angle < -720.0 || joint6Angle > 720.0)
                {
                    return Result.Failure($"Joint 6 angle {joint6Angle}° exceeds NovaTech RT-500 range (±720°)");
                }
            }

            return Result.Success();
        }

        /// <summary>
        /// Validates the entire trajectory for consistency and safety.
        /// Implements cross-cutting business rules.
        /// </summary>
        public Result ValidateTrajectory()
        {
            if (!_points.Any())
                return Result.Failure("Trajectory must contain at least one point");

            var validationResults = _points.Select((point, index) => 
                ValidateTrajectoryPoint(point, index)).ToArray();

            return Result.Combine(validationResults);
        }

        private Result ValidateTrajectoryPoint(TrajectoryPoint point, int index)
        {
            // Speed validation
            if (point.Speed <= 0)
                return Result.Failure($"Point {index}: Speed must be positive, got {point.Speed}");

            // Movement type specific validation
            if (point.IsLinearMovement && point.Position == null)
                return Result.Failure($"Point {index}: Linear movement requires position data");

            if (point.IsJointMovement && (point.Joints == null || point.Joints.Length != 6))
                return Result.Failure($"Point {index}: Joint movement requires 6-element joints array");

            return Result.Success();
        }
    }
}