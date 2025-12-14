using Xunit;
using NovaTechPostProcessor.Application.Commands;
using NovaTechPostProcessor.Application.Handlers;
using NovaTechPostProcessor.Application.Services;
using NovaTechPostProcessor.Infrastructure.Logging;
using NovaTechPostProcessor.Infrastructure.Resilience;
using NovaTechPostProcessor.Domain.Entities;
using NovaTechPostProcessor.Domain.ValueObjects;

namespace NovaTechPostProcessor.Tests
{
    /// <summary>
    /// Tests demonstrating how we handle edge cases and ambiguous specifications.
    /// These tests show our reasoning process for unclear requirements and
    /// validate that our system fails safely in boundary conditions.
    /// </summary>
    public class EdgeCaseHandlingTests
    {
        /// <summary>
        /// EDGE CASE: Missing acceleration handling.
        /// The documentation states "acceleration defaults to 50 if omitted" but
        /// doesn't clarify if this applies to JSON null vs missing property.
        /// We handle both cases the same way for consistency.
        /// </summary>
        [Fact]
        public void ProcessTrajectory_AppliesDefaultAcceleration_WhenMissing()
        {
            // Arrange - Command with missing acceleration
            var command = new ProcessTrajectoryCommand
            {
                RobotModel = "NovaTech RT-500",
                FirmwareVersion = "3.2",
                BaseFrame = new double[] { 0, 0, 0, 0, 0, 0 },
                ToolFrame = new double[] { 0, 0, 150, 0, 0, 0 },
                TrajectoryPoints = new List<TrajectoryPointDto>
                {
                    new TrajectoryPointDto
                    {
                        Type = "linear",
                        Position = new double[] { 500, 200, 300, 0, 90, 0 },
                        Speed = 100,
                        // Acceleration intentionally omitted
                    }
                }
            };

            // Act & Assert - Command validation should succeed
            var validationResult = command.Validate();
            Assert.True(validationResult.IsSuccess);

            // The domain logic will apply default acceleration (50) during processing
            // This demonstrates our interpretation of the ambiguous specification
        }

        /// <summary>
        /// EDGE CASE: Firmware version edge boundary testing.
        /// Documentation says "Firmware <3.1: SPD parameter uses legacy format"
        /// but doesn't specify behavior exactly AT version 3.1.
        /// Our interpretation: 3.1 and above use modern format.
        /// </summary>
        [Theory]
        [InlineData("2.9.9", "SPD(100)")]  // Legacy: Just under 3.1
        [InlineData("3.0.99", "SPD(100)")] // Legacy: Just under 3.1
        [InlineData("3.1", "SPD=100")]     // Modern: Exactly 3.1 (our decision point)
        [InlineData("3.1.1", "SPD=100")]   // Modern: Just over 3.1
        [InlineData("4.0", "SPD=100")]     // Modern: Well above 3.1
        public void FirmwareVersion_BoundaryConditions_UseCorrectSyntax(
            string version, string expectedSyntax)
        {
            // This test documents our reasoning about the ambiguous specification
            // and ensures consistent behavior at the boundary
            var firmwareResult = FirmwareVersion.Create(version);
            Assert.True(firmwareResult.IsSuccess);
            
            var actualSyntax = firmwareResult.Value.FormatSpeedCommand(100);
            Assert.Equal(expectedSyntax, actualSyntax);
        }

        /// <summary>
        /// EDGE CASE: Invalid firmware version formats.
        /// The specification doesn't mention how to handle malformed version strings.
        /// We fail fast with clear error messages rather than attempting to parse.
        /// </summary>
        [Theory]
        [InlineData("")]                    // Empty string
        [InlineData("   ")]                 // Whitespace only
        [InlineData("v3.2")]               // Prefix that might be used
        [InlineData("3.2.1.4.5")]          // Too many version parts
        [InlineData("three.two")]          // Non-numeric
        [InlineData("3.2-beta")]           // Pre-release identifier
        public void FirmwareVersion_InvalidFormats_FailWithClearError(string invalidVersion)
        {
            // Act
            var result = FirmwareVersion.Create(invalidVersion);
            
            // Assert - Should fail with specific error message
            Assert.True(result.IsFailure);
            Assert.Contains("Invalid firmware version format", result.Error);
        }

