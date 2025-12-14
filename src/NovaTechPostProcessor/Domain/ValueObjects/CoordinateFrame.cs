using NovaTechPostProcessor.Domain.Common;
using System;
using System.Linq;

namespace NovaTechPostProcessor.Domain.ValueObjects
{
    /// <summary>
    /// Coordinate frame value object representing 6DOF pose [X,Y,Z,Rx,Ry,Rz].
    /// Encapsulates coordinate system validation and formatting rules.
    /// </summary>
    public class CoordinateFrame : IEquatable<CoordinateFrame>
    {
        public double[] Coordinates { get; private set; }
        public double X => Coordinates[0];
        public double Y => Coordinates[1];
        public double Z => Coordinates[2];
        public double Rx => Coordinates[3];
        public double Ry => Coordinates[4];
        public double Rz => Coordinates[5];

        private CoordinateFrame(double[] coordinates)
        {
            Coordinates = coordinates.ToArray(); // Defensive copy
        }

        /// <summary>
        /// Factory method with coordinate validation.
        /// Ensures frame has correct dimensionality and valid values.
        /// </summary>
        public static Result<CoordinateFrame> Create(double[] coordinates)
        {
            if (coordinates == null)
                return Result.Failure<CoordinateFrame>("Coordinates cannot be null");

            if (coordinates.Length != 6)
                return Result.Failure<CoordinateFrame>($"Coordinate frame must have 6 elements [X,Y,Z,Rx,Ry,Rz], got {coordinates.Length}");

            // Validate for NaN or infinity values
            for (int i = 0; i < coordinates.Length; i++)
            {
                if (double.IsNaN(coordinates[i]) || double.IsInfinity(coordinates[i]))
                {
                    var axis = i < 3 ? new[] { "X", "Y", "Z" }[i] : new[] { "Rx", "Ry", "Rz" }[i - 3];
                    return Result.Failure<CoordinateFrame>($"Invalid coordinate value for {axis}: {coordinates[i]}");
                }
            }

            return Result.Success(new CoordinateFrame(coordinates));
        }

        /// <summary>
        /// Formats coordinate frame for robot programming language output.
        /// Implements consistent precision formatting (1 decimal place).
        /// </summary>
        public string FormatAsRobotCode()
        {
            var formattedCoords = Coordinates.Select(c => c.ToString("F1"));
            return $"P[{string.Join(",", formattedCoords)}]";
        }

        /// <summary>
        /// Domain service: Validates workspace limits for specific robot.
        /// Could be extended with robot-specific workspace constraints.
        /// </summary>
        public Result ValidateWorkspace(string robotModel)
        {
            // Example workspace validation for NovaTech RT-500
            if (robotModel == "NovaTech RT-500")
            {
                // Basic reach validation (simplified)
                var reach = Math.Sqrt(X * X + Y * Y + Z * Z);
                if (reach > 2000) // 2 meter reach limit
                {
                    return Result.Failure($"Position [{X:F1}, {Y:F1}, {Z:F1}] exceeds robot workspace (reach: {reach:F1}mm)");
                }
            }

            return Result.Success();
        }

        // Value object equality implementation
        public bool Equals(CoordinateFrame? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Coordinates.SequenceEqual(other.Coordinates);
        }

        public override bool Equals(object? obj) => Equals(obj as CoordinateFrame);
        
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var coord in Coordinates)
                hash.Add(coord);
            return hash.ToHashCode();
        }

        public override string ToString() => FormatAsRobotCode();

        public static bool operator ==(CoordinateFrame? left, CoordinateFrame? right) => Equals(left, right);
        public static bool operator !=(CoordinateFrame? left, CoordinateFrame? right) => !Equals(left, right);
    }
}