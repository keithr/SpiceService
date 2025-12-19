namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for enum definitions used in the plotting library.
/// </summary>
public class EnumsTests
{
    [Fact]
    public void AnalysisType_ShouldHaveCorrectValues()
    {
        // Verify all expected AnalysisType enum values exist
        var values = Enum.GetValues<AnalysisType>();
        
        Assert.Contains(AnalysisType.DcSweep, values);
        Assert.Contains(AnalysisType.Transient, values);
        Assert.Contains(AnalysisType.Ac, values);
        Assert.Contains(AnalysisType.OperatingPoint, values);
        
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void PlotType_ShouldHaveCorrectValues()
    {
        // Verify all expected PlotType enum values exist
        var values = Enum.GetValues<PlotType>();
        
        Assert.Contains(PlotType.Auto, values);
        Assert.Contains(PlotType.Line, values);
        Assert.Contains(PlotType.Bode, values);
        Assert.Contains(PlotType.Bar, values);
        Assert.Contains(PlotType.Scatter, values);
        
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void ImageFormat_ShouldHaveCorrectValues()
    {
        // Verify all expected ImageFormat enum values exist
        var values = Enum.GetValues<ImageFormat>();
        
        Assert.Contains(ImageFormat.Svg, values);
        Assert.Contains(ImageFormat.Png, values);
        
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void ScaleType_ShouldHaveCorrectValues()
    {
        // Verify all expected ScaleType enum values exist
        var values = Enum.GetValues<ScaleType>();
        
        Assert.Contains(ScaleType.Linear, values);
        Assert.Contains(ScaleType.Log, values);
        
        Assert.Equal(2, values.Length);
    }
}

