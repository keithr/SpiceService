using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for checking crossover compatibility between speakers
/// </summary>
public interface ICrossoverCompatibilityService
{
    /// <summary>
    /// Checks compatibility between a woofer and tweeter for a given crossover configuration
    /// </summary>
    /// <param name="woofer">Woofer T/S parameters</param>
    /// <param name="tweeter">Tweeter T/S parameters</param>
    /// <param name="crossoverFrequency">Crossover frequency in Hz</param>
    /// <param name="crossoverOrder">Crossover order (1, 2, 3, or 4)</param>
    /// <param name="wooferSensitivity">Woofer sensitivity in dB (from metadata, not estimated)</param>
    /// <param name="tweeterSensitivity">Tweeter sensitivity in dB (from metadata, not estimated)</param>
    /// <param name="wooferImpedance">Woofer nominal impedance in ohms (from metadata, not RE)</param>
    /// <param name="tweeterImpedance">Tweeter nominal impedance in ohms (from metadata, not RE)</param>
    /// <returns>Compatibility analysis results</returns>
    CrossoverCompatibilityResult CheckCompatibility(
        SpeakerTsParameters woofer,
        SpeakerTsParameters tweeter,
        double crossoverFrequency,
        int crossoverOrder,
        double? wooferSensitivity = null,
        double? tweeterSensitivity = null,
        double? wooferImpedance = null,
        double? tweeterImpedance = null);
}

