using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for checking crossover compatibility between speakers
/// </summary>
public class CrossoverCompatibilityService : ICrossoverCompatibilityService
{
    private const double BeamingConstant = 13750.0; // Hz·in (approximate beaming frequency formula)
    private const double TweeterFsRatio = 0.5; // Tweeter Fs should be < 0.5 × crossover frequency
    private const double IdealSensitivityTolerance = 3.0; // dB - ideal sensitivity difference
    private const double CmPerInch = 2.54;
    private const double CmSquaredPerInchSquared = CmPerInch * CmPerInch;

    /// <inheritdoc/>
    public CrossoverCompatibilityResult CheckCompatibility(
        SpeakerTsParameters woofer,
        SpeakerTsParameters tweeter,
        double crossoverFrequency,
        int crossoverOrder,
        double? wooferSensitivity = null,
        double? tweeterSensitivity = null,
        double? wooferImpedance = null,
        double? tweeterImpedance = null)
    {
        if (woofer == null)
            throw new ArgumentNullException(nameof(woofer));
        if (tweeter == null)
            throw new ArgumentNullException(nameof(tweeter));
        if (crossoverFrequency <= 0)
            throw new ArgumentException("Crossover frequency must be positive", nameof(crossoverFrequency));
        if (crossoverOrder < 1 || crossoverOrder > 4)
            throw new ArgumentException("Crossover order must be between 1 and 4", nameof(crossoverOrder));

        var result = new CrossoverCompatibilityResult();

        // Calculate woofer diameter from Sd (effective piston area)
        // Sd = π × (diameter/2)², so diameter = 2 × sqrt(Sd / π)
        // Convert from cm² to inches
        var wooferDiameterCm = 2.0 * Math.Sqrt(woofer.Sd / Math.PI);
        var wooferDiameterInches = wooferDiameterCm / CmPerInch;

        // Woofer beaming check: crossover_freq < 13750 / diameter_inches
        result.MaxCrossoverFrequency = BeamingConstant / wooferDiameterInches;
        result.WooferBeamingOk = crossoverFrequency < result.MaxCrossoverFrequency;

        // Tweeter Fs check: tweeter_fs < 0.5 × crossover_freq
        // Minimum crossover frequency = 2 × tweeter Fs
        result.MinCrossoverFrequency = 2.0 * tweeter.Fs;
        result.TweeterFsOk = crossoverFrequency >= result.MinCrossoverFrequency;

        // Sensitivity match check
        // Use provided sensitivity from metadata, or fall back to estimation if not available
        var wooferSens = wooferSensitivity ?? EstimateSensitivity(woofer);
        var tweeterSens = tweeterSensitivity ?? EstimateSensitivity(tweeter);
        result.SensitivityDifference = tweeterSens - wooferSens;
        result.SensitivityMatchOk = Math.Abs(result.SensitivityDifference) <= IdealSensitivityTolerance;

        // Impedance match check
        // Use provided nominal impedance from metadata, or fall back to RE if not available
        result.WooferImpedance = wooferImpedance ?? woofer.Re;
        result.TweeterImpedance = tweeterImpedance ?? tweeter.Re;
        result.ImpedanceMatchOk = Math.Abs((result.WooferImpedance ?? 0) - (result.TweeterImpedance ?? 0)) < 1.0;

        // Calculate compatibility score (0-100)
        result.CompatibilityScore = CalculateCompatibilityScore(result);

        // Generate recommendations and warnings
        GenerateRecommendations(result, wooferDiameterInches, crossoverFrequency, crossoverOrder);

        return result;
    }

    private static double EstimateSensitivity(SpeakerTsParameters speaker)
    {
        // Simplified sensitivity estimation based on T/S parameters
        // Real sensitivity would come from metadata
        // This is a rough approximation: higher BL and lower Re generally means higher sensitivity
        if (speaker.Re <= 0 || speaker.Bl <= 0)
            return 85.0; // Default assumption

        // Rough approximation: sensitivity ≈ 20 × log10(BL / sqrt(Re)) + 85
        // This is very simplified and not accurate, but provides a baseline
        var efficiencyFactor = speaker.Bl / Math.Sqrt(speaker.Re);
        return 85.0 + 20.0 * Math.Log10(Math.Max(0.1, efficiencyFactor));
    }

    private static double CalculateCompatibilityScore(CrossoverCompatibilityResult result)
    {
        double score = 100.0;

        // Deduct points for each issue
        if (!result.WooferBeamingOk)
            score -= 25.0; // Significant issue

        if (!result.TweeterFsOk)
            score -= 30.0; // Critical issue - can damage tweeter

        // Sensitivity mismatch penalty (gradual)
        var sensitivityPenalty = Math.Max(0, Math.Abs(result.SensitivityDifference) - IdealSensitivityTolerance) * 2.0;
        score -= Math.Min(20.0, sensitivityPenalty);

        // Impedance mismatch penalty (smaller)
        if (!result.ImpedanceMatchOk)
            score -= 10.0;

        return Math.Max(0, Math.Min(100, score));
    }

    private static void GenerateRecommendations(
        CrossoverCompatibilityResult result,
        double wooferDiameterInches,
        double crossoverFrequency,
        int crossoverOrder)
    {
        if (!result.WooferBeamingOk)
        {
            result.Warnings.Add(
                $"Woofer beaming may occur above {result.MaxCrossoverFrequency:F0} Hz. " +
                $"Current crossover ({crossoverFrequency:F0} Hz) is above this limit. " +
                $"Consider lowering crossover frequency or using a smaller woofer.");
            
            result.Recommendations.Add(
                $"Lower crossover frequency to {result.MaxCrossoverFrequency * 0.9:F0} Hz or below to avoid beaming.");
        }

        if (!result.TweeterFsOk)
        {
            result.Warnings.Add(
                $"CRITICAL: Crossover frequency ({crossoverFrequency:F0} Hz) is too close to tweeter Fs. " +
                $"Minimum recommended crossover is {result.MinCrossoverFrequency:F0} Hz. " +
                $"This can cause tweeter damage at high power.");
            
            result.Recommendations.Add(
                $"Increase crossover frequency to at least {result.MinCrossoverFrequency:F0} Hz (2× tweeter Fs).");
        }

        if (!result.SensitivityMatchOk)
        {
            var diff = result.SensitivityDifference;
            if (diff > 0)
            {
                result.Recommendations.Add(
                    $"Tweeter is {diff:F1} dB more sensitive than woofer. " +
                    $"Add {diff:F1} dB attenuation to tweeter or increase woofer sensitivity.");
            }
            else
            {
                result.Recommendations.Add(
                    $"Woofer is {Math.Abs(diff):F1} dB more sensitive than tweeter. " +
                    $"Add {Math.Abs(diff):F1} dB attenuation to woofer or increase tweeter sensitivity.");
            }
        }

        if (!result.ImpedanceMatchOk)
        {
            result.Recommendations.Add(
                $"Impedance mismatch: Woofer {result.WooferImpedance:F1}Ω, Tweeter {result.TweeterImpedance:F1}Ω. " +
                $"Ensure crossover network accounts for impedance differences.");
        }

        // General recommendations
        if (result.CompatibilityScore >= 80)
        {
            result.Recommendations.Add("Good compatibility. Proceed with design.");
        }
        else if (result.CompatibilityScore >= 60)
        {
            result.Recommendations.Add("Moderate compatibility. Review warnings and consider adjustments.");
        }
        else
        {
            result.Recommendations.Add("Poor compatibility. Significant design changes recommended.");
        }
    }
}

