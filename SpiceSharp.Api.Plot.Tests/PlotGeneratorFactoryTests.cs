namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PlotGeneratorFactory.
/// </summary>
public class PlotGeneratorFactoryTests
{
    [Fact]
    public void Create_ShouldReturnNonNullInstance()
    {
        // Act
        var generator = PlotGeneratorFactory.Create();

        // Assert
        Assert.NotNull(generator);
    }

    [Fact]
    public void Create_ShouldReturnInstanceImplementingIPlotGenerator()
    {
        // Act
        var generator = PlotGeneratorFactory.Create();

        // Assert
        Assert.IsAssignableFrom<IPlotGenerator>(generator);
    }

    [Fact]
    public void Create_ShouldReturnNewInstanceEachTime()
    {
        // Act
        var generator1 = PlotGeneratorFactory.Create();
        var generator2 = PlotGeneratorFactory.Create();

        // Assert - Factory creates new instances (not singleton)
        Assert.NotSame(generator1, generator2);
    }

    [Fact]
    public void Create_ShouldReturnConsistentInstances()
    {
        // Act
        var generator1 = PlotGeneratorFactory.Create();
        var generator2 = PlotGeneratorFactory.Create();

        // Assert - Both instances should be valid
        Assert.NotNull(generator1);
        Assert.NotNull(generator2);
        Assert.IsAssignableFrom<IPlotGenerator>(generator1);
        Assert.IsAssignableFrom<IPlotGenerator>(generator2);
    }
}

