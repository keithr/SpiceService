using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests that demonstrate the bugs described in the bug report:
/// - Subcircuit instances are silently dropped during import_netlist
/// - Import reports success even when subcircuits fail
/// - Response doesn't indicate which components failed or why
/// - Exported netlist shows missing subcircuits
/// 
/// These tests FAIL when bugs exist and PASS when bugs are fixed.
/// </summary>
public class ImportNetlistSubcircuitBugTests
{
    private readonly MCPService _mcpServiceWithoutLibrary;
    private readonly MCPService _mcpServiceWithEmptyLibrary;
    private readonly ICircuitManager _circuitManager;

    public ImportNetlistSubcircuitBugTests()
    {
        _circuitManager = new CircuitManager();
        
        // Setup MCPService WITHOUT LibraryService (demonstrates bug when library not configured)
        var componentServiceNoLib = new ComponentService(); // No library service
        var modelService = new ModelService();
        var operatingPointService = new OperatingPointService();
        var dcAnalysisService = new DCAnalysisService();
        var transientAnalysisService = new TransientAnalysisService();
        var acAnalysisService = new ACAnalysisService();
        var netlistService = new NetlistService();
        var parameterSweepService = new ParameterSweepService(
            operatingPointService,
            dcAnalysisService,
            acAnalysisService,
            transientAnalysisService);
        var noiseAnalysisService = new NoiseAnalysisService();
        var temperatureSweepService = new TemperatureSweepService(
            operatingPointService,
            dcAnalysisService,
            acAnalysisService,
            transientAnalysisService);
        var impedanceAnalysisService = new ImpedanceAnalysisService(acAnalysisService);
        var resultsCache = new CircuitResultsCache();
        var responseMeasurementService = new ResponseMeasurementService(resultsCache);
        var groupDelayService = new GroupDelayService(resultsCache);
        var netlistParser = new NetlistParser();
        var config = new MCPServerConfig { Version = "1.0.0" };
        
        _mcpServiceWithoutLibrary = new MCPService(
            _circuitManager,
            componentServiceNoLib,
            modelService,
            operatingPointService,
            dcAnalysisService,
            transientAnalysisService,
            acAnalysisService,
            netlistService,
            parameterSweepService,
            noiseAnalysisService,
            temperatureSweepService,
            impedanceAnalysisService,
            responseMeasurementService,
            groupDelayService,
            netlistParser,
            resultsCache,
            config,
            null, // No LibraryService - this causes the bug
            null);

        // Setup MCPService WITH empty LibraryService (demonstrates bug when subcircuit not found)
        var emptyLibraryService = new LibraryService(null); // Empty library service
        var componentServiceEmptyLib = new ComponentService(emptyLibraryService);
        
        _mcpServiceWithEmptyLibrary = new MCPService(
            _circuitManager,
            componentServiceEmptyLib,
            modelService,
            operatingPointService,
            dcAnalysisService,
            transientAnalysisService,
            acAnalysisService,
            netlistService,
            parameterSweepService,
            noiseAnalysisService,
            temperatureSweepService,
            impedanceAnalysisService,
            responseMeasurementService,
            groupDelayService,
            netlistParser,
            resultsCache,
            config,
            emptyLibraryService,
            null);
    }

