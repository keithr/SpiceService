namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for DataSeries class.
/// </summary>
public class DataSeriesTests
{
    [Fact]
    public void Constructor_WithNameAndValues_ShouldCreateInstance()
    {
        // Arrange
        var name = "v(out)";
        var values = new double[] { 1.0, 2.0, 3.0 };

        // Act
        var series = new DataSeries
        {
            Name = name,
            Values = values
        };

        // Assert
        Assert.Equal(name, series.Name);
        Assert.Equal(values, series.Values);
        Assert.Null(series.ImagValues);
        Assert.Null(series.Color);
    }

    [Fact]
    public void Constructor_WithNullName_ShouldAllowNull()
    {
        // Arrange & Act
        var series = new DataSeries
        {
            Name = null!,
            Values = new double[] { 1.0, 2.0 }
        };

        // Assert
        Assert.Null(series.Name);
    }

    [Fact]
    public void Constructor_WithEmptyName_ShouldAllowEmpty()
    {
        // Arrange & Act
        var series = new DataSeries
        {
            Name = string.Empty,
            Values = new double[] { 1.0, 2.0 }
        };

        // Assert
        Assert.Equal(string.Empty, series.Name);
    }

    [Fact]
    public void Constructor_WithNullValues_ShouldAllowNull()
    {
        // Arrange & Act
        var series = new DataSeries
        {
            Name = "test",
            Values = null!
        };

        // Assert
        Assert.Null(series.Values);
    }

    [Fact]
    public void ImagValues_ShouldBeOptional()
    {
        // Arrange
        var series = new DataSeries
        {
            Name = "v(out)",
            Values = new double[] { 1.0, 2.0 }
        };

        // Act & Assert
        Assert.Null(series.ImagValues);

        // Set ImagValues
        series.ImagValues = new double[] { 0.1, 0.2 };
        Assert.NotNull(series.ImagValues);
        Assert.Equal(2, series.ImagValues.Length);
    }

    [Fact]
    public void Color_ShouldBeOptional()
    {
        // Arrange
        var series = new DataSeries
        {
            Name = "v(out)",
            Values = new double[] { 1.0, 2.0 }
        };

        // Act & Assert
        Assert.Null(series.Color);

        // Set Color
        series.Color = "#FF0000";
        Assert.Equal("#FF0000", series.Color);
    }

    [Fact]
    public void DataSeries_ShouldSupportComplexData()
    {
        // Arrange & Act
        var series = new DataSeries
        {
            Name = "v(out)",
            Values = new double[] { 1.0, 2.0, 3.0 },
            ImagValues = new double[] { 0.1, 0.2, 0.3 }
        };

        // Assert
        Assert.NotNull(series.Values);
        Assert.NotNull(series.ImagValues);
        Assert.Equal(3, series.Values.Length);
        Assert.Equal(3, series.ImagValues.Length);
    }
}

