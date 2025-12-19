namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PlotRequest class.
/// </summary>
public class PlotRequestTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var request = new PlotRequest();

        // Assert
        Assert.NotNull(request.Series);
        Assert.NotNull(request.Options);
    }

    [Fact]
    public void RequiredProperties_ShouldBeSettable()
    {
        // Arrange & Act
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Transient,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg
        };

        // Assert
        Assert.Equal(AnalysisType.Transient, request.AnalysisType);
        Assert.Equal(PlotType.Line, request.PlotType);
        Assert.Equal(ImageFormat.Svg, request.ImageFormat);
    }

    [Fact]
    public void XData_ShouldBeSettable()
    {
        // Arrange
        var xData = new double[] { 0.0, 1.0, 2.0, 3.0 };

        // Act
        var request = new PlotRequest
        {
            XData = xData
        };

        // Assert
        Assert.Equal(xData, request.XData);
        Assert.Equal(4, request.XData.Length);
    }

    [Fact]
    public void XLabel_ShouldBeSettable()
    {
        // Arrange & Act
        var request = new PlotRequest
        {
            XLabel = "Time (s)"
        };

        // Assert
        Assert.Equal("Time (s)", request.XLabel);
    }

    [Fact]
    public void Series_ShouldSupportEmptyCollection()
    {
        // Arrange & Act
        var request = new PlotRequest
        {
            Series = new List<DataSeries>()
        };

        // Assert
        Assert.NotNull(request.Series);
        Assert.Empty(request.Series);
    }

    [Fact]
    public void Series_ShouldSupportSingleSeries()
    {
        // Arrange
        var series = new List<DataSeries>
        {
            new DataSeries
            {
                Name = "v(out)",
                Values = new double[] { 1.0, 2.0, 3.0 }
            }
        };

        // Act
        var request = new PlotRequest
        {
            Series = series
        };

        // Assert
        Assert.Single(request.Series);
        Assert.Equal("v(out)", request.Series[0].Name);
    }

    [Fact]
    public void Series_ShouldSupportMultipleSeries()
    {
        // Arrange
        var series = new List<DataSeries>
        {
            new DataSeries { Name = "v(out)", Values = new double[] { 1.0, 2.0 } },
            new DataSeries { Name = "i(R1)", Values = new double[] { 0.1, 0.2 } }
        };

        // Act
        var request = new PlotRequest
        {
            Series = series
        };

        // Assert
        Assert.Equal(2, request.Series.Count);
        Assert.Equal("v(out)", request.Series[0].Name);
        Assert.Equal("i(R1)", request.Series[1].Name);
    }

    [Fact]
    public void Options_ShouldBeInitialized()
    {
        // Arrange & Act
        var request = new PlotRequest();

        // Assert
        Assert.NotNull(request.Options);
        Assert.IsType<PlotOptions>(request.Options);
    }

    [Fact]
    public void Options_ShouldBeSettable()
    {
        // Arrange
        var customOptions = new PlotOptions
        {
            Title = "Custom Title",
            Width = 1000,
            Height = 700
        };

        // Act
        var request = new PlotRequest
        {
            Options = customOptions
        };

        // Assert
        Assert.Equal(customOptions, request.Options);
        Assert.Equal("Custom Title", request.Options.Title);
    }

    [Fact]
    public void XData_ShouldAllowNull()
    {
        // Arrange & Act
        var request = new PlotRequest
        {
            XData = null!
        };

        // Assert
        Assert.Null(request.XData);
    }

    [Fact]
    public void XLabel_ShouldAllowNull()
    {
        // Arrange & Act
        var request = new PlotRequest
        {
            XLabel = null
        };

        // Assert
        Assert.Null(request.XLabel);
    }
}

