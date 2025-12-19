namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PlotGenerator class.
/// </summary>
public class PlotGeneratorTests
{
    [Fact]
    public void PlotGenerator_ShouldImplementIPlotGenerator()
    {
        // Arrange & Act
        var generator = PlotGeneratorFactory.Create();

        // Assert
        Assert.IsAssignableFrom<IPlotGenerator>(generator);
    }

    [Fact]
    public void SupportedFormats_ShouldReturnSvgAndPng()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();

        // Act
        var formats = generator.SupportedFormats;

        // Assert
        Assert.NotNull(formats);
        Assert.Contains("svg", formats);
        Assert.Contains("png", formats);
        Assert.Equal(2, formats.Count);
    }

    [Fact]
    public void GeneratePlot_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GeneratePlot(null!));
    }

    [Fact]
    public void GeneratePlot_WithInvalidRequest_ShouldReturnFailureResult()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = null,
            Series = new List<DataSeries>()
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void GeneratePlot_WithEmptySeries_ShouldReturnFailureResult()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            Series = new List<DataSeries>()
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void GeneratePlot_WithNullXData_ShouldReturnFailureResult()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = null,
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void GeneratePlot_WithMismatchedDataLengths_ShouldReturnFailureResult()
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
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } } // Mismatched length
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}

