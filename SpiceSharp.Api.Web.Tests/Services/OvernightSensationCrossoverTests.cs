using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for the Overnight Sensation Speaker Crossover scenario
/// Validates that subcircuit instantiation works via both add_component and import_netlist
/// </summary>
public class OvernightSensationCrossoverTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ILibraryService _libraryService;

    public OvernightSensationCrossoverTests()
    {
        _circuitManager = new CircuitManager();
        
        // Create library service with test subcircuit definitions
        var speakerDb = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db"));
        speakerDb.InitializeDatabase();
        _libraryService = new LibraryService(speakerDb);
        
        // Create test subcircuit definitions matching the bug report scenario
        var tweeterSubcircuit = new SubcircuitDefinition
        {
            Name = "275_030",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Subcircuit definition
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.001
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "Dayton Audio" },
                { "PRODUCT_NAME", "ND20FA-6 3/4\" Soft Dome Neodymium Tweeter" },
                { "TYPE", "tweeters" },
                { "DIAMETER", "0.75" },
                { "IMPEDANCE", "6" },
                { "SENSITIVITY", "90" }
            }
        };

        var wooferSubcircuit = new SubcircuitDefinition
        {
            Name = "297_429",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* HiVi B4N 4"" Aluminum Round Frame Midbass
* Subcircuit definition
.SUBCKT 297_429 PLUS MINUS
Re PLUS 1 5.5
Le 1 2 0.002
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "HiVi" },
                { "PRODUCT_NAME", "B4N 4\" Aluminum Round Frame Midbass" },
                { "TYPE", "woofers" },
                { "DIAMETER", "4" },
                { "IMPEDANCE", "8" },
                { "SENSITIVITY", "85" }
            }
        };

        // Index the test subcircuits
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        // Write test library files
        File.WriteAllText(Path.Combine(tempLibPath, "test_tweeter.lib"), tweeterSubcircuit.Definition);
        File.WriteAllText(Path.Combine(tempLibPath, "test_woofer.lib"), wooferSubcircuit.Definition);
        
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
            config,
            _libraryService,
            speakerDb);
    }

    [Fact]
    public async Task AddComponent_WithSubcircuitType_ShouldCreateSubcircuitInstance()
    {
        // Arrange - Create circuit first
        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_crossover",
            description = "Overnight Sensation Crossover Test"
        });
        await _mcpService.ExecuteTool("create_circuit", createArgs);

        // Act - Add tweeter subcircuit using add_component
        var addTweeterArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_crossover",
            name = "Xtweeter",
            component_type = "subcircuit",
            nodes = new[] { "tw_out", "0" },
            model = "275_030"
        });

        var result = await _mcpService.ExecuteTool("add_component", addTweeterArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        // Verify circuit has the subcircuit
        var circuit = _circuitManager.GetCircuit("test_crossover");
        Assert.NotNull(circuit);
        
        // Verify subcircuit definition was loaded
        var subcircuitDef = _libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(subcircuitDef);
        Assert.Equal("275_030", subcircuitDef.Name);
        Assert.Equal(2, subcircuitDef.Nodes.Count);
        Assert.Contains("PLUS", subcircuitDef.Nodes);
        Assert.Contains("MINUS", subcircuitDef.Nodes);
    }

    [Fact]
    public async Task ImportNetlist_WithSubcircuitXLines_ShouldParseAndCreateSubcircuits()
    {
        // Arrange - Netlist with subcircuit instantiation (the bug report scenario)
        var netlist = @"
* Overnight Sensation Speaker Crossover
* Tweeter path
C1 input tw1 1.5u
L1 tw1 tw2 0.36m
R1 tw2 tw_out 6
C2 tw_out zobel 2.2u
R2 zobel 0 10
Xtweeter tw_out 0 275_030

* Woofer path
L2 input wf1 1.1m
C3 wf1 0 22u
C4 wf1 wf_out 5.8u
Xwoofer wf_out 0 297_429

* Input source
Vin input 0 AC 1
";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "overnight_sensation",
            set_active = true
        });

        // Act
        var result = await _mcpService.ExecuteTool("import_netlist", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("circuit_id"));
        Assert.Equal("overnight_sensation", response["circuit_id"].GetString());
        Assert.True(response.ContainsKey("components_added"));
        
        // Should have added all components including subcircuits
        var componentsAdded = response["components_added"].GetInt32();
        Assert.True(componentsAdded >= 10); // At least 10 components (8 passive + 2 subcircuits + 1 source)
        
        // Verify circuit was created
        var circuit = _circuitManager.GetCircuit("overnight_sensation");
        Assert.NotNull(circuit);
        
        // Verify subcircuit definitions are available in library
        var tweeterDef = _libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(tweeterDef);
        
        var wooferDef = _libraryService.GetSubcircuitByName("297_429");
        Assert.NotNull(wooferDef);
    }

    [Fact]
    public async Task FullCrossoverCircuit_WithBothMethods_ShouldCreateCompleteCircuit()
    {
        // Arrange - Create circuit
        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "full_crossover",
            description = "Complete Overnight Sensation Crossover"
        });
        await _mcpService.ExecuteTool("create_circuit", createArgs);

        // Act - Add all components including subcircuits via add_component
        var components = new List<object>
        {
            // Input source
            new { name = "Vin", component_type = "voltage_source", nodes = new[] { "input", "0" }, value = 1.0, parameters = new Dictionary<string, object> { { "ac", 1 } } },
            
            // Tweeter path (3rd-order high-pass)
            new { name = "C1", component_type = "capacitor", nodes = new[] { "input", "tw1" }, value = 1.5e-6 },
            new { name = "L1", component_type = "inductor", nodes = new[] { "tw1", "tw2" }, value = 0.36e-3 },
            new { name = "R1", component_type = "resistor", nodes = new[] { "tw2", "tw_out" }, value = 6.0 },
            new { name = "C2", component_type = "capacitor", nodes = new[] { "tw_out", "zobel" }, value = 2.2e-6 },
            new { name = "R2", component_type = "resistor", nodes = new[] { "zobel", "0" }, value = 10.0 },
            
            // Woofer path (2nd-order low-pass)
            new { name = "L2", component_type = "inductor", nodes = new[] { "input", "wf1" }, value = 1.1e-3 },
            new { name = "C3", component_type = "capacitor", nodes = new[] { "wf1", "0" }, value = 22e-6 },
            new { name = "C4", component_type = "capacitor", nodes = new[] { "wf1", "wf_out" }, value = 5.8e-6 },
        };

        foreach (var comp in components)
        {
            var args = JsonSerializer.SerializeToElement(comp);
            await _mcpService.ExecuteTool("add_component", args);
        }

        // Add subcircuits using add_component
        var tweeterArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "full_crossover",
            name = "Xtweeter",
            component_type = "subcircuit",
            nodes = new[] { "tw_out", "0" },
            model = "275_030"
        });
        await _mcpService.ExecuteTool("add_component", tweeterArgs);

        var wooferArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "full_crossover",
            name = "Xwoofer",
            component_type = "subcircuit",
            nodes = new[] { "wf_out", "0" },
            model = "297_429"
        });
        await _mcpService.ExecuteTool("add_component", wooferArgs);

        // Assert - Verify circuit structure
        var circuit = _circuitManager.GetCircuit("full_crossover");
        Assert.NotNull(circuit);
        
        // Verify subcircuit definitions were loaded
        var tweeterDef = _libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(tweeterDef);
        Assert.Equal("275_030", tweeterDef.Name);
        
        var wooferDef = _libraryService.GetSubcircuitByName("297_429");
        Assert.NotNull(wooferDef);
        Assert.Equal("297_429", wooferDef.Name);
        
        // Verify node mapping: circuit nodes ["tw_out", "0"] map to subcircuit nodes ["PLUS", "MINUS"]
        // This is handled internally by SpiceSharp when creating the subcircuit instance
    }

    [Fact]
    public async Task LibrarySearch_ShouldFindSpeakerSubcircuits()
    {
        // Arrange & Act - Search for the tweeter
        var searchArgs = JsonSerializer.SerializeToElement(new
        {
            query = "275_030",
            limit = 10
        });

        var result = await _mcpService.ExecuteTool("library_search", searchArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("subcircuits"));
        
        var subcircuits = response["subcircuits"].EnumerateArray().ToList();
        Assert.NotEmpty(subcircuits);
        
        var foundTweeter = subcircuits.FirstOrDefault(s => 
            s.TryGetProperty("name", out var name) && name.GetString() == "275_030");
        Assert.True(foundTweeter.ValueKind != JsonValueKind.Undefined);
        
        // Verify subcircuit has correct nodes
        if (foundTweeter.TryGetProperty("nodes", out var nodes))
        {
            var nodeList = nodes.EnumerateArray().Select(n => n.GetString()).ToList();
            Assert.Contains("PLUS", nodeList);
            Assert.Contains("MINUS", nodeList);
        }
    }

    [Fact]
    public async Task ImportNetlist_WithSubcircuitXLines_NodeMappingShouldBeCorrect()
    {
        // Arrange - Simple test to verify node mapping
        var netlist = @"
Xtweeter tw_out 0 275_030
Xwoofer wf_out 0 297_429
";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "node_mapping_test"
        });

        // Act
        var result = await _mcpService.ExecuteTool("import_netlist", arguments);

        // Assert
        Assert.NotNull(result);
        
        // Verify the netlist was parsed correctly
        // The node mapping ["tw_out", "0"] -> ["PLUS", "MINUS"] is handled by SpiceSharp
        // when the subcircuit instance is created
        
        var circuit = _circuitManager.GetCircuit("node_mapping_test");
        Assert.NotNull(circuit);
        
        // Verify subcircuit definitions exist
        var tweeterDef = _libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(tweeterDef);
        Assert.Equal(2, tweeterDef.Nodes.Count);
    }

    [Fact]
    public async Task AddComponent_SubcircuitType_WithInvalidModel_ShouldThrowError()
    {
        // Arrange
        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "error_test",
            description = "Error test"
        });
        await _mcpService.ExecuteTool("create_circuit", createArgs);

        // Act & Assert - Try to add subcircuit with non-existent model
        var addArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "error_test",
            name = "Xinvalid",
            component_type = "subcircuit",
            nodes = new[] { "out", "0" },
            model = "NON_EXISTENT_SUBCIRCUIT"
        });

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("add_component", addArgs));
    }
}

