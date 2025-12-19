namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PlotOptions class.
/// </summary>
public class PlotOptionsTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var options = new PlotOptions();

        // Assert
        Assert.True(options.ShowGrid);
        Assert.True(options.ShowLegend);
        Assert.Equal(800, options.Width);
        Assert.Equal(600, options.Height);
        Assert.Equal(ScaleType.Linear, options.XScale);
        Assert.Equal(ScaleType.Linear, options.YScale);
    }

    [Fact]
    public void XScale_ShouldDefaultToLinear()
    {
        // Arrange & Act
        var options = new PlotOptions();

        // Assert
        Assert.Equal(ScaleType.Linear, options.XScale);

        // Act - Change to Log
        options.XScale = ScaleType.Log;
        Assert.Equal(ScaleType.Log, options.XScale);
    }

    [Fact]
    public void YScale_ShouldDefaultToLinear()
    {
        // Arrange & Act
        var options = new PlotOptions();

        // Assert
        Assert.Equal(ScaleType.Linear, options.YScale);

        // Act - Change to Log
        options.YScale = ScaleType.Log;
        Assert.Equal(ScaleType.Log, options.YScale);
    }

    [Fact]
    public void PropertySetters_ShouldWorkCorrectly()
    {
        // Arrange
        var options = new PlotOptions();

        // Act
        options.Title = "Test Title";
        options.YLabel = "Voltage (V)";
        options.ShowGrid = false;
        options.ShowLegend = false;
        options.Width = 1000;
        options.Height = 700;

        // Assert
        Assert.Equal("Test Title", options.Title);
        Assert.Equal("Voltage (V)", options.YLabel);
        Assert.False(options.ShowGrid);
        Assert.False(options.ShowLegend);
        Assert.Equal(1000, options.Width);
        Assert.Equal(700, options.Height);
    }

    [Fact]
    public void ColorPalette_ShouldBeNullable()
    {
        // Arrange & Act
        var options = new PlotOptions();

        // Assert - ColorPalette is optional and can be null
        Assert.Null(options.ColorPalette);
    }

    [Fact]
    public void ColorPalette_ShouldAllowSetting()
    {
        // Arrange
        var options = new PlotOptions();
        var palette = new string[] { "#FF0000", "#00FF00", "#0000FF" };

        // Act
        options.ColorPalette = palette;

        // Assert
        Assert.Equal(palette, options.ColorPalette);
        Assert.Equal(3, options.ColorPalette.Length);
    }

    [Fact]
    public void ColorPalette_ShouldAllowNull()
    {
        // Arrange
        var options = new PlotOptions();

        // Act
        options.ColorPalette = null!;

        // Assert
        Assert.Null(options.ColorPalette);
    }
}

