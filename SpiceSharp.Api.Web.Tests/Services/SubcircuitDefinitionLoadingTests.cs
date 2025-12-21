using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using SpiceSharp.Api.Web.Tests.Infrastructure;
using System.IO;
using System.Linq;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests to diagnose and verify subcircuit definition loading during AC analysis.
/// </summary>
public class SubcircuitDefinitionLoadingTests
{
    [Fact]
    public async Task SubcircuitDefinition_SimpleACAnalysis_ShouldWork()
    {
        // Arrange: Create a simple subcircuit that should work without validation issues
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var simpleSubcircuit = @"
* Simple test subcircuit
.SUBCKT SIMPLE_SUB PLUS MINUS
R1 PLUS MINUS 1000
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "SIMPLE_SUB.lib"), simpleSubcircuit);
        });
        using var client = factory.CreateClient();

        // Step 1: Create circuit
        var createCircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "create_circuit", arguments = new { circuit_id = "test_simple_sub" } }
        };
        await client.PostAsJsonAsync("/mcp", createCircuitRequest);

        // Step 2: Add voltage source
        var addVoltageSourceRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    circuit_id = "test_simple_sub",
                    component_type = "voltage_source",
                    name = "V1",
                    nodes = new[] { "in", "0" },
                    parameters = new { ac = 1 },
                    value = 1
                }
            }
        };
        await client.PostAsJsonAsync("/mcp", addVoltageSourceRequest);

        // Step 3: Add simple subcircuit
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
                    circuit_id = "test_simple_sub",
                    component_type = "subcircuit",
                    model = "SIMPLE_SUB",
                    name = "X1",
                    nodes = new[] { "in", "0" }
                }
            }
        };
        var addSubcircuitResponse = await client.PostAsJsonAsync("/mcp", addSubcircuitRequest);
        var addSubcircuitText = await addSubcircuitResponse.Content.ReadAsStringAsync();
        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);
        
        Assert.True(addSubcircuitResponse.IsSuccessStatusCode, 
            $"add_component should succeed. Response: {addSubcircuitText}");
        Assert.False(addSubcircuitJson.TryGetProperty("error", out _), 
            $"add_component should not return an error. Response: {addSubcircuitText}");

        // Step 4: Run AC analysis - THIS SHOULD WORK
        var acAnalysisRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "run_ac_analysis",
                arguments = new
                {
                    circuit_id = "test_simple_sub",
                    start_frequency = 20,
                    stop_frequency = 20000,
                    number_of_points = 50,
                    signals = new[] { "v(in)" }
                }
            }
        };
        var acResponse = await client.PostAsJsonAsync("/mcp", acAnalysisRequest);
        var acText = await acResponse.Content.ReadAsStringAsync();
        var acJson = JsonSerializer.Deserialize<JsonElement>(acText);

        // Extract result from MCP content format
        var acContent = acJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var acResult = JsonSerializer.Deserialize<JsonElement>(acContent!);
        
        // Check for errors
        if (acResult.TryGetProperty("Status", out var statusProp) || acResult.TryGetProperty("status", out statusProp))
        {
            var status = statusProp.GetString();
            if (status != null && status.Contains("Failed", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(
                    $"❌ AC analysis failed for simple subcircuit.\n" +
                    $"Status: {status}\n" +
                    $"Full Response: {acContent}\n\n" +
                    $"This indicates subcircuit definitions aren't being properly loaded or registered.");
            }
        }

        // Verify we got frequency data
        Assert.True(acResult.TryGetProperty("Frequencies", out var freqProp) && freqProp.ValueKind == JsonValueKind.Array,
            $"AC analysis should return frequencies. Response: {acContent}");
        
        var frequencies = freqProp.EnumerateArray().ToList();
        Assert.True(frequencies.Count > 0, $"Should have frequency points. Response: {acContent}");
    }

    [Fact]
    public async Task SubcircuitDefinition_SpeakerACAnalysis_ShouldWork()
    {
        // Arrange: Create a speaker subcircuit (like 275_030)
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var speakerSubcircuit = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 2.73
RsLe 1 2 1e-9
Le 2 MINUS 0.001
Rdc 1 MINUS 1e12
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "275_030.lib"), speakerSubcircuit);
        });
        using var client = factory.CreateClient();

        // Step 1: Create circuit
        var createCircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "create_circuit", arguments = new { circuit_id = "test_speaker_sub" } }
        };
        await client.PostAsJsonAsync("/mcp", createCircuitRequest);

        // Step 2: Add voltage source
        var addVoltageSourceRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    circuit_id = "test_speaker_sub",
                    component_type = "voltage_source",
                    name = "V1",
                    nodes = new[] { "in", "0" },
                    parameters = new { ac = 1 },
                    value = 1
                }
            }
        };
        await client.PostAsJsonAsync("/mcp", addVoltageSourceRequest);

        // Step 3: Add speaker subcircuit
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
                    circuit_id = "test_speaker_sub",
                    component_type = "subcircuit",
                    model = "275_030",
                    name = "Xspk",
                    nodes = new[] { "in", "0" }
                }
            }
        };
        var addSubcircuitResponse = await client.PostAsJsonAsync("/mcp", addSubcircuitRequest);
        var addSubcircuitText = await addSubcircuitResponse.Content.ReadAsStringAsync();
        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);
        
        Assert.True(addSubcircuitResponse.IsSuccessStatusCode, 
            $"add_component should succeed. Response: {addSubcircuitText}");

        // Step 4: Validate circuit
        var validateRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new { name = "validate_circuit", arguments = new { circuit_id = "test_speaker_sub" } }
        };
        var validateResponse = await client.PostAsJsonAsync("/mcp", validateRequest);
        var validateText = await validateResponse.Content.ReadAsStringAsync();
        var validateJson = JsonSerializer.Deserialize<JsonElement>(validateText);
        var validateContent = validateJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        var validateResult = JsonSerializer.Deserialize<JsonElement>(validateContent!);
        
        Assert.True(validateResult.GetProperty("is_valid").GetBoolean(),
            $"Circuit should be valid. Response: {validateContent}");

        // Step 5: Run AC analysis - THIS IS THE KEY TEST
        var acAnalysisRequest = new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "tools/call",
            @params = new
            {
                name = "run_ac_analysis",
                arguments = new
                {
                    circuit_id = "test_speaker_sub",
                    start_frequency = 20,
                    stop_frequency = 20000,
                    number_of_points = 50,
                    signals = new[] { "v(in)" }
                }
            }
        };
        var acResponse = await client.PostAsJsonAsync("/mcp", acAnalysisRequest);
        var acText = await acResponse.Content.ReadAsStringAsync();
        var acJson = JsonSerializer.Deserialize<JsonElement>(acText);

        // Extract result from MCP content format
        var acContent = acJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var acResult = JsonSerializer.Deserialize<JsonElement>(acContent!);
        
        // Check for errors - THIS IS WHERE THE BUG MANIFESTS
        if (acResult.TryGetProperty("Status", out var statusProp) || acResult.TryGetProperty("status", out statusProp))
        {
            var status = statusProp.GetString();
            if (status != null && (status.Contains("Failed", StringComparison.OrdinalIgnoreCase) || 
                                  status.Contains("rule violations", StringComparison.OrdinalIgnoreCase) ||
                                  status.Contains("ValidationFailedException", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.Fail(
                    $"❌ AC analysis failed for speaker subcircuit.\n" +
                    $"Status: {status}\n" +
                    $"Full Response: {acContent}\n\n" +
                    $"This indicates subcircuit definitions aren't being properly loaded or the internal circuit has validation issues.");
            }
        }

        // Verify we got frequency data
        Assert.True(acResult.TryGetProperty("Frequencies", out var freqProp) && freqProp.ValueKind == JsonValueKind.Array,
            $"AC analysis should return frequencies. Response: {acContent}");
        
        var frequencies = freqProp.EnumerateArray().ToList();
        Assert.True(frequencies.Count > 0, $"Should have frequency points. Response: {acContent}");
    }

    [Fact]
    public async Task SubcircuitDefinition_CheckDefinitionAccessibility_ShouldWork()
    {
        // This test verifies that subcircuit definitions are accessible after being added
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var speakerSubcircuit = @"
* Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 2.73
RsLe 1 2 1e-9
Le 2 MINUS 0.001
Rdc 1 MINUS 1e12
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "275_030.lib"), speakerSubcircuit);
        });
        using var client = factory.CreateClient();

        // Step 1: Create circuit
        var createCircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "create_circuit", arguments = new { circuit_id = "test_def_access" } }
        };
        await client.PostAsJsonAsync("/mcp", createCircuitRequest);

        // Step 2: Add voltage source
        var addVoltageSourceRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    circuit_id = "test_def_access",
                    component_type = "voltage_source",
                    name = "V1",
                    nodes = new[] { "in", "0" },
                    parameters = new { ac = 1 },
                    value = 1
                }
            }
        };
        await client.PostAsJsonAsync("/mcp", addVoltageSourceRequest);

        // Step 3: Add speaker subcircuit
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
                    circuit_id = "test_def_access",
                    component_type = "subcircuit",
                    model = "275_030",
                    name = "Xspk",
                    nodes = new[] { "in", "0" }
                }
            }
        };
        var addSubcircuitResponse = await client.PostAsJsonAsync("/mcp", addSubcircuitRequest);
        var addSubcircuitText = await addSubcircuitResponse.Content.ReadAsStringAsync();
        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);
        
        Assert.True(addSubcircuitResponse.IsSuccessStatusCode, 
            $"add_component should succeed. Response: {addSubcircuitText}");

        // Step 4: Check component info - verify definition is accessible
        var componentInfoRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "get_component_info",
                arguments = new
                {
                    circuit_id = "test_def_access",
                    component = "Xspk"
                }
            }
        };
        var componentInfoResponse = await client.PostAsJsonAsync("/mcp", componentInfoRequest);
        var componentInfoText = await componentInfoResponse.Content.ReadAsStringAsync();
        var componentInfoJson = JsonSerializer.Deserialize<JsonElement>(componentInfoText);
        
        var componentInfoContent = componentInfoJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var componentInfoResult = JsonSerializer.Deserialize<JsonElement>(componentInfoContent!);
        
        // Check if model definition was found
        if (componentInfoResult.TryGetProperty("model_error", out var modelErrorProp))
        {
            var modelError = modelErrorProp.GetString();
            if (modelError != null && modelError.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(
                    $"❌ Subcircuit definition not found for component.\n" +
                    $"Model Error: {modelError}\n" +
                    $"Full Response: {componentInfoContent}\n\n" +
                    $"This indicates the subcircuit definition wasn't properly registered in the circuit.");
            }
        }
        
        // Verify component info shows correct model
        Assert.True(componentInfoResult.TryGetProperty("model_name", out var modelNameProp),
            $"Component info should include model_name. Response: {componentInfoContent}");
        Assert.Equal("275_030", modelNameProp.GetString());
    }
}

