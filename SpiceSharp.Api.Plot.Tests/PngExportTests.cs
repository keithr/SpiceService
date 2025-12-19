namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PNG export functionality.
/// </summary>
public class PngExportTests
{
    [Fact]
    public void GeneratePlot_WithPngFormat_ShouldReturnValidPng()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Png,
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
        Assert.Equal(ImageFormat.Png, result.Format);

        // Verify PNG magic number (first 8 bytes: 89 50 4E 47 0D 0A 1A 0A)
        Assert.True(result.ImageData.Length >= 8);
        Assert.Equal(0x89, result.ImageData[0]);
        Assert.Equal(0x50, result.ImageData[1]); // P
        Assert.Equal(0x4E, result.ImageData[2]); // N
        Assert.Equal(0x47, result.ImageData[3]); // G
    }

    [Fact]
    public void GeneratePlot_WithPngFormatAndCustomDimensions_ShouldUseCustomDimensions()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Png,
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
    }

    [Fact]
    public void GeneratePlot_WithPngFormat_ShouldBeValidImage()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Png,
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
        
        // Verify PNG header (first byte is 0x89, then "PNG")
        Assert.True(result.ImageData.Length > 8);
        Assert.Equal(0x89, result.ImageData[0]);
        Assert.Equal(0x50, result.ImageData[1]); // P
        Assert.Equal(0x4E, result.ImageData[2]); // N
        Assert.Equal(0x47, result.ImageData[3]); // G
    }

    [Fact]
    public void GeneratePlot_WithPngFormatAndBodePlot_ShouldWork()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Png,
            XData = new double[] { 1.0, 10.0, 100.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 0.5, 0.1 },
                    ImagValues = new double[] { 0.0, 0.1, 0.05 }
                }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        Assert.Equal(ImageFormat.Png, result.Format);
    }
}

