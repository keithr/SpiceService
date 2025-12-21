using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for LibrarySearch MCP tool - covers Phase 1A.4 from implementation plan
/// </summary>
public class LibrarySearchToolTests : IDisposable
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;
    private readonly string _testLibDir;

    public LibrarySearchToolTests()
    {
        _circuitManager = new CircuitManager();
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
        _resultsCache = new CircuitResultsCache();
        var responseMeasurementService = new ResponseMeasurementService(_resultsCache);
        var groupDelayService = new GroupDelayService(_resultsCache);
        var netlistParser = new NetlistParser();
        
        // Create test library directory
        _testLibDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLibDir);
        
        // Create test library files
        File.WriteAllText(Path.Combine(_testLibDir, "test.lib"), @"
.MODEL D1N4001 D (IS=1E-14 RS=0.5 N=1.5)
.MODEL D1N4002 D (IS=2E-14 RS=0.6 N=1.6)
.MODEL Q2N3904 NPN (IS=1E-16 BF=100)
.SUBCKT irf1010n 1 2 3
M1 9 7 8 8 MM L=100u W=100u
.MODEL MM NMOS LEVEL=1 VTO=3.74111
.ENDS
");

        var libraryService = new LibraryService();
        libraryService.IndexLibraries(new[] { _testLibDir });

        var config = new MCPServerConfig 
        { 
            Version = "1.0.0",
            LibraryPaths = new[] { _testLibDir }
        };
        
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
            _resultsCache,
            config,
            libraryService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLibDir))
        {
            Directory.Delete(_testLibDir, recursive: true);
        }
    }

    [Fact]
    public async Task SearchLibrary_WithQuery_ReturnsMatchingModels()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "D1N",
            limit = 10
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("models", out var modelsArray));
        Assert.True(modelsArray.GetArrayLength() >= 2);
        
        var modelNames = modelsArray.EnumerateArray()
            .Select(m => m.GetProperty("model_name").GetString())
            .ToList();
        Assert.Contains("D1N4001", modelNames);
        Assert.Contains("D1N4002", modelNames);
    }

    [Fact]
    public async Task SearchLibrary_WithTypeFilter_FiltersByType()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "",
            type = "diode",
            limit = 10
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var modelsArray = resultData.GetProperty("models");
        Assert.Equal(2, modelsArray.GetArrayLength());
        
        // All should be diodes
        foreach (var model in modelsArray.EnumerateArray())
        {
            Assert.Equal("diode", model.GetProperty("model_type").GetString());
        }
    }

    [Fact]
    public async Task SearchLibrary_WithLimit_RespectsLimit()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "",
            limit = 2
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var modelsArray = resultData.GetProperty("models");
        Assert.True(modelsArray.GetArrayLength() <= 2);
    }

    [Fact]
    public async Task SearchLibrary_WithEmptyQuery_ReturnsAllModels()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "",
            limit = 100
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var modelsArray = resultData.GetProperty("models");
        Assert.Equal(3, modelsArray.GetArrayLength());
    }

    [Fact]
    public async Task SearchLibrary_WithoutLibraryService_ReturnsHelpfulError()
    {
        // Arrange - Create MCPService without library service
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
        // Note: libraryService is null (not configured)
        var mcpServiceWithoutLib = new MCPService(
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
            libraryService: null);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "D1N"
        });

        // Act
        var result = await mcpServiceWithoutLib.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("error", out _), "Should have error field");
        Assert.True(resultData.TryGetProperty("message", out var message));
        var messageText = message.GetString() ?? "";
        Assert.True(messageText.Contains("not available", StringComparison.OrdinalIgnoreCase) || 
                   messageText.Contains("not configured", StringComparison.OrdinalIgnoreCase),
                   $"Message should mention 'not available' or 'not configured'. Got: {messageText}");
        Assert.Equal(0, resultData.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task SearchLibrary_ReturnsSubcircuitsWhenQueryMatches()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "irf1010",
            limit = 10
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("subcircuits", out var subcircuitsArray));
        Assert.True(subcircuitsArray.GetArrayLength() >= 1);
        
        var subcircuit = subcircuitsArray.EnumerateArray().First();
        Assert.Equal("irf1010n", subcircuit.GetProperty("name").GetString());
        Assert.Equal("subcircuit", subcircuit.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SearchLibrary_ReturnsBothModelsAndSubcircuits()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "",
            limit = 100
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("models", out var modelsArray));
        Assert.True(resultData.TryGetProperty("subcircuits", out var subcircuitsArray));
        Assert.True(modelsArray.GetArrayLength() >= 3);
        Assert.True(subcircuitsArray.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task SearchLibrary_ResponseIncludesCorrectFieldsForSubcircuits()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "irf1010n",
            limit = 10
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var subcircuitsArray = resultData.GetProperty("subcircuits");
        var subcircuit = subcircuitsArray.EnumerateArray().First();
        
        Assert.True(subcircuit.TryGetProperty("name", out _));
        Assert.True(subcircuit.TryGetProperty("type", out _));
        Assert.True(subcircuit.TryGetProperty("nodes", out _));
        Assert.True(subcircuit.TryGetProperty("node_count", out _));
        Assert.Equal("subcircuit", subcircuit.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SearchLibrary_HandlesEmptyResultsGracefully()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            query = "nonexistent_component_xyz",
            limit = 10
        });

        // Act
        var result = await _mcpService.ExecuteTool("library_search", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("models", out var modelsArray));
        Assert.True(resultData.TryGetProperty("subcircuits", out var subcircuitsArray));
        Assert.Equal(0, modelsArray.GetArrayLength());
        Assert.Equal(0, subcircuitsArray.GetArrayLength());
    }
}
