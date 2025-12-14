using NovaTechPostProcessor.Domain.Common;
using System;

namespace NovaTechPostProcessor.Domain.ValueObjects
{
    /// <summary>
    /// Acceleration value object with domain validation.
    /// Encapsulates acceleration constraints per robot documentation (10-100%).
    /// </summary>
    public class Acceleration : IEquatable<Acceleration>
    {
        public const int DefaultValue = 50;
        public const int MinValue = 10;
        public const int MaxValue = 100;

        public int Value { get; private set; }

        private Acceleration(int value)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method with range validation.
        /// Enforces business rule: acceleration must be 10-100% per robot specification.
        /// </summary>
        public static Result<Acceleration> Create(int value)
        {
            if (value < MinValue || value > MaxValue)
                return Result.Failure<Acceleration>(
                    $"Acceleration must be between {MinValue}-{MaxValue}% per robot specification, got: {value}");

            return Result.Success(new Acceleration(value));
        }

        /// <summary>
        /// Creates default acceleration value (50%) as specified in documentation.
        /// </summary>
        public static Acceleration Default() => new(DefaultValue);

        // Value object equality implementation
        public bool Equals(Acceleration? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Value == other.Value;
        }

        public override bool Equals(object? obj) => Equals(obj as Acceleration);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(Acceleration? left, Acceleration? right) => Equals(left, right);
        public static bool operator !=(Acceleration? left, Acceleration? right) => !Equals(left, right);

        // Implicit conversion for convenience
        public static implicit operator int(Acceleration acceleration) => acceleration.Value;
    }
}