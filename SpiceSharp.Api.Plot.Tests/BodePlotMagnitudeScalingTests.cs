namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for Bode plot magnitude axis scaling.
/// </summary>
public class BodePlotMagnitudeScalingTests
{
    [Fact]
    public void GeneratePlot_WithNegativeMagnitudeValues_ShouldDisplayCorrectly()
    {
        // Arrange - High-pass filter with negative dB values
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 10.0, 100.0, 1000.0, 10000.0, 100000.0 },
            XLabel = "Frequency (Hz)",
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 0.01, 0.1, 0.707, 0.99, 1.0 },  // Real part
                    ImagValues = new double[] { 0.0, 0.0, 0.707, 0.14, 0.01 }  // Imaginary part
                }
            },
            Options = new PlotOptions
            {
                Title = "High-Pass Filter Bode Plot"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify SVG is generated (magnitude should be visible even with negative dB)
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithAllNegativeMagnitudeValues_ShouldAutoScale()
    {
        // Arrange - All negative dB values (typical for filters)
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 10.0, 100.0, 1000.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 0.01, 0.1, 0.707 },  // All < 1.0 (negative dB)
                    ImagValues = new double[] { 0.0, 0.0, 0.707 }
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
    public void GeneratePlot_WithMixedMagnitudeValues_ShouldAutoScale()
    {
        // Arrange - Mix of positive and negative dB
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 10.0, 1.0, 0.1 },  // Positive, zero, negative dB
                    ImagValues = new double[] { 0.0, 0.0, 0.0 }
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

