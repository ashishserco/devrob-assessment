using System.Text.Json;
using NovaTechPostProcessor.Models;
using NovaTechPostProcessor.Processor;

namespace NovaTechPostProcessor
{
    /// <summary>
    /// Main program entry point for the NovaTech RT-500 trajectory postprocessor.
    /// Processes JSON trajectory files and generates robot code.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("NovaTech RT-500 Trajectory Postprocessor");
                Console.WriteLine("========================================");
                Console.WriteLine();

                var processor = new NovaTechPostProcessor.Processor.NovaTechPostProcessor();

                // Process sample trajectory
                await ProcessTrajectoryFile("sample_trajectory.json", "sample_trajectory_output.txt", processor);
                
                // Process edge cases trajectory
                await ProcessTrajectoryFile("edge_cases_trajectory.json", "edge_cases_trajectory_output.txt", processor);

                Console.WriteLine("Processing completed successfully!");
                Console.WriteLine("Output files generated:");
                Console.WriteLine("- sample_trajectory_output.txt");
                Console.WriteLine("- edge_cases_trajectory_output.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Processes a single trajectory file and generates output.
        /// Handles file I/O, JSON deserialization, processing, and error handling.
        /// </summary>
        /// <param name="inputFile">Path to input JSON file</param>
        /// <param name="outputFile">Path to output robot code file</param>
        /// <param name="processor">Postprocessor instance</param>
        static async Task ProcessTrajectoryFile(string inputFile, string outputFile, NovaTechPostProcessor.Processor.NovaTechPostProcessor processor)
        {
            try
            {
                Console.WriteLine($"Processing {inputFile}...");

                // Read and parse JSON file
                var jsonContent = await File.ReadAllTextAsync(inputFile);
                var trajectoryData = JsonSerializer.Deserialize<TrajectoryData>(jsonContent);

                if (trajectoryData == null)
                    throw new InvalidOperationException($"Failed to deserialize trajectory data from {inputFile}");

                // Process trajectory and generate robot code
                var robotCode = processor.ProcessTrajectory(trajectoryData);

                // Write output file
                await File.WriteAllTextAsync(outputFile, robotCode);

                Console.WriteLine($"✓ Generated {outputFile}");
                Console.WriteLine($"  Robot: {trajectoryData.Robot}");
                Console.WriteLine($"  Firmware: {trajectoryData.FirmwareVersion}");
                Console.WriteLine($"  Trajectory points: {trajectoryData.Trajectory.Count}");
                Console.WriteLine();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"✗ Error: Input file {inputFile} not found");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"✗ Error parsing JSON from {inputFile}: {ex.Message}");
                throw;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"✗ Validation error in {inputFile}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Unexpected error processing {inputFile}: {ex.Message}");
                throw;
            }
        }
    }
}