using SpiceSharp.Api.Web.Tests.Infrastructure;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services.HttpIntegration;

/// <summary>
/// HTTP integration tests that test through the actual ASP.NET Core host and HTTP interface.
/// These tests reproduce the real client usage scenario and can catch DI wiring issues
/// that don't show up in unit tests.
/// </summary>
public class LibraryServiceDIWiringHttpTests
{
    /// <summary>
    /// Test that reproduces the exact DI wiring issue through HTTP.
    /// 
    /// THE BUG: In Program.cs, ComponentService is registered (line 26) BEFORE LibraryService (line 160).
    /// ComponentService's factory calls sp.GetService&lt;ILibraryService&gt;() which returns null
    /// if LibraryService hasn't been registered yet. If MCPService (which needs ComponentService)
    /// is resolved during the first HTTP request before LibraryService is fully initialized,
    /// ComponentService will be created with null LibraryService.
    /// 
    /// This test reproduces the real client scenario:
    /// 1. Client calls library_search - works (LibraryService exists)
    /// 2. Client calls add_component with subcircuit - FAILS if ComponentService got null LibraryService
    /// </summary>
    [Fact]
    public async Task HttpIntegration_LibraryServiceExistsButComponentServiceDoesntGetIt_ShouldFail()
    {
        // Arrange - Create a factory with test library containing a subcircuit
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var subcircuitDef = @"
* Test Subcircuit - Dayton Audio ND20FA-6 Tweeter
* Model: 275_030
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.001
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "test.lib"), subcircuitDef);
        });
        
        using var client = factory.CreateClient();
        
        // Step 1: Verify LibraryService exists and works via library_search
        // This proves LibraryService is registered and functional
        var searchRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "library_search",
                arguments = new { query = "275_030" }
            }
        };

        var searchResponse = await client.PostAsJsonAsync("/mcp", searchRequest);
        var searchResponseText = await searchResponse.Content.ReadAsStringAsync();
        
        Assert.True(searchResponse.IsSuccessStatusCode, 
            $"library_search should succeed. Status: {searchResponse.StatusCode}, Response: {searchResponseText}");
        
        var searchResult = await searchResponse.Content.ReadFromJsonAsync<JsonElement>();
        var searchContent = searchResult.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var searchJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(searchContent!);
        Assert.NotNull(searchJson);
        var searchCount = searchJson["count"].GetInt32();
        Assert.True(searchCount >= 1, 
            $"LibraryService should find subcircuits. Found: {searchCount}, Response: {searchContent}");

        // Step 2: Create a circuit (this might trigger MCPService resolution, which needs ComponentService)
        var createCircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "create_circuit",
                arguments = new
                {
                    circuit_id = "di_wiring_http_test",
                    make_active = true
                }
            }
        };

        var createResponse = await client.PostAsJsonAsync("/mcp", createCircuitRequest);
        var createResponseText = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.IsSuccessStatusCode,
            $"create_circuit should succeed. Status: {createResponse.StatusCode}, Response: {createResponseText}");

        // Step 3: THE CRITICAL TEST - Try to add a subcircuit component
        // If ComponentService was created with null LibraryService, this will fail
        var addComponentRequest = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    name = "Xspk",
                    component_type = "subcircuit",
                    model = "275_030",
                    nodes = new[] { "in", "0" }
                }
            }
        };

        var addResponse = await client.PostAsJsonAsync("/mcp", addComponentRequest);
        var addResponseText = await addResponse.Content.ReadAsStringAsync();
        
        // Parse the response to check for errors
        JsonElement addResult;
        try
        {
            addResult = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch
        {
            Assert.Fail(
                $"BUG REPRODUCED: Failed to parse add_component response. " +
                $"Status: {addResponse.StatusCode}, Response: {addResponseText}");
            return;
        }

        // Check if there's an error in the JSON-RPC response
        if (addResult.TryGetProperty("error", out var error))
        {
            var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
            var errorData = error.TryGetProperty("data", out var data) ? data.GetString() : null;
            
            // Check for the specific error that indicates LibraryService is not available
            if (errorMessage != null && (
                errorMessage.Contains("library service is not available", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("LibraryService", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("subcircuit", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.Fail(
                    $"BUG REPRODUCED via HTTP: LibraryService exists (library_search found {searchCount} subcircuits) " +
                    $"but ComponentService cannot add subcircuit component.\n" +
                    $"Error: {errorMessage}\n" +
                    $"Data: {errorData}\n" +
                    $"Full Response: {addResponseText}\n\n" +
                    $"This indicates ComponentService was created before LibraryService was registered, " +
                    $"or DI resolution order is incorrect. ComponentService factory in Program.cs line 26-30 " +
                    $"calls sp.GetService<ILibraryService>() which returns null if LibraryService " +
                    $"isn't registered yet (LibraryService is registered at line 160).");
            }
            
            Assert.Fail(
                $"add_component failed with error: {errorMessage}\n" +
                $"Data: {errorData}\n" +
                $"Full Response: {addResponseText}");
        }

        // If we get here, the request succeeded - verify the component was added
        Assert.True(addResponse.IsSuccessStatusCode,
            $"add_component should succeed. Status: {addResponse.StatusCode}, Response: {addResponseText}");

        var addContent = addResult.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var addJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(addContent!);
        Assert.NotNull(addJson);
        
        // Verify the component was actually added
        var status = addJson.TryGetValue("status", out var statusElement) 
            ? statusElement.GetString() 
            : null;
        
        Assert.Equal("added", status);
    }

    /// <summary>
    /// Test that verifies services work correctly even when resolved in different orders.
    /// This test makes multiple sequential HTTP requests to simulate real client usage
    /// where services might be resolved at different times.
    /// </summary>
    [Fact]
    public async Task HttpIntegration_ServiceResolutionOrder_ShouldWork()
    {
        // Arrange - Create factory with test library
        using var factory = SpiceServiceWebApplicationFactory.CreateWithTestLibrary(libPath =>
        {
            var subcircuitDef = @"
* Test Speaker Subcircuit
.SUBCKT test_speaker PLUS MINUS
Re PLUS 1 5.5
Le 1 2 0.002
.ENDS
";
            File.WriteAllText(Path.Combine(libPath, "test.lib"), subcircuitDef);
        });
        
        using var client = factory.CreateClient();

        // Act - Make multiple sequential requests to test service resolution order
        // First request: Create circuit (might trigger MCPService/ComponentService resolution)
        var createRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "create_circuit",
                arguments = new { circuit_id = "test1", make_active = true }
            }
        };

        var createResponse = await client.PostAsJsonAsync("/mcp", createRequest);
        var createResponseText = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.IsSuccessStatusCode,
            $"create_circuit should succeed. Status: {createResponse.StatusCode}, Response: {createResponseText}");

        // Second request: Add subcircuit component (requires ComponentService to have LibraryService)
        // This should work even if services were resolved in different order
        var addRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "add_component",
                arguments = new
                {
                    name = "X1",
                    component_type = "subcircuit",
                    model = "test_speaker",
                    nodes = new[] { "in", "0" }
                }
            }
        };

        var addResponse = await client.PostAsJsonAsync("/mcp", addRequest);
        var addResponseText = await addResponse.Content.ReadAsStringAsync();
        
        // Assert - Should succeed regardless of service resolution order
        if (!addResponse.IsSuccessStatusCode)
        {
            JsonElement? errorResult = null;
            try
            {
                errorResult = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
            }
            catch { }
            
            var errorMsg = "Unknown error";
            if (errorResult.HasValue && errorResult.Value.TryGetProperty("error", out var error))
            {
                errorMsg = error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown" : "Unknown";
            }
            
            Assert.Fail(
                $"Service resolution order issue detected via HTTP.\n" +
                $"Status: {addResponse.StatusCode}\n" +
                $"Error: {errorMsg}\n" +
                $"Full Response: {addResponseText}\n\n" +
                $"This suggests ComponentService was resolved before LibraryService was available.");
        }

        addResponse.EnsureSuccessStatusCode();
        
        // Verify the component was actually added
        var addResult = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        var addContent = addResult.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var addJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(addContent!);
        Assert.NotNull(addJson);
        var status = addJson!["status"].GetString();
        Assert.Equal("added", status);
    }
}

