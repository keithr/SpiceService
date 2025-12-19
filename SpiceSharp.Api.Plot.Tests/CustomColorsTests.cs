namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for custom color functionality.
/// </summary>
public class CustomColorsTests
{
    [Fact]
    public void GeneratePlot_WithCustomColorPalette_ShouldApplyColors()
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
                new DataSeries { Name = "v(in)", Values = new double[] { 0.5, 1.5, 2.5 } }
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

    [Fact]
    public void GeneratePlot_WithoutColorPalette_ShouldUseDefaultColors()
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
                new DataSeries { Name = "v(in)", Values = new double[] { 0.5, 1.5, 2.5 } }
            },
            Options = new PlotOptions
            {
                ColorPalette = null
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithColorPaletteShorterThanSeries_ShouldUseAvailableColors()
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
                ColorPalette = new[] { "#FF0000" } // Only one color for three series
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithInvalidColorStrings_ShouldHandleGracefully()
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
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 2.0, 3.0 },
                    Color = "INVALID_COLOR"
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should handle invalid colors gracefully (use default or skip)
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithHexColor_ShouldParseCorrectly()
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
                    Name = "i(R1)",
                    Values = new double[] { 0.1, 0.2 },
                    Color = "#FF0000" // Red
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
    public void GeneratePlot_WithShortHexColor_ShouldParseCorrectly()
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
                    Name = "i(R1)",
                    Values = new double[] { 0.1, 0.2 },
                    Color = "#F00" // Short hex for red
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
    public void GeneratePlot_WithSeriesSpecificColor_ShouldOverridePalette()
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
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 2.0 },
                    Color = "#00FF00" // Green (should override palette)
                },
                new DataSeries
                {
                    Name = "v(in)",
                    Values = new double[] { 0.5, 1.5 }
                    // No color - should use palette
                }
            },
            Options = new PlotOptions
            {
                ColorPalette = new[] { "#FF0000", "#0000FF" }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

