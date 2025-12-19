using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for PULSE voltage source functionality in transient analysis.
/// Validates that step response and pulse waveforms work correctly.
/// </summary>
public class PulseVoltageSourceTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;
    private readonly ITestOutputHelper _output;

    public PulseVoltageSourceTests(ITestOutputHelper output)
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
        _resultsCache = new CircuitResultsCache();
        var responseMeasurementService = new ResponseMeasurementService(_resultsCache);
        var groupDelayService = new GroupDelayService(_resultsCache);
        var netlistParser = new NetlistParser();
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
            _resultsCache,
            config,
            null,
            null);
    }

    [Fact]
    public async Task PulseVoltageSource_RCChargingCircuit_ShouldProduceStepResponse()
    {
        // Arrange: Create RC charging circuit with PULSE voltage source
        // Step from 0V to 5V at t=0
        var circuitId = "rc_charging_pulse";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "RC charging circuit with PULSE source"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        // Add PULSE voltage source: step from 0V to 5V
        var pulseSourceArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "input", "0" },
            value = 0.0, // Initial value
            parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "pulse_v1", 0.0 },      // Initial value
                { "pulse_v2", 5.0 },      // Pulsed value
                { "pulse_td", 0.0 },       // Delay time
                { "pulse_tr", 1e-9 },      // Rise time (very fast step)
                { "pulse_tf", 1e-9 },     // Fall time
                { "pulse_pw", 1.0 },      // Pulse width (long enough for RC response)
                { "pulse_per", 2.0 }      // Period
            }
        });

        var addSourceResult = await _mcpService.ExecuteTool("add_component", pulseSourceArgs);
        var sourceText = addSourceResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        _output.WriteLine($"Add PULSE source result: {sourceText}");

        // Add RC components: R=1kΩ, C=1µF (τ = RC = 1ms)
        var rArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "R1",
            component_type = "resistor",
            nodes = new[] { "input", "output" },
            value = 1000.0
        });
        await _mcpService.ExecuteTool("add_component", rArgs);

        var cArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "C1",
            component_type = "capacitor",
            nodes = new[] { "output", "0" },
            value = 1e-6
        });
        await _mcpService.ExecuteTool("add_component", cArgs);

        // Act: Run transient analysis
        var transientArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_time = 0.0,
            stop_time = 0.01, // 10ms (10 time constants)
            time_step = 1e-5, // 10µs steps
            signals = new[] { "v(output)", "v(input)" },
            use_initial_conditions = false
        });

        JsonElement? resultData = null;
        Exception? transientException = null;
        
        try
        {
            var transientResult = await _mcpService.ExecuteTool("run_transient_analysis", transientArgs);
            var transientText = transientResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
            Assert.NotNull(transientText);
            resultData = JsonSerializer.Deserialize<JsonElement>(transientText);
        }
        catch (Exception ex)
        {
            transientException = ex;
            _output.WriteLine($"Transient analysis failed: {ex.Message}");
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
        }

        // Assert: Verify results
        if (transientException != null)
        {
            _output.WriteLine($"\n=== PULSE Source Test FAILED ===");
            _output.WriteLine($"Error: {transientException.Message}");
            _output.WriteLine($"Stack trace: {transientException.StackTrace}");
            
            // Export netlist to see what was actually created
            try
            {
                var netlistArgs = JsonSerializer.SerializeToElement(new { circuit_id = circuitId });
                var netlistResult = await _mcpService.ExecuteTool("export_netlist", netlistArgs);
                var netlist = netlistResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
                _output.WriteLine($"\nNetlist:\n{netlist}");
            }
            catch { }

            Assert.True(false, $"PULSE voltage source failed: {transientException.Message}");
        }

        Assert.True(resultData.HasValue, "Should have transient analysis result");
        var result = resultData.Value;
        
        Assert.True(result.TryGetProperty("Time", out var timeProp), "Should have time array");
        var timeArray = timeProp.EnumerateArray().ToList();
        Assert.True(timeArray.Count > 0, "Should have time points");
        
        Assert.True(result.TryGetProperty("Signals", out var signalsProp), "Should have signals object");
        Assert.True(signalsProp.TryGetProperty("v(output)", out var vOutProp), "Should have output voltage signal");
        Assert.True(signalsProp.TryGetProperty("v(input)", out var vInProp), "Should have input voltage signal");

        var inputVoltage = vInProp.EnumerateArray().Select(v => v.GetDouble()).ToList();
        var outputVoltage = vOutProp.EnumerateArray().Select(v => v.GetDouble()).ToList();
        var time = timeArray.Select(t => t.GetDouble()).ToList();

        // Verify step response characteristics
        // At t=0, output should be near 0V
        var initialOutput = outputVoltage[0];
        _output.WriteLine($"Initial output voltage: {initialOutput}V (expected ~0V)");

        // At t >> τ, output should approach 5V
        var finalOutput = outputVoltage[outputVoltage.Count - 1];
        _output.WriteLine($"Final output voltage: {finalOutput}V (expected ~5V)");

        // Input should step from 0V to 5V
        var initialInput = inputVoltage[0];
        var finalInput = inputVoltage[inputVoltage.Count - 1];
        _output.WriteLine($"Input voltage: {initialInput}V -> {finalInput}V (expected 0V -> 5V)");

        // Verify exponential charging curve
        // At t = τ, output should be ~63% of final value (5V * 0.632 = 3.16V)
        var tau = 0.001; // RC = 1ms
        var tauIndex = time.FindIndex(t => t >= tau);
        if (tauIndex >= 0 && tauIndex < outputVoltage.Count)
        {
            var voltageAtTau = outputVoltage[tauIndex];
            var expectedAtTau = 5.0 * (1 - Math.Exp(-1)); // ~3.16V
            _output.WriteLine($"Voltage at τ (1ms): {voltageAtTau}V (expected ~{expectedAtTau:F2}V)");
            
            var error = Math.Abs(voltageAtTau - expectedAtTau);
            Assert.True(error < 0.5, $"Voltage at τ should be ~{expectedAtTau:F2}V, got {voltageAtTau:F2}V (error: {error:F2}V)");
        }

        Assert.True(Math.Abs(initialOutput) < 0.1, $"Initial output should be ~0V, got {initialOutput}V");
        Assert.True(Math.Abs(finalOutput - 5.0) < 0.5, $"Final output should be ~5V, got {finalOutput}V");
        Assert.True(Math.Abs(initialInput) < 0.1, $"Initial input should be ~0V, got {initialInput}V");
        Assert.True(Math.Abs(finalInput - 5.0) < 0.1, $"Final input should be ~5V, got {finalInput}V");
    }

    [Fact]
    public async Task PulseVoltageSource_AlternativeParameterFormat_ShouldWork()
    {
        // Test alternative parameter format: v1, v2 instead of pulse_v1, pulse_v2
        var circuitId = "rc_charging_pulse_alt";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "RC circuit with PULSE source (alternative format)"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        // Add PULSE source with alternative parameter names
        var pulseSourceArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "input", "0" },
            value = 0.0,
            parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "v1", 0.0 },           // Alternative format
                { "v2", 5.0 },           // Alternative format
                { "td", 0.0 },
                { "tr", 1e-9 },
                { "tf", 1e-9 },
                { "pw", 1.0 },
                { "per", 2.0 }
            }
        });

        try
        {
            await _mcpService.ExecuteTool("add_component", pulseSourceArgs);
            
            // Add RC components
            var rArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "R1",
                component_type = "resistor",
                nodes = new[] { "input", "output" },
                value = 1000.0
            });
            await _mcpService.ExecuteTool("add_component", rArgs);

            var cArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "C1",
                component_type = "capacitor",
                nodes = new[] { "output", "0" },
                value = 1e-6
            });
            await _mcpService.ExecuteTool("add_component", cArgs);

            // Run transient analysis
            var transientArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                start_time = 0.0,
                stop_time = 0.01,
                time_step = 1e-5,
                signals = new[] { "v(output)" }
            });

            var transientResult = await _mcpService.ExecuteTool("run_transient_analysis", transientArgs);
            var transientText = transientResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
            Assert.NotNull(transientText);
            var result = JsonSerializer.Deserialize<JsonElement>(transientText);
            
            Assert.True(result.TryGetProperty("Time", out var timeProp));
            Assert.True(result.TryGetProperty("Signals", out var signalsProp));
            Assert.True(signalsProp.TryGetProperty("v(output)", out _));
            var timeCount = timeProp.EnumerateArray().Count();
            _output.WriteLine($"Alternative format test: SUCCESS - {timeCount} time points");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Alternative format test FAILED: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task PulseVoltageSource_NetlistExport_ShouldShowPulseParameters()
    {
        // Verify that PULSE parameters are correctly exported to netlist
        var circuitId = "pulse_netlist_test";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "PULSE source netlist export test"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        var pulseSourceArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "input", "0" },
            value = 0.0,
            parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "pulse_v1", 0.0 },
                { "pulse_v2", 5.0 },
                { "pulse_td", 0.0 },
                { "pulse_tr", 1e-6 },
                { "pulse_tf", 1e-6 },
                { "pulse_pw", 1e-3 },
                { "pulse_per", 2e-3 }
            }
        });

        await _mcpService.ExecuteTool("add_component", pulseSourceArgs);

        // Export netlist
        var netlistArgs = JsonSerializer.SerializeToElement(new { circuit_id = circuitId });
        var netlistResult = await _mcpService.ExecuteTool("export_netlist", netlistArgs);
        var netlist = netlistResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        
        Assert.NotNull(netlist);
        _output.WriteLine($"Netlist:\n{netlist}");

        // Verify netlist contains PULSE parameters
        Assert.Contains("PULSE", netlist, StringComparison.OrdinalIgnoreCase);
        // Note: Exact format depends on netlist exporter implementation
    }
}
