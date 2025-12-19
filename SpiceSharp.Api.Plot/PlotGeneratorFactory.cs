namespace SpiceSharp.Api.Plot;

/// <summary>
/// Factory for creating plot generator instances.
/// </summary>
public static class PlotGeneratorFactory
{
    /// <summary>
    /// Create a plot generator instance.
    /// </summary>
    /// <returns>A new instance of IPlotGenerator.</returns>
    public static IPlotGenerator Create()
    {
        return new PlotGenerator();
    }
}

