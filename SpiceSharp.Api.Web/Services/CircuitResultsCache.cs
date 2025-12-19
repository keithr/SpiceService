using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Cache for storing circuit analysis results for plotting.
/// </summary>
public class CircuitResultsCache
{
    private readonly Dictionary<string, CachedAnalysisResult> _cache = new();
    private readonly object _lock = new();

    /// <summary>
    /// Store analysis results for a circuit.
    /// </summary>
    public void Store(string circuitId, CachedAnalysisResult results)
    {
        lock (_lock)
        {
            _cache[circuitId] = results;
        }
    }

    /// <summary>
    /// Get cached analysis results for a circuit.
    /// </summary>
    public CachedAnalysisResult? Get(string circuitId)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(circuitId, out var result) ? result : null;
        }
    }

    /// <summary>
    /// Clear cached results for a circuit.
    /// </summary>
    public void Clear(string circuitId)
    {
        lock (_lock)
        {
            _cache.Remove(circuitId);
        }
    }

    /// <summary>
    /// Clear all cached results.
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }
}

/// <summary>
/// Cached analysis result that can be used for plotting.
/// </summary>
public class CachedAnalysisResult
{
    /// <summary>
    /// Type of analysis: "dc_sweep", "transient", "ac", "operating_point"
    /// </summary>
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// X-axis data (time, frequency, sweep values, etc.)
    /// </summary>
    public double[]? XData { get; set; }

    /// <summary>
    /// X-axis label
    /// </summary>
    public string? XLabel { get; set; }

    /// <summary>
    /// Signal data: key is signal name (e.g., "v(out)"), value is array of real values
    /// </summary>
    public Dictionary<string, double[]> Signals { get; set; } = new();

    /// <summary>
    /// Imaginary parts for complex signals (AC analysis): key is signal name, value is array of imaginary values
    /// </summary>
    public Dictionary<string, double[]> ImaginarySignals { get; set; } = new();

    /// <summary>
    /// Operating point data (for bar charts): key is node/component name, value is the value
    /// </summary>
    public Dictionary<string, double> OperatingPointData { get; set; } = new();
}

