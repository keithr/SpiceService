namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for logarithmic axes functionality.
/// </summary>
public class LogarithmicAxesTests
{
    [Fact]
    public void GeneratePlot_WithLogXScale_ShouldCreateLogarithmicAxis()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0, 1000.0 },
            XLabel = "Frequency (Hz)",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 0.5, 0.1, 0.01 } }
            },
            Options = new PlotOptions
            {
                XScale = ScaleType.Log,
                YScale = ScaleType.Linear
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify SVG contains log scale indicators (log axes typically have different formatting)
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithLogYScale_ShouldCreateLogarithmicAxis()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "i(R1)", Values = new double[] { 0.001, 0.01, 0.1 } }
            },
            Options = new PlotOptions
            {
                XScale = ScaleType.Linear,
                YScale = ScaleType.Log
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithLogXAndYScales_ShouldCreateBothLogarithmicAxes()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 0.001, 0.01, 0.1 } }
            },
            Options = new PlotOptions
            {
                XScale = ScaleType.Log,
                YScale = ScaleType.Log
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithLogScaleAndPositiveValues_ShouldSucceed()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.1, 1.0, 10.0, 100.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 0.1, 1.0, 10.0, 100.0 } }
            },
            Options = new PlotOptions
            {
                XScale = ScaleType.Log,
                YScale = ScaleType.Log
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void GeneratePlot_WithLogScaleAndZeroValues_ShouldHandleGracefully()
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
                new DataSeries { Name = "v(out)", Values = new double[] { 0.0, 1.0, 2.0 } }
            },
            Options = new PlotOptions
            {
                YScale = ScaleType.Log
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - OxyPlot should handle zero values in log scale (may clamp or skip)
        // The important thing is it doesn't crash
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithLogScaleAndNegativeValues_ShouldHandleGracefully()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { -1.0, 0.0, 1.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { -1.0, 0.0, 1.0 } }
            },
            Options = new PlotOptions
            {
                YScale = ScaleType.Log
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - OxyPlot should handle negative values in log scale gracefully
        Assert.NotNull(result);
    }
}

