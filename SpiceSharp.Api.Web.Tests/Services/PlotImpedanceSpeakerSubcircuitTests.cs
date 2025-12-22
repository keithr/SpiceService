using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for plot_impedance with real speaker subcircuits
/// Reproduces the client's real-world scenario: speaker crossover with subcircuit speakers
/// </summary>
public class PlotImpedanceSpeakerSubcircuitTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ILibraryService _libraryService;

    public PlotImpedanceSpeakerSubcircuitTests()
    {
        _circuitManager = new CircuitManager();
        
        // Create library service with speaker subcircuit definitions
        var speakerDb = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db"));
        speakerDb.InitializeDatabase();
        _libraryService = new LibraryService(speakerDb);
        
        // Create test subcircuit definitions matching real speaker models
        var tweeterSubcircuit = new SubcircuitDefinition
        {
            Name = "275_030",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* MANUFACTURER: Dayton Audio
* PART_NUMBER: 275-030
* PRODUCT_NAME: Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* TYPE: tweeters
* DIAMETER: 0.75
* IMPEDANCE: 6
* FS: 2000
* QTS: 0.5
* SENSITIVITY: 90
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 6.0
Le 1 2 0.001
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "Dayton Audio" },
                { "PART_NUMBER", "275-030" },
                { "PRODUCT_NAME", "ND20FA-6 3/4\" Soft Dome Neodymium Tweeter" },
                { "TYPE", "tweeters" },
                { "DIAMETER", "0.75" },
                { "IMPEDANCE", "6" },
                { "SENSITIVITY", "90" }
            },
            TsParameters = new Dictionary<string, double>
            {
                { "FS", 2000.0 },
                { "QTS", 0.5 }
            }
        };

        var wooferSubcircuit = new SubcircuitDefinition
        {
            Name = "297_429",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = @"
* HiVi B4N 4"" Aluminum Round Frame Midbass
* MANUFACTURER: HiVi
* PART_NUMBER: 297-429
* PRODUCT_NAME: HiVi B4N 4"" Aluminum Round Frame Midbass
* TYPE: woofers
* DIAMETER: 4
* IMPEDANCE: 8
* FS: 80
* QTS: 0.4
* SENSITIVITY: 85
.SUBCKT 297_429 PLUS MINUS
Re PLUS 1 8.0
Le 1 2 0.002
.ENDS
",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "HiVi" },
                { "PART_NUMBER", "297-429" },
                { "PRODUCT_NAME", "B4N 4\" Aluminum Round Frame Midbass" },
                { "TYPE", "woofers" },
                { "DIAMETER", "4" },
                { "IMPEDANCE", "8" },
                { "SENSITIVITY", "85" }
            },
            TsParameters = new Dictionary<string, double>
            {
                { "FS", 80.0 },
                { "QTS", 0.4 }
            }
        };

        // Index the test subcircuits
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        // Write test library files
        File.WriteAllText(Path.Combine(tempLibPath, "test_tweeter.lib"), tweeterSubcircuit.Definition);
        File.WriteAllText(Path.Combine(tempLibPath, "test_woofer.lib"), wooferSubcircuit.Definition);
        
        _libraryService.IndexLibraries(new[] { tempLibPath });

        // Create services with library service
        var componentService = new ComponentService(_libraryService);
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
        var impedanceAnalysisService = new ImpedanceAnalysisService(acAnalysisService, _libraryService);
        var resultsCache = new CircuitResultsCache();
        var responseMeasurementService = new ResponseMeasurementService(resultsCache);
        var groupDelayService = new GroupDelayService(resultsCache);
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
            resultsCache,
            config,
            _libraryService,
            speakerDb);
    }

    [Fact]
    public async Task PlotImpedance_OvernightSensationCrossover_WithSpeakerSubcircuits_ShouldWork()
    {
        // Arrange: Create Paul Carmody's Overnight Sensation crossover with real speaker subcircuits
        // This matches the client's real-world scenario
        var circuitId = "overnight_sensation_impedance";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Overnight Sensation Crossover - Impedance Test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService(_libraryService);

        // Add input voltage source (for AC analysis - will be removed by impedance tool)
        // Actually, let's NOT add it - the impedance tool should handle this
        // But wait - the client said they need to remove it. Let's test both scenarios.

        // High-pass section (Tweeter path)
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "input", "tw1" },
            Value = 1.5e-6  // 1.5µF
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "inductor",
            Name = "L1",
            Nodes = new List<string> { "tw1", "tw2" },
            Value = 0.00036  // 0.36mH
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "tw2", "tw_out" },
            Value = 6.0
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C2",
            Nodes = new List<string> { "tw_out", "zobel" },
            Value = 2.2e-6  // 2.2µF
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R2",
            Nodes = new List<string> { "zobel", "0" },
            Value = 10.0
        });

        // Add tweeter subcircuit
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "subcircuit",
            Name = "Xtweeter",
            Nodes = new List<string> { "tw_out", "0" },
            Model = "275_030"
        });

        // Low-pass section (Woofer path)
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "inductor",
            Name = "L2",
            Nodes = new List<string> { "input", "wf1" },
            Value = 0.0011  // 1.1mH
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C3",
            Nodes = new List<string> { "wf1", "0" },
            Value = 22e-6  // 22µF
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C4",
            Nodes = new List<string> { "wf1", "wf_out" },
            Value = 5.8e-6  // 5.8µF
        });

        // Add woofer subcircuit
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "subcircuit",
            Name = "Xwoofer",
            Nodes = new List<string> { "wf_out", "0" },
            Model = "297_429"
        });

        // Note: NO voltage source - impedance tool will add its own

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 20,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        // Should have SVG content
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        // Validate SVG structure
        var svgText = textContent.Text;
        Assert.Contains("<svg", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</svg>", svgText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_TweeterPort_WithSubcircuit_ShouldWork()
    {
        // Arrange: Test impedance at tweeter port (tw_out to 0)
        var circuitId = "tweeter_impedance";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Tweeter Impedance Test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService(_libraryService);

        // Add tweeter subcircuit directly
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "subcircuit",
            Name = "Xtweeter",
            Nodes = new List<string> { "tw_out", "0" },
            Model = "275_030"
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "tw_out",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 20,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_WooferPort_WithSubcircuit_ShouldWork()
    {
        // Arrange: Test impedance at woofer port (wf_out to 0)
        var circuitId = "woofer_impedance";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Woofer Impedance Test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService(_libraryService);

        // Add woofer subcircuit directly
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "subcircuit",
            Name = "Xwoofer",
            Nodes = new List<string> { "wf_out", "0" },
            Model = "297_429"
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "wf_out",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 20,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }
}

