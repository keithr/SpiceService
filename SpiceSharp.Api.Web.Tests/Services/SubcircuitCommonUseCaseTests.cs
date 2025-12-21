using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests covering common real-world use cases for subcircuits.
/// These tests ensure the most common workflows work correctly.
/// </summary>
public class SubcircuitCommonUseCaseTests
{
    private readonly MCPService _mcpService;
    private readonly MCPService _mcpServiceWithoutLibrary;
    private readonly ICircuitManager _circuitManager;
    private readonly ILibraryService _libraryService;

    public SubcircuitCommonUseCaseTests()
    {
        _circuitManager = new CircuitManager();
        
        // Create library service with test subcircuit definitions
        var speakerDb = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db"));
        speakerDb.InitializeDatabase();
        _libraryService = new LibraryService(speakerDb);
        
        // Create test subcircuit definitions matching real-world scenarios
        var tweeterSubcircuit = new SubcircuitDefinition
        {
            Name = "275_030",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.001
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "Dayton Audio" },
                { "PRODUCT_NAME", "ND20FA-6 3/4\" Soft Dome Neodymium Tweeter" },
                { "TYPE", "tweeters" }
            }
        };

        var wooferSubcircuit = new SubcircuitDefinition
        {
            Name = "297_429",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* HiVi B4N 4"" Aluminum Round Frame Midbass
.SUBCKT 297_429 PLUS MINUS
Re PLUS 1 5.5
Le 1 2 0.002
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "HiVi" },
                { "PRODUCT_NAME", "B4N 4\" Aluminum Round Frame Midbass" },
                { "TYPE", "woofers" }
            }
        };

        // Index the test subcircuits
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        File.WriteAllText(Path.Combine(tempLibPath, "tweeter.lib"), tweeterSubcircuit.Definition);
        File.WriteAllText(Path.Combine(tempLibPath, "woofer.lib"), wooferSubcircuit.Definition);
        
        _libraryService.IndexLibraries(new[] { tempLibPath });

        // Create MCPService WITH library service (properly wired)
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
        var config = new MCPServerConfig { LibraryPaths = new[] { tempLibPath } };
        var speakerDatabaseService = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db"));
        speakerDatabaseService.InitializeDatabase();
        
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
            config,
            _libraryService,
            speakerDatabaseService);

        // Create MCPService WITHOUT library service (for error testing)
        var componentServiceNoLib = new ComponentService(null);
        _mcpServiceWithoutLibrary = new MCPService(
            new CircuitManager(),
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
            new MCPServerConfig(),
            null,
            null);
    }

    /// <summary>
    /// Common Use Case 1: library_search → import_netlist → run_ac_analysis
    /// This is the most common workflow: find a speaker, import a circuit with it, analyze
    /// </summary>
    [Fact]
    public async Task CommonUseCase_SearchImportAnalyze_WorksEndToEnd()
    {
        // Step 1: Search for subcircuit
        var searchArgs = JsonSerializer.SerializeToElement(new { query = "275_030" });
        var searchResult = await _mcpService.ExecuteTool("library_search", searchArgs);
        Assert.NotNull(searchResult);
        var searchText = searchResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var searchJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(searchText);
        Assert.NotNull(searchJson);
        Assert.True(searchJson["count"].GetInt32() >= 1);

        // Step 2: Import netlist with the subcircuit
        var netlist = @"Test Circuit
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end";
        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "search_import_analyze",
            set_active = true
        });
        var importResult = await _mcpService.ExecuteTool("import_netlist", importArgs);
        Assert.NotNull(importResult);
        var importText = importResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var importJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(importText);
        Assert.NotNull(importJson);
        Assert.Equal("Success", importJson["status"].GetString());
        Assert.Equal(2, importJson["components_added"].GetInt32());

        // Step 3: Run AC analysis
        var acArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "search_import_analyze",
            start_frequency = 20.0,
            stop_frequency = 20000.0,
            number_of_points = 100,
            signals = new[] { "v(in)" }
        });
        var acResult = await _mcpService.ExecuteTool("run_ac_analysis", acArgs);
        Assert.NotNull(acResult);
        var acText = acResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        Assert.Contains("v(in)", acText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Common Use Case 2: Import netlist with multiple different subcircuits
    /// Real-world scenario: Crossover circuit with tweeter and woofer
    /// </summary>
    [Fact]
    public async Task CommonUseCase_ImportNetlistWithMultipleDifferentSubcircuits_ShouldSucceed()
    {
        var netlist = @"Overnight Sensation Crossover
Vin input 0 DC 1 AC 1
C1 input tw1 1.5u
L1 tw1 tw2 0.36m
R1 tw2 tw_out 6
Xtweeter tw_out 0 275_030
L2 input wf1 1.1m
C2 wf1 0 22u
Xwoofer wf_out 0 297_429
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "multi_subcircuit",
            set_active = true
        });
        var result = await _mcpService.ExecuteTool("import_netlist", importArgs);
        Assert.NotNull(result);
        var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
        Assert.NotNull(json);
        Assert.Equal("Success", json["status"].GetString());
        Assert.True(json["components_added"].GetInt32() >= 7); // V1, C1, L1, R1, Xtweeter, L2, C2, Xwoofer

        // Verify both subcircuits are in the circuit
        var circuit = _circuitManager.GetCircuit("multi_subcircuit");
        Assert.NotNull(circuit);
        var componentService = new ComponentService(_libraryService);
        Assert.NotNull(componentService.GetComponent(circuit, "Xtweeter"));
        Assert.NotNull(componentService.GetComponent(circuit, "Xwoofer"));
    }

    /// <summary>
    /// Common Use Case 3: Import netlist when LibraryService is NOT wired
    /// Should report clear error (not silent failure)
    /// </summary>
    [Fact]
    public async Task CommonUseCase_ImportNetlistWhenLibraryServiceNotWired_ShouldReportClearError()
    {
        var netlist = @"Test Circuit
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "no_lib_import",
            set_active = true
        });
        var result = await _mcpServiceWithoutLibrary.ExecuteTool("import_netlist", importArgs);
        Assert.NotNull(result);
        var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
        Assert.NotNull(json);
        
        // Should report Partial Success with failed_components
        Assert.True(json.ContainsKey("status"));
        var status = json["status"].GetString();
        Assert.True(status == "Partial Success" || status == "Failed");
        
        // Should have failed_components array
        Assert.True(json.ContainsKey("failed_components"));
        var failedComponents = json["failed_components"];
        Assert.True(failedComponents.ValueKind == JsonValueKind.Array);
        
        // Error message should mention library service
        var failedComponentsText = failedComponents.ToString();
        Assert.Contains("library service", failedComponentsText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Common Use Case 4: Add component with subcircuit that doesn't exist in library
    /// Should report clear error (not silent failure)
    /// </summary>
    [Fact]
    public async Task CommonUseCase_AddComponentWithNonExistentSubcircuit_ShouldReportClearError()
    {
        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "non_existent_subcircuit",
            make_active = true
        });
        await _mcpService.ExecuteTool("create_circuit", createArgs);

        var addArgs = JsonSerializer.SerializeToElement(new
        {
            name = "Xspk",
            component_type = "subcircuit",
            model = "NON_EXISTENT_12345",
            nodes = new[] { "in", "0" }
        });

        // Should throw exception with clear error message
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("add_component", addArgs));
        
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("library", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Common Use Case 5: Import netlist with subcircuit that doesn't exist in library
    /// Should report clear error in failed_components
    /// </summary>
    [Fact]
    public async Task CommonUseCase_ImportNetlistWithNonExistentSubcircuit_ShouldReportClearError()
    {
        var netlist = @"Test Circuit
V1 in 0 DC 1 AC 1
Xspk in 0 NON_EXISTENT_12345
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "non_existent_import",
            set_active = true
        });
        var result = await _mcpService.ExecuteTool("import_netlist", importArgs);
        Assert.NotNull(result);
        var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
        Assert.NotNull(json);
        
        // Should report Partial Success (V1 added, Xspk failed)
        Assert.Equal("Partial Success", json["status"].GetString());
        Assert.Equal(1, json["components_added"].GetInt32()); // Only V1
        
        // Should have failed_components
        Assert.True(json.ContainsKey("failed_components"));
        var failedComponents = json["failed_components"];
        Assert.True(failedComponents.ValueKind == JsonValueKind.Array);
        
        // Error should mention subcircuit not found
        var failedComponentsText = failedComponents.ToString();
        Assert.Contains("not found", failedComponentsText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Common Use Case 6: Subcircuit with custom node names (not PLUS/MINUS)
    /// Real-world: Some subcircuits use different node naming conventions
    /// </summary>
    [Fact]
    public async Task CommonUseCase_SubcircuitWithCustomNodeNames_ShouldWork()
    {
        // Create subcircuit with custom node names
        var customSubcircuit = new SubcircuitDefinition
        {
            Name = "custom_nodes",
            Nodes = new List<string> { "INPUT", "OUTPUT", "GND" },
            Definition = @"
* Custom Node Names Subcircuit
.SUBCKT custom_nodes INPUT OUTPUT GND
R1 INPUT OUTPUT 100
C1 OUTPUT GND 1u
.ENDS
",
            Metadata = new Dictionary<string, string>()
        };

        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_custom_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        File.WriteAllText(Path.Combine(tempLibPath, "custom.lib"), customSubcircuit.Definition);
        _libraryService.IndexLibraries(new[] { tempLibPath });

        var netlist = @"Test Custom Nodes
V1 in 0 DC 1 AC 1
X1 in out 0 custom_nodes
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "custom_nodes_test",
            set_active = true
        });
        var result = await _mcpService.ExecuteTool("import_netlist", importArgs);
        Assert.NotNull(result);
        var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
        Assert.NotNull(json);
        Assert.Equal("Success", json["status"].GetString());
        Assert.Equal(2, json["components_added"].GetInt32());
    }

    /// <summary>
    /// Common Use Case 7: Complete Overnight Sensation crossover with both speakers
    /// This is the exact scenario from the bug report
    /// </summary>
    [Fact]
    public async Task CommonUseCase_OvernightSensationCrossover_CompleteCircuit_Works()
    {
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
            circuit_name = "overnight_complete",
            set_active = true
        });
        var importResult = await _mcpService.ExecuteTool("import_netlist", importArgs);
        Assert.NotNull(importResult);
        var importText = importResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var importJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(importText);
        Assert.NotNull(importJson);
        
        // Should succeed with all components
        Assert.Equal("Success", importJson["status"].GetString());
        Assert.Equal(11, importJson["components_added"].GetInt32()); // Vin + 9 passives + 2 subcircuits
        
        // Verify export includes both subcircuits
        var exportArgs = JsonSerializer.SerializeToElement(new { circuit_id = "overnight_complete" });
        var exportResult = await _mcpService.ExecuteTool("export_netlist", exportArgs);
        Assert.NotNull(exportResult);
        var exportText = exportResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        Assert.Contains("Xtweeter", exportText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", exportText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Xwoofer", exportText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("297_429", exportText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Common Use Case 8: Round-trip import/export/re-import with subcircuits
    /// Ensures subcircuits are preserved through export/import cycle
    /// </summary>
    [Fact]
    public async Task CommonUseCase_RoundTripImportExportReimport_PreservesSubcircuits()
    {
        // Step 1: Import original netlist
        var originalNetlist = @"Test Round Trip
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end";
        var import1Args = JsonSerializer.SerializeToElement(new
        {
            netlist = originalNetlist,
            circuit_name = "roundtrip_1",
            set_active = true
        });
        var import1Result = await _mcpService.ExecuteTool("import_netlist", import1Args);
        Assert.NotNull(import1Result);
        var import1Text = import1Result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var import1Json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(import1Text);
        Assert.Equal("Success", import1Json["status"].GetString());

        // Step 2: Export
        var exportArgs = JsonSerializer.SerializeToElement(new { circuit_id = "roundtrip_1" });
        var exportResult = await _mcpService.ExecuteTool("export_netlist", exportArgs);
        Assert.NotNull(exportResult);
        var exportedNetlist = exportResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        Assert.Contains("Xspk", exportedNetlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", exportedNetlist, StringComparison.OrdinalIgnoreCase);

        // Step 3: Re-import exported netlist
        var import2Args = JsonSerializer.SerializeToElement(new
        {
            netlist = exportedNetlist,
            circuit_name = "roundtrip_2",
            set_active = true
        });
        var import2Result = await _mcpService.ExecuteTool("import_netlist", import2Args);
        Assert.NotNull(import2Result);
        var import2Text = import2Result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var import2Json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(import2Text);
        Assert.Equal("Success", import2Json["status"].GetString());
        Assert.Equal(2, import2Json["components_added"].GetInt32());

        // Step 4: Verify subcircuit is still there
        var export2Args = JsonSerializer.SerializeToElement(new { circuit_id = "roundtrip_2" });
        var export2Result = await _mcpService.ExecuteTool("export_netlist", export2Args);
        Assert.NotNull(export2Result);
        var exported2Netlist = export2Result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        Assert.Contains("Xspk", exported2Netlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", exported2Netlist, StringComparison.OrdinalIgnoreCase);
    }
}

