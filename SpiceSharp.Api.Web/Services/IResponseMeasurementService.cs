using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Service for extracting specific measurements from simulation results
/// </summary>
public interface IResponseMeasurementService
{
    /// <summary>
    /// Performs a measurement on cached analysis results
    /// </summary>
    /// <param name="circuitId">Circuit ID to measure</param>
    /// <param name="measurement">Type of measurement (e.g., "bandwidth_3db", "gain_at_freq")</param>
    /// <param name="signal">Signal to measure (e.g., "v(out)")</param>
    /// <param name="reference">Reference signal for ratio measurements (optional)</param>
    /// <param name="frequency">Frequency for point measurements (optional)</param>
    /// <param name="threshold">Threshold value for crossing measurements (optional)</param>
    /// <param name="analysisId">Which cached analysis to use (optional, uses most recent if omitted)</param>
    /// <returns>Measurement result with value and unit</returns>
    MeasurementResult Measure(
        string circuitId,
        string measurement,
        string signal,
        string? reference,
        double? frequency,
        double? threshold,
        string? analysisId);
}

/// <summary>
/// Result of a measurement
/// </summary>
public class MeasurementResult
{
    /// <summary>
    /// Measured value
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "Hz", "dB", "V", "s")
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Description of the measurement
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
