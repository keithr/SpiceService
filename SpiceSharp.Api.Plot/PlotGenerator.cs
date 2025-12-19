using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlotSvg = OxyPlot.SvgExporter;
using OxyPlotPng = OxyPlot.SkiaSharp.PngExporter;
using System.Xml;

namespace SpiceSharp.Api.Plot;

/// <summary>
/// Implementation of IPlotGenerator for generating plots from SPICE analysis data.
/// </summary>
internal class PlotGenerator : IPlotGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats { get; } = new[] { "svg", "png" };

    /// <inheritdoc/>
    public PlotResult GeneratePlot(PlotRequest request)
    {
        // Validate request
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Validate XData
        if (request.XData == null || request.XData.Length == 0)
        {
            return PlotResult.FailureResult("XData is required and cannot be empty");
        }

        // Validate Series
        if (request.Series == null || request.Series.Count == 0)
        {
            return PlotResult.FailureResult("At least one data series is required");
        }

        // Validate data lengths match
        foreach (var series in request.Series)
        {
            if (series.Values == null || series.Values.Length == 0)
            {
                return PlotResult.FailureResult($"Series '{series.Name}' has no data");
            }

            if (series.Values.Length != request.XData.Length)
            {
                return PlotResult.FailureResult(
                    $"Series '{series.Name}' has {series.Values.Length} data points, but XData has {request.XData.Length} points");
            }
        }

        try
        {
            // Determine plot type
            PlotType plotType = request.PlotType;
            if (plotType == PlotType.Auto)
            {
                plotType = DeterminePlotType(request);
            }

            // Create plot model based on type
            PlotModel plotModel = plotType switch
            {
                PlotType.Bode => CreateBodePlotModel(request),
                PlotType.Line => CreatePlotModel(request),
                PlotType.Bar => CreateBarPlotModel(request),
                PlotType.Scatter => CreateScatterPlotModel(request),
                _ => CreatePlotModel(request)
            };

            // Export based on format
            byte[] imageData = request.ImageFormat switch
            {
                ImageFormat.Svg => ExportToSvg(plotModel, request.Options),
                ImageFormat.Png => ExportToPng(plotModel, request.Options),
                _ => throw new NotSupportedException($"Image format {request.ImageFormat} is not supported")
            };

            return PlotResult.SuccessResult(imageData, request.ImageFormat);
        }
        catch (Exception ex)
        {
            return PlotResult.FailureResult($"Plot generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a PlotModel from the request.
    /// </summary>
    private PlotModel CreatePlotModel(PlotRequest request)
    {
        var model = new PlotModel();

        // Set title
        model.Title = request.Options.Title ?? GenerateDefaultTitle(request);

        // Create X-axis
        Axis xAxis = request.Options.XScale == ScaleType.Log
            ? new LogarithmicAxis { Position = AxisPosition.Bottom }
            : new LinearAxis { Position = AxisPosition.Bottom };

        // Use custom label if provided and not empty, otherwise use default
        xAxis.Title = !string.IsNullOrWhiteSpace(request.XLabel) 
            ? request.XLabel 
            : GenerateDefaultXLabel(request);
        model.Axes.Add(xAxis);

        // Create Y-axis
        Axis yAxis = request.Options.YScale == ScaleType.Log
            ? new LogarithmicAxis { Position = AxisPosition.Left }
            : new LinearAxis { Position = AxisPosition.Left };

        // Use custom label if provided and not empty, otherwise use default
        yAxis.Title = !string.IsNullOrWhiteSpace(request.Options.YLabel) 
            ? request.Options.YLabel 
            : GenerateDefaultYLabel(request, request.Options.InvertedSignals);
        model.Axes.Add(yAxis);

        // Configure grid
        if (request.Options.ShowGrid)
        {
            xAxis.MajorGridlineStyle = LineStyle.Solid;
            xAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
            yAxis.MajorGridlineStyle = LineStyle.Solid;
            yAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
        }

        // Configure legend
        model.IsLegendVisible = request.Options.ShowLegend && request.Series.Count > 1;

        // Add line series
        AddLineSeries(model, request);

        return model;
    }

    /// <summary>
    /// Adds line series to the plot model.
    /// </summary>
    private void AddLineSeries(PlotModel model, PlotRequest request)
    {
        var colorPalette = request.Options.ColorPalette ?? GetDefaultColorPalette();
        int colorIndex = 0;

        foreach (var dataSeries in request.Series)
        {
            var lineSeries = new LineSeries
            {
                Title = dataSeries.Name,
                StrokeThickness = 2
            };

            // Add data points
            if (request.XData != null && dataSeries.Values != null)
            {
                for (int i = 0; i < request.XData.Length; i++)
                {
                    lineSeries.Points.Add(new DataPoint(request.XData[i], dataSeries.Values[i]));
                }
            }

            // Set color
            OxyColor? color = null;
            if (!string.IsNullOrEmpty(dataSeries.Color))
            {
                color = ParseColor(dataSeries.Color);
            }
            else if (colorPalette != null && colorIndex < colorPalette.Length)
            {
                color = ParseColor(colorPalette[colorIndex]);
            }

            if (color.HasValue)
            {
                lineSeries.Color = color.Value;
            }

            model.Series.Add(lineSeries);
            colorIndex++;
        }
    }

    /// <summary>
    /// Parses a color string (hex or named color) to OxyColor.
    /// </summary>
    private OxyColor? ParseColor(string colorString)
    {
        if (string.IsNullOrWhiteSpace(colorString))
        {
            return null;
        }

        // Try hex color (#RRGGBB or #RGB)
        if (colorString.StartsWith("#", StringComparison.Ordinal))
        {
            try
            {
                var hex = colorString.Substring(1);
                if (hex.Length == 6)
                {
                    var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    return OxyColor.FromRgb((byte)r, (byte)g, (byte)b);
                }
                else if (hex.Length == 3)
                {
                    var r = Convert.ToInt32(hex.Substring(0, 1) + hex.Substring(0, 1), 16);
                    var g = Convert.ToInt32(hex.Substring(1, 1) + hex.Substring(1, 1), 16);
                    var b = Convert.ToInt32(hex.Substring(2, 1) + hex.Substring(2, 1), 16);
                    return OxyColor.FromRgb((byte)r, (byte)g, (byte)b);
                }
            }
            catch
            {
                // Invalid hex format, try named color
            }
        }

        // Try named color
        try
        {
            return OxyColor.Parse(colorString);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the default color palette.
    /// </summary>
    private string[] GetDefaultColorPalette()
    {
        return new[]
        {
            "#1F77B4", // Blue
            "#FF7F0E", // Orange
            "#2CA02C", // Green
            "#D62728", // Red
            "#9467BD", // Purple
            "#8C564B", // Brown
            "#E377C2", // Pink
            "#7F7F7F", // Gray
            "#BCBD22", // Olive
            "#17BECF"  // Cyan
        };
    }

    /// <summary>
    /// Exports the plot model to SVG format.
    /// </summary>
    private byte[] ExportToSvg(PlotModel model, PlotOptions options)
    {
        // Enforce minimum dimensions
        const int minWidth = 100;
        const int minHeight = 100;
        const int maxWidth = 10000;
        const int maxHeight = 10000;

        var width = Math.Clamp(options.Width, minWidth, maxWidth);
        var height = Math.Clamp(options.Height, minHeight, maxHeight);

        using var stream = new MemoryStream();
        var exporter = new OxyPlotSvg
        {
            Width = width,
            Height = height
        };
        
        exporter.Export(model, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Exports the plot model to PNG format.
    /// </summary>
    private byte[] ExportToPng(PlotModel model, PlotOptions options)
    {
        // Enforce minimum dimensions
        const int minWidth = 100;
        const int minHeight = 100;
        const int maxWidth = 10000;
        const int maxHeight = 10000;

        var width = Math.Clamp(options.Width, minWidth, maxWidth);
        var height = Math.Clamp(options.Height, minHeight, maxHeight);

        using var stream = new MemoryStream();
        var exporter = new OxyPlotPng
        {
            Width = width,
            Height = height
        };
        
        exporter.Export(model, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Generates a default title based on the request.
    /// </summary>
    private string GenerateDefaultTitle(PlotRequest request)
    {
        return request.AnalysisType switch
        {
            AnalysisType.DcSweep => "DC Sweep Analysis",
            AnalysisType.Transient => "Transient Analysis",
            AnalysisType.Ac => "AC Analysis",
            AnalysisType.OperatingPoint => "Operating Point Analysis",
            _ => "SPICE Analysis"
        };
    }

    /// <summary>
    /// Generates a default X-axis label based on the request.
    /// </summary>
    private string GenerateDefaultXLabel(PlotRequest request)
    {
        return request.AnalysisType switch
        {
            AnalysisType.DcSweep => "Voltage (V)",
            AnalysisType.Transient => "Time (s)",
            AnalysisType.Ac => "Frequency (Hz)",
            AnalysisType.OperatingPoint => "Signal",
            _ => "X"
        };
    }

    /// <summary>
    /// Generates a default Y-axis label based on the request and inverted signals.
    /// </summary>
    private string GenerateDefaultYLabel(PlotRequest request, HashSet<string>? invertedSignals = null)
    {
        string baseLabel = request.AnalysisType switch
        {
            AnalysisType.DcSweep => "Current (A)",
            AnalysisType.Transient => "Voltage (V)",
            AnalysisType.Ac => "Magnitude (dB)",
            AnalysisType.OperatingPoint => "Value",
            _ => "Y"
        };

        // If there are inverted signals, append indication to the label
        if (invertedSignals != null && invertedSignals.Count > 0)
        {
            // Check if any of the series are inverted
            bool hasInvertedSeries = request.Series.Any(s => 
                s.Name != null && invertedSignals.Contains(s.Name, StringComparer.OrdinalIgnoreCase));

            if (hasInvertedSeries)
            {
                // For current signals, indicate they're shown in positive convention
                if (request.AnalysisType == AnalysisType.DcSweep)
                {
                    return "Current (A) [positive convention]";
                }
                else
                {
                    return $"{baseLabel} [inverted]";
                }
            }
        }

        return baseLabel;
    }

    /// <summary>
    /// Determines the plot type based on analysis type when Auto is selected.
    /// </summary>
    private PlotType DeterminePlotType(PlotRequest request)
    {
        return request.AnalysisType switch
        {
            AnalysisType.Ac => PlotType.Bode,
            AnalysisType.OperatingPoint => PlotType.Bar,
            _ => PlotType.Line
        };
    }

    /// <summary>
    /// Creates a Bode plot model (two-panel: magnitude and phase).
    /// </summary>
    private PlotModel CreateBodePlotModel(PlotRequest request)
    {
        var model = new PlotModel();
        model.Title = request.Options.Title ?? GenerateDefaultTitle(request);

        // Calculate magnitude range for auto-scaling
        var magnitudeRange = CalculateMagnitudeRange(request);

        // Magnitude axis (top panel) - LINEAR (dB is already logarithmic scale)
        var magAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Magnitude (dB)",
            Key = "mag",
            StartPosition = 0.55,  // Top half
            EndPosition = 1.0,
            Minimum = magnitudeRange.Min,
            Maximum = magnitudeRange.Max
        };

        // Phase axis (bottom panel) - linear
        var phaseAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Phase (°)",
            Key = "phase",
            StartPosition = 0.0,   // Bottom half
            EndPosition = 0.45
        };

        // Shared frequency axis (logarithmic)
        var freqAxis = new LogarithmicAxis
        {
            Position = AxisPosition.Bottom,
            Title = !string.IsNullOrWhiteSpace(request.XLabel) 
                ? request.XLabel 
                : "Frequency (Hz)"
        };

        model.Axes.Add(magAxis);
        model.Axes.Add(phaseAxis);
        model.Axes.Add(freqAxis);

        // Configure grid
        if (request.Options.ShowGrid)
        {
            magAxis.MajorGridlineStyle = LineStyle.Solid;
            magAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
            phaseAxis.MajorGridlineStyle = LineStyle.Solid;
            phaseAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
            freqAxis.MajorGridlineStyle = LineStyle.Solid;
            freqAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
        }

        // Configure legend
        model.IsLegendVisible = request.Options.ShowLegend && request.Series.Count > 1;

        // Add magnitude and phase series
        AddBodeSeries(model, request);

        return model;
    }

    /// <summary>
    /// Calculates the magnitude range (in dB) for auto-scaling the Bode plot magnitude axis.
    /// </summary>
    private (double Min, double Max) CalculateMagnitudeRange(PlotRequest request)
    {
        double minDb = double.MaxValue;
        double maxDb = double.MinValue;
        bool hasData = false;

        // Calculate dB values for all series
        foreach (var dataSeries in request.Series)
        {
            if (request.XData != null && dataSeries.Values != null)
            {
                for (int i = 0; i < request.XData.Length && i < dataSeries.Values.Length; i++)
                {
                    double real = dataSeries.Values[i];
                    double imag = dataSeries.ImagValues != null && i < dataSeries.ImagValues.Length
                        ? dataSeries.ImagValues[i]
                        : 0.0;

                    // Calculate magnitude in dB
                    double magnitude = Math.Sqrt(real * real + imag * imag);
                    if (magnitude > 0)
                    {
                        double magnitudeDb = 20.0 * Math.Log10(magnitude);
                        minDb = Math.Min(minDb, magnitudeDb);
                        maxDb = Math.Max(maxDb, magnitudeDb);
                        hasData = true;
                    }
                }
            }
        }

        // If no valid data, use default range
        if (!hasData)
        {
            return (-60.0, 20.0);  // Default range for filters
        }

        // Add margins (5dB on each side, minimum 20dB range)
        double margin = 5.0;
        double range = maxDb - minDb;
        if (range < 20.0)
        {
            // Ensure minimum range of 20dB for readability
            double center = (minDb + maxDb) / 2.0;
            minDb = center - 10.0;
            maxDb = center + 10.0;
        }
        else
        {
            minDb -= margin;
            maxDb += margin;
        }

        return (minDb, maxDb);
    }

    /// <summary>
    /// Adds Bode plot series (magnitude and phase) to the model.
    /// </summary>
    private void AddBodeSeries(PlotModel model, PlotRequest request)
    {
        var colorPalette = request.Options.ColorPalette ?? GetDefaultColorPalette();
        int colorIndex = 0;

        foreach (var dataSeries in request.Series)
        {
            var magnitudeSeries = new LineSeries
            {
                Title = $"|{dataSeries.Name ?? "Signal"}|",
                YAxisKey = "mag",
                StrokeThickness = 2
            };

            var phaseSeries = new LineSeries
            {
                Title = $"∠{dataSeries.Name ?? "Signal"}",
                YAxisKey = "phase",
                StrokeThickness = 2
            };

            // Add data points
            if (request.XData != null && dataSeries.Values != null)
            {
                for (int i = 0; i < request.XData.Length; i++)
                {
                    double real = dataSeries.Values[i];
                    double imag = dataSeries.ImagValues != null && i < dataSeries.ImagValues.Length
                        ? dataSeries.ImagValues[i]
                        : 0.0;

                    // Calculate magnitude in dB: 20 * log10(sqrt(real^2 + imag^2))
                    double magnitude = Math.Sqrt(real * real + imag * imag);
                    double magnitudeDb = magnitude > 0
                        ? 20.0 * Math.Log10(magnitude)
                        : -1000.0; // Very negative dB for zero magnitude

                    // Calculate phase in degrees: atan2(imag, real) * 180 / PI
                    double phase = Math.Atan2(imag, real) * 180.0 / Math.PI;

                    magnitudeSeries.Points.Add(new DataPoint(request.XData[i], magnitudeDb));
                    phaseSeries.Points.Add(new DataPoint(request.XData[i], phase));
                }
            }

            // Set colors
            OxyColor? color = null;
            if (!string.IsNullOrEmpty(dataSeries.Color))
            {
                color = ParseColor(dataSeries.Color);
            }
            else if (colorPalette != null && colorIndex < colorPalette.Length)
            {
                color = ParseColor(colorPalette[colorIndex]);
            }

            if (color.HasValue)
            {
                magnitudeSeries.Color = color.Value;
                phaseSeries.Color = color.Value;
            }

            model.Series.Add(magnitudeSeries);
            model.Series.Add(phaseSeries);
            colorIndex++;
        }
    }

    /// <summary>
    /// Creates a bar chart plot model.
    /// </summary>
    private PlotModel CreateBarPlotModel(PlotRequest request)
    {
        var model = new PlotModel();
        model.Title = request.Options.Title ?? GenerateDefaultTitle(request);

        // Create category axis for X
        var categoryAxis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Title = !string.IsNullOrWhiteSpace(request.XLabel) 
                ? request.XLabel 
                : GenerateDefaultXLabel(request)
        };

        // Create Y-axis
        Axis yAxis = request.Options.YScale == ScaleType.Log
            ? new LogarithmicAxis { Position = AxisPosition.Left }
            : new LinearAxis { Position = AxisPosition.Left };

        // Use custom label if provided and not empty, otherwise use default
        yAxis.Title = !string.IsNullOrWhiteSpace(request.Options.YLabel) 
            ? request.Options.YLabel 
            : GenerateDefaultYLabel(request, request.Options.InvertedSignals);
        model.Axes.Add(categoryAxis);
        model.Axes.Add(yAxis);

        // Configure grid
        if (request.Options.ShowGrid)
        {
            yAxis.MajorGridlineStyle = LineStyle.Solid;
            yAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
        }

        // Configure legend
        model.IsLegendVisible = request.Options.ShowLegend && request.Series.Count > 1;

        // Add bar series
        AddBarSeries(model, request, categoryAxis);

        return model;
    }

    /// <summary>
    /// Adds bar series to the plot model.
    /// </summary>
    private void AddBarSeries(PlotModel model, PlotRequest request, CategoryAxis categoryAxis)
    {
        // Generate category labels from XData
        var categories = new List<string>();
        if (request.XData != null)
        {
            for (int i = 0; i < request.XData.Length; i++)
            {
                categories.Add($"Category {i + 1}");
            }
        }
        categoryAxis.ItemsSource = categories;

        var colorPalette = request.Options.ColorPalette ?? GetDefaultColorPalette();
        int colorIndex = 0;

        foreach (var dataSeries in request.Series)
        {
            var barSeries = new BarSeries
            {
                Title = dataSeries.Name ?? string.Empty,
                LabelPlacement = LabelPlacement.Inside
            };

            // Add data points
            if (request.XData != null && dataSeries.Values != null)
            {
                for (int i = 0; i < request.XData.Length && i < dataSeries.Values.Length; i++)
                {
                    barSeries.Items.Add(new BarItem(dataSeries.Values[i]));
                }
            }

            // Set color
            OxyColor? color = null;
            if (!string.IsNullOrEmpty(dataSeries.Color))
            {
                color = ParseColor(dataSeries.Color);
            }
            else if (colorPalette != null && colorIndex < colorPalette.Length)
            {
                color = ParseColor(colorPalette[colorIndex]);
            }

            if (color.HasValue)
            {
                barSeries.FillColor = color.Value;
            }

            model.Series.Add(barSeries);
            colorIndex++;
        }
    }

    /// <summary>
    /// Creates a scatter plot model.
    /// </summary>
    private PlotModel CreateScatterPlotModel(PlotRequest request)
    {
        var model = new PlotModel();
        model.Title = request.Options.Title ?? GenerateDefaultTitle(request);

        // Create X-axis
        Axis xAxis = request.Options.XScale == ScaleType.Log
            ? new LogarithmicAxis { Position = AxisPosition.Bottom }
            : new LinearAxis { Position = AxisPosition.Bottom };

        // Use custom label if provided and not empty, otherwise use default
        xAxis.Title = !string.IsNullOrWhiteSpace(request.XLabel) 
            ? request.XLabel 
            : GenerateDefaultXLabel(request);
        model.Axes.Add(xAxis);

        // Create Y-axis
        Axis yAxis = request.Options.YScale == ScaleType.Log
            ? new LogarithmicAxis { Position = AxisPosition.Left }
            : new LinearAxis { Position = AxisPosition.Left };

        // Use custom label if provided and not empty, otherwise use default
        yAxis.Title = !string.IsNullOrWhiteSpace(request.Options.YLabel) 
            ? request.Options.YLabel 
            : GenerateDefaultYLabel(request, request.Options.InvertedSignals);
        model.Axes.Add(yAxis);

        // Configure grid
        if (request.Options.ShowGrid)
        {
            xAxis.MajorGridlineStyle = LineStyle.Solid;
            xAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
            yAxis.MajorGridlineStyle = LineStyle.Solid;
            yAxis.MajorGridlineColor = OxyColor.FromRgb(220, 220, 220);
        }

        // Configure legend
        model.IsLegendVisible = request.Options.ShowLegend && request.Series.Count > 1;

        // Add scatter series
        AddScatterSeries(model, request);

        return model;
    }

    /// <summary>
    /// Adds scatter series to the plot model.
    /// </summary>
    private void AddScatterSeries(PlotModel model, PlotRequest request)
    {
        var colorPalette = request.Options.ColorPalette ?? GetDefaultColorPalette();
        int colorIndex = 0;

        foreach (var dataSeries in request.Series)
        {
            var scatterSeries = new ScatterSeries
            {
                Title = dataSeries.Name ?? string.Empty,
                MarkerType = MarkerType.Circle,
                MarkerSize = 5
            };

            // Add data points
            if (request.XData != null && dataSeries.Values != null)
            {
                for (int i = 0; i < request.XData.Length && i < dataSeries.Values.Length; i++)
                {
                    scatterSeries.Points.Add(new ScatterPoint(request.XData[i], dataSeries.Values[i]));
                }
            }

            // Set color
            OxyColor? color = null;
            if (!string.IsNullOrEmpty(dataSeries.Color))
            {
                color = ParseColor(dataSeries.Color);
            }
            else if (colorPalette != null && colorIndex < colorPalette.Length)
            {
                color = ParseColor(colorPalette[colorIndex]);
            }

            if (color.HasValue)
            {
                scatterSeries.MarkerFill = color.Value;
                scatterSeries.MarkerStroke = color.Value;
            }

            model.Series.Add(scatterSeries);
            colorIndex++;
        }
    }
}

