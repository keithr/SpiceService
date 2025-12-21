namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Result of a speaker search query
/// </summary>
public class SpeakerSearchResult
{
    /// <summary>
    /// Subcircuit name (model identifier)
    /// </summary>
    public string SubcircuitName { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer name
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Part number
    /// </summary>
    public string? PartNumber { get; set; }

    /// <summary>
    /// Driver type (e.g., "woofers", "tweeters", "midrange")
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Diameter in inches
    /// </summary>
    public double? Diameter { get; set; }

    /// <summary>
    /// Impedance in ohms
    /// </summary>
    public int? Impedance { get; set; }

    /// <summary>
    /// Power handling (RMS) in watts
    /// </summary>
    public int? PowerRms { get; set; }

    /// <summary>
    /// Sensitivity in dB
    /// </summary>
    public double? Sensitivity { get; set; }

    /// <summary>
    /// Price in USD
    /// </summary>
    public double? Price { get; set; }

    /// <summary>
    /// Thiele-Small parameters
    /// </summary>
    public Dictionary<string, double> TsParameters { get; set; } = new();
}

