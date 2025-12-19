namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for full line plot generation flow.
/// </summary>
public class FullLinePlotFlowTests
{
    [Fact]
    public void GeneratePlot_WithDcSweepData_ShouldGenerateValidSvg()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 0.5, 1.0, 1.5, 2.0 },
            XLabel = "Voltage (V)",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "i(R1)", Values = new double[] { 0.0, 0.1, 0.2, 0.3, 0.4 } }
            },
            Options = new PlotOptions
            {
                Title = "DC Sweep - Current vs Voltage",
                YLabel = "Current (A)",
                ShowGrid = true,
                ShowLegend = true
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        Assert.True(result.ImageData.Length > 0);
        Assert.Equal(ImageFormat.Svg, result.Format);
        Assert.Null(result.ErrorMessage);

        // Verify SVG content
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithTransientData_ShouldGenerateValidSvg()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 0.001, 0.002, 0.003, 0.004 },
            XLabel = "Time (s)",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 0.0, 1.5, 2.8, 3.5, 3.8 } }
            },
            Options = new PlotOptions
            {
                Title = "Transient Response",
                YLabel = "Voltage (V)",
                Width = 1000,
                Height = 600
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        Assert.Equal(ImageFormat.Svg, result.Format);
    }

    [Fact]
    public void GeneratePlot_WithMultipleSignals_ShouldGenerateValidSvg()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } },
                new DataSeries { Name = "v(in)", Values = new double[] { 0.5, 1.5, 2.5 } },
                new DataSeries { Name = "i(R1)", Values = new double[] { 0.1, 0.2, 0.3 } }
            },
            Options = new PlotOptions
            {
                ShowLegend = true
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        Assert.Equal(ImageFormat.Svg, result.Format);
    }

    [Fact]
    public void GeneratePlot_WithCustomColors_ShouldApplyColors()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 2.0 },
                    Color = "#FF0000"
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithColorPalette_ShouldApplyPalette()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } },
                new DataSeries { Name = "v(in)", Values = new double[] { 0.5, 1.5 } }
            },
            Options = new PlotOptions
            {
                ColorPalette = new[] { "#2196F3", "#F44336" }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

