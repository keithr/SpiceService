using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests using the actual parts_express_complete.lib file to catch real-world issues
/// </summary>
public class RealLibraryFileTests
{
    private readonly string _realLibraryPath;
    
    public RealLibraryFileTests()
    {
        // Try to find the actual parts_express_complete.lib file
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "libraries", "parts_express_complete.lib"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "libraries", "parts_express_complete.lib"),
            Path.Combine(Directory.GetCurrentDirectory(), "libraries", "parts_express_complete.lib"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "libraries", "parts_express_complete.lib"),
        };
        
        _realLibraryPath = possiblePaths.FirstOrDefault(File.Exists) ?? string.Empty;
    }
    
    [Fact]
    public void RealLibraryFile_Exists_CanBeFound()
    {
        // This test verifies the actual library file exists
        // If this fails, the file wasn't committed/pushed correctly
        Assert.True(File.Exists(_realLibraryPath), 
            $"parts_express_complete.lib not found. Searched: {string.Join(", ", _realLibraryPath)}");
    }
    
    [Fact]
    public void RealLibraryFile_ContainsSpeakerSubcircuits()
    {
        if (string.IsNullOrEmpty(_realLibraryPath) || !File.Exists(_realLibraryPath))
        {
            // Skip if file doesn't exist (might not be in test environment)
            return;
        }
        
        // Arrange
        var parser = new SpiceLibParser();
        var content = File.ReadAllText(_realLibraryPath);
        
        // Act
        var subcircuits = parser.ParseSubcircuits(content);
        
        // Assert - Should find speaker subcircuits
        Assert.NotEmpty(subcircuits);
        
        // Verify specific speakers mentioned in bug report exist
        var speaker275_030 = subcircuits.FirstOrDefault(s => s.Name == "275_030");
        Assert.NotNull(speaker275_030);
        Assert.Equal(2, speaker275_030.Nodes?.Count); // PLUS, MINUS
        
        var speaker264_880 = subcircuits.FirstOrDefault(s => s.Name == "264_880");
        Assert.NotNull(speaker264_880);
    }
    
    [Fact]
    public async Task RealLibraryFile_Indexing_PopulatesDatabaseAndLibraryIndex()
    {
        if (string.IsNullOrEmpty(_realLibraryPath) || !File.Exists(_realLibraryPath))
        {
            // Skip if file doesn't exist
            return;
        }
        
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        var libraryDir = Path.GetDirectoryName(_realLibraryPath)!;
        
        // Act - Index the real library file
        libraryService.IndexLibraries(new[] { libraryDir });
        
        // Assert - Verify subcircuits are in library index
        var subcircuit275_030 = libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(subcircuit275_030);
        
        var subcircuit264_880 = libraryService.GetSubcircuitByName("264_880");
        Assert.NotNull(subcircuit264_880);
        
        // Assert - Verify database was populated
        var speaker275_030 = speakerDb.GetSpeakerByName("275_030");
        Assert.NotNull(speaker275_030);
        
        var speaker264_880 = speakerDb.GetSpeakerByName("264_880");
        Assert.NotNull(speaker264_880);
        
        // Cleanup
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch { }
    }
    
    [Fact]
    public async Task RealLibraryFile_DatabaseLibrarySync_WorksCorrectly()
    {
        if (string.IsNullOrEmpty(_realLibraryPath) || !File.Exists(_realLibraryPath))
        {
            // Skip if file doesn't exist
            return;
        }
        
        // Arrange - Simulate the bug scenario:
        // 1. Database has entries (from previous indexing or manual insertion)
        // 2. Library index is empty (service restarted, file added after startup, etc.)
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        
        // First, index to populate database
        var libraryService1 = new LibraryService(speakerDb);
        var libraryDir = Path.GetDirectoryName(_realLibraryPath)!;
        libraryService1.IndexLibraries(new[] { libraryDir });
        
        // Verify database has entries
        var speaker275_030 = speakerDb.GetSpeakerByName("275_030");
        Assert.NotNull(speaker275_030);
        
        // Now create a NEW library service (simulating service restart with empty index)
        var libraryService2 = new LibraryService(speakerDb);
        // Don't index yet - library index is empty but database has entries
        
        // Create MCPService with empty library index but populated database
        var circuitManager = new CircuitManager();
        var componentService = new ComponentService(libraryService2);
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
            LibraryPaths = new[] { libraryDir }
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
            libraryService2, // Empty index!
            speakerDb, // But database has entries
            enclosureDesignService,
            crossoverCompatibilityService);
        
        // Act - Search speakers (should find in database)
        var searchArgs = JsonSerializer.Serialize(new
        {
            diameter_min = 4.0,
            diameter_max = 5.0
        });
        var speakerSearchResult = await mcpService.ExecuteTool("search_speakers_by_parameters", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        
        Assert.NotNull(speakerSearchResult);
        var resultText = speakerSearchResult.Content[0].Text;
        var resultJson = JsonDocument.Parse(resultText);
        
        // Assert - Should show warnings about missing subcircuits in library
        bool hasWarnings = false;
        if (resultJson.RootElement.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
        {
            var warningsArray = warnings.EnumerateArray().ToList();
            if (warningsArray.Any())
            {
                hasWarnings = true;
                Assert.Contains(warningsArray, w => w.GetString()!.Contains("not in library index"));
            }
        }
        
        // Also check for missing_subcircuits or available_in_library flags
        if (!hasWarnings && resultJson.RootElement.TryGetProperty("speakers", out var speakers) && speakers.ValueKind == JsonValueKind.Array)
        {
            var speakersArray = speakers.EnumerateArray().ToList();
            var speakersWithMissing = speakersArray.Where(s => 
                s.TryGetProperty("available_in_library", out var avail) && 
                avail.ValueKind == JsonValueKind.False).ToList();
            Assert.NotEmpty(speakersWithMissing); // At least some speakers should show available_in_library: false
        }
        
        // Act - Run reindex_libraries to fix the disconnect
        var reindexArgs = JsonSerializer.Serialize(new { });
        var reindexResult = await mcpService.ExecuteTool("reindex_libraries", JsonSerializer.Deserialize<JsonElement>(reindexArgs));
        
        Assert.NotNull(reindexResult);
        Assert.NotEmpty(reindexResult.Content);
        
        // Assert - After reindexing, subcircuits should be in library index
        var subcircuit275_030 = libraryService2.GetSubcircuitByName("275_030");
        Assert.NotNull(subcircuit275_030); // This should NOT be null after reindexing
        
        // Assert - Search again, warnings should be gone
        var speakerSearchResult2 = await mcpService.ExecuteTool("search_speakers_by_parameters", JsonSerializer.Deserialize<JsonElement>(searchArgs));
        var resultText2 = speakerSearchResult2.Content[0].Text;
        var resultJson2 = JsonDocument.Parse(resultText2);
        
        // Should have no warnings or missing_subcircuits after reindexing
        if (resultJson2.RootElement.TryGetProperty("warnings", out var warnings2) && warnings2.ValueKind == JsonValueKind.Array)
        {
            var warningsArray2 = warnings2.EnumerateArray().ToList();
            var missingWarnings = warningsArray2.Where(w => w.GetString()!.Contains("not in library index")).ToList();
            Assert.Empty(missingWarnings); // No missing library warnings after reindexing
        }
        
        // Verify missing_subcircuits is null or empty after reindexing
        if (resultJson2.RootElement.TryGetProperty("missing_subcircuits", out var missing2) && missing2.ValueKind == JsonValueKind.Array)
        {
            var missingArray2 = missing2.EnumerateArray().ToList();
            Assert.Empty(missingArray2); // No missing subcircuits after reindexing
        }
        
        // Assert - Can now add subcircuit to circuit
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
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch { }
    }
    
    [Fact]
    public async Task RealLibraryFile_ReindexLibraries_ActuallyWorks()
    {
        if (string.IsNullOrEmpty(_realLibraryPath) || !File.Exists(_realLibraryPath))
        {
            // Skip if file doesn't exist
            return;
        }
        
        // Arrange - Create service with library file but don't index on startup
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var libraryService = new LibraryService(speakerDb);
        
        var libraryDir = Path.GetDirectoryName(_realLibraryPath)!;
        
        var circuitManager = new CircuitManager();
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
            LibraryPaths = new[] { libraryDir }
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
        
        // Act - Before indexing, subcircuit should not be found
        var beforeSubcircuit = libraryService.GetSubcircuitByName("275_030");
        // This might be null if indexing didn't happen on startup (depends on MCPService constructor)
        
        // Act - Run reindex_libraries
        var reindexArgs = JsonSerializer.Serialize(new { });
        var reindexResult = await mcpService.ExecuteTool("reindex_libraries", JsonSerializer.Deserialize<JsonElement>(reindexArgs));
        
        Assert.NotNull(reindexResult);
        Assert.NotEmpty(reindexResult.Content);
        
        var reindexText = reindexResult.Content[0].Text;
        var reindexJson = JsonDocument.Parse(reindexText);
        
        // Assert - Should report success
        Assert.True(reindexJson.RootElement.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());
        
        // Assert - After reindexing, subcircuit should be found
        var afterSubcircuit = libraryService.GetSubcircuitByName("275_030");
        Assert.NotNull(afterSubcircuit);
        Assert.Equal("275_030", afterSubcircuit.Name);
        
        // Assert - Database should have the speaker
        var speaker = speakerDb.GetSpeakerByName("275_030");
        Assert.NotNull(speaker);
        
        // Cleanup
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch { }
    }
}

