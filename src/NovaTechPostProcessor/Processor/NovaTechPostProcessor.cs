using NovaTechPostProcessor.Models;
using NovaTechPostProcessor.Specification;
using System.Text;

namespace NovaTechPostProcessor.Processor
{
    /// <summary>
    /// Core postprocessor that converts trajectory JSON data into valid NovaTech RT-500 robot code.
    /// Handles firmware differences, validation, and proper command formatting.
    /// </summary>
    public class NovaTechPostProcessor
    {
        /// <summary>
        /// Processes trajectory data and generates robot code.
        /// Performs validation and applies robot-specific formatting rules.
        /// </summary>
        /// <param name="trajectoryData">Input trajectory data from JSON</param>
        /// <returns>Generated robot code as string</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        public string ProcessTrajectory(TrajectoryData trajectoryData)
        {
            // Validate input data
            ValidateTrajectoryData(trajectoryData);
            
            var output = new StringBuilder();
            
            // Add header comment
            output.AppendLine($"// Generated code for {trajectoryData.Robot}");
            output.AppendLine($"// Firmware version: {trajectoryData.FirmwareVersion}");
            output.AppendLine();
            
            // Add BASE and TOOL commands (must precede motion commands)
            output.AppendLine(GenerateBaseCommand(trajectoryData.BaseFrame));
            output.AppendLine(GenerateToolCommand(trajectoryData.ToolFrame));
            output.AppendLine();
            
            // Process each trajectory point
            foreach (var point in trajectoryData.Trajectory)
            {
                ValidateTrajectoryPoint(point);
                
                string command = point.Type.ToLower() switch
                {
                    "linear" => GenerateLinearMoveCommand(point, trajectoryData.FirmwareVersion),
                    "joint" => GenerateJointMoveCommand(point, trajectoryData.FirmwareVersion),
                    _ => throw new ArgumentException($"Unsupported movement type: {point.Type}")
                };
                
                output.AppendLine(command);
            }
            
            return output.ToString();
        }
        
        /// <summary>
        /// Validates the overall trajectory data structure and content.
        /// </summary>
        private void ValidateTrajectoryData(TrajectoryData data)
        {
            if (string.IsNullOrEmpty(data.Robot))
                throw new ArgumentException("Robot model must be specified");
            
            if (data.Robot != NovaTechRT500Spec.RobotModel)
                throw new ArgumentException($"Unsupported robot model: {data.Robot}");
            
            if (string.IsNullOrEmpty(data.FirmwareVersion))
                throw new ArgumentException("Firmware version must be specified");
            
            if (!NovaTechRT500Spec.IsValidPositionArray(data.BaseFrame))
                throw new ArgumentException("Base frame must contain 6 elements");
            
            if (!NovaTechRT500Spec.IsValidPositionArray(data.ToolFrame))
                throw new ArgumentException("Tool frame must contain 6 elements");
            
            if (data.Trajectory == null || data.Trajectory.Count == 0)
                throw new ArgumentException("Trajectory must contain at least one point");
        }
        
        /// <summary>
        /// Validates individual trajectory point data.
        /// </summary>
        private void ValidateTrajectoryPoint(TrajectoryPoint point)
        {
            // Validate speed - must be positive
            if (!NovaTechRT500Spec.IsValidSpeed(point.Speed))
                throw new ArgumentException($"Invalid speed value: {point.Speed}. Speed must be positive.");
            
            // Validate movement type and corresponding data
            switch (point.Type.ToLower())
            {
                case "linear":
                    if (point.Position == null || !NovaTechRT500Spec.IsValidPositionArray(point.Position))
                        throw new ArgumentException("Linear movement requires valid position array with 6 elements");
                    break;
                    
                case "joint":
                    if (point.Joints == null || !NovaTechRT500Spec.IsValidJointArray(point.Joints))
                        throw new ArgumentException("Joint movement requires valid joints array with 6 elements");
                    
                    // Special validation for Joint 6 extended range
                    if (!NovaTechRT500Spec.IsValidJoint6Angle(point.Joints[5]))
                        throw new ArgumentException($"Joint 6 angle {point.Joints[5]} exceeds valid range (±720°)");
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported movement type: {point.Type}");
            }
        }
        
        /// <summary>
        /// Generates BASE command with coordinate formatting.
        /// BASE command sets the base coordinate frame for subsequent moves.
        /// </summary>
        private string GenerateBaseCommand(double[] baseFrame)
        {
            var positionArray = NovaTechRT500Spec.FormatPositionArray(baseFrame);
            return $"{NovaTechRT500Spec.BaseCommand} {positionArray}";
        }
        
        /// <summary>
        /// Generates TOOL command with coordinate formatting.
        /// TOOL command defines the tool offset from robot flange.
        /// </summary>
        private string GenerateToolCommand(double[] toolFrame)
        {
            var positionArray = NovaTechRT500Spec.FormatPositionArray(toolFrame);
            return $"{NovaTechRT500Spec.ToolCommand} {positionArray}";
        }
        
        /// <summary>
        /// Generates MOVL command for linear (Cartesian) motion.
        /// DOCX Format: MOVL P[x,y,z,rx,ry,rz] SPD=v ACC=a
        /// </summary>
        private string GenerateLinearMoveCommand(TrajectoryPoint point, string firmwareVersion)
        {
            var positionArray = NovaTechRT500Spec.FormatPositionArray(point.Position!);
            var speedCommand = NovaTechRT500Spec.FormatSpeedCommand(firmwareVersion, point.Speed);
            var acceleration = point.Acceleration ?? NovaTechRT500Spec.DefaultAcceleration;
            
            return $"{NovaTechRT500Spec.LinearMoveCommand} {positionArray} {speedCommand} ACC={acceleration}";
        }
        
        /// <summary>
        /// Generates MOVJ command for joint motion.
        /// DOCX Format: MOVJ J[j1,j2,j3,j4,j5,j6] SPD=v%
        /// </summary>
        private string GenerateJointMoveCommand(TrajectoryPoint point, string firmwareVersion)
        {
            var jointArray = NovaTechRT500Spec.FormatJointArray(point.Joints!);
            var speedCommand = NovaTechRT500Spec.FormatSpeedCommand(firmwareVersion, point.Speed);
            var acceleration = point.Acceleration ?? NovaTechRT500Spec.DefaultAcceleration;
            
            // For joint motion, speed is percentage of max velocity per joint (as per DOCX)
            return $"{NovaTechRT500Spec.JointMoveCommand} {jointArray} {speedCommand}% ACC={acceleration}";
        }
    }
}