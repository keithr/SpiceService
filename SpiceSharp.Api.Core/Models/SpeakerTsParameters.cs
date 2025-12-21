namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Thiele/Small parameters for a speaker driver
/// </summary>
public class SpeakerTsParameters
{
    /// <summary>
    /// Free-air resonance frequency (Hz)
    /// </summary>
    public double Fs { get; set; }

    /// <summary>
    /// Total Q factor
    /// </summary>
    public double Qts { get; set; }

    /// <summary>
    /// Electrical Q factor
    /// </summary>
    public double Qes { get; set; }

    /// <summary>
    /// Mechanical Q factor
    /// </summary>
    public double Qms { get; set; }

    /// <summary>
    /// Equivalent air compliance (liters)
    /// </summary>
    public double Vas { get; set; }

    /// <summary>
    /// DC resistance (ohms)
    /// </summary>
    public double Re { get; set; }

    /// <summary>
    /// Voice coil inductance (mH)
    /// </summary>
    public double Le { get; set; }

    /// <summary>
    /// Force factor (T·m)
    /// </summary>
    public double Bl { get; set; }

    /// <summary>
    /// Maximum linear excursion (mm)
    /// </summary>
    public double Xmax { get; set; }

    /// <summary>
    /// Moving mass (grams)
    /// </summary>
    public double Mms { get; set; }

    /// <summary>
    /// Compliance (mm/N)
    /// </summary>
    public double Cms { get; set; }

    /// <summary>
    /// Effective piston area (cm²)
    /// </summary>
    public double Sd { get; set; }
}

