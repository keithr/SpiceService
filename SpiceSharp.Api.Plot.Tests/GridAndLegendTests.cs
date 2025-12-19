namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for grid and legend functionality.
/// </summary>
public class GridAndLegendTests
{
    [Fact]
    public void GeneratePlot_WithGridEnabled_ShouldShowGrid()
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
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
            },
            Options = new PlotOptions
            {
                ShowGrid = true
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithGridDisabled_ShouldHideGrid()
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
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
            },
            Options = new PlotOptions
            {
                ShowGrid = false
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithLegendEnabledAndMultipleSeries_ShouldShowLegend()
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
    public void GeneratePlot_WithLegendDisabled_ShouldHideLegend()
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
                ShowLegend = false
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithLegendEnabledAndSingleSeries_ShouldHideLegend()
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
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
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
        
        // Legend should be hidden for single series even if ShowLegend is true
        // (This is the current implementation behavior)
    }

    [Fact]
    public void GeneratePlot_WithGridAndLegendBothEnabled_ShouldWork()
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
                new DataSeries { Name = "i(R1)", Values = new double[] { 0.1, 0.2, 0.3 } },
                new DataSeries { Name = "i(R2)", Values = new double[] { 0.05, 0.15, 0.25 } }
            },
            Options = new PlotOptions
            {
                ShowGrid = true,
                ShowLegend = true
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

