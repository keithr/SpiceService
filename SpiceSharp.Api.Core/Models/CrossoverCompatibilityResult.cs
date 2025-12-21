namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Results of crossover compatibility analysis
/// </summary>
public class CrossoverCompatibilityResult
{
    /// <summary>
    /// Overall compatibility score (0 to 100, higher is better)
    /// </summary>
    public double CompatibilityScore { get; set; }

    /// <summary>
    /// Whether woofer beaming is acceptable at crossover frequency
    /// </summary>
    public bool WooferBeamingOk { get; set; }

    /// <summary>
    /// Maximum recommended crossover frequency for woofer (Hz)
    /// </summary>
    public double MaxCrossoverFrequency { get; set; }

    /// <summary>
    /// Whether tweeter Fs is acceptable (should be less than 0.5 times crossover frequency)
    /// </summary>
    public bool TweeterFsOk { get; set; }

    /// <summary>
    /// Minimum recommended crossover frequency for tweeter (Hz)
    /// </summary>
    public double MinCrossoverFrequency { get; set; }

    /// <summary>
    /// Sensitivity difference between woofer and tweeter (dB)
    /// </summary>
    public double SensitivityDifference { get; set; }

    /// <summary>
    /// Whether sensitivity match is acceptable (within 3dB ideal)
    /// </summary>
    public bool SensitivityMatchOk { get; set; }

    /// <summary>
    /// Whether impedance match is acceptable
    /// </summary>
    public bool ImpedanceMatchOk { get; set; }

    /// <summary>
    /// Woofer impedance (ohms)
    /// </summary>
    public double? WooferImpedance { get; set; }

    /// <summary>
    /// Tweeter impedance (ohms)
    /// </summary>
    public double? TweeterImpedance { get; set; }

    /// <summary>
    /// List of recommendations for improving compatibility
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// List of warnings about potential issues
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

