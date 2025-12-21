namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Sealed box design calculation results
/// </summary>
public class SealedBoxDesign
{
    /// <summary>
    /// Required box volume (liters)
    /// </summary>
    public double VolumeLiters { get; set; }

    /// <summary>
    /// Required box volume (cubic feet)
    /// </summary>
    public double VolumeCubicFeet { get; set; }

    /// <summary>
    /// System Q factor (Qtc)
    /// </summary>
    public double Qtc { get; set; }

    /// <summary>
    /// -3dB frequency (Hz)
    /// </summary>
    public double F3 { get; set; }

    /// <summary>
    /// System resonance frequency (Hz)
    /// </summary>
    public double Fc { get; set; }
}

