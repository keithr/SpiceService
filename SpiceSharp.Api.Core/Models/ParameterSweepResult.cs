namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Result of a parameter sweep analysis
/// </summary>
public class ParameterSweepResult
{
    /// <summary>
    /// The parameter path that was swept (e.g., "R1.value" or "LED_MODEL.IS")
    /// </summary>
    public string ParameterPath { get; set; } = string.Empty;

    /// <summary>
    /// The values of the swept parameter
    /// </summary>
    public List<double> ParameterValues { get; set; } = new();

    /// <summary>
    /// Results for each exported signal, keyed by export name
    /// For operating point and DC: one value per parameter point
    /// For AC and transient: aggregated values (magnitude at last frequency, or steady-state value)
    /// </summary>
    public Dictionary<string, List<double>> Results { get; set; } = new();

    /// <summary>
    /// Full AC analysis data: for each parameter value, stores frequency response.
    /// Key: export name, Value: List of frequency responses (one per parameter point).
    /// Each frequency response is a List of magnitude values at each frequency.
    /// </summary>
    public Dictionary<string, List<List<double>>> ACResults { get; set; } = new();

    /// <summary>
    /// AC frequency points (same for all parameter values)
    /// </summary>
    public List<double>? ACFrequencies { get; set; }

    /// <summary>
    /// Full transient analysis data: for each parameter value, stores time series.
    /// Key: export name, Value: List of time series (one per parameter point).
    /// Each time series is a List of signal values at each time point.
    /// </summary>
    public Dictionary<string, List<List<double>>> TransientResults { get; set; } = new();

    /// <summary>
    /// Transient time points (same for all parameter values)
    /// </summary>
    public List<double>? TransientTime { get; set; }

    /// <summary>
    /// The type of analysis that was run for each sweep point
    /// </summary>
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// Units for each measurement
    /// </summary>
    public Dictionary<string, string> Units { get; set; } = new();

    /// <summary>
    /// Time taken for the analysis in milliseconds
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the analysis
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

