namespace SpiceSharp.Api.Plot;

/// <summary>
/// Options for customizing plot appearance and behavior.
/// </summary>
public class PlotOptions
{
    /// <summary>
    /// Gets or sets the plot title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the Y-axis label.
    /// </summary>
    public string? YLabel { get; set; }

    /// <summary>
    /// Gets or sets the X-axis scale type.
    /// </summary>
    public ScaleType XScale { get; set; } = ScaleType.Linear;

    /// <summary>
    /// Gets or sets the Y-axis scale type.
    /// </summary>
    public ScaleType YScale { get; set; } = ScaleType.Linear;

    /// <summary>
    /// Gets or sets a value indicating whether to show grid lines.
    /// </summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show the legend.
    /// </summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// Gets or sets the plot width in pixels.
    /// </summary>
    public int Width { get; set; } = 800;

    /// <summary>
    /// Gets or sets the plot height in pixels.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// Gets or sets the color palette for series (hex colors or named colors).
    /// </summary>
    public string[]? ColorPalette { get; set; }

    /// <summary>
    /// Gets or sets the set of signal names that have been inverted (multiplied by -1).
    /// This is used to update axis labels to reflect the inversion.
    /// </summary>
    public HashSet<string>? InvertedSignals { get; set; }
}

