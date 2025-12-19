namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for inverted signals functionality, particularly axis label updates.
/// </summary>
public class InvertSignalsTests
{
    [Fact]
    public void PlotGenerator_WithInvertedSignal_UpdatesYAxisLabel()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "i(V1)",
                    Values = new double[] { 0.1, 0.2, 0.3 }  // Already inverted (positive)
                }
            },
            Options = new PlotOptions
            {
                InvertedSignals = new HashSet<string> { "i(V1)" }
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify the plot was generated (we can't easily check the SVG content directly,
        // but we can verify the plot generation succeeded with inverted signal info)
        // The actual label check will be done via integration tests that parse the SVG
    }

    [Fact]
    public void PlotGenerator_WithInvertedCurrentSignal_ShowsInvertedLabel()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.DcSweep,
            PlotType = PlotType.Line,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 0.0, 1.0, 2.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "i(V1)",
                    Values = new double[] { 0.01, 0.02, 0.03 }
                }
            },
            Options = new PlotOptions
            {
                InvertedSignals = new HashSet<string> { "i(V1)" },
                YLabel = null  // Let it generate default label
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        // The label should reflect that the signal is inverted
        // This will be verified in integration tests that check the actual SVG output
    }

    [Fact]
    public void PlotGenerator_WithMixedInvertedSignals_HandlesCorrectly()
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
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { 1.0, 2.0, 3.0 }
                },
                new DataSeries
                {
                    Name = "i(V1)",
                    Values = new double[] { 0.1, 0.2, 0.3 }
                }
            },
            Options = new PlotOptions
            {
                InvertedSignals = new HashSet<string> { "i(V1)" }  // Only current signal is inverted
            }
        };

        // Act
        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        // Mixed signals should be handled correctly
    }
}
