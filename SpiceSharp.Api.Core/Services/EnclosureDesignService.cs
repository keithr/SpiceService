using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for calculating speaker enclosure designs
/// </summary>
public class EnclosureDesignService : IEnclosureDesignService
{
    private const double LitersPerCubicFoot = 28.3168;
    private const double CmPerInch = 2.54;
    private const double MaxPortVelocity = 17.0; // m/s - above this, port may chuff

    /// <inheritdoc/>
    public SealedBoxDesign CalculateSealedBox(SpeakerTsParameters speaker, double targetQtc)
    {
        if (speaker == null)
            throw new ArgumentNullException(nameof(speaker));
        
        if (targetQtc <= 0 || targetQtc > 2.0)
            throw new ArgumentException("Target Qtc must be between 0 and 2.0", nameof(targetQtc));

        // Sealed box formula: Vb = Vas / (α² - 1)
        // where α = Qtc / Qts
        var alpha = targetQtc / speaker.Qts;
        var alphaSquared = alpha * alpha;
        
        if (alphaSquared <= 1.0)
            throw new ArgumentException($"Target Qtc ({targetQtc}) must be greater than Qts ({speaker.Qts})", nameof(targetQtc));

        var volumeLiters = speaker.Vas / (alphaSquared - 1.0);
        var volumeCubicFeet = volumeLiters / LitersPerCubicFoot;

        // System resonance frequency: Fc = Fs × α
        var fc = speaker.Fs * alpha;

        // -3dB frequency: F3 = Fc × sqrt((1/Qtc²) + 2 - sqrt((1/Qtc²) + 4))
        // Simplified approximation: F3 ≈ Fc × 0.707 for Butterworth
        var f3 = fc * Math.Sqrt((1.0 / (targetQtc * targetQtc)) + 2.0 - 
                                 Math.Sqrt((1.0 / (targetQtc * targetQtc)) + 4.0));

        return new SealedBoxDesign
        {
            VolumeLiters = volumeLiters,
            VolumeCubicFeet = volumeCubicFeet,
            Qtc = targetQtc,
            F3 = f3,
            Fc = fc
        };
    }

    /// <inheritdoc/>
    public VentedBoxDesign CalculateVentedBox(SpeakerTsParameters speaker, string alignment)
    {
        if (speaker == null)
            throw new ArgumentNullException(nameof(speaker));

        double volumeLiters;
        double fb;

        switch (alignment.ToUpperInvariant())
        {
            case "QB3":
                // QB3 alignment: Vb = 15 × Qts³ × Vas
                // Fb = Fs / (Qts × 1.4)
                volumeLiters = 15.0 * Math.Pow(speaker.Qts, 3) * speaker.Vas;
                fb = speaker.Fs / (speaker.Qts * 1.4);
                break;

            case "B4":
                // B4 alignment: Vb = 20 × Qts³ × Vas
                // Fb = Fs / (Qts × 1.2)
                volumeLiters = 20.0 * Math.Pow(speaker.Qts, 3) * speaker.Vas;
                fb = speaker.Fs / (speaker.Qts * 1.2);
                break;

            case "SBB4":
                // SBB4 alignment: Vb = 10 × Qts³ × Vas
                // Fb = Fs / (Qts × 1.6)
                volumeLiters = 10.0 * Math.Pow(speaker.Qts, 3) * speaker.Vas;
                fb = speaker.Fs / (speaker.Qts * 1.6);
                break;

            case "C4":
                // C4 alignment: Vb = 5 × Qts³ × Vas
                // Fb = Fs / (Qts × 2.0)
                volumeLiters = 5.0 * Math.Pow(speaker.Qts, 3) * speaker.Vas;
                fb = speaker.Fs / (speaker.Qts * 2.0);
                break;

            default:
                throw new ArgumentException($"Unknown alignment type: {alignment}. Supported: QB3, B4, SBB4, C4", nameof(alignment));
        }

        var volumeCubicFeet = volumeLiters / LitersPerCubicFoot;

        // Calculate -3dB frequency (approximation for vented boxes)
        var f3 = fb * 0.7; // Rough approximation

        // Calculate port dimensions
        // Port area should be roughly Sd/3 to Sd/4
        var portArea = speaker.Sd / 3.5; // cm²
        var portDiameterCm = Math.Sqrt((4.0 * portArea) / Math.PI);
        var portDiameterInches = portDiameterCm / CmPerInch;

        // Port length calculation (simplified - actual formula is more complex)
        // Lv ≈ (23562.5 × Dv² × Np) / (Fb² × Vb) - (0.732 × Dv)
        // Simplified: Lv ≈ (c² × Sd) / (4π² × Fb² × Vb) - 0.85 × Dv
        // where c = 343 m/s (speed of sound)
        var speedOfSound = 34300.0; // cm/s
        var portLengthCm = (speedOfSound * speedOfSound * portArea) / 
                          (4.0 * Math.PI * Math.PI * fb * fb * volumeLiters * 1000.0) - 
                          (0.85 * portDiameterCm);
        
        // Ensure minimum port length
        if (portLengthCm < portDiameterCm)
            portLengthCm = portDiameterCm * 1.5;

        var portLengthInches = portLengthCm / CmPerInch;

        // Calculate port velocity (simplified - assumes 1W input)
        // V = (0.5 × sqrt(P × ρ)) / (π × r²)
        // Simplified warning calculation
        double? maxPortVelocity = null;
        string? portVelocityWarning = null;
        
        // Rough estimate: if port is too small relative to Sd, velocity will be high
        if (portArea < speaker.Sd / 5.0)
        {
            maxPortVelocity = 25.0; // Estimated high velocity
            portVelocityWarning = $"Port velocity may exceed {MaxPortVelocity} m/s at high power. Consider larger port diameter.";
        }

        return new VentedBoxDesign
        {
            VolumeLiters = volumeLiters,
            VolumeCubicFeet = volumeCubicFeet,
            Fb = fb,
            F3 = f3,
            PortDiameterCm = portDiameterCm,
            PortDiameterInches = portDiameterInches,
            PortLengthCm = portLengthCm,
            PortLengthInches = portLengthInches,
            MaxPortVelocity = maxPortVelocity,
            PortVelocityWarning = portVelocityWarning
        };
    }
}

