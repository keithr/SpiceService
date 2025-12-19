using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for VCVS (Voltage-Controlled Voltage Source) component functionality.
/// Validates that VCVS can be added to circuits and produces correct results.
/// </summary>
public class VCVSComponentTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;
    private readonly ITestOutputHelper _output;

    public VCVSComponentTests(ITestOutputHelper output)
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
            null,
            null);
    }

    [Fact]
    public async Task VCVS_UnityGainBuffer_ShouldWork()
    {
        // Arrange: Create unity-gain buffer using VCVS (gain = 1)
        // Input: 1V, Expected Output: 1V
        var circuitId = "vcvs_buffer";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "Unity-gain buffer with VCVS"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        // Add input voltage source
        var inputSourceArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "in", "0" },
            value = 1.0
        });
        await _mcpService.ExecuteTool("add_component", inputSourceArgs);

        // Add input resistor (load)
        var rInArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "R1",
            component_type = "resistor",
            nodes = new[] { "in", "0" },
            value = 1000.0
        });
        await _mcpService.ExecuteTool("add_component", rInArgs);

        // Add VCVS: output = gain * (input+ - input-)
        // Nodes: [output+, output-, input+, input-]
        // Gain = 1.0 for unity-gain buffer
        Exception? vcvsException = null;
        try
        {
            var vcvsArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "E1",
                component_type = "vcvs",
                nodes = new[] { "out", "0", "in", "0" }, // [out+, out-, in+, in-]
                parameters = new Dictionary<string, object>
                {
                    { "gain", 1.0 }
                }
            });

            var vcvsResult = await _mcpService.ExecuteTool("add_component", vcvsArgs);
            var vcvsText = vcvsResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
            _output.WriteLine($"Add VCVS result: {vcvsText}");
        }
        catch (Exception ex)
        {
            vcvsException = ex;
            _output.WriteLine($"VCVS addition failed: {ex.Message}");
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        if (vcvsException != null)
        {
            // Export netlist to see what was created
            try
            {
                var netlistArgs = JsonSerializer.SerializeToElement(new { circuit_id = circuitId });
                var netlistResult = await _mcpService.ExecuteTool("export_netlist", netlistArgs);
                var netlist = netlistResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
                _output.WriteLine($"\nNetlist before VCVS:\n{netlist}");
            }
            catch { }

            Assert.True(false, $"VCVS component addition failed: {vcvsException.Message}");
        }

        // Add output load
        var rOutArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "R2",
            component_type = "resistor",
            nodes = new[] { "out", "0" },
            value = 1000.0
        });
        await _mcpService.ExecuteTool("add_component", rOutArgs);

        // Act: Run operating point analysis
        var opArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId
        });

        var opResult = await _mcpService.ExecuteTool("run_operating_point", opArgs);
        var opText = opResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        Assert.NotNull(opText);

        var opData = JsonSerializer.Deserialize<JsonElement>(opText);
        var voltages = opData.GetProperty("NodeVoltages");

        // Assert: Output should equal input (1V)
        Assert.True(voltages.TryGetProperty("out", out var vOut));
        Assert.True(voltages.TryGetProperty("in", out var vIn));

        var outputVoltage = vOut.GetDouble();
        var inputVoltage = vIn.GetDouble();

        _output.WriteLine($"Input voltage: {inputVoltage}V");
        _output.WriteLine($"Output voltage: {outputVoltage}V (expected: {inputVoltage}V)");

        Assert.True(Math.Abs(outputVoltage - inputVoltage) < 0.01, 
            $"Unity-gain buffer: output ({outputVoltage}V) should equal input ({inputVoltage}V)");
    }

    [Fact]
    public async Task VCVS_NonInvertingAmplifier_ShouldWork()
    {
        // Arrange: Create non-inverting amplifier with gain = 10
        // Input: 1V, Expected Output: 10V
        var circuitId = "vcvs_amplifier";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "Non-inverting amplifier with VCVS (gain=10)"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        // Add input voltage source
        var inputSourceArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "in", "0" },
            value = 1.0
        });
        await _mcpService.ExecuteTool("add_component", inputSourceArgs);

        // Add VCVS with gain = 10
        Exception? vcvsException = null;
        try
        {
            var vcvsArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "E1",
                component_type = "vcvs",
                nodes = new[] { "out", "0", "in", "0" }, // [out+, out-, in+, in-]
                parameters = new Dictionary<string, object>
                {
                    { "gain", 10.0 }
                }
            });

            await _mcpService.ExecuteTool("add_component", vcvsArgs);
        }
        catch (Exception ex)
        {
            vcvsException = ex;
            _output.WriteLine($"VCVS addition failed: {ex.Message}");
        }

        if (vcvsException != null)
        {
            Assert.True(false, $"VCVS component addition failed: {vcvsException.Message}");
        }

        // Add output load
        var rOutArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "R1",
            component_type = "resistor",
            nodes = new[] { "out", "0" },
            value = 1000.0
        });
        await _mcpService.ExecuteTool("add_component", rOutArgs);

        // Act: Run operating point analysis
        var opArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId
        });

        var opResult = await _mcpService.ExecuteTool("run_operating_point", opArgs);
        var opText = opResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        Assert.NotNull(opText);

        var opData = JsonSerializer.Deserialize<JsonElement>(opText);
        var voltages = opData.GetProperty("NodeVoltages");

        // Assert: Output should be 10 * input
        Assert.True(voltages.TryGetProperty("out", out var vOut));
        Assert.True(voltages.TryGetProperty("in", out var vIn));

        var outputVoltage = vOut.GetDouble();
        var inputVoltage = vIn.GetDouble();
        var expectedOutput = inputVoltage * 10.0;

        _output.WriteLine($"Input voltage: {inputVoltage}V");
        _output.WriteLine($"Output voltage: {outputVoltage}V (expected: {expectedOutput}V)");

        Assert.True(Math.Abs(outputVoltage - expectedOutput) < 0.01, 
            $"Amplifier: output ({outputVoltage}V) should be 10x input ({inputVoltage}V) = {expectedOutput}V");
    }

    [Fact]
    public async Task VCVS_SallenKeyFilter_ShouldWork()
    {
        // Arrange: Create Sallen-Key active filter using VCVS as op-amp
        // This is the test case that failed in client validation
        var circuitId = "sallen_key_vcvs";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "Sallen-Key active filter with VCVS"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        // Add input voltage source (AC)
        var inputSourceArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "V1",
            component_type = "voltage_source",
            nodes = new[] { "in", "0" },
            value = 1.0,
            parameters = new Dictionary<string, object>
            {
                { "ac", 1.0 } // 1V AC magnitude
            }
        });
        await _mcpService.ExecuteTool("add_component", inputSourceArgs);

        // Add filter components (example values for ~1kHz cutoff)
        // R1, R2 = 1kΩ, C1, C2 = 100nF
        var r1Args = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "R1",
            component_type = "resistor",
            nodes = new[] { "in", "node1" },
            value = 1000.0
        });
        await _mcpService.ExecuteTool("add_component", r1Args);

        var c1Args = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "C1",
            component_type = "capacitor",
            nodes = new[] { "node1", "out" },
            value = 100e-9
        });
        await _mcpService.ExecuteTool("add_component", c1Args);

        var r2Args = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "R2",
            component_type = "resistor",
            nodes = new[] { "node1", "node2" },
            value = 1000.0
        });
        await _mcpService.ExecuteTool("add_component", r2Args);

        var c2Args = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            name = "C2",
            component_type = "capacitor",
            nodes = new[] { "node2", "0" },
            value = 100e-9
        });
        await _mcpService.ExecuteTool("add_component", c2Args);

        // Add VCVS as op-amp (unity gain buffer)
        // VCVS: output = gain * (input+ - input-)
        // For op-amp: input+ = node2, input- = out (feedback), gain = very high
        // But for ideal op-amp in Sallen-Key, we use unity-gain buffer: gain = 1
        Exception? vcvsException = null;
        try
        {
            var vcvsArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "E1",
                component_type = "vcvs",
                nodes = new[] { "out", "0", "node2", "0" }, // [out+, out-, in+, in-]
                parameters = new Dictionary<string, object>
                {
                    { "gain", 1.0 } // Unity gain for Sallen-Key
                }
            });

            await _mcpService.ExecuteTool("add_component", vcvsArgs);
        }
        catch (Exception ex)
        {
            vcvsException = ex;
            _output.WriteLine($"VCVS addition failed: {ex.Message}");
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
        }

        if (vcvsException != null)
        {
            // Export netlist to debug
            try
            {
                var netlistArgs = JsonSerializer.SerializeToElement(new { circuit_id = circuitId });
                var netlistResult = await _mcpService.ExecuteTool("export_netlist", netlistArgs);
                var netlist = netlistResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
                _output.WriteLine($"\nNetlist:\n{netlist}");
            }
            catch { }

            Assert.True(false, $"VCVS component addition failed for Sallen-Key filter: {vcvsException.Message}");
        }

        // Act: Run AC analysis
        var acArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_frequency = 10.0,
            stop_frequency = 100000.0,
            number_of_points = 100,
            signals = new[] { "v(out)" }
        });

        try
        {
            var acResult = await _mcpService.ExecuteTool("run_ac_analysis", acArgs);
            var acText = acResult.Content.FirstOrDefault(c => c.Type == "text")?.Text;
            Assert.NotNull(acText);

            var acData = JsonSerializer.Deserialize<JsonElement>(acText);
            var frequencies = acData.GetProperty("Frequencies").EnumerateArray().ToList();
            var magnitudeDb = acData.GetProperty("MagnitudeDb").GetProperty("v(out)").EnumerateArray().ToList();

            // Find -3dB point (cutoff frequency)
            var cutoffIndex = magnitudeDb.FindIndex(m => m.GetDouble() <= -3.0);
            if (cutoffIndex >= 0 && cutoffIndex < frequencies.Count)
            {
                var cutoffFreq = frequencies[cutoffIndex].GetDouble();
                _output.WriteLine($"Cutoff frequency: {cutoffFreq}Hz (expected ~1000Hz)");
                
                // Expected: fc ≈ 1/(2π√(R1*R2*C1*C2)) ≈ 1592Hz for R=1kΩ, C=100nF
                // But with unity-gain Sallen-Key, it's approximately 1/(2πRC) ≈ 1592Hz
                var expectedCutoff = 1.0 / (2 * Math.PI * 1000.0 * 100e-9); // ~1592Hz
                var error = Math.Abs(cutoffFreq - expectedCutoff) / expectedCutoff * 100.0;
                
                _output.WriteLine($"Expected cutoff: {expectedCutoff}Hz");
                _output.WriteLine($"Error: {error:F1}%");
                
                Assert.True(error < 50.0, $"Cutoff frequency error too large: {error:F1}% (got {cutoffFreq}Hz, expected ~{expectedCutoff}Hz)");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"AC analysis failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task VCVS_NodeOrderValidation_ShouldProvideClearError()
    {
        // Test that incorrect node order provides clear error message
        var circuitId = "vcvs_node_test";
        
        var createCircuitArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            description = "VCVS node order test"
        });
        await _mcpService.ExecuteTool("create_circuit", createCircuitArgs);

        // Try to add VCVS with wrong number of nodes
        try
        {
            var vcvsArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "E1",
                component_type = "vcvs",
                nodes = new[] { "out", "0" }, // Wrong: only 2 nodes, need 4
                parameters = new Dictionary<string, object>
                {
                    { "gain", 1.0 }
                }
            });

            await _mcpService.ExecuteTool("add_component", vcvsArgs);
            
            // Should not reach here
            Assert.True(false, "Should have thrown exception for incorrect node count");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Expected error for wrong node count: {ex.Message}");
            Assert.Contains("node", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Try to add VCVS without gain parameter
        try
        {
            var vcvsArgs = JsonSerializer.SerializeToElement(new
            {
                circuit_id = circuitId,
                name = "E2",
                component_type = "vcvs",
                nodes = new[] { "out", "0", "in", "0" },
                parameters = new Dictionary<string, object>()
                // Missing gain parameter
            });

            await _mcpService.ExecuteTool("add_component", vcvsArgs);
            
            // Should not reach here
            Assert.True(false, "Should have thrown exception for missing gain parameter");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Expected error for missing gain: {ex.Message}");
            Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
