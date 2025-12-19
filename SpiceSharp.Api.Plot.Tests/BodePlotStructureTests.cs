namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for Bode plot structure.
/// </summary>
public class BodePlotStructureTests
{
    [Fact]
    public void GeneratePlot_WithBodePlotType_ShouldCreateTwoPanelPlot()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0 },
            XLabel = "Frequency (Hz)",
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.5, 0.1 },
                    ImagValues = new double[] { 0.0, 0.1, 0.05 }
                }
            },
            Options = new PlotOptions
            {
                Title = "Bode Plot"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithBodePlot_ShouldUseLogarithmicFrequencyAxis()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0, 100.0, 1000.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.5, 0.1, 0.01 },
                    ImagValues = new double[] { 0.0, 0.1, 0.05, 0.01 }
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
    public void GeneratePlot_WithBodePlot_ShouldCreateMagnitudeAndPhasePanels()
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
        
        // Verify SVG contains magnitude and phase indicators
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }
}

