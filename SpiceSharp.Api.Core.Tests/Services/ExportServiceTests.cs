using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ExportService
/// </summary>
public class ExportServiceTests
{
    private readonly ExportService _exportService;

    public ExportServiceTests()
    {
        _exportService = new ExportService();
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_ReturnsFormattedCSV()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0, 0.1, 0.2 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 0.0, 1.234e-9, 5.678e-8 } },
                { "v(anode)", new List<double> { 0.0, 0.1, 0.2 } }
            },
            Units = new Dictionary<string, string>
            {
                { "i(D1)", "A" },
                { "v(anode)", "V" }
            },
            Status = "Success"
        };

        // Act
        var csv = _exportService.ExportToCSV(result);

        // Assert
        Assert.NotNull(csv);
        Assert.Contains("V1", csv); // Should contain sweep variable name
        Assert.Contains("i(D1)", csv); // Should contain export names
        Assert.Contains("v(anode)", csv);
        Assert.Contains("0", csv); // Should contain data (0.0 may be formatted as 0)
        Assert.Contains("1.234", csv); // Should contain scientific notation data
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_IncludesHeaders_WhenOptionSet()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0, 0.1 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 0.0, 1e-9 } }
            }
        };
        var options = new ExportOptions { IncludeHeaders = true };

        // Act
        var csv = _exportService.ExportToCSV(result, options);

        // Assert
        Assert.Contains("V1", csv); // Header should be present
        Assert.Contains("i(D1)", csv);
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_ExcludesHeaders_WhenOptionSet()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0, 0.1 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 0.0, 1e-9 } }
            }
        };
        var options = new ExportOptions { IncludeHeaders = false };

        // Act
        var csv = _exportService.ExportToCSV(result, options);

        // Assert
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // First non-comment line should be data, not header
        var firstDataLine = lines.FirstOrDefault(l => !l.TrimStart().StartsWith("#"));
        Assert.NotNull(firstDataLine);
        Assert.DoesNotContain("V1", firstDataLine); // Header should not be in data line
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_UsesCustomDelimiter_WhenProvided()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0, 0.1 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 0.0, 1e-9 } }
            }
        };
        var options = new ExportOptions { Delimiter = ";" };

        // Act
        var csv = _exportService.ExportToCSV(result, options);

        // Assert
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headerLine = lines.FirstOrDefault(l => l.Contains("V1") && !l.TrimStart().StartsWith("#"));
        Assert.NotNull(headerLine);
        Assert.Contains(";", headerLine); // Should use semicolon delimiter
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_PreservesPrecision_WhenScientificNotation()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 1.23456789012345e-15 } }
            }
        };

        // Act
        var csv = _exportService.ExportToCSV(result);

        // Assert
        Assert.Contains("1.23456789012345", csv); // Should preserve precision
    }

    [Fact]
    public void ExportToCSV_ACAnalysisResult_ReturnsFormattedCSV()
    {
        // Arrange
        var result = new ACAnalysisResult
        {
            Frequencies = new List<double> { 1000.0, 2000.0 },
            MagnitudeDb = new Dictionary<string, List<double>>
            {
                { "v(out)", new List<double> { -3.0, -6.0 } }
            },
            PhaseDegrees = new Dictionary<string, List<double>>
            {
                { "v(out)", new List<double> { 45.0, 90.0 } }
            },
            Status = "Success"
        };

        // Act
        var csv = _exportService.ExportToCSV(result);

        // Assert
        Assert.NotNull(csv);
        Assert.Contains("Frequency", csv);
        Assert.Contains("v(out)", csv);
        Assert.Contains("1000", csv);
    }

    [Fact]
    public void ExportToCSV_TransientAnalysisResult_ReturnsFormattedCSV()
    {
        // Arrange
        var result = new TransientAnalysisResult
        {
            Time = new List<double> { 0.0, 1e-3, 2e-3 },
            Signals = new Dictionary<string, List<double>>
            {
                { "v(out)", new List<double> { 0.0, 3.3, 0.0 } }
            },
            Status = "Success"
        };

        // Act
        var csv = _exportService.ExportToCSV(result);

        // Assert
        Assert.NotNull(csv);
        Assert.Contains("Time", csv);
        Assert.Contains("v(out)", csv);
        Assert.Contains("0.0", csv);
    }

    [Fact]
    public void ExportToCSV_OperatingPointResult_ReturnsFormattedCSV()
    {
        // Arrange
        var result = new OperatingPointResult
        {
            NodeVoltages = new Dictionary<string, double>
            {
                { "anode", 2.5 },
                { "cathode", 0.0 }
            },
            BranchCurrents = new Dictionary<string, double>
            {
                { "i(D1)", 0.02 }
            },
            Status = "Success"
        };

        // Act
        var csv = _exportService.ExportToCSV(result);

        // Assert
        Assert.NotNull(csv);
        Assert.Contains("anode", csv);
        Assert.Contains("2.5", csv);
        Assert.Contains("i(D1)", csv);
        Assert.Contains("0.02", csv);
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_IncludesMetadata_WhenOptionSet()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 0.0 } }
            },
            Status = "Success"
        };
        var options = new ExportOptions { IncludeMetadata = true };

        // Act
        var csv = _exportService.ExportToCSV(result, options);

        // Assert
        Assert.Contains("#", csv); // Should contain comment lines
    }

    [Fact]
    public void ExportToCSV_DCAnalysisResult_ExcludesMetadata_WhenOptionSet()
    {
        // Arrange
        var result = new DCAnalysisResult
        {
            SweepVariable = "V1",
            SweepValues = new List<double> { 0.0 },
            Results = new Dictionary<string, List<double>>
            {
                { "i(D1)", new List<double> { 0.0 } }
            }
        };
        var options = new ExportOptions { IncludeMetadata = false };

        // Act
        var csv = _exportService.ExportToCSV(result, options);

        // Assert
        Assert.DoesNotContain("#", csv); // Should not contain comment lines
    }
}

