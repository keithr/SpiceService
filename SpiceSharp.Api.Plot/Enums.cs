namespace SpiceSharp.Api.Plot;

/// <summary>
/// Type of SPICE analysis that produced the data.
/// </summary>
public enum AnalysisType
{
    /// <summary>
    /// DC sweep analysis.
    /// </summary>
    DcSweep,

    /// <summary>
    /// Transient (time-domain) analysis.
    /// </summary>
    Transient,

    /// <summary>
    /// AC (frequency-domain) analysis.
    /// </summary>
    Ac,

    /// <summary>
    /// DC operating point analysis.
    /// </summary>
    OperatingPoint
}

/// <summary>
/// Type of plot to generate.
/// </summary>
public enum PlotType
{
    /// <summary>
    /// Automatically select plot type based on analysis type.
    /// </summary>
    Auto,

    /// <summary>
    /// Line plot (for DC sweep, transient analysis).
    /// </summary>
    Line,

    /// <summary>
    /// Bode plot (magnitude and phase) for AC analysis.
    /// </summary>
    Bode,

    /// <summary>
    /// Bar chart (for operating point comparisons).
    /// </summary>
    Bar,

    /// <summary>
    /// Scatter plot (for custom X-Y relationships).
    /// </summary>
    Scatter
}

/// <summary>
/// Image format for plot output.
/// </summary>
public enum ImageFormat
{
    /// <summary>
    /// Scalable Vector Graphics format.
    /// </summary>
    Svg,

    /// <summary>
    /// Portable Network Graphics format.
    /// </summary>
    Png
}

/// <summary>
/// Scale type for plot axes.
/// </summary>
public enum ScaleType
{
    /// <summary>
    /// Linear scale.
    /// </summary>
    Linear,

    /// <summary>
    /// Logarithmic scale.
    /// </summary>
    Log
}

