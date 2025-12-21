using System.Net.Http.Json;
using System.Text.Json;
using SpiceSharp.Api.Web.Tests.Infrastructure;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests that verify the fix works correctly - subcircuits should work when LibraryService
/// is registered before ComponentService (the correct DI order now in Program.cs).
/// </summary>
public class SubcircuitLibraryServiceWiringFixedTests
{
    [Fact]
    public async Task Fixed_AddComponentWithSubcircuit_ShouldWork()
    {
        // Setup: Create test library with subcircuit
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
            System.IO.File.WriteAllText(System.IO.Path.Combine(libPath, "275_030.lib"), subcircuitDef);
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
                arguments = new { circuit_id = "test_fixed" }
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

        // Step 3: Add subcircuit component - THIS SHOULD WORK NOW
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
        Assert.False(addSubcircuitJson.TryGetProperty("error", out _),
            $"add_component should not have errors. Response: {addSubcircuitText}");

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

        // Step 4: Verify it was added via export_netlist
        var exportRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "export_netlist",
                arguments = new { circuit_id = "test_fixed" }
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
    public async Task Fixed_ImportNetlistWithSubcircuit_ShouldWork()
    {
        // Setup: Create test library with subcircuit
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
            System.IO.File.WriteAllText(System.IO.Path.Combine(libPath, "275_030.lib"), subcircuitDef);
        });
        using var client = factory.CreateClient();

        // Import netlist with subcircuit - THIS SHOULD WORK NOW
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
                    circuit_name = "simple_speaker_test_fixed",
                    netlist = @"Simple Speaker Test
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
        Assert.False(importJson.TryGetProperty("error", out _),
            $"import_netlist should not have top-level errors. Response: {importText}");

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

