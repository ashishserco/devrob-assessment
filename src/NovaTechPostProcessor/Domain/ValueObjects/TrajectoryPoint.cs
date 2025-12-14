using NovaTechPostProcessor.Domain.Common;
using System;

namespace NovaTechPostProcessor.Domain.ValueObjects
{
    /// <summary>
    /// Trajectory point value object representing a single movement command.
    /// Encapsulates movement validation and type safety rules.
    /// </summary>
    public class TrajectoryPoint : IEquatable<TrajectoryPoint>
    {
        public MovementType MovementType { get; private set; }
        public CoordinateFrame? Position { get; private set; }
        public double[]? Joints { get; private set; }
        public Speed Speed { get; private set; }
        public Acceleration Acceleration { get; private set; }

        public bool IsLinearMovement => MovementType == MovementType.Linear;
        public bool IsJointMovement => MovementType == MovementType.Joint;

        private TrajectoryPoint(
            MovementType movementType,
            CoordinateFrame? position,
            double[]? joints,
            Speed speed,
            Acceleration acceleration)
        {
            MovementType = movementType;
            Position = position;
            Joints = joints?.ToArray(); // Defensive copy
            Speed = speed;
            Acceleration = acceleration;
        }

        /// <summary>
        /// Factory method with comprehensive validation.
        /// Ensures trajectory point is created in valid state with proper type safety.
        /// </summary>
        public static Result<TrajectoryPoint> Create(
            string movementType,
            double[]? position,
            double[]? joints,
            int speed,
            int? acceleration = null)
        {
            // Parse movement type
            var movementTypeResult = ParseMovementType(movementType);
            if (movementTypeResult.IsFailure)
                return Result.Failure<TrajectoryPoint>(movementTypeResult.Error);

            // Create speed value object
            var speedResult = Speed.Create(speed);
            if (speedResult.IsFailure)
                return Result.Failure<TrajectoryPoint>(speedResult.Error);

            // Create acceleration value object (with default if not provided)
            var accelerationResult = Acceleration.Create(acceleration ?? 50);
            if (accelerationResult.IsFailure)
                return Result.Failure<TrajectoryPoint>(accelerationResult.Error);

            // Validate movement type specific data
            CoordinateFrame? positionFrame = null;
            double[]? validatedJoints = null;

            if (movementTypeResult.Value == MovementType.Linear)
            {
                if (position == null)
                    return Result.Failure<TrajectoryPoint>("Linear movement requires position data");

                var positionResult = CoordinateFrame.Create(position);
                if (positionResult.IsFailure)
                    return Result.Failure<TrajectoryPoint>($"Invalid position data: {positionResult.Error}");

                positionFrame = positionResult.Value;
            }
            else if (movementTypeResult.Value == MovementType.Joint)
            {
                if (joints == null)
                    return Result.Failure<TrajectoryPoint>("Joint movement requires joints data");

                var jointsValidation = ValidateJointsArray(joints);
                if (jointsValidation.IsFailure)
                    return Result.Failure<TrajectoryPoint>(jointsValidation.Error);

                validatedJoints = joints;
            }

            return Result.Success(new TrajectoryPoint(
                movementTypeResult.Value,
                positionFrame,
                validatedJoints,
                speedResult.Value,
                accelerationResult.Value));
        }

        private static Result<MovementType> ParseMovementType(string movementType)
        {
            if (string.IsNullOrWhiteSpace(movementType))
                return Result.Failure<MovementType>("Movement type cannot be empty");

            return movementType.ToLowerInvariant() switch
            {
                "linear" => Result.Success(MovementType.Linear),
                "joint" => Result.Success(MovementType.Joint),
                _ => Result.Failure<MovementType>($"Unsupported movement type: {movementType}. Supported types: linear, joint")
            };
        }

        private static Result ValidateJointsArray(double[] joints)
        {
            if (joints.Length != 6)
                return Result.Failure($"Joint array must have 6 elements [J1-J6], got {joints.Length}");

            for (int i = 0; i < joints.Length; i++)
            {
                if (double.IsNaN(joints[i]) || double.IsInfinity(joints[i]))
                    return Result.Failure($"Invalid joint value for J{i + 1}: {joints[i]}");
            }

            return Result.Success();
        }

        /// <summary>
        /// Formats trajectory point as robot programming language command.
        /// Delegates to appropriate formatter based on movement type.
        /// </summary>
        public string FormatAsRobotCommand(FirmwareVersion firmware)
        {
            return MovementType switch
            {
                MovementType.Linear => FormatLinearCommand(firmware),
                MovementType.Joint => FormatJointCommand(firmware),
                _ => throw new InvalidOperationException($"Unknown movement type: {MovementType}")
            };
        }

        private string FormatLinearCommand(FirmwareVersion firmware)
        {
            var speedCommand = firmware.FormatSpeedCommand(Speed.Value);
            return $"MOVL {Position!.FormatAsRobotCode()} {speedCommand} ACC={Acceleration.Value}";
        }

        private string FormatJointCommand(FirmwareVersion firmware)
        {
            var jointsFormatted = string.Join(",", Array.ConvertAll(Joints!, j => j.ToString("F1")));
            var speedCommand = firmware.FormatSpeedCommand(Speed.Value);
            return $"MOVJ J[{jointsFormatted}] {speedCommand}% ACC={Acceleration.Value}";
        }

        // Value object equality implementation
        public bool Equals(TrajectoryPoint? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            
            return MovementType == other.MovementType &&
                   Equals(Position, other.Position) &&
                   (Joints?.SequenceEqual(other.Joints ?? Array.Empty<double>()) ?? other.Joints == null) &&
                   Speed.Equals(other.Speed) &&
                   Acceleration.Equals(other.Acceleration);
        }

        public override bool Equals(object? obj) => Equals(obj as TrajectoryPoint);
        public override int GetHashCode() => HashCode.Combine(MovementType, Position, Speed, Acceleration);

        public static bool operator ==(TrajectoryPoint? left, TrajectoryPoint? right) => Equals(left, right);
        public static bool operator !=(TrajectoryPoint? left, TrajectoryPoint? right) => !Equals(left, right);
    }

    /// <summary>
    /// Movement type enumeration for type safety.
    /// </summary>
    public enum MovementType
    {
        Linear,
        Joint
    }
}