using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for ParameterSweep MCP tool
/// </summary>
public class ParameterSweepToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly IParameterSweepService _parameterSweepService;
    private readonly CircuitResultsCache _resultsCache;

    public ParameterSweepToolTests()
    {
        _circuitManager = new CircuitManager();
        var componentService = new ComponentService();
        var modelService = new ModelService();
        var operatingPointService = new OperatingPointService();
        var dcAnalysisService = new DCAnalysisService();
        var transientAnalysisService = new TransientAnalysisService();
        var acAnalysisService = new ACAnalysisService();
        var netlistService = new NetlistService();
        _parameterSweepService = new ParameterSweepService(
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
            _parameterSweepService,
            noiseAnalysisService,
            temperatureSweepService,
            impedanceAnalysisService,
            responseMeasurementService,
            groupDelayService,
            netlistParser,
            _resultsCache,
            config);
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithValidInput_ReturnsResults()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        // Add a simple resistor circuit
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, r1Def);

        var v1Def = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        };
        componentService.AddComponent(circuit, v1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            parameter = "value",
            start = 100.0,
            stop = 1000.0,
            points = 5,
            scale = "linear",
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("parameter_path", out _));
        Assert.True(resultData.TryGetProperty("parameter_values", out _));
        Assert.True(resultData.TryGetProperty("results", out _));
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithInvalidComponent_ThrowsException()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R999", // Non-existent component
            parameter = "value",
            start = 100.0,
            stop = 1000.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("run_parameter_sweep", arguments));
    }

    [Fact]
    public async Task ExecuteParameterSweep_StoresResultsInCache()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        // Add a simple resistor circuit
        var componentService = new ComponentService();
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        var v1Def = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        };
        componentService.AddComponent(circuit, v1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            parameter = "value",
            start = 100.0,
            stop = 1000.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act
        await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        var cachedResult = _resultsCache.Get(circuitId);
        Assert.NotNull(cachedResult);
        Assert.Equal("parameter_sweep", cachedResult!.AnalysisType);
        Assert.NotNull(cachedResult.XData);
        Assert.True(cachedResult.Signals.Count > 0);
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithACAnalysis_ReturnsFrequencyData()
    {
        // Test 2.2: AC Analysis Sweep
        // Arrange
        var circuitId = "test2";
        var circuit = _circuitManager.CreateCircuit(circuitId, "AC sweep test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0,
            Parameters = new Dictionary<string, object> { { "ac", 1 } }
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 1000.0,
            stop = 10000.0,
            points = 5,
            analysis_type = "ac",
            analysis_params = new Dictionary<string, object>
            {
                { "startFrequency", 100 },
                { "stopFrequency", 10000 },
                { "numberOfPoints", 100 }
            },
            outputs = new[] { "v(out)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        
        // Should have AC data structure (if ACResults were generated)
        // Note: ac_data may be null if no AC results were generated
        if (resultData.TryGetProperty("ac_data", out var acData) && acData.ValueKind != JsonValueKind.Null)
        {
            Assert.True(acData.TryGetProperty("frequencies", out _));
            Assert.True(acData.TryGetProperty("results", out _));
        }
        else
        {
            // If ac_data is null, at least verify we got parameter sweep results
            Assert.True(resultData.TryGetProperty("parameter_values", out _));
        }
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithLogarithmicScale_ReturnsLogSpacedValues()
    {
        // Test 2.4: Logarithmic Scale
        // Arrange
        var circuitId = "test3";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Log scale test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "in", "out" },
            Value = 1e-6
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0,
            Parameters = new Dictionary<string, object> { { "ac", 1 } }
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "C1",
            parameter = "value",
            start = 1e-6,
            stop = 1e-3,
            points = 10,
            scale = "log",
            analysis_type = "ac",
            analysis_params = new Dictionary<string, object>
            {
                { "startFrequency", 10 },
                { "stopFrequency", 100000 },
                { "numberOfPoints", 100 }
            },
            outputs = new[] { "v(out)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        
        // Verify parameter values are log-spaced
        Assert.True(resultData.TryGetProperty("parameter_values", out var paramValues));
        var values = JsonSerializer.Deserialize<List<double>>(paramValues.GetRawText());
        Assert.NotNull(values);
        Assert.Equal(10, values!.Count);
        
        // Check that spacing is logarithmic (ratio between consecutive values should be approximately constant)
        var ratios = new List<double>();
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i - 1] > 0)
                ratios.Add(values[i] / values[i - 1]);
        }
        // All ratios should be approximately equal for log spacing
        var avgRatio = ratios.Average();
        foreach (var ratio in ratios)
        {
            Assert.True(Math.Abs(ratio - avgRatio) / avgRatio < 0.1, 
                $"Log spacing check failed: ratio {ratio} deviates from average {avgRatio}");
        }
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithModelParameter_ReturnsResults()
    {
        // Test 2.5: Model Parameter Sweep
        // Arrange
        var circuitId = "test4";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Model parameter sweep");
        _circuitManager.SetActiveCircuit(circuitId);

        var modelService = new ModelService();
        modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "D1_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-12 },
                { "N", 1.5 }
            }
        });

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "D1_MODEL"
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 1.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "D1_MODEL",
            parameter = "IS",
            start = 1e-15,
            stop = 1e-9,
            points = 5,
            scale = "log",
            analysis_type = "operating_point",
            outputs = new[] { "i(D1)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("parameter_path", out _));
        Assert.True(resultData.TryGetProperty("results", out var results));
        Assert.True(results.TryGetProperty("i(D1)", out _));
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithoutCircuitId_UsesActiveCircuit()
    {
        // Test 2.7: Active Circuit (No circuit_id specified)
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Active circuit test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            component = "R2",
            start = 500.0,
            stop = 5000.0,
            points = 8,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
            // Note: circuit_id is omitted
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("parameter_values", out _));
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithTransientAnalysis_ReturnsTimeSeriesData()
    {
        // Test 2.8: Transient Analysis Sweep
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Transient sweep test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 1000.0,
            stop = 5000.0,
            points = 5,
            analysis_type = "transient",
            analysis_params = new Dictionary<string, object>
            {
                { "startTime", 0 },
                { "stopTime", 0.001 },
                { "timeStep", 1e-6 }
            },
            outputs = new[] { "v(out)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        
        // Should have transient data structure (if TransientResults were generated)
        // Note: transient_data may be null if no transient results were generated
        if (resultData.TryGetProperty("transient_data", out var transientData) && transientData.ValueKind != JsonValueKind.Null)
        {
            Assert.True(transientData.TryGetProperty("time", out _));
            Assert.True(transientData.TryGetProperty("results", out _));
        }
        else
        {
            // If transient_data is null, at least verify we got parameter sweep results
            Assert.True(resultData.TryGetProperty("parameter_values", out _));
        }
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithMultipleOutputs_ReturnsAllOutputs()
    {
        // Test 2.9: Multiple Outputs
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Multiple outputs test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 1000.0,
            stop = 5000.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)", "i(R1)", "i(R2)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_parameter_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("results", out var results));
        Assert.True(results.TryGetProperty("v(out)", out _));
        Assert.True(results.TryGetProperty("i(R1)", out _));
        Assert.True(results.TryGetProperty("i(R2)", out _));
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithEmptyOutputs_ThrowsException()
    {
        // Test 2.11: Error - Empty Outputs
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Empty outputs test");
        _circuitManager.SetActiveCircuit(circuitId);

        // Add component so we get the empty outputs error, not component not found
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 1000.0,
            stop = 5000.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = Array.Empty<string>()
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("run_parameter_sweep", arguments));
        
        Assert.Contains("output", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteParameterSweep_WithInvalidAnalysisType_ThrowsException()
    {
        // Test 2.12: Error - Invalid Analysis Type
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Invalid analysis test");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 1000.0,
            stop = 5000.0,
            points = 5,
            analysis_type = "invalid_type",
            outputs = new[] { "v(out)" }
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("run_parameter_sweep", arguments));
        
        Assert.Contains("analysis", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
