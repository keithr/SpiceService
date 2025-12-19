namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Result of a temperature sweep analysis
/// </summary>
public class TemperatureSweepResult
{
    /// <summary>
    /// The temperature values that were swept (in Celsius)
    /// </summary>
    public List<double> TemperatureValues { get; set; } = new();

    /// <summary>
    /// Results for each exported signal, keyed by export name
    /// </summary>
    public Dictionary<string, List<double>> Results { get; set; } = new();

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

