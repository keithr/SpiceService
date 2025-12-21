using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests that verify the exact bug report scenario:
/// - Speakers can be found via search_speakers_by_parameters
/// - Speakers show available_in_library: true
/// - BUT cannot be added to circuits via add_component
/// </summary>
public class SpeakerSubcircuitAddComponentTests
{
    [Fact]
    public async Task BugReport_AddComponent_WithSpeakerSubcircuit_ShouldWork()
    {
        // This test reproduces the exact bug report scenario
        // Arrange - Use actual library file if available, or create realistic test file
        var circuitManager = new CircuitManager();
        
        // Create test library with speaker subcircuit matching real format
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        // Use format matching parts_express_complete.lib
        var speakerSubcircuit = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
* Manufacturer: Dayton Audio
* Type: tweeters
* Diameter: 1.75
* Impedance: 6
* Sensitivity: 92
* FS: 2000
* QTS: 0.5
* QES: 0.6
* QMS: 2.5
* VAS: 0.05
* RE: 6.0
* LE: 0.05mH
* BL: 2.5
* XMAX: 0.5
* MMS: 0.5
* CMS: 0.0001
* SD: 24.0
* PRICE: 25.00
.SUBCKT 275_030 PLUS MINUS
* Voice coil electrical impedance (Re + jI%Le)
Re PLUS 1 6.0
Le 1 2 0.05mH
*
* Motional impedance (mechanical system reflected to electrical)
* Using Thiele-Small parameters
Rms 2 3 9.984000
Lms 3 4 0.143682H
Cms 4 MINUS 0.044074F
.ENDS 275_030
";
        File.WriteAllText(Path.Combine(tempLibPath, "speaker.lib"), speakerSubcircuit);
        
        // Create LibraryService with SpeakerDatabaseService
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        // Index libraries (this populates both library index and database)
        libraryService.IndexLibraries(new[] { tempLibPath });
        
        var componentService = new ComponentService(libraryService);
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
        var enclosureDesignService = new EnclosureDesignService();
        var crossoverCompatibilityService = new CrossoverCompatibilityService();
        
        var config = new MCPServerConfig
        {
            Version = "1.0.0",
            LibraryPaths = new[] { tempLibPath }
        };
        
        var mcpService = new MCPService(
            circuitManager,
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
            libraryService,
            speakerDb,
            enclosureDesignService,
            crossoverCompatibilityService);
        
        // Act - Step 1: Search speakers (as reported in bug)
        var searchArgs = JsonSerializer.Serialize(new
        {
            manufacturer = "Dayton Audio"
        });
        var speakerSearchResult = await mcpService.ExecuteTool("search_speakers_by_parameters", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(speakerSearchResult);
        Assert.NotEmpty(speakerSearchResult.Content);
        
        var speakerResultText = speakerSearchResult.Content[0].Text;
        var speakerResultJson = JsonDocument.Parse(speakerResultText);
        
        // Verify speakers were found
        Assert.True(speakerResultJson.RootElement.TryGetProperty("count", out var count));
        Assert.True(count.GetInt32() > 0);
        
        // Verify available_in_library is true (as reported in bug)
        Assert.True(speakerResultJson.RootElement.TryGetProperty("results", out var results));
        var firstResult = results[0];
        Assert.True(firstResult.TryGetProperty("subcircuit_name", out var subcircuitNameElement));
        var subcircuitName = subcircuitNameElement.GetString();
        Assert.NotNull(subcircuitName);
        
        if (firstResult.TryGetProperty("available_in_library", out var availableInLibrary))
        {
            Assert.True(availableInLibrary.GetBoolean(), "Speaker should be available in library");
        }
        
        // Step 2: Verify library_search can find it (as reported in bug)
        var libSearchArgs = JsonSerializer.Serialize(new { query = subcircuitName });
        var libSearchResult = await mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(libSearchArgs));
        
        Assert.NotNull(libSearchResult);
        Assert.NotEmpty(libSearchResult.Content);
        
        var libResultText = libSearchResult.Content[0].Text;
        var libResultJson = JsonDocument.Parse(libResultText);
        
        Assert.True(libResultJson.RootElement.TryGetProperty("subcircuits", out var subcircuits));
        var subcircuitArray = subcircuits.EnumerateArray().ToList();
        Assert.Contains(subcircuitArray, s => s.GetProperty("name").GetString() == subcircuitName);
        
        // Step 3: Create circuit (as reported in bug)
        var circuitId = "overnight_sensation";
        circuitManager.CreateCircuit(circuitId, "Overnight Sensation Test");
        circuitManager.SetActiveCircuit(circuitId);
        
        // Step 4: Add voltage source (as in bug report)
        var vinArgs = JsonSerializer.Serialize(new
        {
            name = "Vin",
            component_type = "voltage_source",
            nodes = new[] { "input", "0" },
            value = 1,
            parameters = new { ac = 1 }
        });
        var vinResult = await mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(vinArgs));
        Assert.NotNull(vinResult);
        Assert.NotEmpty(vinResult.Content);
        
