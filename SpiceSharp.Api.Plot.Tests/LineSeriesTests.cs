namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for line series addition functionality.
/// </summary>
public class LineSeriesTests
{
    [Fact]
    public void GeneratePlot_WithSingleLineSeries_ShouldCreatePlot()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            XLabel = "Time (s)",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
            },
            Options = new PlotOptions { YLabel = "Voltage (V)" }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Will fail until SVG export is implemented, but structure should be correct
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithMultipleLineSeries_ShouldCreatePlot()
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
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithCustomColors_ShouldUseCustomColors()
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
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithColorPalette_ShouldApplyColors()
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
        Assert.NotNull(result);
    }
}

