using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for EnclosureDesignService
/// </summary>
public class EnclosureDesignServiceTests
{
    private SpeakerTsParameters CreateTestSpeaker()
    {
        return new SpeakerTsParameters
        {
            Fs = 42.18,
            Qts = 0.35,
            Qes = 0.38,
            Qms = 4.92,
            Vas = 11.2,
            Re = 2.73,
            Le = 0.65,
            Bl = 8.27,
            Xmax = 8.2,
            Mms = 35.3,
            Cms = 0.4667,
            Sd = 214.0 // cm²
        };
    }

    [Fact]
    public void CalculateSealedBox_ButterworthAlignment_ReturnsCorrectVolume()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();
        var targetQtc = 0.707; // Butterworth alignment

        // Act
        var result = service.CalculateSealedBox(speaker, targetQtc);

        // Assert
        Assert.True(result.VolumeLiters > 0);
        Assert.True(result.VolumeCubicFeet > 0);
        Assert.Equal(targetQtc, result.Qtc, 3); // Within 0.001
        Assert.True(result.F3 > 0);
        Assert.True(result.Fc > 0);
        
        // Verify volume conversion
        var expectedCubicFeet = result.VolumeLiters / 28.3168;
        Assert.Equal(expectedCubicFeet, result.VolumeCubicFeet, 2);
    }

    [Fact]
    public void CalculateSealedBox_BesselAlignment_ReturnsCorrectVolume()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();
        var targetQtc = 0.577; // Bessel alignment

        // Act
        var result = service.CalculateSealedBox(speaker, targetQtc);

        // Assert
        Assert.True(result.VolumeLiters > 0);
        Assert.Equal(targetQtc, result.Qtc, 3);
        // Bessel should have larger box than Butterworth
        var butterworthResult = service.CalculateSealedBox(speaker, 0.707);
        Assert.True(result.VolumeLiters > butterworthResult.VolumeLiters);
    }

    [Fact]
    public void CalculateSealedBox_ReturnsCorrectF3()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();
        var targetQtc = 0.707;

        // Act
        var result = service.CalculateSealedBox(speaker, targetQtc);

        // Assert
        // F3 should be higher than Fs for sealed box
        Assert.True(result.F3 > speaker.Fs);
        Assert.True(result.Fc > speaker.Fs);
    }

    [Fact]
    public void CalculateVentedBox_QB3Alignment_ReturnsCorrectVolume()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();

        // Act
        var result = service.CalculateVentedBox(speaker, "QB3");

        // Assert
        Assert.True(result.VolumeLiters > 0);
        Assert.True(result.VolumeCubicFeet > 0);
        Assert.True(result.Fb > 0);
        Assert.True(result.F3 > 0);
        Assert.True(result.PortDiameterInches > 0);
        Assert.True(result.PortLengthInches > 0);
        
        // QB3: Vb ≈ 15 × Qts³ × Vas
        var expectedVolume = 15 * Math.Pow(speaker.Qts, 3) * speaker.Vas;
        Assert.Equal(expectedVolume, result.VolumeLiters, 1); // Within 0.1 liters
    }

    [Fact]
    public void CalculateVentedBox_QB3Alignment_ReturnsCorrectTuningFrequency()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();

        // Act
        var result = service.CalculateVentedBox(speaker, "QB3");

        // Assert
        // QB3: Fb ≈ Fs / (Qts × 1.4)
        var expectedFb = speaker.Fs / (speaker.Qts * 1.4);
        Assert.Equal(expectedFb, result.Fb, 1); // Within 0.1 Hz
    }

    [Fact]
    public void CalculateVentedBox_ReturnsPortDimensions()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();

        // Act
        var result = service.CalculateVentedBox(speaker, "QB3");

        // Assert
        Assert.True(result.PortDiameterInches > 0);
        Assert.True(result.PortLengthInches > 0);
        Assert.True(result.PortDiameterCm > 0);
        Assert.True(result.PortLengthCm > 0);
        
        // Verify unit conversions
        Assert.Equal(result.PortDiameterInches * 2.54, result.PortDiameterCm, 2);
        Assert.Equal(result.PortLengthInches * 2.54, result.PortLengthCm, 2);
    }

    [Fact]
    public void CalculateVentedBox_HandlesPortVelocityWarning()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();

        // Act
        var result = service.CalculateVentedBox(speaker, "QB3");

        // Assert
        // Port velocity should be calculated if possible
        // If too high (> 17 m/s), warning should be set
        if (result.MaxPortVelocity.HasValue && result.MaxPortVelocity > 17)
        {
            Assert.NotNull(result.PortVelocityWarning);
        }
    }

    [Fact]
    public void CalculateVentedBox_B4Alignment_ReturnsDifferentVolume()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();

        // Act
        var qb3Result = service.CalculateVentedBox(speaker, "QB3");
        var b4Result = service.CalculateVentedBox(speaker, "B4");

        // Assert
        // B4 should have different volume than QB3
        Assert.NotEqual(qb3Result.VolumeLiters, b4Result.VolumeLiters);
    }

    [Fact]
    public void CalculateVentedBox_InvalidAlignment_ThrowsException()
    {
        // Arrange
        var service = new EnclosureDesignService();
        var speaker = CreateTestSpeaker();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.CalculateVentedBox(speaker, "INVALID"));
    }
}

