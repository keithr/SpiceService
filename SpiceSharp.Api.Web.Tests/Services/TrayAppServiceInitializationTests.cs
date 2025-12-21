using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Tests.Infrastructure;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests that verify the tray app service initialization order works correctly.
/// 
/// The tray app creates services in this order:
/// 1. CircuitManager
/// 2. Other services (ModelService, etc.)
/// 3. LibraryService (if library paths configured)
/// 4. ComponentService (AFTER LibraryService, with LibraryService passed in)
/// 5. MCPService (with ComponentService that has LibraryService)
/// 
/// This test verifies that ComponentService gets LibraryService correctly.
/// </summary>
public class TrayAppServiceInitializationTests
{
    [Fact]
    public async Task TrayAppInitializationOrder_ComponentServiceGetsLibraryService_ShouldWork()
    {
        // Simulate the tray app initialization order
        // Step 1: Create CircuitManager
        var circuitManager = new CircuitManager();
        
        // Step 2: Create other services (like tray app does)
        var modelService = new ModelService();
        var operatingPointService = new OperatingPointService();
        var dcAnalysisService = new DCAnalysisService();
        var transientAnalysisService = new TransientAnalysisService();
        var acAnalysisService = new ACAnalysisService();
        var netlistService = new NetlistService();
        
        // Step 3: Create SpeakerDatabaseService
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDatabaseService = new SpeakerDatabaseService(tempDbPath);
        speakerDatabaseService.InitializeDatabase();
        
        // Step 4: Create LibraryService FIRST (with test library)
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        
        var subcircuitDef = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS MINUS 2.73
Le PLUS MINUS 0.001
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "275_030.lib"), subcircuitDef);
        
        var libraryService = new LibraryService(speakerDatabaseService);
        libraryService.IndexLibraries(new[] { tempLibPath });
        
        // Step 5: Create ComponentService AFTER LibraryService (like tray app does now)
        var componentService = new ComponentService(libraryService);
        
        // Verify ComponentService has LibraryService
        // We can't directly check the private field, but we can test it by adding a subcircuit
        var circuit = new CircuitModel
        {
            Id = "test_tray_init",
            Description = "Test circuit for tray app initialization"
        };
        
        // Add a voltage source first
        var voltageSource = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 1.0,
            Parameters = new Dictionary<string, object> { { "ac", 1 } }
        };
        componentService.AddComponent(circuit, voltageSource);
        
        // Now add a subcircuit - THIS SHOULD WORK if LibraryService is available
        var subcircuitComponent = new ComponentDefinition
        {
            Name = "Xspk",
            ComponentType = "subcircuit",
            Model = "275_030",
            Nodes = new List<string> { "in", "0" }
        };
        
        // This should NOT throw an exception about "library service is not available"
        var subcircuitEntity = componentService.AddComponent(circuit, subcircuitComponent);
        
        // Verify the subcircuit was added
        Assert.NotNull(subcircuitEntity);
        
        // Get the component from the circuit manager or check via GetComponent
        var addedComponent = componentService.GetComponent(circuit, "Xspk");
        Assert.NotNull(addedComponent);
        
        // Verify the subcircuit definition exists in the circuit
        // ComponentService stores definitions internally, so we verify via GetComponent
        var componentInfo = componentService.GetComponent(circuit, "Xspk");
        Assert.NotNull(componentInfo);
        
        // Cleanup
        try
        {
            Directory.Delete(tempLibPath, true);
            File.Delete(tempDbPath);
        }
        catch { }
    }

    [Fact]
    public async Task TrayAppInitializationOrder_HttpIntegration_ShouldWork()
    {
        // This test uses the actual HTTP endpoint to verify the tray app initialization works
        // The SpiceServiceWebApplicationFactory uses the correct DI order from Program.cs
        // which should match the tray app's corrected initialization order
        
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var subcircuitDef = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS MINUS 2.73
Le PLUS MINUS 0.001
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "275_030.lib"), subcircuitDef);
        });
        using var client = factory.CreateClient();

        // Step 1: Create circuit
        var createRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "create_circuit",
                arguments = new { circuit_id = "test_tray_http" }
            }
        };
        await client.PostAsJsonAsync("/mcp", createRequest);

        // Step 2: Add voltage source
        var addVoltageRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    component_type = "voltage_source",
                    name = "V1",
                    nodes = new[] { "in", "0" },
                    parameters = new Dictionary<string, object> { { "ac", 1 } },
                    value = 1
                }
            }
        };
        await client.PostAsJsonAsync("/mcp", addVoltageRequest);

        // Step 3: Add subcircuit - THIS SHOULD WORK with correct initialization order
        var addSubcircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    component_type = "subcircuit",
                    model = "275_030",
                    name = "Xspk",
                    nodes = new[] { "in", "0" }
                }
            }
        };

        var addSubcircuitResponse = await client.PostAsJsonAsync("/mcp", addSubcircuitRequest);
        var addSubcircuitText = await addSubcircuitResponse.Content.ReadAsStringAsync();

        Assert.True(addSubcircuitResponse.IsSuccessStatusCode,
            $"add_component should succeed. Response: {addSubcircuitText}");

        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);

        // Should NOT have errors
        Assert.False(addSubcircuitJson.TryGetProperty("error", out var error),
            $"add_component should not have errors. Error: {error}, Full Response: {addSubcircuitText}");

        // Extract result
        var addSubcircuitContent = addSubcircuitJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        var addSubcircuitResult = JsonSerializer.Deserialize<JsonElement>(addSubcircuitContent!);

        // Verify status is "added"
        Assert.True(addSubcircuitResult.TryGetProperty("status", out var statusProp),
            $"Response should have 'status' property. Response: {addSubcircuitContent}");
        Assert.Equal("added", statusProp.GetString());

        // Step 4: Verify via export_netlist
        var exportRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "export_netlist",
                arguments = new { circuit_id = "test_tray_http" }
            }
        };

        var exportResponse = await client.PostAsJsonAsync("/mcp", exportRequest);
        var exportText = await exportResponse.Content.ReadAsStringAsync();
        var exportJson = JsonSerializer.Deserialize<JsonElement>(exportText);

        var exportContent = exportJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        string netlist;
        if (exportContent!.TrimStart().StartsWith("{"))
        {
            var exportResult = JsonSerializer.Deserialize<JsonElement>(exportContent);
            netlist = exportResult.GetProperty("netlist").GetString() ?? "";
        }
        else
        {
            netlist = exportContent;
        }

        Assert.Contains("Xspk", netlist);
        Assert.Contains("275_030", netlist);
    }

    [Fact]
    public async Task TrayAppInitializationOrder_ImportNetlist_ShouldWork()
    {
        // Test import_netlist with subcircuit - should work with correct initialization
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var subcircuitDef = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS MINUS 2.73
Le PLUS MINUS 0.001
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "275_030.lib"), subcircuitDef);
        });
        using var client = factory.CreateClient();

        // Import netlist with subcircuit
        var importRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "import_netlist",
                arguments = new
                {
                    circuit_name = "test_tray_import",
                    netlist = @"Test Tray Import
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end"
                }
            }
        };

        var importResponse = await client.PostAsJsonAsync("/mcp", importRequest);
        var importText = await importResponse.Content.ReadAsStringAsync();

        Assert.True(importResponse.IsSuccessStatusCode,
            $"import_netlist should succeed. Response: {importText}");

        var importJson = JsonSerializer.Deserialize<JsonElement>(importText);

        // Should NOT have top-level errors
        Assert.False(importJson.TryGetProperty("error", out var error),
            $"import_netlist should not have top-level errors. Error: {error}, Full Response: {importText}");

        // Extract result
        var importContent = importJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        var importResult = JsonSerializer.Deserialize<JsonElement>(importContent!);

        var componentsAdded = importResult.GetProperty("components_added").GetInt32();
        var totalComponents = importResult.GetProperty("total_components").GetInt32();
        var status = importResult.GetProperty("status").GetString();

        // Verify all components were added successfully
        Assert.True(componentsAdded == totalComponents,
            $"All components should be added. Expected {totalComponents}, got {componentsAdded}. Response: {importContent}");
        Assert.True(status == "Success",
            $"Status should be 'Success', got '{status}'. Response: {importContent}");

        // Verify no failed components
        if (importResult.TryGetProperty("failed_components", out var failed) &&
            failed.ValueKind == JsonValueKind.Array)
        {
            var failedComponents = failed.EnumerateArray().ToList();
            Assert.Empty(failedComponents);
        }
    }
}

