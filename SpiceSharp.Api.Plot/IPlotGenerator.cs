namespace SpiceSharp.Api.Plot;

/// <summary>
/// Main entry point for generating plots from SPICE analysis data.
/// </summary>
public interface IPlotGenerator
{
    /// <summary>
    /// Generate a plot from analysis results.
    /// </summary>
    /// <param name="request">Plot configuration and data.</param>
    /// <returns>Rendered plot result containing image data or error information.</returns>
    PlotResult GeneratePlot(PlotRequest request);

    /// <summary>
    /// Gets the list of supported image formats.
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }
}

