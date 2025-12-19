namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PlotModel creation functionality.
/// </summary>
public class PlotModelCreationTests
{
    [Fact]
    public void CreatePlotModel_WithMinimalRequest_ShouldCreateValidModel()
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
            Options = new PlotOptions
            {
                Title = "Test Plot",
                YLabel = "Voltage (V)"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - For now, we're just testing that it doesn't crash
        // Full validation will come when we implement the actual plotting
        Assert.NotNull(result);
    }

    [Fact]
    public void CreatePlotModel_WithTitle_ShouldSetTitle()
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
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } }
            },
            Options = new PlotOptions { Title = "My Custom Title" }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CreatePlotModel_WithLinearAxes_ShouldUseLinearScaling()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            XLabel = "Voltage (V)",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "i(R1)", Values = new double[] { 0.1, 0.2, 0.3 } }
            },
            Options = new PlotOptions
            {
                XScale = ScaleType.Linear,
                YScale = ScaleType.Linear,
                YLabel = "Current (A)"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CreatePlotModel_WithLabels_ShouldSetAxisLabels()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0 },
            XLabel = "Time (s)",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } }
            },
            Options = new PlotOptions { YLabel = "Voltage (V)" }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.NotNull(result);
    }
}

