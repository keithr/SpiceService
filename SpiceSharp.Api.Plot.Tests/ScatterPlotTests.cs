namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for scatter plot functionality.
/// </summary>
public class ScatterPlotTests
{
    [Fact]
    public void GeneratePlot_WithScatterPlot_ShouldCreateScatterSeries()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Scatter,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.1, 0.2, 0.3, 0.4, 0.5 },
            XLabel = "Current (A)",
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(D1)",
                    Values = new double[] { 0.6, 0.65, 0.7, 0.72, 0.75 }
                }
            },
            Options = new PlotOptions
            {
                Title = "Diode Characteristic",
                YLabel = "Voltage (V)"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithScatterPlotAndMultipleSeries_ShouldCreateMultipleScatterSeries()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Scatter,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.1, 0.2, 0.3 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(D1)", Values = new double[] { 0.6, 0.65, 0.7 } },
                new DataSeries { Name = "v(D2)", Values = new double[] { 0.55, 0.6, 0.65 } }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

