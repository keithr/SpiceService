using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Integration tests for subcircuit end-to-end workflows
/// </summary>
public class SubcircuitIntegrationTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ILibraryService _libraryService;

    public SubcircuitIntegrationTests()
    {
        _circuitManager = new CircuitManager();
        
        // Create library service with test subcircuit definitions
        var speakerDb = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db"));
        speakerDb.InitializeDatabase();
        _libraryService = new LibraryService(speakerDb);
        
        // Create test subcircuit definitions
        var testSubcircuit = new SubcircuitDefinition
        {
            Name = "test_speaker",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* Test Speaker Subcircuit
.SUBCKT test_speaker PLUS MINUS
Re PLUS 1 5.5
Le 1 2 0.002
Ce 2 MINUS 0.0001
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "Test" },
                { "PRODUCT_NAME", "Test Speaker" },
                { "TYPE", "woofers" },
                { "DIAMETER", "4" },
                { "IMPEDANCE", "8" },
                { "SENSITIVITY", "85" }
            }
        };

        // Index the test subcircuit
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        // Write test library file
        File.WriteAllText(Path.Combine(tempLibPath, "test_speaker.lib"), testSubcircuit.Definition);
        
        _libraryService.IndexLibraries(new[] { tempLibPath });

        // Create services with library service
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
    public async Task FullWorkflow_SearchAddSimulate_WorksEndToEnd()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        // Act - Step 1: Search for subcircuit
        var searchArgs = JsonSerializer.Serialize(new
        {
            query = "test_speaker"
        });
        var searchResult = await _mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        Assert.NotNull(searchResult);
        
        // Step 2: Add subcircuit to circuit
        var addArgs = JsonSerializer.Serialize(new
        {
            name = "X1",
            component_type = "subcircuit",
            nodes = new[] { "out", "0" },
            model = "test_speaker"
        });
        var addResult = await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(addArgs));
        Assert.NotNull(addResult);

        // Step 3: Add voltage source for AC analysis
        var voltageArgs = JsonSerializer.Serialize(new
        {
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "out", "0" },
            value = 1.0,
            parameters = new Dictionary<string, object> { { "ac", 1 } }
        });
        var voltageResult = await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(voltageArgs));
        Assert.NotNull(voltageResult);

        // Step 4: Run AC analysis
        var acArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId,
            start_frequency = 20.0,
            stop_frequency = 20000.0,
            number_of_points = 100,
            signals = new[] { "v(out)" }
        });
        var acResult = await _mcpService.ExecuteTool("run_ac_analysis", JsonSerializer.Deserialize<JsonElement>(acArgs));
        Assert.NotNull(acResult);

        // Assert - Verify analysis succeeded
        var acResultText = acResult.Content.FirstOrDefault()?.Text ?? "";
        Assert.NotEmpty(acResultText);
        // Should contain analysis results
        Assert.Contains("v(out)", acResultText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullWorkflow_ImportExportRoundTrip_PreservesSubcircuits()
    {
        // Arrange
        var originalNetlist = @"
* Test circuit with subcircuit
V1 input 0 1 AC 1
X1 input 0 test_speaker
";

        // Act - Step 1: Import netlist
        var importArgs = JsonSerializer.Serialize(new
        {
            netlist = originalNetlist,
            circuit_name = "imported_circuit",
            set_active = true
        });
        var importResult = await _mcpService.ExecuteTool("import_netlist", JsonSerializer.Deserialize<JsonElement>(importArgs));
        Assert.NotNull(importResult);
        
        var importText = importResult.Content.FirstOrDefault()?.Text ?? "";
        var importJson = JsonSerializer.Deserialize<JsonElement>(importText);
        var circuitId = importJson.GetProperty("circuit_id").GetString();
        Assert.NotNull(circuitId);

        // Step 2: Export netlist
        var exportArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId
        });
        var exportResult = await _mcpService.ExecuteTool("export_netlist", JsonSerializer.Deserialize<JsonElement>(exportArgs));
        Assert.NotNull(exportResult);
        
        var exportText = exportResult.Content.FirstOrDefault()?.Text ?? "";
        // ExportNetlist returns the netlist as plain text, not JSON
        var exportedNetlist = exportText;
        Assert.NotNull(exportedNetlist);

        // Step 3: Verify subcircuit is in exported netlist
        Assert.Contains("X1", exportedNetlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test_speaker", exportedNetlist, StringComparison.OrdinalIgnoreCase);

        // Step 4: Re-import exported netlist
        var reimportArgs = JsonSerializer.Serialize(new
        {
            netlist = exportedNetlist,
            circuit_name = "reimported_circuit",
            set_active = true
        });
        var reimportResult = await _mcpService.ExecuteTool("import_netlist", JsonSerializer.Deserialize<JsonElement>(reimportArgs));
        Assert.NotNull(reimportResult);

        // Step 5: Run analysis on re-imported circuit
        var reimportText = reimportResult.Content.FirstOrDefault()?.Text ?? "";
        var reimportJson = JsonSerializer.Deserialize<JsonElement>(reimportText);
        var reimportCircuitId = reimportJson.GetProperty("circuit_id").GetString();
        Assert.NotNull(reimportCircuitId);

        var acArgs = JsonSerializer.Serialize(new
        {
            circuit_id = reimportCircuitId,
            start_frequency = 20.0,
            stop_frequency = 20000.0,
            number_of_points = 100,
            signals = new[] { "v(input)" }
        });
        var acResult = await _mcpService.ExecuteTool("run_ac_analysis", JsonSerializer.Deserialize<JsonElement>(acArgs));
        Assert.NotNull(acResult);

        // Assert - Verify analysis succeeded
        var acResultText = acResult.Content.FirstOrDefault()?.Text ?? "";
        Assert.NotEmpty(acResultText);
        Assert.Contains("v(input)", acResultText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OvernightSensationCrossover_CompleteCircuit_Works()
    {
        // Arrange - Create complete crossover circuit with both speakers
        var circuitId = "crossover_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Overnight Sensation Crossover");
        _circuitManager.SetActiveCircuit(circuitId);

        // Create tweeter subcircuit definition
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
"
        };

        // Create woofer subcircuit definition
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
"
        };

        // Index subcircuits
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        File.WriteAllText(Path.Combine(tempLibPath, "tweeter.lib"), tweeterSubcircuit.Definition);
        File.WriteAllText(Path.Combine(tempLibPath, "woofer.lib"), wooferSubcircuit.Definition);
        _libraryService.IndexLibraries(new[] { tempLibPath });

        // Act - Build complete crossover circuit
        // Input voltage source
        var v1Args = JsonSerializer.Serialize(new
        {
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "input", "0" },
            value = 1.0,
            parameters = new Dictionary<string, object> { { "ac", 1 } }
        });
        await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(v1Args));

        // Crossover components (simplified)
        var c1Args = JsonSerializer.Serialize(new
        {
            name = "C1",
            component_type = "capacitor",
            nodes = new[] { "input", "tweeter_in" },
            value = 3.3e-6
        });
        await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(c1Args));

        var l1Args = JsonSerializer.Serialize(new
        {
            name = "L1",
            component_type = "inductor",
            nodes = new[] { "input", "woofer_in" },
            value = 0.001
        });
        await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(l1Args));

        // Add tweeter subcircuit
        var tweeterArgs = JsonSerializer.Serialize(new
        {
            name = "Xtweeter",
            component_type = "subcircuit",
            nodes = new[] { "tweeter_in", "0" },
            model = "275_030"
        });
        await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(tweeterArgs));

        // Add woofer subcircuit
        var wooferArgs = JsonSerializer.Serialize(new
        {
            name = "Xwoofer",
            component_type = "subcircuit",
            nodes = new[] { "woofer_in", "0" },
            model = "297_429"
        });
        await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(wooferArgs));

        // Validate circuit
        var validateArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId
        });
        var validateResult = await _mcpService.ExecuteTool("validate_circuit", JsonSerializer.Deserialize<JsonElement>(validateArgs));
        var validateText = validateResult.Content.FirstOrDefault()?.Text ?? "";
        var validateJson = JsonSerializer.Deserialize<JsonElement>(validateText);
        var isValid = validateJson.GetProperty("is_valid").GetBoolean();
        Assert.True(isValid, $"Circuit validation failed. Errors: {validateText}");

        // Run AC analysis
        var acArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId,
            start_frequency = 20.0,
            stop_frequency = 20000.0,
            number_of_points = 100,
            signals = new[] { "v(tweeter_in)", "v(woofer_in)" }
        });
        var acResult = await _mcpService.ExecuteTool("run_ac_analysis", JsonSerializer.Deserialize<JsonElement>(acArgs));
        Assert.NotNull(acResult);

        // Assert - Verify analysis succeeded
        var acResultText = acResult.Content.FirstOrDefault()?.Text ?? "";
        Assert.NotEmpty(acResultText);
        Assert.Contains("v(tweeter_in)", acResultText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v(woofer_in)", acResultText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleSubcircuitInstances_SameDefinition_Works()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        // Act - Add multiple instances of the same subcircuit
        for (int i = 1; i <= 3; i++)
        {
            var args = JsonSerializer.Serialize(new
            {
                name = $"X{i}",
                component_type = "subcircuit",
                nodes = new[] { $"node{i}", "0" },
                model = "test_speaker"
            });
            var result = await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(args));
            Assert.NotNull(result);
        }

        // Assert - Verify all instances work
        // Check that definition is registered (should only be one definition for all instances)
        var spiceCircuit = circuit.GetSpiceSharpCircuit();
        var definitionCount = 0;
        foreach (var entity in spiceCircuit)
        {
            if (entity.Name == "test_speaker")
            {
                definitionCount++;
            }
        }
        
        // Should have exactly one definition
        Assert.Equal(1, definitionCount);

        // Verify all instances exist
        var instance1 = spiceCircuit.TryGetEntity("X1", out var e1) ? e1 : null;
        var instance2 = spiceCircuit.TryGetEntity("X2", out var e2) ? e2 : null;
        var instance3 = spiceCircuit.TryGetEntity("X3", out var e3) ? e3 : null;
        
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotNull(instance3);

        // Verify circuit can be analyzed
        var v1Args = JsonSerializer.Serialize(new
        {
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "node1", "0" },
            value = 1.0,
            parameters = new Dictionary<string, object> { { "ac", 1 } }
        });
        await _mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(v1Args));

        var acArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId,
            start_frequency = 20.0,
            stop_frequency = 20000.0,
            number_of_points = 100,
            signals = new[] { "v(node1)", "v(node2)", "v(node3)" }
        });
        var acResult = await _mcpService.ExecuteTool("run_ac_analysis", JsonSerializer.Deserialize<JsonElement>(acArgs));
        Assert.NotNull(acResult);

        var acResultText = acResult.Content.FirstOrDefault()?.Text ?? "";
        Assert.NotEmpty(acResultText);
    }
}