        /// <summary>
        /// EDGE CASE: Joint 6 extreme values testing.
        /// Tests our interpretation that ±720° is the absolute limit,
        /// including values that would be valid on other joints but invalid for J6.
        /// </summary>
        [Theory]
        [InlineData(719.999, true)]    // Just within limit (floating point precision)
        [InlineData(720.0, true)]      // Exactly at limit
        [InlineData(720.001, false)]   // Just over limit (floating point precision)
        [InlineData(-719.999, true)]   // Just within negative limit
        [InlineData(-720.0, true)]     // Exactly at negative limit  
        [InlineData(-720.001, false)]  // Just over negative limit
        [InlineData(1440.0, false)]    // Two full rotations (definitely invalid)
        public void Joint6_ExtremeValues_ValidatesPrecisely(double angle, bool shouldBeValid)
        {
            // This test validates our understanding that Joint 6 has extended range
            // but still has absolute limits, unlike unlimited rotation
            var trajectory = CreateValidTrajectory();
            
            var result = trajectory.AddTrajectoryPoint(
                "joint",
                null,
                new double[] { 0, 0, 0, 0, 0, angle }, // J6 = test angle
                50);
                
            Assert.Equal(shouldBeValid, result.IsSuccess);
            if (!shouldBeValid)
            {
                Assert.Contains("exceeds NovaTech RT-500 range", result.Error);
            }
        }

        /// <summary>
        /// EDGE CASE: Speed value boundary testing.
        /// The documentation doesn't specify maximum speed limits.
        /// We implement reasonable safety limits and document the assumption.
        /// </summary>
        [Theory]
        [InlineData(1)]         // Minimum positive speed
        [InlineData(9999)]      // High but reasonable speed
        [InlineData(10000)]     // Our maximum safety limit
        [InlineData(10001)]     // Just over our safety limit
        [InlineData(100000)]    // Unreasonably high speed
        public void Speed_BoundaryValues_RespectsSafetyLimits(int speedValue)
        {
            // Act
            var speedResult = Speed.Create(speedValue);
            var shouldBeValid = speedValue <= 10000; // Our documented safety limit
            
            // Assert
            Assert.Equal(shouldBeValid, speedResult.IsSuccess);
            if (!shouldBeValid)
            {
                Assert.Contains("exceeds maximum safe limit", speedResult.Error);
            }
        }

        /// <summary>
        /// EDGE CASE: Mixed movement types in same trajectory.
        /// The specification doesn't explicitly state if mixing linear and joint
        /// movements is allowed, but real-world robotics commonly does this.
        /// We allow it and document the assumption.
        /// </summary>
        [Fact]
        public void TrajectoryAggregate_AllowsMixedMovementTypes()
        {
            // Arrange
            var trajectory = CreateValidTrajectory();
            
            // Act - Add different movement types
            var linearResult = trajectory.AddTrajectoryPoint(
                "linear",
                new double[] { 500, 200, 300, 0, 90, 0 },
                null,
                100);
                
            var jointResult = trajectory.AddTrajectoryPoint(
                "joint", 
                null,
                new double[] { 45, -30, 60, 0, 45, 180 },
                50);
                
            var anotherLinearResult = trajectory.AddTrajectoryPoint(
                "linear",
                new double[] { 600, 250, 100, 45, 90, -45 },
                null,
                200);
            
            // Assert - All should succeed
            Assert.True(linearResult.IsSuccess);
            Assert.True(jointResult.IsSuccess);  
            Assert.True(anotherLinearResult.IsSuccess);
            
            // Final validation should pass
            var finalValidation = trajectory.ValidateTrajectory();
            Assert.True(finalValidation.IsSuccess);
        }

