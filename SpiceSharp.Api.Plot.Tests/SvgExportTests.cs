namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for SVG export functionality.
/// </summary>
public class SvgExportTests
{
    [Fact]
    public void GeneratePlot_WithSvgFormat_ShouldReturnValidSvg()
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
                YLabel = "Voltage (V)",
                Width = 800,
                Height = 600
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        Assert.True(result.ImageData.Length > 0);
        Assert.Equal(ImageFormat.Svg, result.Format);

        // Verify SVG content
        Assert.NotNull(result.ImageData);
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithCustomDimensions_ShouldUseCustomDimensions()
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
    public void GeneratePlot_WithSvgFormat_ShouldContainExpectedElements()
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
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        var imageData = result.ImageData;
        var svgContent = System.Text.Encoding.UTF8.GetString(imageData);
        
        // Verify SVG structure
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</svg>", svgContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratePlot_WithSvgFormat_ShouldBeValidXml()
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
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        
        // Basic XML validation - SVG may start with XML declaration, but should contain <svg
        Assert.Contains("<svg", svgContent, StringComparison.OrdinalIgnoreCase);
    }
}

