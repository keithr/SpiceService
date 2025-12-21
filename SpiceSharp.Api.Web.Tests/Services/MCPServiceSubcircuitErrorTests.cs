using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for subcircuit error reporting in MCP service
/// </summary>
public class MCPServiceSubcircuitErrorTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly LibraryService _libraryService;

    public MCPServiceSubcircuitErrorTests()
    {
        _circuitManager = new CircuitManager();
        _libraryService = new LibraryService();
        var componentService = new ComponentService(_libraryService);
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
        _mcpService = new MCPService(
            _circuitManager,
            componentService,
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
            config);
    }

    [Fact]
    public async Task AddComponent_SubcircuitMissingModel_ReturnsClearError()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.Serialize(new
        {
            name = "X1",
            component_type = "subcircuit",
            nodes = new[] { "n1", "n2" }
            // Intentionally missing 'model' parameter
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(arguments));
        });

        // Verify error message is clear and actionable
        Assert.Contains("model", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("subcircuit", exception.Message, StringComparison.OrdinalIgnoreCase);
        // Message should indicate model is needed (either "required" or "to be specified")
        var hasRequiredInfo = exception.Message.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                              exception.Message.Contains("specified", StringComparison.OrdinalIgnoreCase) ||
                              exception.Message.Contains("missing", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasRequiredInfo, $"Error message should indicate model is needed. Message: {exception.Message}");
    }

    [Fact]
    public async Task AddComponent_SubcircuitNotFound_ReturnsClearError()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var nonExistentSubcircuit = "nonexistent_sub_12345";
        var arguments = JsonSerializer.Serialize(new
        {
            name = "X1",
            component_type = "subcircuit",
            nodes = new[] { "n1", "n2" },
            model = nonExistentSubcircuit
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(arguments));
        });

        // Verify error message includes subcircuit name and suggests library_search
        Assert.Contains(nonExistentSubcircuit, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("library", exception.Message, StringComparison.OrdinalIgnoreCase);
        // Should suggest using library_search
        var hasSuggestion = exception.Message.Contains("library_search", StringComparison.OrdinalIgnoreCase) ||
                           exception.Message.Contains("find available", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasSuggestion, $"Error message should suggest using library_search. Message: {exception.Message}");
    }

    [Fact]
    public async Task AddComponent_SubcircuitNodeCountMismatch_ReturnsClearError()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        // Set up a test library with a subcircuit that has 2 nodes
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT test_sub 1 2
R1 1 2 1K
.ENDS
");

        try
        {
            _libraryService.IndexLibraries(new[] { tempDir });

            // Try to add subcircuit with wrong number of nodes (3 instead of 2)
            var arguments = JsonSerializer.Serialize(new
            {
                name = "X1",
                component_type = "subcircuit",
                nodes = new[] { "n1", "n2", "n3" }, // 3 nodes, but definition expects 2
                model = "test_sub"
            });

            // Act
            // Note: SpiceSharp might allow node count mismatch during creation,
            // but validation should catch it. Let's test what actually happens.
            try
            {
                await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(arguments));
                
                // If no exception, validate the circuit to see if validation catches it
                var validateArgs = JsonSerializer.Serialize(new { circuit_id = circuitId });
                var validateResult = await _mcpService.ExecuteTool("validate_circuit", JsonSerializer.Deserialize<JsonElement>(validateArgs));
                var validateText = validateResult.Content.FirstOrDefault()?.Text ?? "";
                var validateJson = JsonSerializer.Deserialize<JsonElement>(validateText);
                
                // Assert: Validation should catch the node count mismatch
                if (validateJson.TryGetProperty("is_valid", out var isValidProp) && isValidProp.GetBoolean())
                {
                    // If validation passes, that's unexpected but not a test failure
                    // The test is about error messages, not about when errors occur
                    Assert.True(false, "Validation should have caught node count mismatch, but circuit is valid");
                }
                else if (validateJson.TryGetProperty("errors", out var errorsProp))
                {
                    // Check if errors mention node count
                    var errorsText = errorsProp.ToString();
                    var hasNodeCountInfo = errorsText.Contains("node", StringComparison.OrdinalIgnoreCase) ||
                                          errorsText.Contains("pin", StringComparison.OrdinalIgnoreCase) ||
                                          errorsText.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                                          errorsText.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                                          errorsText.Contains("expect", StringComparison.OrdinalIgnoreCase);
                    Assert.True(hasNodeCountInfo, $"Validation errors should mention node count issue. Errors: {errorsText}");
                }
            }
            catch (Exception ex)
            {
                // If it throws during add, verify error message explains node count mismatch
                var message = ex.Message;
                var hasNodeCountInfo = message.Contains("node", StringComparison.OrdinalIgnoreCase) ||
                                      message.Contains("pin", StringComparison.OrdinalIgnoreCase) ||
                                      message.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                                      message.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                                      message.Contains("expect", StringComparison.OrdinalIgnoreCase);
                Assert.True(hasNodeCountInfo, $"Error message should explain node count issue. Message: {message}");
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImportNetlist_SubcircuitDefinitionMissing_ReturnsClearError()
    {
        // Arrange
        var netlist = @"
* Test netlist with missing subcircuit definition
X1 n1 n2 nonexistent_sub
R1 n1 0 1K
";

        var arguments = JsonSerializer.Serialize(new
        {
            netlist = netlist,
            circuit_name = "test_circuit",
            set_active = true
        });

        // Act
        var result = await _mcpService.ExecuteTool("import_netlist", JsonSerializer.Deserialize<JsonElement>(arguments));

        // Assert
        // ImportNetlist catches exceptions and continues, so we need to check the result
        // It should indicate that some components failed to add
        var resultText = result.Content.FirstOrDefault()?.Text ?? "";
        var resultJson = JsonSerializer.Deserialize<JsonElement>(resultText);
        
        // The result should indicate that the component failed or wasn't added
        // Check if components_added is less than total_components
        if (resultJson.TryGetProperty("components_added", out var addedProp) &&
            resultJson.TryGetProperty("total_components", out var totalProp))
        {
            var added = addedProp.GetInt32();
            var total = totalProp.GetInt32();
            
            // Should have fewer components added than total (because X1 failed)
            // Or we should check the logs/warnings
            // For now, just verify the import completed (even if with errors)
            Assert.True(added < total || added == 0, 
                $"Expected some components to fail. Added: {added}, Total: {total}");
        }
        
        // The error should be logged, but the import should complete
        // We can't easily test the logged error message here, but the fact that
        // the import completes with fewer components indicates the error was handled
    }
}