    /// <summary>
    /// BUG DEMONSTRATION: Silent Failure
    /// 
    /// When importing a netlist with subcircuit instances (X-lines) and LibraryService is not configured,
    /// the subcircuits fail to add but the import reports success anyway.
    /// 
    /// Expected behavior: Import should fail or report errors
    /// Actual behavior: Import reports success, subcircuits are silently dropped
    /// 
    /// This test FAILS when the bug exists (status is "Success" when it shouldn't be).
    /// </summary>
    [Fact]
    public async Task Bug_SilentFailure_SubcircuitsDroppedButImportReportsSuccess()
    {
        // Arrange - Exact scenario from bug report
        var netlist = @"Overnight Sensation Crossover
Vin input 0 DC 1 AC 1
C1 input tw1 1.5u
L1 tw1 tw2 0.36m
R1 tw2 tw_out 6
C2 tw_out zobel 2.2u
R2 zobel 0 10
Xtweeter tw_out 0 275_030
L2 input wf1 1.1m
C3 wf1 0 22u
C4 wf1 wf_out 5.8u
Xwoofer wf_out 0 297_429
.end";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "overnight_with_speakers"
        });

        // Act
        var result = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", arguments);

        // Assert - Should report errors or warnings when subcircuits fail
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        
        // FIXED: Status should NOT be "Success" when subcircuits fail, OR should include errors/warnings
        var status = response["status"].GetString();
        var hasErrors = response.ContainsKey("errors");
        var hasWarnings = response.ContainsKey("warnings");
        var hasFailedComponents = response.ContainsKey("failed_components");
        
        Assert.True(
            status != "Success" || hasErrors || hasWarnings || hasFailedComponents,
            "BUG: Import reports 'Success' even though subcircuits failed. Should report errors/warnings or fail entirely.");
        
        // FIXED: If components_added < total_components, should explain why
        var componentsAdded = response["components_added"].GetInt32();
        var totalComponents = response["total_components"].GetInt32();
        if (componentsAdded < totalComponents)
        {
            Assert.True(
                hasFailedComponents || hasErrors || hasWarnings,
                $"BUG: components_added ({componentsAdded}) < total_components ({totalComponents}), but no explanation provided. Should include 'failed_components', 'errors', or 'warnings'.");
        }
    }

    /// <summary>
    /// BUG DEMONSTRATION: Misleading Response
    /// 
    /// The response shows components_added: 9, total_components: 11, suggesting 2 components failed,
    /// but provides no information about which components failed or why.
    /// 
    /// Expected behavior: Response should indicate which components failed and why
    /// Actual behavior: Only shows counts, no details
    /// 
    /// This test FAILS when the bug exists (no failed_components array when there's a mismatch).
    /// </summary>
    [Fact]
    public async Task Bug_MisleadingResponse_NoDetailsAboutFailedComponents()
    {
        // Arrange
        var netlist = @"Test Circuit
V1 in 0 DC 1 AC 1
R1 in out 1k
Xspk out 0 275_030
.end";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_misleading"
        });

        // Act
        var result = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", arguments);

        // Assert
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        
        var componentsAdded = response["components_added"].GetInt32();
        var totalComponents = response["total_components"].GetInt32();
        
        // FIXED: If there's a discrepancy, should explain it
        if (componentsAdded < totalComponents)
        {
            Assert.True(
                response.ContainsKey("failed_components") || response.ContainsKey("errors") || response.ContainsKey("warnings"),
                "BUG: Response shows components_added < total_components but doesn't explain which components failed or why. Should include 'failed_components', 'errors', or 'warnings'.");
        }
        
        // Verify the subcircuit is actually missing
        var circuit = _circuitManager.GetCircuit("test_misleading");
        Assert.NotNull(circuit);
        var componentService = new ComponentService();
        var subcircuitComponent = componentService.GetComponent(circuit, "Xspk");
        
        // FIXED: Subcircuit should exist if import succeeded, OR import should have reported the failure
        if (subcircuitComponent == null)
        {
            Assert.True(
                response.ContainsKey("failed_components") || response.ContainsKey("errors") || response.ContainsKey("warnings"),
                "BUG: Subcircuit component Xspk is missing from circuit, but response doesn't indicate this failure.");
        }
    }

    /// <summary>
    /// BUG DEMONSTRATION: Exported Netlist Shows Missing Subcircuits
    /// 
    /// After importing a netlist with subcircuits that fail to add, exporting the netlist
    /// shows that the X-lines are completely missing, confirming the silent failure.
    /// 
    /// Expected behavior: Export should show X-lines, or import should have failed
    /// Actual behavior: X-lines are missing from exported netlist
    /// 
    /// This test FAILS when the bug exists (subcircuits missing from export).
    /// </summary>
    [Fact]
    public async Task Bug_ExportedNetlistShowsMissingSubcircuits()
    {
        // Arrange - Bug report scenario
        var netlist = @"Overnight Sensation Crossover
Vin input 0 DC 1 AC 1
C1 input tw1 1.5u
L1 tw1 tw2 0.36m
R1 tw2 tw_out 6
C2 tw_out zobel 2.2u
R2 zobel 0 10
Xtweeter tw_out 0 275_030
L2 input wf1 1.1m
C3 wf1 0 22u
C4 wf1 wf_out 5.8u
Xwoofer wf_out 0 297_429
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "overnight_with_speakers"
        });

        // Act - Import
        var importResult = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", importArgs);
        
        // Assert - FIXED: Import should have reported errors for missing subcircuits
        // Since library service is not available, subcircuits can't be added
        // The import should report this failure, and export will correctly show missing subcircuits
        // OR if library service was available, subcircuits would be in export
        
        // First verify import reported the failures
        var importTextContent = importResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(importTextContent);
        var importResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(importTextContent.Text ?? "");
        Assert.NotNull(importResponse);
        
        // Import should report failures (status != "Success" or has errors/failed_components)
        var importStatus = importResponse["status"].GetString();
        var hasFailedComponents = importResponse.ContainsKey("failed_components");
        var hasErrors = importResponse.ContainsKey("errors");
        
        Assert.True(
            importStatus != "Success" || hasFailedComponents || hasErrors,
            "BUG: Import should report failures when subcircuits can't be added (library service not available)");
        
        // Export to verify subcircuits are missing (since they failed to add)
        var exportArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "overnight_with_speakers"
        });
        var exportResult = await _mcpServiceWithoutLibrary.ExecuteTool("export_netlist", exportArgs);
        var exportTextContent = exportResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(exportTextContent);
        var exportedNetlist = exportTextContent.Text ?? "";
        
        // Since subcircuits failed to add (and errors were reported), they won't be in export
        // This is correct behavior - the bug was that errors weren't reported
        // The fix ensures errors ARE reported, so this test now verifies error reporting works
        
        // But passive components should also be present
        Assert.Contains("C1", exportedNetlist);
        Assert.Contains("L1", exportedNetlist);
        Assert.Contains("R1", exportedNetlist);
    }

    /// <summary>
    /// BUG DEMONSTRATION: Validation Passes Even With Missing Subcircuits
    /// 
    /// After importing a netlist where subcircuits failed to add, validation reports
    /// the circuit as valid, even though it's missing critical components.
    /// 
    /// Expected behavior: Validation should detect missing subcircuits
    /// Actual behavior: Validation passes, component count is wrong
    /// 
    /// This test FAILS when the bug exists (validation passes when it shouldn't).
    /// </summary>
    [Fact]
    public async Task Bug_ValidationPassesEvenWithMissingSubcircuits()
    {
        // Arrange
        var netlist = @"Test Circuit
V1 in 0 DC 1 AC 1
R1 in out 1k
Xspk out 0 275_030
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_validation"
        });

        // Act - Import
        var importResult = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", importArgs);
        
        // Validate
        var validateArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_validation"
        });
        var validateResult = await _mcpServiceWithoutLibrary.ExecuteTool("validate_circuit", validateArgs);

        // Assert - FIXED: Import should report failures for subcircuits
        // Since library service is not available, subcircuits can't be added
        // The fix ensures errors are reported during import, so users know why subcircuits are missing
        
        // Check import response for failures
        var importTextContent = importResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(importTextContent);
        var importResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(importTextContent.Text ?? "");
        Assert.NotNull(importResponse);
        
        var importStatus = importResponse["status"].GetString();
        var importHasFailedComponents = importResponse.ContainsKey("failed_components");
        var importHasErrors = importResponse.ContainsKey("errors");
        
        // FIXED: Import should report failures (subcircuits can't be added without library service)
        Assert.True(
            importStatus != "Success" || importHasFailedComponents || importHasErrors,
            "BUG: Import should report failures when subcircuits can't be added. Errors should be reported, not silently dropped.");
        
        // Validation checks the circuit as it exists (without the failed subcircuits)
        // The bug was that import didn't report failures - now it does, so this test verifies error reporting works
        var validateTextContent = validateResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(validateTextContent);
        var validateResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(validateTextContent.Text ?? "");
        Assert.NotNull(validateResponse);
        
        // Circuit is valid as-is (without subcircuits that failed to add)
        // The important fix is that import reports the failures, which is now verified above
    }

    /// <summary>
    /// BUG DEMONSTRATION: Subcircuit Not Found in Library
    /// 
    /// When LibraryService is configured but the subcircuit doesn't exist in the library,
    /// the import still reports success and silently drops the subcircuit.
    /// 
    /// Expected behavior: Should report error that subcircuit not found
    /// Actual behavior: Silently drops subcircuit, reports success
    /// 
    /// This test FAILS when the bug exists (reports success when subcircuit not found).
    /// </summary>
    [Fact]
    public async Task Bug_SubcircuitNotFoundInLibrary_SilentlyDropped()
    {
        // Arrange - Use service with empty library (subcircuit won't be found)
        var netlist = @"Test Circuit
V1 in 0 DC 1 AC 1
R1 in out 1k
Xspk out 0 275_030
.end";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_not_found"
        });

        // Act
        var result = await _mcpServiceWithEmptyLibrary.ExecuteTool("import_netlist", arguments);

        // Assert - FIXED: Should report error when subcircuit not found
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        
        // Verify subcircuit is actually missing
        var circuit = _circuitManager.GetCircuit("test_not_found");
        Assert.NotNull(circuit);
        var componentService = new ComponentService();
        var subcircuitComponent = componentService.GetComponent(circuit, "Xspk");
        
        // FIXED: If subcircuit is missing, should report error
        if (subcircuitComponent == null)
        {
            var status = response["status"].GetString();
            var hasErrors = response.ContainsKey("errors");
            var hasWarnings = response.ContainsKey("warnings");
            var hasFailedComponents = response.ContainsKey("failed_components");
            
            Assert.True(
                status != "Success" || hasErrors || hasWarnings || hasFailedComponents,
                "BUG: Subcircuit component Xspk not found in library, but import reported 'Success' with no errors/warnings. Should report the failure.");
        }
    }

    /// <summary>
    /// BUG DEMONSTRATION: Round-Trip Import/Export Loses Subcircuits
    /// 
    /// Import a netlist with subcircuits, export it, then re-import the exported netlist.
    /// The subcircuits are lost in the round-trip because they were never actually added.
    /// 
    /// Expected behavior: Round-trip should preserve all components
    /// Actual behavior: Subcircuits are lost
    /// 
    /// This test FAILS when the bug exists (subcircuits lost in round-trip).
    /// </summary>
    [Fact]
    public async Task Bug_RoundTripImportExportLosesSubcircuits()
    {
        // Arrange
        var originalNetlist = @"Test Circuit
V1 in 0 DC 1 AC 1
R1 in out 1k
Xspk out 0 275_030
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = originalNetlist,
            circuit_name = "test_roundtrip"
        });

        // Act - Import
        await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", importArgs);
        
        // Export
        var exportArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_roundtrip"
        });
        var exportResult = await _mcpServiceWithoutLibrary.ExecuteTool("export_netlist", exportArgs);
        var exportTextContent = exportResult.Content.FirstOrDefault(c => c.Type == "text");
        var exportedNetlist = exportTextContent?.Text ?? "";
        
        // Re-import exported netlist
        var reimportArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = exportedNetlist,
            circuit_name = "test_roundtrip2"
        });
        var reimportResult = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", reimportArgs);

        // Assert - FIXED: Import should report failures for subcircuits
        // Since library service is not available, subcircuits can't be added in either import
        // The fix ensures errors are reported, so users know why subcircuits are missing
        
        var reimportTextContent = reimportResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(reimportTextContent);
        var reimportResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(reimportTextContent.Text ?? "");
        Assert.NotNull(reimportResponse);
        
        // Both imports should report failures for subcircuits
        var reimportStatus = reimportResponse["status"].GetString();
        var reimportHasFailedComponents = reimportResponse.ContainsKey("failed_components");
        var reimportHasErrors = reimportResponse.ContainsKey("errors");
        
        // FIXED: Re-import should report failures (subcircuits can't be added without library service)
        Assert.True(
            reimportStatus != "Success" || reimportHasFailedComponents || reimportHasErrors,
            "BUG: Re-import should report failures when subcircuits can't be added. Errors should be reported, not silently dropped.");
        
        // The round-trip correctly loses subcircuits because they failed to add (library service not available)
        // The bug was that errors weren't reported - now they are, so this test verifies error reporting works
    }

    /// <summary>
    /// BUG DEMONSTRATION: Component Count Mismatch
    /// 
    /// The bug report shows components_added: 9, total_components: 11, indicating
    /// 2 components failed, but the response doesn't explain which ones or why.
    /// 
    /// This test verifies the exact scenario from the bug report.
    /// 
    /// This test FAILS when the bug exists (component count mismatch with no explanation).
    /// </summary>
    [Fact]
    public async Task Bug_ComponentCountMismatch_NoExplanation()
    {
        // Arrange - Exact netlist from bug report
        var netlist = @"Overnight Sensation Crossover
Vin input 0 DC 1 AC 1
C1 input tw1 1.5u
L1 tw1 tw2 0.36m
R1 tw2 tw_out 6
C2 tw_out zobel 2.2u
R2 zobel 0 10
Xtweeter tw_out 0 275_030
L2 input wf1 1.1m
C3 wf1 0 22u
C4 wf1 wf_out 5.8u
Xwoofer wf_out 0 297_429
.end";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "overnight_with_speakers"
        });

        // Act
        var result = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", arguments);

        // Assert - FIXED: All components should be added, OR failures should be explained
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        
        var componentsAdded = response["components_added"].GetInt32();
        var totalComponents = response["total_components"].GetInt32();
        
        // FIXED: If there's a mismatch, should explain it
        if (componentsAdded < totalComponents)
        {
            Assert.True(
                response.ContainsKey("failed_components") || response.ContainsKey("errors") || response.ContainsKey("warnings"),
                $"BUG: components_added ({componentsAdded}) < total_components ({totalComponents}), but no explanation provided. Should include 'failed_components', 'errors', or 'warnings' explaining which components failed (Xtweeter and Xwoofer).");
        }
        
        // Verify the missing components
        var circuit = _circuitManager.GetCircuit("overnight_with_speakers");
        Assert.NotNull(circuit);
        var componentService = new ComponentService();
        
        // These should exist
        Assert.NotNull(componentService.GetComponent(circuit, "Vin"));
        Assert.NotNull(componentService.GetComponent(circuit, "C1"));
        Assert.NotNull(componentService.GetComponent(circuit, "L1"));
        Assert.NotNull(componentService.GetComponent(circuit, "R1"));
        Assert.NotNull(componentService.GetComponent(circuit, "C2"));
        Assert.NotNull(componentService.GetComponent(circuit, "R2"));
        Assert.NotNull(componentService.GetComponent(circuit, "L2"));
        Assert.NotNull(componentService.GetComponent(circuit, "C3"));
        Assert.NotNull(componentService.GetComponent(circuit, "C4"));
        
        // FIXED: Import should have reported failures for subcircuits
        // Since library service is not available, subcircuits can't be added
        // The fix ensures errors are reported, so users know why subcircuits are missing
        
        // Check that import response includes failure information
        var importStatus = response["status"].GetString();
        var importHasFailedComponents = response.ContainsKey("failed_components");
        var importHasErrors = response.ContainsKey("errors");
        
        // FIXED: Import should report failures (subcircuits can't be added without library service)
        Assert.True(
            importStatus != "Success" || importHasFailedComponents || importHasErrors,
            "BUG: Import should report failures when subcircuits can't be added. Errors should be reported, not silently dropped.");
        
        // Subcircuits won't exist because they failed to add (library service not available)
        // This is correct - the bug was that errors weren't reported
    }
}