        // Step 5: Add speaker subcircuit (THIS IS WHERE THE BUG OCCURS)
        var addArgs = JsonSerializer.Serialize(new
        {
            name = "Xtweeter",
            component_type = "subcircuit",
            nodes = new[] { "tw_out", "0" },
            model = subcircuitName
        });
        
        // This should NOT fail - but it does in the bug report
        var addResult = await mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(addArgs));
        
        // Assert - Should succeed
        Assert.NotNull(addResult);
        Assert.NotEmpty(addResult.Content);
        
        // Verify component was actually added
        var circuit = circuitManager.GetCircuit(circuitId);
        Assert.NotNull(circuit);
        
        // Step 6: Run AC analysis (as in bug report test case)
        var acArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId,
            signals = new[] { "v(tw_out)" },
            start_frequency = 20,
            stop_frequency = 20000,
            number_of_points = 100
        });
        var acResult = await mcpService.ExecuteTool("run_ac_analysis", JsonSerializer.Deserialize<JsonElement>(acArgs));
        
        Assert.NotNull(acResult);
        Assert.NotEmpty(acResult.Content);
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch (IOException) { }
    }
    
    [Fact]
    public async Task BugReport_ImportNetlist_WithSpeakerSubcircuitXLine_ShouldWork()
    {
        // This test reproduces the import_netlist failure from the bug report
        // Arrange
        var circuitManager = new CircuitManager();
        
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        var speakerSubcircuit = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 6.0
Le 1 2 0.05mH
Rms 2 3 9.984000
Lms 3 4 0.143682H
Cms 4 MINUS 0.044074F
.ENDS 275_030
";
        File.WriteAllText(Path.Combine(tempLibPath, "speaker.lib"), speakerSubcircuit);
        
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        libraryService.IndexLibraries(new[] { tempLibPath });
        
        var componentService = new ComponentService(libraryService);
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
        var enclosureDesignService = new EnclosureDesignService();
        var crossoverCompatibilityService = new CrossoverCompatibilityService();
        
        var config = new MCPServerConfig
        {
            Version = "1.0.0",
            LibraryPaths = new[] { tempLibPath }
        };
        
        var mcpService = new MCPService(
            circuitManager,
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
            libraryService,
            speakerDb,
            enclosureDesignService,
            crossoverCompatibilityService);
        
        // Act - Import netlist with X-line (as in bug report)
        // SPICE netlists: first line is title (ignored by parser), then components
        var netlistContent = @"Overnight Sensation Test
Vin input 0 DC 1 AC 1
C1 input tw1 1.5u
L1 tw1 tw2 0.36m
R1 tw2 tw_out 6
Xtweeter tw_out 0 275_030
.end";
        
        var importArgs = JsonSerializer.Serialize(new
        {
            netlist = netlistContent,
            circuit_name = "overnight_sensation"
        });
        
        // This should NOT fail - but it does in the bug report
        var importResult = await mcpService.ExecuteTool("import_netlist", JsonSerializer.Deserialize<JsonElement>(importArgs));
        
        // Assert - Should succeed
        Assert.NotNull(importResult);
        Assert.NotEmpty(importResult.Content);
        
        // Verify circuit was created
        var circuit = circuitManager.GetCircuit("overnight_sensation");
        Assert.NotNull(circuit);
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch (IOException) { }
    }
}

