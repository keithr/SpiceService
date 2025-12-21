using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Integration tests for speaker database and library index connection
/// </summary>
public class SpeakerLibraryIntegrationTests
{
    [Fact]
    public async Task FullWorkflow_SearchSpeakers_AddToCircuit_Works()
    {
        // Arrange
        var circuitManager = new CircuitManager();
        
        // Create test library with speaker subcircuit
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
        
        // Verify speakers were found
        Assert.True(speakerResultJson.RootElement.TryGetProperty("count", out var count));
        Assert.True(count.GetInt32() > 0);
        
        // Verify subcircuit_name is present
        Assert.True(speakerResultJson.RootElement.TryGetProperty("results", out var results));
        var firstResult = results[0];
        Assert.True(firstResult.TryGetProperty("subcircuit_name", out var subcircuitNameElement));
        var subcircuitName = subcircuitNameElement.GetString();
        Assert.NotNull(subcircuitName);
        
        // Step 2: Verify library_search can find the subcircuit
        var libSearchArgs = JsonSerializer.Serialize(new { query = subcircuitName });
        var libSearchResult = await mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(libSearchArgs));
        
        Assert.NotNull(libSearchResult);
        Assert.NotEmpty(libSearchResult.Content);
        
        var libResultText = libSearchResult.Content[0].Text;
        var libResultJson = JsonDocument.Parse(libResultText);
        
        // Verify subcircuit was found in library
        Assert.True(libResultJson.RootElement.TryGetProperty("subcircuits", out var subcircuits));
        var subcircuitArray = subcircuits.EnumerateArray().ToList();
        Assert.Contains(subcircuitArray, s => s.GetProperty("name").GetString() == subcircuitName);
        
        // Step 3: Add subcircuit to circuit
        var circuitId = "test_circuit";
        circuitManager.CreateCircuit(circuitId, "Test");
        circuitManager.SetActiveCircuit(circuitId);
        
        var addArgs = JsonSerializer.Serialize(new
        {
            name = "X1",
            component_type = "subcircuit",
            nodes = new[] { "out", "0" },
            model = subcircuitName
        });
        var addResult = await mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(addArgs));
        
        Assert.NotNull(addResult);
        Assert.NotEmpty(addResult.Content);
        
        // Step 4: Run AC analysis
        var acArgs = JsonSerializer.Serialize(new
        {
            circuit_id = circuitId,
            start_frequency = 20.0,
            stop_frequency = 20000.0,
            number_of_points = 100,
            signals = new[] { "v(out)" }
        });
        var acResult = await mcpService.ExecuteTool("run_ac_analysis", JsonSerializer.Deserialize<JsonElement>(acArgs));
        
        Assert.NotNull(acResult);
        Assert.NotEmpty(acResult.Content);
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
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
    public async Task ReindexLibraries_UpdatesBothDatabaseAndIndex()
    {
        // Arrange
        var circuitManager = new CircuitManager();
        
        // Create initial library
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        var initialSubcircuit = @"
* Initial Speaker
.SUBCKT initial_speaker PLUS MINUS
Re PLUS 1 5.5
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "initial.lib"), initialSubcircuit);
        
        // Create LibraryService with SpeakerDatabaseService
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        // Index initial library
        libraryService.IndexLibraries(new[] { tempLibPath });
        
        // Verify initial subcircuit is indexed
        var initialSubcircuitDef = libraryService.GetSubcircuitByName("initial_speaker");
        Assert.NotNull(initialSubcircuitDef);
        
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
        
        // Act - Add new subcircuit to library
        var newSubcircuit = @"
