using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using System.IO;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests to catalog library models and identify which ones cannot be instantiated.
/// This helps understand the depth of model compatibility issues.
/// </summary>
public class LibraryModelInstantiationTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;
    private readonly ITestOutputHelper _output;

    public LibraryModelInstantiationTests(ITestOutputHelper output)
    {
        _output = output;
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
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
        
        // Create library service - will be null if no library paths configured
        // This test will work even without library service, just won't find models
        ILibraryService? libraryService = null;
        try
        {
            libraryService = new LibraryService();
            // Try to index default library paths if they exist
            var defaultPaths = new[] { 
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "libraries"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample_libraries")
            };
            var existingPaths = defaultPaths.Where(Directory.Exists).ToList();
            if (existingPaths.Any())
            {
                libraryService.IndexLibraries(existingPaths);
            }
        }
        catch
        {
            // Library service creation failed - test will handle this
            libraryService = null;
        }
        
        var config = new MCPServerConfig { Version = "1.0.0" };
        _mcpService = new MCPService(
            _circuitManager,
            _componentService,
            _modelService,
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
            null);
    }

    [Fact]
    public async Task CatalogLibraryModels_InstantiationStatus()
    {
        // Arrange: Search for all models in the library
        var searchArgs = JsonSerializer.SerializeToElement(new
        {
            query = "",
            limit = 10000, // Get all models
            include_parameters = true
        });

        var searchResult = await _mcpService.ExecuteTool("library_search", searchArgs);
        var searchText = searchResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        Assert.NotNull(searchText);

        var searchData = JsonSerializer.Deserialize<JsonElement>(searchText);
        var models = searchData.GetProperty("models").EnumerateArray().ToList();

        _output.WriteLine($"Found {models.Count} models in library");

        // Catalog results
        var instantiationResults = new List<ModelInstantiationResult>();
        var circuitId = "model_test_circuit";

        // Create a test circuit
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "Model instantiation test circuit"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        if (models.Count == 0)
        {
            _output.WriteLine("No models found in library. Library service may not be configured.");
            return; // Skip test if no library
        }

        // Test each model
        foreach (var modelElement in models)
        {
            var modelName = modelElement.GetProperty("model_name").GetString();
            var modelType = modelElement.GetProperty("model_type").GetString();
            
            if (string.IsNullOrEmpty(modelName) || string.IsNullOrEmpty(modelType))
                continue;

            var result = new ModelInstantiationResult
            {
                ModelName = modelName!,
                ModelType = modelType!,
                HasParameters = modelElement.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object
            };

            try
            {
                // Try to instantiate the model based on type
                var instantiated = await TryInstantiateModel(circuitId, modelName, modelType, modelElement);
                result.CanInstantiate = instantiated;
                result.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                result.CanInstantiate = false;
                result.ErrorMessage = ex.Message;
            }

            instantiationResults.Add(result);
        }

        // Analyze results
        var totalModels = instantiationResults.Count;
        var instantiableModels = instantiationResults.Count(r => r.CanInstantiate);
        var nonInstantiableModels = instantiationResults.Where(r => !r.CanInstantiate).ToList();

        _output.WriteLine($"\n=== Model Instantiation Summary ===");
        _output.WriteLine($"Total models: {totalModels}");
        _output.WriteLine($"Successfully instantiated: {instantiableModels} ({100.0 * instantiableModels / totalModels:F1}%)");
        _output.WriteLine($"Failed to instantiate: {nonInstantiableModels.Count} ({100.0 * nonInstantiableModels.Count / totalModels:F1}%)");

        // Group failures by type
        var failuresByType = nonInstantiableModels
            .GroupBy(r => r.ModelType)
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"\n=== Failures by Model Type ===");
        foreach (var group in failuresByType)
        {
            _output.WriteLine($"{group.Key}: {group.Count()} failures");
            var sampleFailures = group.Take(5).ToList();
            foreach (var failure in sampleFailures)
            {
                _output.WriteLine($"  - {failure.ModelName}: {failure.ErrorMessage?.Substring(0, Math.Min(80, failure.ErrorMessage?.Length ?? 0)) ?? "Unknown error"}");
            }
            if (group.Count() > 5)
            {
                _output.WriteLine($"  ... and {group.Count() - 5} more");
            }
        }

        // List all non-instantiable models
        _output.WriteLine($"\n=== All Non-Instantiable Models ===");
        foreach (var failure in nonInstantiableModels.OrderBy(r => r.ModelType).ThenBy(r => r.ModelName))
        {
            _output.WriteLine($"{failure.ModelType}: {failure.ModelName} - {failure.ErrorMessage}");
        }

        // Assertions for tracking
        Assert.True(totalModels > 0, "Should have found models in library");
        
        // Store results for inspection
        var summary = new
        {
            total_models = totalModels,
            instantiable = instantiableModels,
            non_instantiable = nonInstantiableModels.Count,
            success_rate = 100.0 * instantiableModels / totalModels,
            failures_by_type = failuresByType.ToDictionary(
                g => g.Key,
                g => new { count = g.Count(), models = g.Select(r => new { r.ModelName, r.ErrorMessage }).ToList() }
            )
        };

        _output.WriteLine($"\n=== JSON Summary ===");
        _output.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

        // This test documents the issue - we expect some failures
        // The key is understanding which models fail and why
    }

    private async Task<bool> TryInstantiateModel(string circuitId, string modelName, string modelType, JsonElement modelElement)
    {
        // Map model types to component types
        var componentType = modelType.ToLower() switch
        {
            "npn" or "bjt_npn" => "bjt_npn",
            "pnp" or "bjt_pnp" => "bjt_pnp",
            "nmos" or "mosfet_n" or "n-channel" => "mosfet_n",
            "pmos" or "mosfet_p" or "p-channel" => "mosfet_p",
            "njfet" or "jfet_n" => "jfet_n",
            "pjfet" or "jfet_p" => "jfet_p",
            "diode" => "diode",
            _ => modelType.ToLower()
        };

        // Get parameters if available
        var parameters = new Dictionary<string, object>();
        if (modelElement.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var param in paramsProp.EnumerateObject())
            {
                if (param.Value.ValueKind == JsonValueKind.Number)
                {
                    parameters[param.Name] = param.Value.GetDouble();
                }
            }
        }

        try
        {
            // Create a minimal circuit to test model instantiation
            // For BJT: need collector, base, emitter nodes
            // For MOSFET: need drain, gate, source, bulk nodes
            // For JFET: need drain, gate, source nodes
            // For Diode: need anode, cathode nodes

            var nodes = componentType switch
            {
                "bjt_npn" or "bjt_pnp" => new[] { "c", "b", "e" },
                "mosfet_n" or "mosfet_p" => new[] { "d", "g", "s", "b" },
                "jfet_n" or "jfet_p" => new[] { "d", "g", "s" },
                "diode" => new[] { "a", "k" },
                _ => new[] { "1", "2" } // Generic 2-node
            };

            // Try to add component with this model
            var componentName = $"TEST_{modelName.Replace(".", "_").Replace("-", "_")}";
            
            var addComponentArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                component_name = componentName,
                component_type = componentType,
                nodes = nodes,
                model = modelName,
                parameters = parameters.Count > 0 ? parameters : null
            });

            await _mcpService.ExecuteTool("add_component", addComponentArgs);

            // If we get here, the model was instantiated successfully
            return true;
        }
        catch (Exception)
        {
            // Model instantiation failed
            return false;
        }
    }

    private class ModelInstantiationResult
    {
        public string ModelName { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public bool HasParameters { get; set; }
        public bool CanInstantiate { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
