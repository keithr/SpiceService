namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Vented box design calculation results
/// </summary>
public class VentedBoxDesign
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
    /// Tuning frequency (Hz)
    /// </summary>
    public double Fb { get; set; }

    /// <summary>
    /// -3dB frequency (Hz)
    /// </summary>
    public double F3 { get; set; }

    /// <summary>
    /// Port diameter (inches)
    /// </summary>
    public double PortDiameterInches { get; set; }

    /// <summary>
    /// Port length (inches)
    /// </summary>
    public double PortLengthInches { get; set; }

    /// <summary>
    /// Port diameter (cm)
    /// </summary>
    public double PortDiameterCm { get; set; }

    /// <summary>
    /// Port length (cm)
    /// </summary>
    public double PortLengthCm { get; set; }

    /// <summary>
    /// Maximum port velocity warning (m/s) - if exceeded, port may chuff
    /// </summary>
    public double? MaxPortVelocity { get; set; }

    /// <summary>
    /// Warning message if port velocity is too high
    /// </summary>
    public string? PortVelocityWarning { get; set; }
}

