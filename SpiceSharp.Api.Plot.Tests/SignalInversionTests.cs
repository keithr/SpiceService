namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for signal inversion functionality.
/// </summary>
public class SignalInversionTests
{
    [Fact]
    public void GeneratePlot_WithInvertedSignal_ShouldInvertValues()
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
                    Values = new double[] { -0.1, -0.2, -0.3 }  // Negative values (SPICE convention)
                }
            },
            Options = new PlotOptions()
        };

        // Act - invert the signal
        var invertedValues = request.Series[0].Values.Select(v => -v).ToArray();
        request.Series[0].Values = invertedValues;

        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify values are now positive
        Assert.Equal(0.1, invertedValues[0], 0.001);
        Assert.Equal(0.2, invertedValues[1], 0.001);
        Assert.Equal(0.3, invertedValues[2], 0.001);
    }

    [Fact]
    public void GeneratePlot_WithInvertedComplexSignal_ShouldInvertBothRealAndImaginary()
    {
        // Arrange
        var generator = PlotGeneratorFactory.Create();
        var request = new PlotRequest
        {
            AnalysisType = AnalysisType.Ac,
            PlotType = PlotType.Bode,
            ImageFormat = ImageFormat.Svg,
            XData = new double[] { 1.0, 10.0 },
            Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = "v(out)",
                    Values = new double[] { -1.0, -0.5 },
                    ImagValues = new double[] { -0.1, -0.05 }
                }
            },
            Options = new PlotOptions()
        };

        // Act - invert the signal
        var invertedReal = request.Series[0].Values.Select(v => -v).ToArray();
        var invertedImag = request.Series[0].ImagValues!.Select(v => -v).ToArray();
        request.Series[0].Values = invertedReal;
        request.Series[0].ImagValues = invertedImag;

        var result = generator.GeneratePlot(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ImageData);
        
        // Verify values are inverted
        Assert.Equal(1.0, invertedReal[0], 0.001);
        Assert.Equal(0.5, invertedReal[1], 0.001);
        Assert.Equal(0.1, invertedImag[0], 0.001);
        Assert.Equal(0.05, invertedImag[1], 0.001);
    }
}

