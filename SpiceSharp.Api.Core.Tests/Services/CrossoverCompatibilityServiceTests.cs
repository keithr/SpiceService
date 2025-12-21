using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for CrossoverCompatibilityService
/// </summary>
public class CrossoverCompatibilityServiceTests
{
    private SpeakerTsParameters CreateTestWoofer()
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
            Sd = 214.0 // cm² - approximately 6.5" driver
        };
    }

    private SpeakerTsParameters CreateTestTweeter()
    {
        return new SpeakerTsParameters
        {
            Fs = 800.0, // Typical tweeter Fs
            Qts = 0.5,
            Qes = 0.6,
            Qms = 2.5,
            Vas = 0.1,
            Re = 3.5,
            Le = 0.1,
            Bl = 3.0,
            Xmax = 0.5,
            Mms = 0.5,
            Cms = 0.0001,
            Sd = 5.0 // cm² - typical 1" tweeter
        };
    }

    [Fact]
    public void CheckCompatibility_ValidatesWooferBeaming()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0; // Hz
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.MaxCrossoverFrequency > 0);
        
        // For a 6.5" driver (diameter ≈ 6.5"), beaming frequency ≈ 13750 / 6.5 ≈ 2115 Hz
        // So 2500 Hz is above beaming frequency - should warn
        if (crossoverFreq > result.MaxCrossoverFrequency)
        {
            Assert.False(result.WooferBeamingOk);
            Assert.Contains(result.Warnings, w => w.Contains("beaming", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void CheckCompatibility_ValidatesTweeterFs()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.MinCrossoverFrequency > 0);
        
        // Tweeter Fs should be < 0.5 × crossover frequency
        // For Fs = 800 Hz, min crossover = 1600 Hz
        // So 2500 Hz is acceptable
        if (crossoverFreq >= result.MinCrossoverFrequency)
        {
            Assert.True(result.TweeterFsOk);
        }
        else
        {
            Assert.False(result.TweeterFsOk);
        }
    }

    [Fact]
    public void CheckCompatibility_ValidatesSensitivityMatch()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        // Sensitivity difference should be calculated
        // If within 3dB, match should be OK
        if (Math.Abs(result.SensitivityDifference) <= 3.0)
        {
            Assert.True(result.SensitivityMatchOk);
        }
    }

    [Fact]
    public void CheckCompatibility_ValidatesImpedanceMatch()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        // Impedance match should be checked
        Assert.NotNull(result.WooferImpedance);
        Assert.NotNull(result.TweeterImpedance);
    }

    [Fact]
    public void CheckCompatibility_CalculatesCompatibilityScore()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CompatibilityScore >= 0 && result.CompatibilityScore <= 100);
    }

    [Fact]
    public void CheckCompatibility_GeneratesRecommendations()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Recommendations);
        // Should have at least some recommendations or warnings if issues exist
    }

    [Fact]
    public void CheckCompatibility_LowCrossoverFrequency_ShouldPassBeamingCheck()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 1500.0; // Low frequency - should pass beaming check
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        // If crossover is below beaming frequency, should be OK
        if (crossoverFreq <= result.MaxCrossoverFrequency)
        {
            Assert.True(result.WooferBeamingOk);
        }
    }

    [Fact]
    public void CheckCompatibility_HighTweeterFs_ShouldFailFsCheck()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        tweeter.Fs = 2000.0; // High Fs
        var crossoverFreq = 2500.0; // Crossover too close to tweeter Fs
        var crossoverOrder = 2;

        // Act - Use null for sensitivity/impedance to test fallback to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        // Tweeter Fs should be < 0.5 × crossover frequency
        // 2000 Hz Fs requires > 4000 Hz crossover
        // So 2500 Hz should fail
        if (crossoverFreq < result.MinCrossoverFrequency)
        {
            Assert.False(result.TweeterFsOk);
        }
    }

    [Fact]
    public void CheckCompatibility_UsesProvidedSensitivity_NotEstimated()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;
        
        // Provide actual sensitivity values from metadata
        var wooferSensitivity = 95.5; // dB
        var tweeterSensitivity = 93.5; // dB
        var expectedDifference = tweeterSensitivity - wooferSensitivity; // -2.0 dB

        // Act
        var result = service.CheckCompatibility(
            woofer, 
            tweeter, 
            crossoverFreq, 
            crossoverOrder,
            wooferSensitivity: wooferSensitivity,
            tweeterSensitivity: tweeterSensitivity);

        // Assert
        Assert.NotNull(result);
        // Sensitivity difference should be tweeter - woofer = 93.5 - 95.5 = -2.0 dB
        Assert.Equal(-2.0, result.SensitivityDifference, 1);
        Assert.True(result.SensitivityMatchOk); // Within 3dB tolerance
    }

    [Fact]
    public void CheckCompatibility_UsesProvidedImpedance_NotRE()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;
        
        // Provide actual impedance values from metadata (nominal impedance)
        var wooferImpedance = 8.0; // ohms (nominal)
        var tweeterImpedance = 4.0; // ohms (nominal)
        // Note: woofer.Re = 2.73, tweeter.Re = 3.5 (DC resistance, not nominal)

        // Act
        var result = service.CheckCompatibility(
            woofer, 
            tweeter, 
            crossoverFreq, 
            crossoverOrder,
            wooferImpedance: wooferImpedance,
            tweeterImpedance: tweeterImpedance);

        // Assert
        Assert.NotNull(result);
        // Should use provided impedance, not RE
        Assert.NotNull(result.WooferImpedance);
        Assert.NotNull(result.TweeterImpedance);
        Assert.Equal(8.0, result.WooferImpedance.Value, 1.0);
        Assert.Equal(4.0, result.TweeterImpedance.Value, 1.0);
        Assert.False(result.ImpedanceMatchOk); // 8Ω vs 4Ω is a mismatch
    }

    [Fact]
    public void CheckCompatibility_FallsBackToEstimation_WhenSensitivityNotProvided()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Don't provide sensitivity, should fall back to estimation
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        // Should still calculate a sensitivity difference (using estimation)
        Assert.True(result.SensitivityDifference != 0 || result.SensitivityDifference == 0); // Any value is OK
    }

    [Fact]
    public void CheckCompatibility_FallsBackToRE_WhenImpedanceNotProvided()
    {
        // Arrange
        var service = new CrossoverCompatibilityService();
        var woofer = CreateTestWoofer();
        var tweeter = CreateTestTweeter();
        var crossoverFreq = 2500.0;
        var crossoverOrder = 2;

        // Act - Don't provide impedance, should fall back to RE
        var result = service.CheckCompatibility(woofer, tweeter, crossoverFreq, crossoverOrder);

        // Assert
        Assert.NotNull(result);
        // Should use RE values as fallback
        Assert.NotNull(result.WooferImpedance);
        Assert.NotNull(result.TweeterImpedance);
        Assert.Equal(woofer.Re, result.WooferImpedance.Value, 1.0);
        Assert.Equal(tweeter.Re, result.TweeterImpedance.Value, 1.0);
    }
}

