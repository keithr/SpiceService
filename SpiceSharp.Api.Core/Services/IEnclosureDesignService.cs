using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for calculating speaker enclosure designs
/// </summary>
public interface IEnclosureDesignService
{
    /// <summary>
    /// Calculates sealed box design parameters
    /// </summary>
    /// <param name="speaker">Speaker T/S parameters</param>
    /// <param name="targetQtc">Target Qtc value (typically 0.707 for Butterworth, 0.577 for Bessel, 1.0 for Critically Damped)</param>
    /// <returns>Sealed box design results</returns>
    SealedBoxDesign CalculateSealedBox(SpeakerTsParameters speaker, double targetQtc);

    /// <summary>
    /// Calculates vented box design parameters
    /// </summary>
    /// <param name="speaker">Speaker T/S parameters</param>
    /// <param name="alignment">Vented box alignment type (QB3, B4, SBB4, etc.)</param>
    /// <returns>Vented box design results</returns>
    VentedBoxDesign CalculateVentedBox(SpeakerTsParameters speaker, string alignment);
}

