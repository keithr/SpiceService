using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests to verify that LibraryService is properly wired to ComponentService
/// and that add_component and import_netlist can use subcircuits from the library.
/// 
/// These tests catch the bug where LibraryService exists (library_search works)
/// but is not available to add_component and import_netlist.
/// </summary>
public class LibraryServiceWiringTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ILibraryService _libraryService;
    private readonly IComponentService _componentService;

    public LibraryServiceWiringTests()
    {
        _circuitManager = new CircuitManager();
        
        // Create library service with test subcircuit definitions
        var speakerDb = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db"));
        speakerDb.InitializeDatabase();
        _libraryService = new LibraryService(speakerDb);
        
        // Create test subcircuit definition matching the bug report scenario
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

        // Index the test subcircuit
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        // Write test library file
        File.WriteAllText(Path.Combine(tempLibPath, "test_tweeter.lib"), tweeterSubcircuit.Definition);
        
        _libraryService.IndexLibraries(new[] { tempLibPath });

        // Create services with library service properly wired (like Program.cs does)
        _componentService = new ComponentService(_libraryService); // LibraryService wired here
        var componentService = _componentService;
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
            componentService, // ComponentService with LibraryService wired
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
            _libraryService, // LibraryService also passed to MCPService
            speakerDatabaseService);
    }

    /// <summary>
    /// Test 1: Verify library_search works (baseline test)
    /// This should pass - library_search uses LibraryService directly
    /// </summary>
    [Fact]
    public async Task LibrarySearch_WithSubcircuitInLibrary_ShouldFindSubcircuit()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "275_030"
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("count"));
        Assert.True(response["count"].GetInt32() >= 1);
    }

    /// <summary>
    /// Test 2: Verify add_component with subcircuit works when LibraryService is wired
    /// This test catches the bug where add_component fails even though library_search works
    /// </summary>
    [Fact]
    public async Task AddComponent_WithSubcircuit_WhenLibraryServiceWired_ShouldSucceed()
    {
        // Arrange
        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_subcircuit_add",
            make_active = true
        });
        await _mcpService.ExecuteTool("create_circuit", createArgs);

        var addVoltageSourceArgs = JsonSerializer.SerializeToElement(new
        {
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "in", "0" },
            value = 1,
            parameters = new Dictionary<string, object> { { "ac", 1 } }
        });
        await _mcpService.ExecuteTool("add_component", addVoltageSourceArgs);

        var addSubcircuitArgs = JsonSerializer.SerializeToElement(new
        {
            name = "Xspk",
            component_type = "subcircuit",
            model = "275_030",
            nodes = new[] { "in", "0" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("add_component", addSubcircuitArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("added", response["status"].GetString());
        
        // Verify component was actually added to circuit
        var circuit = _circuitManager.GetCircuit("test_subcircuit_add");
        Assert.NotNull(circuit);
        var component = _componentService.GetComponent(circuit, "Xspk");
        Assert.NotNull(component);
    }

    /// <summary>
    /// Test 3: Verify import_netlist with subcircuit works when LibraryService is wired
    /// This test catches the bug where import_netlist fails even though library_search works
    /// </summary>
    [Fact]
    public async Task ImportNetlist_WithSubcircuit_WhenLibraryServiceWired_ShouldSucceed()
    {
        // Arrange
        var netlist = @"Test Speaker Import
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_speaker_import",
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
        
        // Should have added both components (V1 and Xspk)
        Assert.True(response.ContainsKey("components_added"));
        Assert.Equal(2, response["components_added"].GetInt32());
        
        // Status should be Success (not Partial Success)
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("Success", response["status"].GetString());
        
        // Should NOT have failed_components
        Assert.False(response.ContainsKey("failed_components") && response["failed_components"].ValueKind != JsonValueKind.Null);
        
        // Verify subcircuit was actually added to circuit
        var circuit = _circuitManager.GetCircuit("test_speaker_import");
        Assert.NotNull(circuit);
        var component = _componentService.GetComponent(circuit, "Xspk");
        Assert.NotNull(component);
    }

    /// <summary>
    /// Test 4: Verify export_netlist includes subcircuit when it was successfully imported
    /// </summary>
    [Fact]
    public async Task ExportNetlist_AfterImportWithSubcircuit_ShouldIncludeSubcircuit()
    {
        // Arrange - Import netlist with subcircuit
        var netlist = @"Test Speaker Import
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end";

        var importArgs = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_speaker_export",
            set_active = true
        });
        await _mcpService.ExecuteTool("import_netlist", importArgs);

        var exportArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_speaker_export"
        });

        // Act
        var result = await _mcpService.ExecuteTool("export_netlist", exportArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var exportedNetlist = textContent.Text ?? "";
        
        // Should contain the subcircuit instance
        Assert.Contains("Xspk", exportedNetlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", exportedNetlist, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test 5: Verify add_component provides clear error when LibraryService is NOT wired
    /// This test verifies error reporting works correctly
    /// </summary>
    [Fact]
    public async Task AddComponent_WithSubcircuit_WhenLibraryServiceNotWired_ShouldReportClearError()
    {
        // Arrange - Create MCPService WITHOUT LibraryService
        var circuitManager = new CircuitManager();
        var componentServiceNoLib = new ComponentService(null); // No LibraryService
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
        var config = new MCPServerConfig();
        
        var mcpServiceNoLib = new MCPService(
            circuitManager,
            componentServiceNoLib, // ComponentService WITHOUT LibraryService
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
            null, // No LibraryService
            null);

        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test_no_lib",
            make_active = true
        });
        await mcpServiceNoLib.ExecuteTool("create_circuit", createArgs);

        var addSubcircuitArgs = JsonSerializer.SerializeToElement(new
        {
            name = "Xspk",
            component_type = "subcircuit",
            model = "275_030",
            nodes = new[] { "in", "0" }
        });

        // Act & Assert - Should throw exception with clear error message
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await mcpServiceNoLib.ExecuteTool("add_component", addSubcircuitArgs));
        
        // Should contain error message about library service not being available
        Assert.Contains("library service is not available", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

