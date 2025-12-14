using Xunit;
using NovaTechPostProcessor.Domain.ValueObjects;
using NovaTechPostProcessor.Domain.Entities;
using NovaTechPostProcessor.Specification;

namespace NovaTechPostProcessor.Tests
{
    /// <summary>
    /// Tests demonstrating how we handle ambiguous specifications and edge cases.
    /// These tests validate our interpretation of unclear robot documentation
    /// and ensure safe handling of boundary conditions.
    /// </summary>
    public class DomainValidationTests
    {
        /// <summary>
        /// EDGE CASE: Firmware version boundary testing.
        /// The documentation says "Firmware <3.1 uses legacy format" but doesn't specify
        /// exactly what happens AT 3.1. We interpret this as 3.1+ uses modern format.
        /// </summary>
        [Theory]
        [InlineData("3.0", true, "SPD(100)")]   // Legacy: < 3.1
        [InlineData("2.9", true, "SPD(100)")]   // Legacy: < 3.1  
        [InlineData("3.1", false, "SPD=100")]   // Modern: >= 3.1 (our interpretation)
        [InlineData("3.2", false, "SPD=100")]   // Modern: >= 3.1
        [InlineData("4.0", false, "SPD=100")]   // Modern: >= 3.1
        public void FirmwareVersion_SpeedSyntax_HandlesAmbiguousBoundary(
            string versionString, bool shouldUseLegacy, string expectedFormat)
        {
            // Arrange & Act
            var firmwareResult = FirmwareVersion.Create(versionString);
            
            // Assert
            Assert.True(firmwareResult.IsSuccess);
            var firmware = firmwareResult.Value;
            Assert.Equal(shouldUseLegacy, firmware.UsesLegacySyntax);
            Assert.Equal(expectedFormat, firmware.FormatSpeedCommand(100));
        }

