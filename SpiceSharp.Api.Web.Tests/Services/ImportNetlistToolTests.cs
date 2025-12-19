using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for ImportNetlist MCP tool
/// </summary>
public class ImportNetlistToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;

    public ImportNetlistToolTests()
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
            null,
            null);
    }

    [Fact]
    public async Task ExecuteImportNetlist_WithValidNetlist_CreatesCircuit()
    {
        // Arrange
        var netlist = @"
* Simple RC circuit
R1 in out 1k
C1 out 0 100n
V1 in 0 DC 5
";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_import1"
        });

        // Act
        var result = await _mcpService.ExecuteTool("import_netlist", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("circuit_id"));
        var circuitId = response["circuit_id"].GetString();
        Assert.Equal("test_import1", circuitId);
        
        // Verify circuit was created
        var circuit = _circuitManager.GetCircuit(circuitId);
        Assert.NotNull(circuit);
        
        // Verify components were added (check via component service)
        // Note: We'd need to query components to verify, but circuit exists
    }

    [Fact]
    public async Task ExecuteImportNetlist_WithSetActive_ActivatesCircuit()
    {
        // Arrange
        var netlist = @"
R1 in out 1k
";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_import2",
            set_active = true
        });

        // Act
        await _mcpService.ExecuteTool("import_netlist", arguments);

        // Assert
        var activeCircuit = _circuitManager.GetActiveCircuit();
        Assert.NotNull(activeCircuit);
        Assert.Equal("test_import2", activeCircuit.Id);
    }

    [Fact]
    public async Task ExecuteImportNetlist_WithInvalidNetlist_ThrowsException()
    {
        // Arrange
        var netlist = @"
R1 invalid line format
";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_import3"
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _mcpService.ExecuteTool("import_netlist", arguments));
    }

    [Fact]
    public async Task ExecuteImportNetlist_WithModel_CreatesModel()
    {
        // Arrange
        var netlist = @"
D1 anode cathode DMODEL
.MODEL DMODEL D (IS=1e-14 N=1.05)
";

        var arguments = JsonSerializer.SerializeToElement(new
        {
            netlist = netlist,
            circuit_name = "test_import4"
        });

        // Act
        var result = await _mcpService.ExecuteTool("import_netlist", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("components_added"));
        Assert.True(response.ContainsKey("models_added"));
        
        // Should have 1 component and 1 model
        Assert.Equal(1, response["components_added"].GetInt32());
        Assert.Equal(1, response["models_added"].GetInt32());
    }
}
