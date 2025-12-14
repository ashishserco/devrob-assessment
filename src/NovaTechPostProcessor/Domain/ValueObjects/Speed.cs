using NovaTechPostProcessor.Domain.Common;
using System;

namespace NovaTechPostProcessor.Domain.ValueObjects
{
    /// <summary>
    /// Speed value object with domain validation.
    /// Encapsulates speed constraints and safety rules.
    /// </summary>
    public class Speed : IEquatable<Speed>
    {
        public int Value { get; private set; }

        private Speed(int value)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method with safety validation.
        /// Enforces business rule: speed must be positive for safety.
        /// </summary>
        public static Result<Speed> Create(int value)
        {
            if (value <= 0)
                return Result.Failure<Speed>($"Speed must be positive for safety reasons, got: {value}");

            // Additional business rules can be added here
            if (value > 10000) // Example maximum speed limit
                return Result.Failure<Speed>($"Speed {value} exceeds maximum safe limit (10000)");

            return Result.Success(new Speed(value));
        }

        // Value object equality implementation
        public bool Equals(Speed? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Value == other.Value;
        }

        public override bool Equals(object? obj) => Equals(obj as Speed);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(Speed? left, Speed? right) => Equals(left, right);
        public static bool operator !=(Speed? left, Speed? right) => !Equals(left, right);

        // Implicit conversion for convenience
        public static implicit operator int(Speed speed) => speed.Value;
    }
}