* New Speaker: new_speaker
* Type: woofers
* Diameter: 5.0
.SUBCKT new_speaker PLUS MINUS
Re PLUS 1 6.0
Le 1 2 0.003
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "new.lib"), newSubcircuit);
        
        // Reindex libraries
        var reindexArgs = JsonSerializer.Serialize(new { });
        var reindexResult = await mcpService.ExecuteTool("reindex_libraries", JsonSerializer.Deserialize<JsonElement>(reindexArgs));
        
        Assert.NotNull(reindexResult);
        Assert.NotEmpty(reindexResult.Content);
        
        // Assert - Verify both subcircuits are now available
        var initialDef = libraryService.GetSubcircuitByName("initial_speaker");
        Assert.NotNull(initialDef);
        
        var newDef = libraryService.GetSubcircuitByName("new_speaker");
        Assert.NotNull(newDef);
        
        // Verify library_search finds both
        var searchArgs = JsonSerializer.Serialize(new { query = "" });
        var searchResult = await mcpService.ExecuteTool("library_search", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(searchResult);
        Assert.NotEmpty(searchResult.Content);
        
        var searchResultText = searchResult.Content[0].Text;
        var searchResultJson = JsonDocument.Parse(searchResultText);
        
        Assert.True(searchResultJson.RootElement.TryGetProperty("subcircuits", out var subcircuits));
        var subcircuitArray = subcircuits.EnumerateArray().ToList();
        Assert.Contains(subcircuitArray, s => s.GetProperty("name").GetString() == "initial_speaker");
        Assert.Contains(subcircuitArray, s => s.GetProperty("name").GetString() == "new_speaker");
        
        // Verify database was updated (search should find new_speaker)
        var dbSearchArgs = JsonSerializer.Serialize(new
        {
            diameter_min = 4.5,
            diameter_max = 5.5
        });
        var dbSearchResult = await mcpService.ExecuteTool("search_speakers_by_parameters", JsonSerializer.Deserialize<JsonElement>(dbSearchArgs));
        
        Assert.NotNull(dbSearchResult);
        Assert.NotEmpty(dbSearchResult.Content);
        
        var dbResultText = dbSearchResult.Content[0].Text;
        var dbResultJson = JsonDocument.Parse(dbResultText);
        
        Assert.True(dbResultJson.RootElement.TryGetProperty("results", out var dbResults));
        var dbResultsArray = dbResults.EnumerateArray().ToList();
        Assert.Contains(dbResultsArray, r => r.GetProperty("subcircuit_name").GetString() == "new_speaker");
        
        // Cleanup
        Directory.Delete(tempLibPath, true);
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
    public async Task DatabaseLibraryMismatch_DetectedAndReported()
    {
        // Arrange
        var circuitManager = new CircuitManager();
        
        // Create LibraryService with SpeakerDatabaseService
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        // Manually insert a speaker into database that doesn't exist in library
        // (This simulates a scenario where database has data but library index is empty or out of sync)
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO speakers (subcircuit_name, manufacturer, type, diameter, impedance, sensitivity)
            VALUES ('missing_speaker', 'Test', 'woofers', 5.0, 8, 85.0)
        ";
        command.ExecuteNonQuery();
        connection.Close();
        
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
            LibraryPaths = new List<string>() // Empty - no libraries indexed
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
        
        // Act - Search speakers (should find missing_speaker in database)
        var searchArgs = JsonSerializer.Serialize(new
        {
            diameter_min = 4.0,
            diameter_max = 6.0
        });
        var speakerSearchResult = await mcpService.ExecuteTool("search_speakers_by_parameters", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(speakerSearchResult);
        Assert.NotEmpty(speakerSearchResult.Content);
        
        var speakerResultText = speakerSearchResult.Content[0].Text;
        var speakerResultJson = JsonDocument.Parse(speakerResultText);
        
        // Verify warning is present
        Assert.True(speakerResultJson.RootElement.TryGetProperty("warnings", out var warnings));
        var warningsArray = warnings.EnumerateArray().ToList();
        Assert.Contains(warningsArray, w => w.GetString()!.Contains("not in library index"));
        
        // Verify missing_subcircuits list is present
        Assert.True(speakerResultJson.RootElement.TryGetProperty("missing_subcircuits", out var missingSubcircuits));
        var missingArray = missingSubcircuits.EnumerateArray().ToList();
        Assert.Contains(missingArray, s => s.GetString() == "missing_speaker");
        
        // Verify available_in_library flag is false
        Assert.True(speakerResultJson.RootElement.TryGetProperty("results", out var results));
        var firstResult = results[0];
        Assert.True(firstResult.TryGetProperty("available_in_library", out var availableFlag));
        Assert.False(availableFlag.GetBoolean());
        
        // Try to add the subcircuit - should fail with helpful error
        var circuitId = "test_circuit";
        circuitManager.CreateCircuit(circuitId, "Test");
        circuitManager.SetActiveCircuit(circuitId);
        
        var addArgs = JsonSerializer.Serialize(new
        {
            name = "X1",
            component_type = "subcircuit",
            nodes = new[] { "out", "0" },
            model = "missing_speaker"
        });
        
        // Assert - Should throw exception with helpful message
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await mcpService.ExecuteTool("add_component", JsonSerializer.Deserialize<JsonElement>(addArgs)));
        
        Assert.Contains("not found", exception.Message);
        Assert.Contains("reindex_libraries", exception.Message);
        
        // Cleanup
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

