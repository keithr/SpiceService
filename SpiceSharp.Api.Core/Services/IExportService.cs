using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for exporting analysis results to various formats
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export DC analysis result to CSV format
    /// </summary>
    string ExportToCSV(DCAnalysisResult result, ExportOptions? options = null);

    /// <summary>
    /// Export AC analysis result to CSV format
    /// </summary>
    string ExportToCSV(ACAnalysisResult result, ExportOptions? options = null);

    /// <summary>
    /// Export transient analysis result to CSV format
    /// </summary>
    string ExportToCSV(TransientAnalysisResult result, ExportOptions? options = null);

    /// <summary>
    /// Export operating point result to CSV format
    /// </summary>
    string ExportToCSV(OperatingPointResult result, ExportOptions? options = null);
}

/// <summary>
/// Options for exporting analysis results
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// CSV delimiter (default: comma)
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Whether to include headers in the CSV (default: true)
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Number of decimal places for formatting (default: 15 for precision)
    /// </summary>
    public int Precision { get; set; } = 15;

    /// <summary>
    /// Number format string (default: "G" for general format)
    /// </summary>
    public string NumberFormat { get; set; } = "G";

    /// <summary>
    /// Whether to include metadata comments (default: true)
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
}

