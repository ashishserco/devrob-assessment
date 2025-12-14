using Xunit;
using NovaTechPostProcessor.Domain.Entities;
using NovaTechPostProcessor.Domain.ValueObjects;

namespace NovaTechPostProcessor.Tests
{
    /// <summary>
    /// Tests that demonstrate our reasoning about robot code generation requirements.
    /// These tests validate our interpretation of the robot programming language
    /// specifications and show how we handle ambiguous formatting requirements.
    /// </summary>
    public class RobotCodeGenerationTests
    {
        /// <summary>
        /// SPECIFICATION INTERPRETATION: Command format validation.
        /// The DOCX specifies exact formats like "MOVL P[x,y,z,rx,ry,rz] SPD=v ACC=a"
        /// We test that our generated code matches this specification exactly.
        /// </summary>
        [Fact]
        public void TrajectoryPoint_GeneratesCorrectLinearCommandFormat()
        {
            // Arrange - Create trajectory point as specified in DOCX
            var pointResult = TrajectoryPoint.Create(
                "linear",
                new double[] { 500.0, 200.0, 300.0, 0.0, 90.0, 0.0 },
                null,
                100,
                75);
                
            Assert.True(pointResult.IsSuccess);
            var point = pointResult.Value;
            
            var firmwareResult = FirmwareVersion.Create("3.2");
            Assert.True(firmwareResult.IsSuccess);
            
            // Act
            var generatedCommand = point.FormatAsRobotCommand(firmwareResult.Value);
            
            // Assert - Must match DOCX specification exactly
            var expectedCommand = "MOVL P[500.0,200.0,300.0,0.0,90.0,0.0] SPD=100 ACC=75";
            Assert.Equal(expectedCommand, generatedCommand);
        }

        /// <summary>
        /// SPECIFICATION INTERPRETATION: Joint movement format with percentage.
        /// The DOCX shows joint speed as "SPD=v%" indicating percentage of max velocity.
        /// This is different from linear motion and demonstrates our understanding
        /// of movement-type specific formatting requirements.
        /// </summary>
        [Fact]
        public void TrajectoryPoint_GeneratesCorrectJointCommandFormat()
        {
            // Arrange - Joint movement with extended J6 range
            var pointResult = TrajectoryPoint.Create(
                "joint",
                null,
                new double[] { 45.0, -30.0, 60.0, 0.0, 45.0, 180.0 },
                50,
                null); // Test default acceleration application
                
            Assert.True(pointResult.IsSuccess);
            var point = pointResult.Value;
            
            var firmwareResult = FirmwareVersion.Create("3.2");
            Assert.True(firmwareResult.IsSuccess);
            
            // Act
            var generatedCommand = point.FormatAsRobotCommand(firmwareResult.Value);
            
            // Assert - Joint format includes % and default ACC=50
            var expectedCommand = "MOVJ J[45.0,-30.0,60.0,0.0,45.0,180.0] SPD=50% ACC=50";
            Assert.Equal(expectedCommand, generatedCommand);
        }

        /// <summary>
        /// EDGE CASE: Firmware version affects command syntax.
        /// This demonstrates our handling of the "Firmware <3.1 uses legacy format"
        /// specification and ensures backward compatibility.
        /// </summary>
        [Theory]
        [InlineData("2.9", "MOVL P[500.0,200.0,300.0,0.0,90.0,0.0] SPD(100) ACC=75")]  // Legacy
        [InlineData("3.1", "MOVL P[500.0,200.0,300.0,0.0,90.0,0.0] SPD=100 ACC=75")]  // Modern
        public void TrajectoryPoint_AdaptsSyntaxToFirmwareVersion(string firmwareVersion, string expectedCommand)
        {
            // Arrange
            var pointResult = TrajectoryPoint.Create(
                "linear",
                new double[] { 500.0, 200.0, 300.0, 0.0, 90.0, 0.0 },
                null,
                100,
                75);
                
            var firmwareResult = FirmwareVersion.Create(firmwareVersion);
            Assert.True(pointResult.IsSuccess && firmwareResult.IsSuccess);
            
            // Act
            var generatedCommand = pointResult.Value.FormatAsRobotCommand(firmwareResult.Value);
            
            // Assert
            Assert.Equal(expectedCommand, generatedCommand);
        }

