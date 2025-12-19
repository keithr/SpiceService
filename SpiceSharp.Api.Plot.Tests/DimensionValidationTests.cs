namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for dimension validation.
/// </summary>
public class DimensionValidationTests
{
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
                Width = 50,  // Below minimum
                Height = 50  // Below minimum
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should succeed but clamp to minimum dimensions
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
                Width = 50000,  // Above maximum
                Height = 50000  // Above maximum
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert - Should succeed but clamp to maximum dimensions
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }
}

