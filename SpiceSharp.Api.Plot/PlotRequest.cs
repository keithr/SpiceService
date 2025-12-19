namespace SpiceSharp.Api.Plot;

/// <summary>
/// Request object for generating a plot from analysis results.
/// </summary>
public class PlotRequest
{
    /// <summary>
    /// Gets or sets the type of analysis that produced the data.
    /// </summary>
    public AnalysisType AnalysisType { get; set; }

    /// <summary>
    /// Gets or sets the type of plot to generate.
    /// </summary>
    public PlotType PlotType { get; set; }

    /// <summary>
    /// Gets or sets the image format for the output.
    /// </summary>
    public ImageFormat ImageFormat { get; set; }

    /// <summary>
    /// Gets or sets the X-axis data points (e.g., time, frequency, sweep values).
    /// </summary>
    public double[]? XData { get; set; }

    /// <summary>
    /// Gets or sets the X-axis label (e.g., "Time (s)", "Frequency (Hz)").
    /// </summary>
    public string? XLabel { get; set; }

    /// <summary>
    /// Gets or sets the collection of Y-axis data series to plot.
    /// </summary>
    public IList<DataSeries> Series { get; set; } = new List<DataSeries>();

    /// <summary>
    /// Gets or sets the plot customization options.
    /// </summary>
    public PlotOptions Options { get; set; } = new PlotOptions();
}

