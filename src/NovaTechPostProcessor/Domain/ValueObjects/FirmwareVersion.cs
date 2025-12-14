using NovaTechPostProcessor.Domain.Common;
using System;

namespace NovaTechPostProcessor.Domain.ValueObjects
{
    /// <summary>
    /// Firmware version value object with semantic version comparison.
    /// Encapsulates firmware-specific behavior rules.
    /// </summary>
    public class FirmwareVersion : IEquatable<FirmwareVersion>, IComparable<FirmwareVersion>
    {
        public Version Version { get; private set; }
        public string VersionString { get; private set; }

        private FirmwareVersion(Version version, string versionString)
        {
            Version = version;
            VersionString = versionString;
        }

        /// <summary>
        /// Factory method with version validation.
        /// Ensures firmware version is in valid semantic format.
        /// </summary>
        public static Result<FirmwareVersion> Create(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return Result.Failure<FirmwareVersion>("Invalid firmware version format: " + (versionString ?? ""));

            if (!Version.TryParse(versionString, out var version))
                return Result.Failure<FirmwareVersion>($"Invalid firmware version format: {versionString}");

            return Result.Success(new FirmwareVersion(version, versionString));
        }

        /// <summary>
        /// Business rule: Determines if firmware uses legacy command syntax.
        /// Encapsulates firmware-specific behavior.
        /// </summary>
        public bool UsesLegacySyntax => Version < new Version(3, 1);

        /// <summary>
        /// Domain service method: Formats speed command based on firmware version.
        /// Implements firmware-specific command generation rules.
        /// </summary>
        public string FormatSpeedCommand(int speed)
        {
            return UsesLegacySyntax ? $"SPD({speed})" : $"SPD={speed}";
        }

        // Value object equality and comparison implementation
        public bool Equals(FirmwareVersion? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Version.Equals(other.Version);
        }

        public override bool Equals(object? obj) => Equals(obj as FirmwareVersion);
        public override int GetHashCode() => Version.GetHashCode();
        public override string ToString() => VersionString;

        public int CompareTo(FirmwareVersion? other)
        {
            if (other is null) return 1;
            return Version.CompareTo(other.Version);
        }

        public static bool operator ==(FirmwareVersion? left, FirmwareVersion? right) => Equals(left, right);
        public static bool operator !=(FirmwareVersion? left, FirmwareVersion? right) => !Equals(left, right);
        public static bool operator <(FirmwareVersion left, FirmwareVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(FirmwareVersion left, FirmwareVersion right) => left.CompareTo(right) > 0;
    }
}