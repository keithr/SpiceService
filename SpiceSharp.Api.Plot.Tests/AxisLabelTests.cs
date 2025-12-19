namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for axis label handling.
/// </summary>
public class AxisLabelTests
{
    [Fact]
    public void GeneratePlot_WithCustomXLabel_ShouldUseCustomLabel()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            XLabel = "Custom X Label",
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
            },
            Options = new PlotOptions()
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("Custom X Label", svgContent);
    }

    [Fact]
    public void GeneratePlot_WithCustomYLabel_ShouldUseCustomLabel()
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
                YLabel = "Custom Y Label"
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("Custom Y Label", svgContent);
    }

    [Fact]
    public void GeneratePlot_WithEmptyXLabel_ShouldUseDefault()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            XLabel = "",  // Empty string should be treated as missing
            Series = new List<DataSeries>
            {
                new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0, 3.0 } }
            },
            Options = new PlotOptions()
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Should use default label (Time (s) for transient)
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.Contains("Time", svgContent);
    }

    [Fact]
    public void GeneratePlot_WithEmptyYLabel_ShouldUseDefault()
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
                YLabel = ""  // Empty string should be treated as missing
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Should use default label (Voltage (V) for transient) - check for "Voltage" or "(V)"
        var svgContent = System.Text.Encoding.UTF8.GetString(result.ImageData);
        Assert.True(svgContent.Contains("Voltage") || svgContent.Contains("(V)"), 
            "SVG should contain default Y-axis label for transient analysis");
    }
}

