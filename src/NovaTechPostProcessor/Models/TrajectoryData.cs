using System.Text.Json.Serialization;

namespace NovaTechPostProcessor.Models
{
    /// <summary>
    /// Represents the complete trajectory data input from JSON files.
    /// This is the root object that contains all robot movement information.
    /// </summary>
    public class TrajectoryData
    {
        [JsonPropertyName("robot")]
        public string Robot { get; set; } = string.Empty;

        [JsonPropertyName("firmware_version")]
        public string FirmwareVersion { get; set; } = string.Empty;

        [JsonPropertyName("base_frame")]
        public double[] BaseFrame { get; set; } = Array.Empty<double>();

        [JsonPropertyName("tool_frame")]
        public double[] ToolFrame { get; set; } = Array.Empty<double>();

        [JsonPropertyName("trajectory")]
        public List<TrajectoryPoint> Trajectory { get; set; } = new();
    }

    /// <summary>
    /// Represents a single movement point in the robot trajectory.
    /// Can be either linear motion (MOVL) or joint motion (MOVJ).
    /// </summary>
    public class TrajectoryPoint
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Cartesian position for linear movements [X, Y, Z, Rx, Ry, Rz]
        /// Units: mm for position, degrees for rotation
        /// </summary>
        [JsonPropertyName("position")]
        public double[]? Position { get; set; }

        /// <summary>
        /// Joint angles for joint movements [J1, J2, J3, J4, J5, J6]
        /// Units: degrees. Joint 6 supports ±720° range.
        /// </summary>
        [JsonPropertyName("joints")]
        public double[]? Joints { get; set; }

        /// <summary>
        /// Movement speed. Must be positive value.
        /// Units depend on movement type (mm/s for linear, deg/s for joint)
        /// </summary>
        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        /// <summary>
        /// Optional acceleration parameter. Defaults to 50 if not specified.
        /// </summary>
        [JsonPropertyName("acceleration")]
        public int? Acceleration { get; set; }
    }
}