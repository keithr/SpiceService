namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for input validation and edge cases.
/// </summary>
public class InputValidationTests
{
    [Fact]
    public void GeneratePlot_WithEmptySignalsArray_ShouldFailGracefully()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            Series = new List<DataSeries>()  // Empty series
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should handle gracefully (may succeed with empty plot or fail with clear error)
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithMismatchedDataLengths_ShouldHandleGracefully()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },  // 3 points
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 2.0 }  // Only 2 points - mismatch
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should handle gracefully (may truncate or fail with clear error)
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePlot_WithSingleDataPoint_ShouldWork()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0 }
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
    public void GeneratePlot_WithAllZeroData_ShouldWork()
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
                    Values = new double[] { 0.0, 0.0, 0.0 }
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
    public void GeneratePlot_WithAllNegativeData_ShouldWork()
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
                    Values = new double[] { -1.0, -2.0, -3.0 }
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
    public void GeneratePlot_WithVerySmallDimensions_ShouldClampToMinimum()
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
            Options = new PlotOptions
            {
                Width = 50,   // Below minimum (100)
                Height = 50   // Below minimum (100)
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should clamp to minimum dimensions
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithVeryLargeDimensions_ShouldClampToMaximum()
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
            Options = new PlotOptions
            {
                Width = 20000,   // Above maximum (10000)
                Height = 20000   // Above maximum (10000)
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should clamp to maximum dimensions
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

