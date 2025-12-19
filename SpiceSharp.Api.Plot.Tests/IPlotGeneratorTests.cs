namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for IPlotGenerator interface contract.
/// </summary>
public class IPlotGeneratorTests
{
    [Fact]
    public void Interface_ShouldDefineGeneratePlotMethod()
    {
        // Arrange
        var method = typeof(IPlotGenerator).GetMethod(nameof(IPlotGenerator.GeneratePlot));

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(PlotResult), method!.ReturnType);
        Assert.Single(method.GetParameters());
        Assert.Equal(typeof(PlotRequest), method.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void Interface_ShouldDefineSupportedFormatsProperty()
    {
        // Arrange
        var property = typeof(IPlotGenerator).GetProperty(nameof(IPlotGenerator.SupportedFormats));

        // Assert
        Assert.NotNull(property);
        Assert.True(property!.CanRead);
        Assert.False(property.CanWrite);
        Assert.Equal(typeof(IReadOnlyList<string>), property.PropertyType);
    }

    [Fact]
    public void Interface_ShouldBePublic()
    {
        // Assert
        Assert.True(typeof(IPlotGenerator).IsPublic);
    }
}

