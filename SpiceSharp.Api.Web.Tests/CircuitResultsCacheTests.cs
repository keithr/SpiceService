using SpiceSharp.Api.Web.Services;
using Xunit;

namespace SpiceSharp.Api.Web.Tests;

/// <summary>
/// Tests for CircuitResultsCache.
/// </summary>
public class CircuitResultsCacheTests
{
    [Fact]
    public void Store_ShouldStoreResults()
    {
        // Arrange
        var cache = new CircuitResultsCache();
        var circuitId = "test_circuit";
        var results = new CachedAnalysisResult
        {
            AnalysisType = "dc_sweep",
            Signals = new Dictionary<string, double[]>
            {
                { "v(out)", new double[] { 1.0, 2.0, 3.0 } }
            },
            XData = new double[] { 0.0, 1.0, 2.0 }
        };

        // Act
        cache.Store(circuitId, results);

        // Assert
        var retrieved = cache.Get(circuitId);
        Assert.NotNull(retrieved);
        Assert.Equal(results.AnalysisType, retrieved!.AnalysisType);
    }

    [Fact]
    public void Get_WithNonExistentCircuit_ShouldReturnNull()
    {
        // Arrange
        var cache = new CircuitResultsCache();

        // Act
        var result = cache.Get("non_existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Clear_ShouldRemoveResults()
    {
        // Arrange
        var cache = new CircuitResultsCache();
        var circuitId = "test_circuit";
        var results = new CachedAnalysisResult
        {
            AnalysisType = "transient",
            Signals = new Dictionary<string, double[]>()
        };
        cache.Store(circuitId, results);

        // Act
        cache.Clear(circuitId);

        // Assert
        var retrieved = cache.Get(circuitId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void Store_ShouldOverwriteExistingResults()
    {
        // Arrange
        var cache = new CircuitResultsCache();
        var circuitId = "test_circuit";
        var results1 = new CachedAnalysisResult { AnalysisType = "dc_sweep" };
        var results2 = new CachedAnalysisResult { AnalysisType = "transient" };

        // Act
        cache.Store(circuitId, results1);
        cache.Store(circuitId, results2);

        // Assert
        var retrieved = cache.Get(circuitId);
        Assert.NotNull(retrieved);
        Assert.Equal("transient", retrieved!.AnalysisType);
    }
}

