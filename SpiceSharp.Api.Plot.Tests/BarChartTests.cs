namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for bar chart functionality.
/// </summary>
public class BarChartTests
{
    [Fact]
    public void GeneratePlot_WithBarChart_ShouldCreateBarSeries()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.OperatingPoint,
            PlotType = PlotType.Bar,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0, 1, 2, 3 },  // Category indices
            XLabel = "Signal",
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.5, 2.3, 0.8, 3.2 }
                }
            },
            Options = new PlotOptions
            {
                Title = "Operating Point Values"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithBarChartAndMultipleSeries_ShouldCreateMultipleBars()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.OperatingPoint,
            PlotType = PlotType.Bar,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0, 1, 2 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.5, 2.3, 0.8 } },
                new DataSeries { Name = "v(in)", Values = new double[] { 0.5, 1.2, 0.3 } }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithBarChartAndOperatingPoint_ShouldWork()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.OperatingPoint,
            PlotType = PlotType.Bar,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0, 1, 2, 3, 4 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "Node Voltages",
                    Values = new double[] { 5.0, 3.3, 0.0, 1.2, 2.5 }
                }
            },
            Options = new PlotOptions
            {
                YLabel = "Voltage (V)"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

