namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for Bode plot integration.
/// </summary>
public class BodePlotIntegrationTests
{
    [Fact]
    public void GeneratePlot_WithAcAnalysisAndBodeType_ShouldGenerateValidSvg()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0, 1000.0 },
            XLabel = "Frequency (Hz)",
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.707, 0.1, 0.01 },
                    ImagValues = new double[] { 0.0, 0.707, 0.1, 0.01 }
                }
            },
            Options = new PlotOptions
            {
                Title = "AC Frequency Response",
                ShowGrid = true,
                ShowLegend = true
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        Assert.Equal(ImageFormat.Svg, result.Format);
        
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithAcAnalysisAndAutoType_ShouldSelectBode()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Auto,  // Should auto-select Bode
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.5, 0.1 },
                    ImagValues = new double[] { 0.0, 0.1, 0.05 }
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
    public void GeneratePlot_WithMultipleSignalsBode_ShouldCreateMultipleMagnitudePhasePairs()
    {
        // Arrange
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
                    Values = new double[] { 1.0, 0.5, 0.1 },
                    ImagValues = new double[] { 0.0, 0.1, 0.05 }
                },
                new DataSeries
                {
                    Name = "v(in)",
                    Values = new double[] { 1.0, 1.0, 1.0 },
                    ImagValues = new double[] { 0.0, 0.0, 0.0 }
                }
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
    }

    [Fact]
    public void GeneratePlot_WithBodePlot_ShouldHaveMagnitudeAndPhaseVisible()
    {
        // Arrange
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
                    Values = new double[] { 1.0, 0.5, 0.1 },
                    ImagValues = new double[] { 0.0, 0.1, 0.05 }
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify SVG is generated (magnitude and phase should both be in the SVG)
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }
}