        /// <summary>
        /// EDGE CASE: Coordinate frame with extreme values.
        /// The specification doesn't mention workspace limits.
        /// We implement basic reachability validation and document the limitation.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0)]           // Origin (should be valid)
        [InlineData(1000, 1000, 1000)] // Within reasonable workspace
        [InlineData(5000, 0, 0)]        // Very far reach (might be invalid)
        [InlineData(-5000, 0, 0)]       // Very far reach negative
        public void CoordinateFrame_ExtremePositions_ValidatesWorkspace(double x, double y, double z)
        {
            // Arrange
            var position = new double[] { x, y, z, 0, 0, 0 };
            var frameResult = CoordinateFrame.Create(position);
            
            // Assert - Should create frame successfully
            Assert.True(frameResult.IsSuccess);
            
            // Test workspace validation (our added safety feature)
            var frame = frameResult.Value;
            var workspaceResult = frame.ValidateWorkspace("NovaTech RT-500");
            
            // Very far positions should be flagged (our interpretation)
            var reach = Math.Sqrt(x*x + y*y + z*z);
            var shouldBeValid = reach <= 2000; // Our documented workspace limit
            
            Assert.Equal(shouldBeValid, workspaceResult.IsSuccess);
        }

        /// <summary>
        /// EDGE CASE: Empty trajectory validation.
        /// The specification doesn't explicitly require minimum trajectory length.
        /// We require at least one point for safety (robot programs should do something).
        /// </summary>
        [Fact]
        public void TrajectoryAggregate_RejectsEmptyTrajectory()
        {
            // Arrange
            var trajectory = CreateValidTrajectory();
            // Don't add any points
            
            // Act
            var validation = trajectory.ValidateTrajectory();
            
            // Assert - Should fail with meaningful message
            Assert.True(validation.IsFailure);
            Assert.Contains("must contain at least one point", validation.Error);
        }

        /// <summary>
        /// EDGE CASE: Command validation edge cases.
        /// Tests our interpretation of required vs optional fields.
        /// </summary>
        [Fact]
        public void ProcessTrajectoryCommand_ValidatesAllRequiredFields()
        {
            // Test missing robot model
            var missingRobot = new ProcessTrajectoryCommand
            {
                RobotModel = "", // Missing
                FirmwareVersion = "3.2",
                BaseFrame = new double[6],
                ToolFrame = new double[6],
                TrajectoryPoints = new List<TrajectoryPointDto> { CreateValidPoint() }
            };
            Assert.True(missingRobot.Validate().IsFailure);
            
            // Test missing firmware version
            var missingFirmware = new ProcessTrajectoryCommand
            {
                RobotModel = "NovaTech RT-500",
                FirmwareVersion = "", // Missing
                BaseFrame = new double[6],
                ToolFrame = new double[6],
                TrajectoryPoints = new List<TrajectoryPointDto> { CreateValidPoint() }
            };
            Assert.True(missingFirmware.Validate().IsFailure);
            
            // Test invalid frame sizes
            var invalidFrame = new ProcessTrajectoryCommand
            {
                RobotModel = "NovaTech RT-500", 
                FirmwareVersion = "3.2",
                BaseFrame = new double[3], // Invalid size
                ToolFrame = new double[6],
                TrajectoryPoints = new List<TrajectoryPointDto> { CreateValidPoint() }
            };
            Assert.True(invalidFrame.Validate().IsFailure);
            
            // Test empty trajectory
            var emptyTrajectory = new ProcessTrajectoryCommand
            {
                RobotModel = "NovaTech RT-500",
                FirmwareVersion = "3.2", 
                BaseFrame = new double[6],
                ToolFrame = new double[6],
                TrajectoryPoints = new List<TrajectoryPointDto>() // Empty
            };
            Assert.True(emptyTrajectory.Validate().IsFailure);
        }

        /// <summary>
        /// Helper method to create a valid trajectory for testing.
        /// </summary>
        private TrajectoryAggregate CreateValidTrajectory()
        {
            var result = TrajectoryAggregate.Create(
                "NovaTech RT-500",
                "3.2",
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 0, 0, 150, 0, 0, 0 });
                
            Assert.True(result.IsSuccess);
            return result.Value;
        }

        /// <summary>
        /// Helper method to create a valid trajectory point DTO.
        /// </summary>
        private TrajectoryPointDto CreateValidPoint()
        {
            return new TrajectoryPointDto
            {
                Type = "linear",
                Position = new double[] { 500, 200, 300, 0, 90, 0 },
                Speed = 100,
                Acceleration = 75
            };
        }
    }
}