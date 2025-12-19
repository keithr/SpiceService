namespace SpiceSharp.Api.Plot.Tests;

/// <summary>
/// Tests for PlotResult class.
/// </summary>
public class PlotResultTests
{
    [Fact]
    public void Success_ShouldBeSettable()
    {
        // Arrange & Act
        var result = new PlotResult
        {
            Success = true
        };

        // Assert
        Assert.True(result.Success);

        result.Success = false;
        Assert.False(result.Success);
    }

    [Fact]
    public void ImageData_ShouldBeNullWhenFailed()
    {
        // Arrange & Act
        var result = new PlotResult
        {
            Success = false,
            ImageData = null
        };

        // Assert
        Assert.Null(result.ImageData);
    }

    [Fact]
    public void ImageData_ShouldBePopulatedWhenSuccess()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = new PlotResult
        {
            Success = true,
            ImageData = imageData
        };

        // Assert
        Assert.NotNull(result.ImageData);
        Assert.Equal(imageData, result.ImageData);
        Assert.Equal(5, result.ImageData.Length);
    }

    [Fact]
    public void Format_ShouldBeSettable()
    {
        // Arrange & Act
        var result = new PlotResult
        {
            Format = ImageFormat.Svg
        };

        // Assert
        Assert.Equal(ImageFormat.Svg, result.Format);

        result.Format = ImageFormat.Png;
        Assert.Equal(ImageFormat.Png, result.Format);
    }

    [Fact]
    public void ErrorMessage_ShouldBeNullWhenSuccess()
    {
        // Arrange & Act
        var result = new PlotResult
        {
            Success = true,
            ErrorMessage = null
        };

        // Assert
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ErrorMessage_ShouldBePopulatedWhenFailed()
    {
        // Arrange & Act
        var result = new PlotResult
        {
            Success = false,
            ErrorMessage = "Plot generation failed"
        };

        // Assert
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("Plot generation failed", result.ErrorMessage);
    }

    [Fact]
    public void SuccessResult_ShouldCreateSuccessResult()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3 };
        var format = ImageFormat.Svg;

        // Act
        var result = PlotResult.SuccessResult(imageData, format);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(imageData, result.ImageData);
        Assert.Equal(format, result.Format);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void FailureResult_ShouldCreateFailureResult()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var result = PlotResult.FailureResult(errorMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.ImageData);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void FailureResult_WithNullErrorMessage_ShouldWork()
    {
        // Act
        var result = PlotResult.FailureResult(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.ImageData);
        Assert.Null(result.ErrorMessage);
    }
}

