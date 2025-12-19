using SpiceSharp.Api.Core.Models;
using System.Globalization;
using System.Text;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for exporting analysis results to various formats
/// </summary>
public class ExportService : IExportService
{
    /// <inheritdoc/>
    public string ExportToCSV(DCAnalysisResult result, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        if (options.IncludeMetadata)
        {
            sb.AppendLine("# DC Sweep Analysis Results");
            sb.AppendLine($"# Sweep Variable: {result.SweepVariable}");
            sb.AppendLine($"# Status: {result.Status}");
            sb.AppendLine($"# Analysis Time: {result.AnalysisTimeMs} ms");
            if (result.Results.Any())
            {
                sb.AppendLine($"# Exports: {string.Join(", ", result.Results.Keys)}");
            }
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
        }

        if (result.SweepValues == null || result.SweepValues.Count == 0)
        {
            return sb.ToString();
        }

        // Build header
        if (options.IncludeHeaders)
        {
            var headers = new List<string> { result.SweepVariable };
            headers.AddRange(result.Results.Keys);
            sb.AppendLine(string.Join(options.Delimiter, headers));
        }

        // Build data rows
        for (int i = 0; i < result.SweepValues.Count; i++)
        {
            var row = new List<string>
            {
                FormatNumber(result.SweepValues[i], options)
            };

            foreach (var export in result.Results.Values)
            {
                if (i < export.Count)
                {
                    row.Add(FormatNumber(export[i], options));
                }
                else
                {
                    row.Add("");
                }
            }

            sb.AppendLine(string.Join(options.Delimiter, row));
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string ExportToCSV(ACAnalysisResult result, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        if (options.IncludeMetadata)
        {
            sb.AppendLine("# AC Analysis Results");
            sb.AppendLine($"# Status: {result.Status}");
            sb.AppendLine($"# Analysis Time: {result.AnalysisTimeMs} ms");
            if (result.MagnitudeDb.Any())
            {
                sb.AppendLine($"# Signals: {string.Join(", ", result.MagnitudeDb.Keys)}");
            }
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
        }

        if (result.Frequencies == null || result.Frequencies.Count == 0)
        {
            return sb.ToString();
        }

        // Build header
        if (options.IncludeHeaders)
        {
            var headers = new List<string> { "Frequency (Hz)" };
            foreach (var signal in result.MagnitudeDb.Keys)
            {
                headers.Add($"{signal} Magnitude (dB)");
                if (result.PhaseDegrees.ContainsKey(signal))
                {
                    headers.Add($"{signal} Phase (deg)");
                }
            }
            sb.AppendLine(string.Join(options.Delimiter, headers));
        }

        // Build data rows
        for (int i = 0; i < result.Frequencies.Count; i++)
        {
            var row = new List<string>
            {
                FormatNumber(result.Frequencies[i], options)
            };

            foreach (var signal in result.MagnitudeDb.Keys)
            {
                if (i < result.MagnitudeDb[signal].Count)
                {
                    row.Add(FormatNumber(result.MagnitudeDb[signal][i], options));
                }
                else
                {
                    row.Add("");
                }

                if (result.PhaseDegrees.ContainsKey(signal))
                {
                    if (i < result.PhaseDegrees[signal].Count)
                    {
                        row.Add(FormatNumber(result.PhaseDegrees[signal][i], options));
                    }
                    else
                    {
                        row.Add("");
                    }
                }
            }

            sb.AppendLine(string.Join(options.Delimiter, row));
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string ExportToCSV(TransientAnalysisResult result, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        if (options.IncludeMetadata)
        {
            sb.AppendLine("# Transient Analysis Results");
            sb.AppendLine($"# Status: {result.Status}");
            sb.AppendLine($"# Analysis Time: {result.AnalysisTimeMs} ms");
            if (result.Signals.Any())
            {
                sb.AppendLine($"# Signals: {string.Join(", ", result.Signals.Keys)}");
            }
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
        }

        if (result.Time == null || result.Time.Count == 0)
        {
            return sb.ToString();
        }

        // Build header
        if (options.IncludeHeaders)
        {
            var headers = new List<string> { "Time (s)" };
            headers.AddRange(result.Signals.Keys);
            sb.AppendLine(string.Join(options.Delimiter, headers));
        }

        // Build data rows
        for (int i = 0; i < result.Time.Count; i++)
        {
            var row = new List<string>
            {
                FormatNumber(result.Time[i], options)
            };

            foreach (var signal in result.Signals.Values)
            {
                if (i < signal.Count)
                {
                    row.Add(FormatNumber(signal[i], options));
                }
                else
                {
                    row.Add("");
                }
            }

            sb.AppendLine(string.Join(options.Delimiter, row));
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string ExportToCSV(OperatingPointResult result, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        if (options.IncludeMetadata)
        {
            sb.AppendLine("# Operating Point Analysis Results");
            sb.AppendLine($"# Status: {result.Status}");
            sb.AppendLine($"# Convergence Iterations: {result.ConvergenceIterations}");
            sb.AppendLine($"# Total Power: {result.TotalPower} W");
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
        }

        // Node Voltages section
        if (result.NodeVoltages != null && result.NodeVoltages.Any())
        {
            if (options.IncludeHeaders)
            {
                sb.AppendLine("Node Voltages");
                sb.AppendLine($"Node{options.Delimiter}Voltage (V)");
            }

            foreach (var kvp in result.NodeVoltages)
            {
                sb.AppendLine($"{kvp.Key}{options.Delimiter}{FormatNumber(kvp.Value, options)}");
            }

            sb.AppendLine();
        }

        // Branch Currents section
        if (result.BranchCurrents != null && result.BranchCurrents.Any())
        {
            if (options.IncludeHeaders)
            {
                sb.AppendLine("Branch Currents");
                sb.AppendLine($"Component{options.Delimiter}Current (A)");
            }

            foreach (var kvp in result.BranchCurrents)
            {
                sb.AppendLine($"{kvp.Key}{options.Delimiter}{FormatNumber(kvp.Value, options)}");
            }

            sb.AppendLine();
        }

        // Power Dissipation section
        if (result.PowerDissipation != null && result.PowerDissipation.Any())
        {
            if (options.IncludeHeaders)
            {
                sb.AppendLine("Power Dissipation");
                sb.AppendLine($"Component{options.Delimiter}Power (W)");
            }

            foreach (var kvp in result.PowerDissipation)
            {
                sb.AppendLine($"{kvp.Key}{options.Delimiter}{FormatNumber(kvp.Value, options)}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Format a number according to the export options
    /// </summary>
    private string FormatNumber(double value, ExportOptions options)
    {
        // Use G format for general (handles scientific notation automatically)
        // or use F format with precision for fixed decimal places
        if (options.NumberFormat == "G")
        {
            return value.ToString("G", CultureInfo.InvariantCulture);
        }
        else if (options.NumberFormat == "F")
        {
            return value.ToString($"F{options.Precision}", CultureInfo.InvariantCulture);
        }
        else
        {
            // Custom format string
            return value.ToString(options.NumberFormat, CultureInfo.InvariantCulture);
        }
    }
}

