namespace SpiceSharp.Api.Plot;

/// <summary>
/// Represents a data series to be plotted.
/// </summary>
public class DataSeries
{
    /// <summary>
    /// Gets or sets the name of the data series (e.g., "v(out)", "i(R1)").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Y-axis values (real part for complex data).
    /// </summary>
    public double[] Values { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the imaginary part values (for AC analysis with complex data).
    /// </summary>
    public double[]? ImagValues { get; set; }

    /// <summary>
    /// Gets or sets an optional color override for this series (hex format or named color).
    /// </summary>
    public string? Color { get; set; }
}

