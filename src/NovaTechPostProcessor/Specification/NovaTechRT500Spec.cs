namespace NovaTechPostProcessor.Specification
{
    /// <summary>
    /// Encapsulates all NovaTech RT-500 robot specifications and constraints.
    /// This class acts as a single source of truth for robot-specific behavior.
    /// </summary>
    public static class NovaTechRT500Spec
    {
        // Robot identification
        public const string RobotModel = "NovaTech RT-500";
        
        // Command syntax
        public const string LinearMoveCommand = "MOVL";
        public const string JointMoveCommand = "MOVJ";
        public const string BaseCommand = "BASE";
        public const string ToolCommand = "TOOL";
        
        // Default values
        public const int DefaultAcceleration = 50;
        
        // Firmware version threshold for speed syntax change
        public static readonly Version FirmwareThreshold = new Version(3, 1);
        
        // Joint constraints
        public const int JointCount = 6;
        public const double Joint6MinDegrees = -720.0;
        public const double Joint6MaxDegrees = 720.0;
        
        /// <summary>
        /// Determines the correct speed syntax based on firmware version.
        /// Legacy firmware (< 3.1) uses SPD(value), modern firmware uses SPD=value.
        /// </summary>
        /// <param name="firmwareVersion">Firmware version string (e.g., "3.2", "2.9")</param>
        /// <param name="speed">Speed value to format</param>
        /// <returns>Properly formatted speed command</returns>
        public static string FormatSpeedCommand(string firmwareVersion, int speed)
        {
            if (!Version.TryParse(firmwareVersion, out var version))
            {
                throw new ArgumentException($"Invalid firmware version format: {firmwareVersion}");
            }
            
            // Legacy syntax for firmware < 3.1
            if (version < FirmwareThreshold)
            {
                return $"SPD({speed})";
            }
            
            // Modern syntax for firmware >= 3.1
            return $"SPD={speed}";
        }
        
        /// <summary>
        /// Validates that speed value is within acceptable range.
        /// Speed must be positive and non-zero.
        /// </summary>
        public static bool IsValidSpeed(int speed)
        {
            return speed > 0;
        }
        
        /// <summary>
        /// Validates Joint 6 angle is within extended range specification.
        /// Joint 6 supports ±720° rotation unlike typical ±180° joints.
        /// </summary>
        public static bool IsValidJoint6Angle(double angle)
        {
            return angle >= Joint6MinDegrees && angle <= Joint6MaxDegrees;
        }
        
        /// <summary>
        /// Validates that joint array has correct number of elements.
        /// </summary>
        public static bool IsValidJointArray(double[] joints)
        {
            return joints.Length == JointCount;
        }
        
        /// <summary>
        /// Validates that position array has correct number of elements.
        /// Position format: [X, Y, Z, Rx, Ry, Rz]
        /// </summary>
        public static bool IsValidPositionArray(double[] position)
        {
            return position.Length == JointCount; // 6 elements for position too
        }
        
        /// <summary>
        /// Formats coordinate values with appropriate precision.
        /// Robot controllers typically use 1 decimal place for positions.
        /// </summary>
        public static string FormatCoordinate(double value)
        {
            return value.ToString("F1");
        }
        
        /// <summary>
        /// Validates acceleration percentage (10-100% as per documentation).
        /// </summary>
        public static bool IsValidAcceleration(int acceleration)
        {
            return acceleration >= 10 && acceleration <= 100;
        }
        
        /// <summary>
        /// Formats position array according to DOCX spec: P[x,y,z,rx,ry,rz]
        /// </summary>
        public static string FormatPositionArray(double[] position)
        {
            var coords = string.Join(",", position.Select(FormatCoordinate));
            return $"P[{coords}]";
        }
        
        /// <summary>
        /// Formats joint array according to DOCX spec: J[j1,j2,j3,j4,j5,j6]
        /// </summary>
        public static string FormatJointArray(double[] joints)
        {
            var coords = string.Join(",", joints.Select(FormatCoordinate));
            return $"J[{coords}]";
        }
    }
}