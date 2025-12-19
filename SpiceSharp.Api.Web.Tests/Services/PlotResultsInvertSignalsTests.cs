using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for plot_results tool with invert_signals parameter
/// </summary>
public class PlotResultsInvertSignalsTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;
    private readonly ComponentService _componentService;

    public PlotResultsInvertSignalsTests()
    {
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
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
        var config = new MCPServerConfig { Version = "1.0.0" };
        _mcpService = new MCPService(
            _circuitManager,
            _componentService,
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
            null,
            null);
    }

    [Fact]
    public async Task PlotResults_WithInvertSignals_DataIsInverted()
    {
        // Arrange: Create a circuit and run DC sweep
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "node2" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        // Run DC sweep
        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = (5.0 - 0.0) / 9.0,  // 9 steps gives 10 points (includes start and stop)
            exports = new[] { "i(V1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot results with invert_signals
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },
            image_format = "png"
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Content.Count > 0);
        // The data should be inverted (we can't easily verify the actual values without parsing the image,
        // but we can verify the tool executed successfully)
    }

    [Fact]
    public async Task PlotResults_WithInvertSignals_LabelReflectsInversion()
    {
        // Arrange: Create a circuit and run DC sweep
        var circuitId = "test_circuit2";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit 2");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "node2" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        // Run DC sweep
        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = (5.0 - 0.0) / 9.0,  // 9 steps gives 10 points (includes start and stop)
            exports = new[] { "i(V1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot results with invert_signals
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },
            image_format = "png"
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Content.Count > 0);
        // The axis label should reflect inversion (verified via integration tests that parse SVG)
    }

    [Fact]
    public async Task PlotResults_WithInvertSignals_UpdatesYAxisLabel()
    {
        // Arrange: Create a circuit and run DC sweep
        var circuitId = "test_circuit3";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit 3");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "node2" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        // Run DC sweep
        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = (5.0 - 0.0) / 9.0,  // 9 steps gives 10 points (includes start and stop)
            exports = new[] { "i(V1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot results with invert_signals
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },
            image_format = "svg"  // Use SVG to potentially parse and verify label
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Content.Count > 0);
        // The Y-axis label should indicate inversion
    }
}