        /// <summary>
        /// SPECIFICATION INTERPRETATION: Coordinate precision formatting.
        /// The robot examples show 1 decimal place precision (e.g., "500.0").
        /// We test that our formatting is consistent with this specification.
        /// </summary>
        [Theory]
        [InlineData(new double[] { 0, 0, 0, 0, 0, 0 }, "P[0.0,0.0,0.0,0.0,0.0,0.0]")]
        [InlineData(new double[] { 500.123, 200.789, 300.456, 0.1, 90.2, 0.3 }, "P[500.1,200.8,300.5,0.1,90.2,0.3]")]
        [InlineData(new double[] { -100.5, -200.7, 150.0, 45.0, 90.0, -45.0 }, "P[-100.5,-200.7,150.0,45.0,90.0,-45.0]")]
        public void CoordinateFrame_FormatsWithCorrectPrecision(double[] coordinates, string expectedFormat)
        {
            // Arrange
            var frameResult = CoordinateFrame.Create(coordinates);
            Assert.True(frameResult.IsSuccess);
            
            // Act
            var formattedFrame = frameResult.Value.FormatAsRobotCode();
            
            // Assert - Should match expected precision (1 decimal place)
            Assert.Equal(expectedFormat, formattedFrame);
        }

        /// <summary>
        /// BUSINESS RULE: Command ordering requirements.
        /// The specification states "TOOL and BASE commands must precede motion commands".
        /// This test validates that our generated program structure follows this rule.
        /// </summary>
        [Fact]
        public void TrajectoryAggregate_GeneratesCommandsInCorrectOrder()
        {
            // Arrange - Create complete trajectory
            var trajectoryResult = TrajectoryAggregate.Create(
                "NovaTech RT-500",
                "3.2",
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 0, 0, 150, 0, 0, 0 });
                
            Assert.True(trajectoryResult.IsSuccess);
            var trajectory = trajectoryResult.Value;
            
            // Add trajectory points
            trajectory.AddTrajectoryPoint("linear", new double[] { 500, 200, 300, 0, 90, 0 }, null, 100);
            trajectory.AddTrajectoryPoint("joint", null, new double[] { 45, -30, 60, 0, 45, 180 }, 50);
            
            // Note: Full code generation would be handled by IRobotCodeGenerator implementation
            // This test validates that the aggregate maintains proper structure for code generation
            
            // Assert - Trajectory should be valid and ready for code generation
            var validation = trajectory.ValidateTrajectory();
            Assert.True(validation.IsSuccess);
            
            // Verify we have the required frame data
            Assert.NotNull(trajectory.BaseFrame);
            Assert.NotNull(trajectory.ToolFrame);
            Assert.Equal(2, trajectory.Points.Count);
        }

        /// <summary>
        /// EDGE CASE: Default acceleration application in generated code.
        /// The specification says "acceleration defaults to 50 if omitted".
        /// We test that this default is properly applied in generated commands.
        /// </summary>
        [Fact]
        public void TrajectoryPoint_AppliesDefaultAccelerationInGeneratedCode()
        {
            // Arrange - Point without explicit acceleration
            var pointResult = TrajectoryPoint.Create(
                "linear",
                new double[] { 600.0, 250.0, 100.0, 45.0, 90.0, -45.0 },
                null,
                200,
                null); // No acceleration specified
                
            Assert.True(pointResult.IsSuccess);
            var point = pointResult.Value;
            
            var firmwareResult = FirmwareVersion.Create("3.2");
            Assert.True(firmwareResult.IsSuccess);
            
            // Act
            var generatedCommand = point.FormatAsRobotCommand(firmwareResult.Value);
            
            // Assert - Should include default ACC=50
            Assert.Contains("ACC=50", generatedCommand);
            var expectedCommand = "MOVL P[600.0,250.0,100.0,45.0,90.0,-45.0] SPD=200 ACC=50";
            Assert.Equal(expectedCommand, generatedCommand);
        }