        /// <summary>
        /// EDGE CASE: Joint 6 extended range validation.
        /// Documentation mentions "J6 rotation has 720° range unlike other joints (±180°)"
        /// We interpret this as ±720° total range based on the ±180° comparison.
        /// </summary>
        [Theory]
        [InlineData(720.0, true)]    // Maximum positive limit
        [InlineData(-720.0, true)]   // Maximum negative limit
        [InlineData(719.9, true)]    // Just within range
        [InlineData(-719.9, true)]   // Just within range
        [InlineData(720.1, false)]   // Just outside range
        [InlineData(-720.1, false)]  // Just outside range
        [InlineData(800.0, false)]   // Well outside range
        [InlineData(0.0, true)]      // Zero position (should be valid)
        [InlineData(360.0, true)]    // Full rotation (should be valid)
        public void Joint6_ExtendedRange_ValidatesCorrectly(double angle, bool shouldBeValid)
        {
            // Act
            var isValid = NovaTechRT500Spec.IsValidJoint6Angle(angle);
            
            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        /// <summary>
        /// EDGE CASE: Speed validation for safety.
        /// Documentation doesn't specify upper limits, so we implement conservative
        /// validation focusing on safety (no negative speeds that could cause damage).
        /// </summary>
        [Theory]
        [InlineData(1, true)]        // Minimum positive speed
        [InlineData(100, true)]      // Normal speed
        [InlineData(1000, true)]     // High speed
        [InlineData(10000, true)]    // Maximum reasonable speed (we set this limit)
        [InlineData(0, false)]       // Zero speed (unsafe - robot won't move)
        [InlineData(-1, false)]      // Negative speed (unsafe - could damage equipment)
        [InlineData(-50, false)]     // Large negative speed (definitely unsafe)
        [InlineData(10001, false)]   // Exceed our safety limit
        public void Speed_SafetyValidation_RejectsUnsafeValues(int speedValue, bool shouldBeValid)
        {
            // Act
            var speedResult = Speed.Create(speedValue);
            
            // Assert
            Assert.Equal(shouldBeValid, speedResult.IsSuccess);
        }

        /// <summary>
        /// AMBIGUOUS SPEC: Acceleration percentage range.
        /// Documentation says "acceleration is % (10-100)" but unclear what the % references.
        /// We implement the range validation as specified but document the ambiguity.
        /// </summary>
        [Theory]
        [InlineData(10, true)]       // Minimum specified
        [InlineData(50, true)]       // Default value (from "defaults to 50")
        [InlineData(100, true)]      // Maximum specified
        [InlineData(9, false)]       // Below minimum
        [InlineData(101, false)]     // Above maximum
        [InlineData(0, false)]       // Zero (below range)
        [InlineData(-10, false)]     // Negative (invalid)
        public void Acceleration_PercentageRange_FollowsSpecification(int accValue, bool shouldBeValid)
        {
            // Act
            var accResult = Acceleration.Create(accValue);
            
            // Assert
            Assert.Equal(shouldBeValid, accResult.IsSuccess);
        }

        /// <summary>
        /// EDGE CASE: Coordinate frame validation.
        /// Tests boundary conditions for coordinate arrays and validates our interpretation
        /// that exactly 6 elements are required [X,Y,Z,Rx,Ry,Rz].
        /// </summary>
        [Fact]
        public void CoordinateFrame_RequiresExactly6Elements()
        {
            // Test valid 6-element array
            var validFrame = CoordinateFrame.Create(new double[] { 0, 0, 0, 0, 0, 0 });
            Assert.True(validFrame.IsSuccess);

            // Test invalid arrays
            var tooFew = CoordinateFrame.Create(new double[] { 0, 0, 0 });
            Assert.True(tooFew.IsFailure);
            Assert.Contains("must have 6 elements", tooFew.Error);

            var tooMany = CoordinateFrame.Create(new double[] { 0, 0, 0, 0, 0, 0, 0 });
            Assert.True(tooMany.IsFailure);
            Assert.Contains("must have 6 elements", tooMany.Error);

            // Test null array
            var nullFrame = CoordinateFrame.Create(null!);
            Assert.True(nullFrame.IsFailure);
            Assert.Contains("cannot be null", nullFrame.Error);
        }

        /// <summary>
        /// EDGE CASE: Invalid coordinate values (NaN, Infinity).
        /// The specification doesn't mention these edge cases, so we proactively
        /// validate against them for robustness.
        /// </summary>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void CoordinateFrame_RejectsInvalidFloatingPointValues(double invalidValue)
        {
            // Arrange - Create array with invalid value in different positions
            var invalidFrames = new[]
            {
                new double[] { invalidValue, 0, 0, 0, 0, 0 },  // X
                new double[] { 0, invalidValue, 0, 0, 0, 0 },  // Y
                new double[] { 0, 0, invalidValue, 0, 0, 0 },  // Z
                new double[] { 0, 0, 0, invalidValue, 0, 0 },  // Rx
                new double[] { 0, 0, 0, 0, invalidValue, 0 },  // Ry
                new double[] { 0, 0, 0, 0, 0, invalidValue }   // Rz
            };

            // Act & Assert
            foreach (var frame in invalidFrames)
            {
                var result = CoordinateFrame.Create(frame);
                Assert.True(result.IsFailure);
                Assert.Contains("Invalid coordinate value", result.Error);
            }
        }

        /// <summary>
        /// BUSINESS RULE: Robot model validation.
        /// We only support NovaTech RT-500 as specified, but the architecture
        /// is designed to easily add new robot vendors.
        /// </summary>
        [Theory]
        [InlineData("NovaTech RT-500", true)]
        [InlineData("KUKA KR10", false)]      // Future robot support
        [InlineData("FANUC R-30iA", false)]   // Future robot support
        [InlineData("ABB IRB 120", false)]    // Future robot support
        [InlineData("", false)]               // Empty string
        [InlineData("Unknown Robot", false)]  // Invalid robot
        public void RobotId_ValidatesSupportedModels(string robotModel, bool shouldBeValid)
        {
            // Act
            var robotResult = RobotId.Create(robotModel);
            
            // Assert
            Assert.Equal(shouldBeValid, robotResult.IsSuccess);
            if (!shouldBeValid)
            {
                Assert.Contains("Unsupported robot model", robotResult.Error);
            }
        }

        /// <summary>
        /// INTEGRATION TEST: Complete trajectory aggregate validation.
        /// This tests our interpretation of the complete business rules
        /// and how different domain objects work together.
        /// </summary>
        [Fact]
        public void TrajectoryAggregate_ValidatesBusinessRules()
        {
            // Arrange - Create valid trajectory
            var trajectoryResult = TrajectoryAggregate.Create(
                "NovaTech RT-500",
                "3.2",
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 0, 0, 150, 0, 0, 0 });

            Assert.True(trajectoryResult.IsSuccess);
            var trajectory = trajectoryResult.Value;

            // Test adding valid point
            var validPointResult = trajectory.AddTrajectoryPoint(
                "linear",
                new double[] { 500, 200, 300, 0, 90, 0 },
                null,
                100,
                75);
            Assert.True(validPointResult.IsSuccess);

            // Test Joint 6 extended range business rule
            var joint6ValidResult = trajectory.AddTrajectoryPoint(
                "joint",
                null,
                new double[] { 0, 0, 0, 0, 0, 350 },  // J6 = 350° (valid for NovaTech)
                50);
            Assert.True(joint6ValidResult.IsSuccess);

            // Test Joint 6 exceeds even extended range
            var joint6InvalidResult = trajectory.AddTrajectoryPoint(
                "joint",
                null,
                new double[] { 0, 0, 0, 0, 0, 800 },  // J6 = 800° (invalid even for NovaTech)
                50);
            Assert.True(joint6InvalidResult.IsFailure);
            Assert.Contains("exceeds NovaTech RT-500 range", joint6InvalidResult.Error);

            // Final validation
            var finalValidation = trajectory.ValidateTrajectory();
            Assert.True(finalValidation.IsSuccess);
        }
    }
}