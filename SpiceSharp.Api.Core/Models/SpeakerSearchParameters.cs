namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Parameters for searching speakers by specifications
/// </summary>
public class SpeakerSearchParameters
{
    /// <summary>
    /// Driver type filter (e.g., "woofers", "tweeters", "midrange")
    /// </summary>
    public List<string>? DriverType { get; set; }

    /// <summary>
    /// Minimum diameter in inches
    /// </summary>
    public double? DiameterMin { get; set; }

    /// <summary>
    /// Maximum diameter in inches
    /// </summary>
    public double? DiameterMax { get; set; }

    /// <summary>
    /// Impedance in ohms
    /// </summary>
    public int? Impedance { get; set; }

    /// <summary>
    /// Minimum FS (free air resonance) in Hz
    /// </summary>
    public double? FsMin { get; set; }

    /// <summary>
    /// Maximum FS in Hz
    /// </summary>
    public double? FsMax { get; set; }

    /// <summary>
    /// Minimum QTS (total Q factor)
    /// </summary>
    public double? QtsMin { get; set; }

    /// <summary>
    /// Maximum QTS
    /// </summary>
    public double? QtsMax { get; set; }

    /// <summary>
    /// Minimum QES (electrical Q factor)
    /// </summary>
    public double? QesMin { get; set; }

    /// <summary>
    /// Maximum QES
    /// </summary>
    public double? QesMax { get; set; }

    /// <summary>
    /// Minimum QMS (mechanical Q factor)
    /// </summary>
    public double? QmsMin { get; set; }

    /// <summary>
    /// Maximum QMS
    /// </summary>
    public double? QmsMax { get; set; }

    /// <summary>
    /// Minimum VAS (equivalent air compliance) in liters
    /// </summary>
    public double? VasMin { get; set; }

    /// <summary>
    /// Maximum VAS in liters
    /// </summary>
    public double? VasMax { get; set; }

    /// <summary>
    /// Minimum sensitivity in dB
    /// </summary>
    public double? SensitivityMin { get; set; }

    /// <summary>
    /// Maximum sensitivity in dB
    /// </summary>
    public double? SensitivityMax { get; set; }

    /// <summary>
    /// Minimum power handling (RMS) in watts
    /// </summary>
    public int? PowerMin { get; set; }

    /// <summary>
    /// Maximum power handling (RMS) in watts
    /// </summary>
    public int? PowerMax { get; set; }

    /// <summary>
    /// Minimum XMAX (maximum linear excursion) in mm
    /// </summary>
    public double? XmaxMin { get; set; }

    /// <summary>
    /// Maximum XMAX in mm
    /// </summary>
    public double? XmaxMax { get; set; }

    /// <summary>
    /// Manufacturer name filter
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Name/Model search (searches subcircuit_name, part_number, and product_name)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Maximum price in USD
    /// </summary>
    public double? PriceMax { get; set; }

    /// <summary>
    /// Sort field: "sensitivity", "price", "fs", "qts", "vas"
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction: "asc" or "desc"
    /// </summary>
    public string? SortDirection { get; set; } = "asc";

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int Limit { get; set; } = 50;
}

