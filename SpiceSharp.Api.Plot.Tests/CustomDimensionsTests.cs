namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for custom dimensions functionality.
/// </summary>
public class CustomDimensionsTests
{
    [Fact]
    public void GeneratePlot_WithCustomWidthAndHeight_ShouldApplyDimensions()
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
                Width = 1000,
                Height = 700
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify SVG contains width/height attributes
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("width", svgContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithDefaultDimensions_ShouldUseDefaults()
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
            Options = new PlotOptions()
            // Use default dimensions (800x600)
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithSmallDimensions_ShouldWork()
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
                new DataSeries { Name = "i(R1)", Values = new double[] { 0.1, 0.2 } }
            },
            Options = new PlotOptions
            {
                Width = 400,
                Height = 300
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithLargeDimensions_ShouldWork()
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
                Width = 2000,
                Height = 1500
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
    }

    [Fact]
    public void GeneratePlot_WithDifferentDimensions_ShouldReflectInSvg()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request1 = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } }
            },
            Options = new PlotOptions { Width = 500, Height = 400 }
        };

        var request2 = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0 },
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } }
            },
            Options = new PlotOptions { Width = 1200, Height = 800 }
        };

        // Act
        var result1 = generator.GeneratePlot(request1);
        var result2 = generator.GeneratePlot(request2);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.NotEqual(result1.ImageData!.Length, result2.ImageData!.Length);
    }
}

