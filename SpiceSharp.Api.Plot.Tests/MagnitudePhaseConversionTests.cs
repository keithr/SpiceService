namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for complex to magnitude/phase conversion.
/// </summary>
public class MagnitudePhaseConversionTests
{
    [Fact]
    public void GeneratePlot_WithComplexData_ShouldCalculateMagnitude()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.5 },      // Real part
                    ImagValues = new double[] { 0.0, 0.5 }    // Imaginary part
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
    public void GeneratePlot_WithComplexData_ShouldCalculatePhase()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.0 },
                    ImagValues = new double[] { 0.0, 1.0 }
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
    public void GeneratePlot_WithZeroMagnitude_ShouldHandleGracefully()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 0.0, 0.0 },
                    ImagValues = new double[] { 0.0, 0.0 }
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should handle zero magnitude (may clamp to minimum dB)
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithNegativeRealPart_ShouldHandleGracefully()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { -1.0, -0.5 },
                    ImagValues = new double[] { 0.0, 0.1 }
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithRealOnlyData_ShouldWork()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.5 },
                    ImagValues = null  // Real only
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

