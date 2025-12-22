using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for plot_impedance with series circuits and complex topologies
/// Reproduces the bug report: series circuits fail while parallel circuits work
/// </summary>
public class PlotImpedanceSeriesCircuitTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public PlotImpedanceSeriesCircuitTests()
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
            _resultsCache,
            config,
            null,
            null);
    }

    [Fact]
    public async Task PlotImpedance_SeriesRC_ShouldWork()
    {
        // Arrange: Create series RC circuit (reported as FAILING)
        // Circuit: input ---[R=1kΩ]---[mid]---[C=1µF]--- 0
        var circuitId = "series_rc";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Series RC Circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        
        // Add resistor R1 (1kΩ) from input to mid
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "input", "mid" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        // Add capacitor C1 (1µF) from mid to ground
        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "mid", "0" },
            Value = 1e-6
        };
        componentService.AddComponent(circuit, c1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "svg",
            output_format = new[] { "text" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        // Should have text content (SVG)
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text" && c.MimeType == "image/svg+xml");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_ParallelRC_ShouldWork()
    {
        // Arrange: Create parallel RC circuit (reported as WORKING)
        // Circuit: input ---[R=1kΩ || C=1µF]--- 0
        var circuitId = "parallel_rc";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Parallel RC Circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        
        // Add resistor R1 (1kΩ) from input to ground
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "input", "0" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        // Add capacitor C1 (1µF) from input to ground (parallel)
        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "input", "0" },
            Value = 1e-6
        };
        componentService.AddComponent(circuit, c1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "svg",
            output_format = new[] { "text" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        // Should have text content (SVG)
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text" && c.MimeType == "image/svg+xml");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_SeriesRL_ShouldWork()
    {
        // Arrange: Create series RL circuit
        // Circuit: input ---[R=1kΩ]---[mid]---[L=1mH]--- 0
        var circuitId = "series_rl";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Series RL Circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "input", "mid" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        var l1Def = new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "mid", "0" },
            Value = 1e-3  // 1mH
        };
        componentService.AddComponent(circuit, l1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "svg",
            output_format = new[] { "text" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text" && c.MimeType == "image/svg+xml");
        Assert.NotNull(textContent);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_RLC_Resonant_ShouldWork()
    {
        // Arrange: Create RLC resonant circuit
        // Circuit: input ---[R=100Ω]---[mid1]---[L=1mH]---[mid2]---[C=1µF]--- 0
        var circuitId = "rlc_resonant";
        var circuit = _circuitManager.CreateCircuit(circuitId, "RLC Resonant Circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "input", "mid1" },
            Value = 100.0
        };
        componentService.AddComponent(circuit, r1Def);

        var l1Def = new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "mid1", "mid2" },
            Value = 1e-3  // 1mH
        };
        componentService.AddComponent(circuit, l1Def);

        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "mid2", "0" },
            Value = 1e-6  // 1µF
        };
        componentService.AddComponent(circuit, c1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "svg",
            output_format = new[] { "text" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text" && c.MimeType == "image/svg+xml");
        Assert.NotNull(textContent);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }
}

