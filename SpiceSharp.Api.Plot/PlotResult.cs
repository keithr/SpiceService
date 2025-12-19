namespace SpiceSharp.Api.Plot;

/// <summary>
/// Result of plot generation operation.
/// </summary>
public class PlotResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the plot generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the generated image data (SVG or PNG bytes).
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// Gets or sets the image format of the generated plot.
    /// </summary>
    public ImageFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the error message if plot generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful plot result.
    /// </summary>
    /// <param name="imageData">The generated image data.</param>
    /// <param name="format">The image format.</param>
    /// <returns>A successful PlotResult.</returns>
    public static PlotResult SuccessResult(byte[] imageData, ImageFormat format)
    {
        return new PlotResult
        {
            Success = true,
            ImageData = imageData,
            Format = format,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a failed plot result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A failed PlotResult.</returns>
    public static PlotResult FailureResult(string errorMessage)
    {
        return new PlotResult
        {
            Success = false,
            ImageData = null,
            ErrorMessage = errorMessage
        };
    }
}

