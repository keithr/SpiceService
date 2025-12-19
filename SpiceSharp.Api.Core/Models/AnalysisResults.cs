namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Result of an operating point analysis.
/// </summary>
public class OperatingPointResult
{
    /// <summary>
    /// Voltages at each node (V).
    /// </summary>
    public Dictionary<string, double> NodeVoltages { get; set; } = new();

    /// <summary>
    /// Currents through each component (A).
    /// </summary>
    public Dictionary<string, double> BranchCurrents { get; set; } = new();

    /// <summary>
    /// Power dissipation for each component (W).
    /// </summary>
    public Dictionary<string, double> PowerDissipation { get; set; } = new();

    /// <summary>
    /// Total power consumed by the circuit (W).
    /// </summary>
    public double TotalPower { get; set; }

    /// <summary>
    /// Number of iterations required for convergence.
    /// </summary>
    public int ConvergenceIterations { get; set; }

    /// <summary>
    /// Status of the analysis.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of a DC sweep analysis.
/// </summary>
public class DCAnalysisResult
{
    /// <summary>
    /// Name of the source being swept.
    /// </summary>
    public string SweepVariable { get; set; } = string.Empty;

    /// <summary>
    /// Sweep values for the independent variable.
    /// </summary>
    public List<double> SweepValues { get; set; } = new();

    /// <summary>
    /// Results for each exported signal.
    /// </summary>
    public Dictionary<string, List<double>> Results { get; set; } = new();

    /// <summary>
    /// Units for each measurement.
    /// </summary>
    public Dictionary<string, string> Units { get; set; } = new();

    /// <summary>
    /// Time taken for the analysis in milliseconds.
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the analysis.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of a transient analysis.
/// </summary>
public class TransientAnalysisResult
{
    /// <summary>
    /// Time points for the simulation (seconds).
    /// </summary>
    public List<double> Time { get; set; } = new();

    /// <summary>
    /// Signal values at each time point.
    /// </summary>
    public Dictionary<string, List<double>> Signals { get; set; } = new();

    /// <summary>
    /// Units for each signal.
    /// </summary>
    public Dictionary<string, string> Units { get; set; } = new();

    /// <summary>
    /// Time taken for the analysis in milliseconds.
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the analysis.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of an AC analysis.
/// </summary>
public class ACAnalysisResult
{
    /// <summary>
    /// Frequency points (Hz).
    /// </summary>
    public List<double> Frequencies { get; set; } = new();

    /// <summary>
    /// Magnitude in dB for each signal.
    /// </summary>
    public Dictionary<string, List<double>> MagnitudeDb { get; set; } = new();

    /// <summary>
    /// Phase in degrees for each signal.
    /// </summary>
    public Dictionary<string, List<double>> PhaseDegrees { get; set; } = new();

    /// <summary>
    /// Time taken for the analysis in milliseconds.
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the analysis.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of a group delay calculation.
/// </summary>
public class GroupDelayResult
{
    /// <summary>
    /// Frequency points (Hz).
    /// </summary>
    public List<double> Frequencies { get; set; } = new();

    /// <summary>
    /// Group delay values (seconds).
    /// </summary>
    public List<double> GroupDelay { get; set; } = new();

    /// <summary>
    /// Signal analyzed.
    /// </summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>
    /// Reference signal (if used).
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Analysis time in milliseconds.
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the calculation.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of a noise analysis.
/// </summary>
public class NoiseAnalysisResult
{
    /// <summary>
    /// Frequency points (Hz).
    /// </summary>
    public List<double> Frequencies { get; set; } = new();

    /// <summary>
    /// Output noise spectral density (V^2/Hz or V/√Hz depending on outputType).
    /// </summary>
    public List<double> OutputNoiseDensity { get; set; } = new();

    /// <summary>
    /// Input-referred noise spectral density (V^2/Hz or V/√Hz).
    /// </summary>
    public List<double> InputReferredNoiseDensity { get; set; } = new();

    /// <summary>
    /// Total integrated output noise (V^2 or V).
    /// </summary>
    public double TotalOutputNoise { get; set; }

    /// <summary>
    /// Total integrated input-referred noise (V^2 or V).
    /// </summary>
    public double TotalInputReferredNoise { get; set; }

    /// <summary>
    /// Output node name.
    /// </summary>
    public string OutputNode { get; set; } = string.Empty;

    /// <summary>
    /// Input source name.
    /// </summary>
    public string InputSource { get; set; } = string.Empty;

    /// <summary>
    /// Time taken for the analysis in milliseconds.
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the analysis.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of an impedance analysis.
/// </summary>
public class ImpedanceAnalysisResult
{
    /// <summary>
    /// Frequency points (Hz).
    /// </summary>
    public List<double> Frequencies { get; set; } = new();

    /// <summary>
    /// Impedance magnitude (Ω).
    /// </summary>
    public List<double> Magnitude { get; set; } = new();

    /// <summary>
    /// Impedance phase (degrees).
    /// </summary>
    public List<double> Phase { get; set; } = new();

    /// <summary>
    /// Positive terminal node of the port.
    /// </summary>
    public string PortPositive { get; set; } = string.Empty;

    /// <summary>
    /// Negative terminal node of the port.
    /// </summary>
    public string PortNegative { get; set; } = string.Empty;

    /// <summary>
    /// Time taken for the analysis in milliseconds.
    /// </summary>
    public double AnalysisTimeMs { get; set; }

    /// <summary>
    /// Status of the analysis.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