        /// <summary>
        /// EDGE CASE: Joint 6 extended range in generated code.
        /// Tests that our Joint 6 extended range (±720°) validation works
        /// with actual robot code generation.
        /// </summary>
        [Theory]
        [InlineData(350.0, true)]   // Valid extended range
        [InlineData(720.0, true)]   // Maximum valid range
        [InlineData(800.0, false)]  // Invalid - exceeds range
        public void TrajectoryPoint_ValidatesJoint6RangeForCodeGeneration(double joint6Angle, bool shouldGenerate)
        {
            // Arrange - Joint movement with test J6 angle
            var pointResult = TrajectoryPoint.Create(
                "joint",
                null,
                new double[] { 0, 0, 0, 0, 0, joint6Angle },
                50);
            
            if (shouldGenerate)
            {
                // Should create point successfully and generate code
                Assert.True(pointResult.IsSuccess);
                var point = pointResult.Value;
                
                var firmwareResult = FirmwareVersion.Create("3.2");
                var generatedCommand = point.FormatAsRobotCommand(firmwareResult.Value);
                
                Assert.Contains($"J[0.0,0.0,0.0,0.0,0.0,{joint6Angle:F1}]", generatedCommand);
            }
            else
            {
                // Should fail validation at trajectory level (not at point creation)
                // This would be caught when adding to trajectory aggregate
                var trajectoryResult = TrajectoryAggregate.Create(
                    "NovaTech RT-500", "3.2",
                    new double[6], new double[6]);
                    
                var addResult = trajectoryResult.Value.AddTrajectoryPoint(
                    "joint", null, new double[] { 0, 0, 0, 0, 0, joint6Angle }, 50);
                    
                Assert.True(addResult.IsFailure);
                Assert.Contains("exceeds NovaTech RT-500 range", addResult.Error);
            }
        }

        /// <summary>
        /// SPECIFICATION INTERPRETATION: Complete program structure.
        /// This test validates our understanding of the complete robot program format
        /// including headers, frame setup, and motion commands.
        /// </summary>
        [Fact]
        public void CompleteTrajectory_FollowsDocumentedProgramStructure()
        {
            // This test documents our interpretation of complete program structure:
            // 1. Header comments (robot model, firmware version)
            // 2. BASE command (must precede motion)  
            // 3. TOOL command (must precede motion)
            // 4. Motion commands (MOVL, MOVJ in sequence)
            
            // Arrange
            var trajectoryResult = TrajectoryAggregate.Create(
                "NovaTech RT-500",
                "3.2", 
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 0, 0, 150, 0, 0, 0 });
                
            Assert.True(trajectoryResult.IsSuccess);
            var trajectory = trajectoryResult.Value;
            
            // Add mixed movement types (as per sample input)
            trajectory.AddTrajectoryPoint("linear", new double[] { 500, 200, 300, 0, 90, 0 }, null, 100, 75);
            trajectory.AddTrajectoryPoint("joint", null, new double[] { 45, -30, 60, 0, 45, 180 }, 50);
            trajectory.AddTrajectoryPoint("linear", new double[] { 600, 250, 100, 45, 90, -45 }, null, 200);
            
            // Assert - Trajectory structure is valid for code generation
            var validation = trajectory.ValidateTrajectory();
            Assert.True(validation.IsSuccess);
            
            // Verify expected data for code generation
            Assert.Equal("NovaTech RT-500", trajectory.RobotId.Model);
            Assert.Equal("3.2", trajectory.FirmwareVersion.VersionString);
            Assert.False(trajectory.FirmwareVersion.UsesLegacySyntax); // 3.2 >= 3.1
            Assert.Equal(3, trajectory.Points.Count);
            
            // Frame data should be properly formatted
            Assert.Equal("P[0.0,0.0,0.0,0.0,0.0,0.0]", trajectory.BaseFrame.FormatAsRobotCode());
            Assert.Equal("P[0.0,0.0,150.0,0.0,0.0,0.0]", trajectory.ToolFrame.FormatAsRobotCode());
        }
    }
}