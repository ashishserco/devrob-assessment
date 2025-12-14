using NovaTechPostProcessor.Domain.Common;
using System;
using System.Collections.Generic;

namespace NovaTechPostProcessor.Domain.ValueObjects
{
    /// <summary>
    /// Robot identifier value object following DDD principles.
    /// Immutable, self-validating, and encapsulates robot identity rules.
    /// </summary>
    public class RobotId : IEquatable<RobotId>
    {
        private static readonly HashSet<string> SupportedRobots = new()
        {
            "NovaTech RT-500",
            // Future: "KUKA KR10", "FANUC R-30iA", "ABB IRB 120"
        };

        public string Model { get; private set; }

        private RobotId(string model)
        {
            Model = model;
        }

        /// <summary>
        /// Factory method with domain validation.
        /// Encapsulates business rules for robot identification.
        /// </summary>
        public static Result<RobotId> Create(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return Result.Failure<RobotId>("Unsupported robot model: . Supported models: " + string.Join(", ", SupportedRobots));

            if (!SupportedRobots.Contains(model))
                return Result.Failure<RobotId>($"Unsupported robot model: {model}. Supported models: {string.Join(", ", SupportedRobots)}");

            return Result.Success(new RobotId(model));
        }

        // Value object equality implementation
        public bool Equals(RobotId? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Model == other.Model;
        }

        public override bool Equals(object? obj) => Equals(obj as RobotId);
        public override int GetHashCode() => Model.GetHashCode();
        public override string ToString() => Model;

        public static bool operator ==(RobotId? left, RobotId? right) => Equals(left, right);
        public static bool operator !=(RobotId? left, RobotId? right) => !Equals(left, right);
    }
}