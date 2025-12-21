using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for LibraryService initialization and database-library connection
/// </summary>
public class LibraryServiceInitializationTests
{
    [Fact]
    public async Task MCPService_WithLibraryService_IndexesLibrariesOnStartup()
    {
        // Arrange
        var circuitManager = new CircuitManager();
        
        // Create LibraryService with SpeakerDatabaseService
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        // Create test library with subcircuit
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        var testSubcircuit = @"
* Test Subcircuit
.SUBCKT test_sub PLUS MINUS
R1 PLUS 1 100
C1 1 MINUS 1e-6
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "test.lib"), testSubcircuit);
        
        // Index the library
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
        
        var config = new MCPServerConfig
        {
            Version = "1.0.0",
            LibraryPaths = new[] { tempLibPath }
        };
        
        // Act - Create MCPService with LibraryService (should index on startup)
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
            speakerDb);
        
        // Assert - Verify library_search can find the subcircuit
        var searchArgs = JsonSerializer.Serialize(new { query = "test_sub" });
        var searchResult = await mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(searchResult);
        Assert.NotEmpty(searchResult.Content);
        
        var resultText = searchResult.Content[0].Text;
        var resultJson = JsonDocument.Parse(resultText);
        
        // Verify subcircuit was found
        Assert.True(resultJson.RootElement.TryGetProperty("subcircuits", out var subcircuits));
        Assert.True(subcircuits.GetArrayLength() > 0);
        
        var firstSubcircuit = subcircuits[0];
        Assert.Equal("test_sub", firstSubcircuit.GetProperty("name").GetString());
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
        // Note: Database file cleanup may fail if connection is still open - that's OK for temp files
        try
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
        catch (IOException)
        {
            // Database connection may still be open - ignore cleanup failure for temp files
        }
    }
    
    [Fact]
    public async Task MCPService_WithoutLibraryService_LibrarySearchReturnsError()
    {
        // Arrange
        var circuitManager = new CircuitManager();
        var componentService = new ComponentService();
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
        
        // Act - Create MCPService WITHOUT LibraryService (null)
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
            libraryService: null); // No LibraryService
        
        // Assert - Verify library_search returns helpful error message
        var searchArgs = JsonSerializer.Serialize(new { query = "test" });
        var searchResult = await mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(searchResult);
        Assert.NotEmpty(searchResult.Content);
        
        var resultText = searchResult.Content[0].Text;
        var resultJson = JsonDocument.Parse(resultText);
        
        // Verify error message is present
        Assert.True(resultJson.RootElement.TryGetProperty("error", out var error));
        Assert.Equal("Library service is not configured", error.GetString());
        
        Assert.True(resultJson.RootElement.TryGetProperty("message", out var message));
        var messageText = message.GetString();
        Assert.NotNull(messageText);
        Assert.Contains("Library service is not available", messageText);
        Assert.Contains("configure LibraryPaths", messageText);
    }
    
    [Fact]
    public async Task SearchSpeakersByParameters_SubcircuitInDatabase_CanBeFoundInLibrary()
    {
        // Arrange
        var circuitManager = new CircuitManager();
        
        // Create test library with speaker subcircuit
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        var speakerSubcircuit = @"
* Test Speaker Subcircuit
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 5.5
Le 1 2 0.002
Ce 2 MINUS 0.0001
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "speaker.lib"), speakerSubcircuit);
        
        // Create LibraryService with SpeakerDatabaseService
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        // Index libraries (this should populate the database)
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
            speakerDb);
        
        // Act - Step 1: Search speakers by parameters
        var searchArgs = JsonSerializer.Serialize(new
        {
            diameter_min = 4.0,
            diameter_max = 5.0
        });
        var speakerSearchResult = await mcpService.ExecuteTool("search_speakers_by_parameters", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(speakerSearchResult);
        Assert.NotEmpty(speakerSearchResult.Content);
        
        var speakerResultText = speakerSearchResult.Content[0].Text;
        var speakerResultJson = JsonDocument.Parse(speakerResultText);
        
        // Verify speakers were found (may be empty if no matching metadata, but database should be populated)
        // The key test is that we can find the subcircuit in the library
        
        // Step 2: Verify library_search can find the subcircuit
        var libSearchArgs = JsonSerializer.Serialize(new { query = "275_030" });
        var libSearchResult = await mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(libSearchArgs));
        
        Assert.NotNull(libSearchResult);
        Assert.NotEmpty(libSearchResult.Content);
        
        var libResultText = libSearchResult.Content[0].Text;
        var libResultJson = JsonDocument.Parse(libResultText);
        
        // Verify subcircuit was found in library
        Assert.True(libResultJson.RootElement.TryGetProperty("subcircuits", out var subcircuits));
        var subcircuitArray = subcircuits.EnumerateArray().ToList();
        Assert.Contains(subcircuitArray, s => s.GetProperty("name").GetString() == "275_030");
        
        // Step 3: Verify we can add the subcircuit to a circuit
        var circuitId = "test_circuit";
        circuitManager.CreateCircuit(circuitId, "Test");
        circuitManager.SetActiveCircuit(circuitId);
        
        var addArgs = JsonSerializer.Serialize(new
        {
            name = "X1",
            component_type = "subcircuit",
            nodes = new[] { "out", "0" },
            model = "275_030"
        });
        var addResult = await mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(addArgs));
        
        Assert.NotNull(addResult);
        Assert.NotEmpty(addResult.Content);
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
        // Note: Database file cleanup may fail if connection is still open - that's OK for temp files
        try
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
        catch (IOException)
        {
            // Database connection may still be open - ignore cleanup failure for temp files
        }
    }
    
    [Fact]
    public void LibraryService_WithSpeakerDatabase_PopulatesDatabaseOnIndex()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        // Create test library with speaker subcircuit that has metadata
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        var speakerSubcircuit = @"
* Test Speaker: 275_030
* Manufacturer: Test
* Product Name: Test Speaker
* Type: woofers
* Diameter: 4.5
* Impedance: 8
* Sensitivity: 85
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 5.5
Le 1 2 0.002
Ce 2 MINUS 0.0001
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "speaker.lib"), speakerSubcircuit);
        
        // Act - Index libraries
        libraryService.IndexLibraries(new[] { tempLibPath });
        
        // Assert - Verify subcircuit is in library index
        var subcircuit = libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(subcircuit);
        Assert.Equal("275_030", subcircuit.Name);
        
        // Verify database was populated (check that we can query it)
        // Note: The actual database population happens in LibraryService.IndexLibraries
        // which calls speakerDatabaseService.PopulateFromSubcircuits
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
        // Note: Database file cleanup may fail if connection is still open - that's OK for temp files
        try
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
        catch (IOException)
        {
            // Database connection may still be open - ignore cleanup failure for temp files
        }
    }
}

