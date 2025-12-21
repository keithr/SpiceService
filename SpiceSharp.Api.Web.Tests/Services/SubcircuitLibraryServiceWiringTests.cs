using System.Net.Http.Json;
using System.Text.Json;
using SpiceSharp.Api.Web.Tests.Infrastructure;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests to reproduce the subcircuit library service wiring issue.
/// This test verifies that LibraryService is available for library_search
/// but NOT available for add_component and import_netlist with subcircuits.
/// </summary>
public class SubcircuitLibraryServiceWiringTests
{

    [Fact]
    public async Task SubcircuitLibraryServiceWiring_CompleteWorkflow_ShouldReproduceIssue()
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
            File.WriteAllText(Path.Combine(libPath, "275_030.lib"), subcircuitDef);
        });
        using var client = factory.CreateClient();

        // Test 1: Library Search Works ✅
        var librarySearchRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "library_search",
                arguments = new
                {
                    query = "275_030",
                    limit = 5
                }
            }
        };

        var librarySearchResponse = await client.PostAsJsonAsync("/mcp", librarySearchRequest);
        var librarySearchText = await librarySearchResponse.Content.ReadAsStringAsync();
        
        Assert.True(librarySearchResponse.IsSuccessStatusCode, 
            $"Library search should succeed. Response: {librarySearchText}");

        var librarySearchJson = JsonSerializer.Deserialize<JsonElement>(librarySearchText);
        
        // Check for errors first
        if (librarySearchJson.TryGetProperty("error", out var librarySearchError))
        {
            Assert.Fail($"Library search failed with error: {librarySearchError}. Full response: {librarySearchText}");
        }

        // Extract the actual result from MCP content format
        var librarySearchContent = librarySearchJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var librarySearchResult = JsonSerializer.Deserialize<JsonElement>(librarySearchContent!);
        Assert.Equal(1, librarySearchResult.GetProperty("count").GetInt32());
        Assert.Equal(1, librarySearchResult.GetProperty("subcircuit_count").GetInt32());
        
        var subcircuits = librarySearchResult.GetProperty("subcircuits").EnumerateArray().ToList();
        Assert.Single(subcircuits);
        Assert.Equal("275_030", subcircuits[0].GetProperty("name").GetString());
        
        System.Diagnostics.Debug.WriteLine($"✅ Test 1 PASSED: Library search works - found subcircuit 275_030");

        // Test 2: Speaker Search Reports Available ✅
        var speakerSearchRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "search_speakers_by_parameters",
                arguments = new
                {
                    manufacturer = "Dayton Audio",
                    limit = 5
                }
            }
        };

        var speakerSearchResponse = await client.PostAsJsonAsync("/mcp", speakerSearchRequest);
        var speakerSearchText = await speakerSearchResponse.Content.ReadAsStringAsync();
        
        Assert.True(speakerSearchResponse.IsSuccessStatusCode,
            $"Speaker search should succeed. Response: {speakerSearchText}");

        var speakerSearchJson = JsonSerializer.Deserialize<JsonElement>(speakerSearchText);
        
        // Check for errors first
        if (speakerSearchJson.TryGetProperty("error", out var speakerSearchError))
        {
            Assert.Fail($"Speaker search failed with error: {speakerSearchError}. Full response: {speakerSearchText}");
        }

        // Extract the actual result from MCP content format
        var speakerSearchContent = speakerSearchJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var speakerSearchResult = JsonSerializer.Deserialize<JsonElement>(speakerSearchContent!);
        var results = speakerSearchResult.GetProperty("results").EnumerateArray().ToList();
        Assert.True(results.Count > 0, "Should find at least one speaker");
        
        var foundSpeaker = results.FirstOrDefault(r => 
            r.TryGetProperty("subcircuit_name", out var name) && 
            name.GetString() == "275_030");
        
        Assert.True(foundSpeaker.ValueKind != JsonValueKind.Null, 
            "Should find speaker with subcircuit_name '275_030'");
        Assert.True(foundSpeaker.GetProperty("available_in_library").GetBoolean(),
            "Speaker should report available_in_library = true");

        // Test 3: Create Circuit ✅
        var createCircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "create_circuit",
                arguments = new
                {
                    circuit_id = "test_subcircuit_simple"
                }
            }
        };

        var createCircuitResponse = await client.PostAsJsonAsync("/mcp", createCircuitRequest);
        var createCircuitText = await createCircuitResponse.Content.ReadAsStringAsync();
        
        Assert.True(createCircuitResponse.IsSuccessStatusCode,
            $"Create circuit should succeed. Response: {createCircuitText}");

        var createCircuitJson = JsonSerializer.Deserialize<JsonElement>(createCircuitText);
        
        // Check for errors first
        if (createCircuitJson.TryGetProperty("error", out var createCircuitError))
        {
            Assert.Fail($"Create circuit failed with error: {createCircuitError}. Full response: {createCircuitText}");
        }

        // Extract the actual result from MCP content format
        var createCircuitContent = createCircuitJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var createCircuitResult = JsonSerializer.Deserialize<JsonElement>(createCircuitContent!);
        Assert.Equal("test_subcircuit_simple", createCircuitResult.GetProperty("circuit_id").GetString());

        // Test 4: Add Voltage Source ✅
        var addVoltageSourceRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
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

        var addVoltageSourceResponse = await client.PostAsJsonAsync("/mcp", addVoltageSourceRequest);
        var addVoltageSourceText = await addVoltageSourceResponse.Content.ReadAsStringAsync();
        
        Assert.True(addVoltageSourceResponse.IsSuccessStatusCode,
            $"Add voltage source should succeed. Response: {addVoltageSourceText}");

        var addVoltageSourceJson = JsonSerializer.Deserialize<JsonElement>(addVoltageSourceText);
        
        // Check for errors first
        if (addVoltageSourceJson.TryGetProperty("error", out var addVoltageError))
        {
            Assert.Fail($"Add voltage source failed with error: {addVoltageError}. Full response: {addVoltageSourceText}");
        }

        // Extract the actual result from MCP content format
        var addVoltageSourceContent = addVoltageSourceJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var addVoltageSourceResult = JsonSerializer.Deserialize<JsonElement>(addVoltageSourceContent!);
        Assert.Equal("added", addVoltageSourceResult.GetProperty("status").GetString());

        // Test 5: Add Subcircuit Component ❌ THIS SHOULD WORK BUT FAILS
        var addSubcircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 5,
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

        // Check if HTTP request failed
        if (!addSubcircuitResponse.IsSuccessStatusCode)
        {
            Assert.Fail(
                $"❌ BUG REPRODUCED: add_component with subcircuit FAILED (HTTP error)\n" +
                $"Library search works (Test 1 passed), but add_component doesn't have access to LibraryService.\n" +
                $"Response: {addSubcircuitText}\n\n" +
                $"This indicates LibraryService is not wired to ComponentService.");
        }

        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);
        
        // Check for JSON-RPC errors
        if (addSubcircuitJson.TryGetProperty("error", out var addSubcircuitError))
        {
            var errorMessage = addSubcircuitError.TryGetProperty("message", out var msg) 
                ? msg.GetString() 
                : addSubcircuitError.ToString();
            
            Assert.Fail(
                $"❌ BUG REPRODUCED: add_component with subcircuit FAILED\n" +
                $"Library search works (Test 1 passed), but add_component doesn't have access to LibraryService.\n" +
                $"Error: {errorMessage}\n" +
                $"Full Response: {addSubcircuitText}\n\n" +
                $"Expected: {{'status': 'added'}}\n" +
                $"Actual: Error response\n\n" +
                $"This indicates LibraryService is not wired to ComponentService.");
        }

        // Extract the actual result from MCP content format
        var addSubcircuitContent = addSubcircuitJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var addSubcircuitResult = JsonSerializer.Deserialize<JsonElement>(addSubcircuitContent!);
        
        // Check if the result indicates failure
        if (addSubcircuitResult.TryGetProperty("status", out var statusProp))
        {
            var status = statusProp.GetString();
            if (status != "added")
            {
                Assert.Fail(
                    $"❌ BUG REPRODUCED: add_component with subcircuit FAILED\n" +
                    $"Library search works (Test 1 passed), but add_component doesn't have access to LibraryService.\n" +
                    $"Response: {addSubcircuitContent}\n\n" +
                    $"Expected: {{'status': 'added'}}\n" +
                    $"Actual: {{'status': '{status}'}}\n\n" +
                    $"This indicates LibraryService is not wired to ComponentService.");
            }
        }
        Assert.Equal("added", addSubcircuitResult.GetProperty("status").GetString());

        // Test 6: Verify Subcircuit in Export ✅
        var exportRequest = new
        {
            jsonrpc = "2.0",
            id = 6,
            method = "tools/call",
            @params = new
            {
                name = "export_netlist",
                arguments = new
                {
                    circuit_id = "test_subcircuit_simple"
                }
            }
        };

        var exportResponse = await client.PostAsJsonAsync("/mcp", exportRequest);
        var exportText = await exportResponse.Content.ReadAsStringAsync();
        
        Assert.True(exportResponse.IsSuccessStatusCode,
            $"Export netlist should succeed. Response: {exportText}");

        var exportJson = JsonSerializer.Deserialize<JsonElement>(exportText);
        
        // Extract the actual result from MCP content format
        var exportContentText = exportJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";
        
        // Try to parse as JSON first (for structured responses)
        string netlist;
        if (exportContentText.TrimStart().StartsWith("{"))
        {
            var exportResult = JsonSerializer.Deserialize<JsonElement>(exportContentText);
            netlist = exportResult.GetProperty("netlist").GetString() ?? "";
        }
        else
        {
            // If it's not JSON, it's likely the raw netlist text
            netlist = exportContentText;
        }

        Assert.Contains("Xspk", netlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", netlist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubcircuitLibraryServiceWiring_ImportNetlist_ShouldReproduceIssue()
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
            File.WriteAllText(Path.Combine(libPath, "275_030.lib"), subcircuitDef);
        });
        using var client = factory.CreateClient();

        // Test: Import Netlist with Subcircuit ❌ THIS SHOULD WORK BUT FAILS
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
                    circuit_name = "simple_speaker_test",
                    netlist = @"Simple Speaker Test
V1 in 0 DC 1 AC 1
Xspk in 0 275_030
.end"
                }
            }
        };

        var importResponse = await client.PostAsJsonAsync("/mcp", importRequest);
        var importText = await importResponse.Content.ReadAsStringAsync();
        var importJson = JsonSerializer.Deserialize<JsonElement>(importText);

        Assert.True(importResponse.IsSuccessStatusCode,
            $"Import netlist should succeed (even if partial). Response: {importText}");

        // Check if there's an error in the response
        if (importJson.TryGetProperty("error", out var error))
        {
            Assert.Fail(
                $"❌ BUG REPRODUCED: import_netlist failed with error\n" +
                $"Response: {importText}\n\n" +
                $"This indicates the import failed before we could check for library service errors.");
        }

        // Extract the actual result from MCP content format
        var importContent = importJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var importResult = JsonSerializer.Deserialize<JsonElement>(importContent!);
        
        if (!importResult.TryGetProperty("components_added", out var componentsAddedElement))
        {
            Assert.Fail($"Import netlist response missing 'components_added' property. Response: {importText}");
        }
        
        if (!importResult.TryGetProperty("total_components", out var totalComponentsElement))
        {
            Assert.Fail($"Import netlist response missing 'total_components' property. Response: {importText}");
        }
        
        if (!importResult.TryGetProperty("status", out var statusElement))
        {
            Assert.Fail($"Import netlist response missing 'status' property. Response: {importText}");
        }

        var componentsAdded = componentsAddedElement.GetInt32();
        var totalComponents = totalComponentsElement.GetInt32();
        var status = statusElement.GetString();

        // Check if subcircuit failed to import
        if (componentsAdded < totalComponents || status != "Success")
        {
            // Check for the specific error
            var failedComponents = new List<JsonElement>();
            if (importResult.TryGetProperty("failed_components", out var failed) && 
                failed.ValueKind == JsonValueKind.Array)
            {
                failedComponents = failed.EnumerateArray().ToList();
            }

            var hasLibraryServiceError = failedComponents.Any(fc =>
                fc.TryGetProperty("error", out var error) &&
                error.GetString()?.Contains("library service is not available", StringComparison.OrdinalIgnoreCase) == true);

            if (hasLibraryServiceError)
            {
                Assert.Fail(
                    $"❌ BUG REPRODUCED: import_netlist with subcircuit FAILED\n" +
                    $"Library search works, but import_netlist doesn't have access to LibraryService.\n" +
                    $"Response: {importText}\n\n" +
                    $"Expected: components_added = {totalComponents}, status = 'Success'\n" +
                    $"Actual: components_added = {componentsAdded}, status = '{status}'\n" +
                    $"Error: 'library service is not available'\n\n" +
                    $"This indicates LibraryService is not wired to NetlistService/ComponentService.");
            }
        }

        // If we get here, verify all components were added
        Assert.Equal(totalComponents, componentsAdded);
        Assert.Equal("Success", status);

        // Verify subcircuit is in the exported netlist
        var exportRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "export_netlist",
                arguments = new
                {
                    circuit_id = "simple_speaker_test"
                }
            }
        };

        var exportResponse = await client.PostAsJsonAsync("/mcp", exportRequest);
        var exportText = await exportResponse.Content.ReadAsStringAsync();
        var exportJson = JsonSerializer.Deserialize<JsonElement>(exportText);

        // Extract the actual result from MCP content format
        var exportContentText = exportJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";
        
        // Try to parse as JSON first (for structured responses)
        string netlist;
        if (exportContentText.TrimStart().StartsWith("{"))
        {
            var exportResult = JsonSerializer.Deserialize<JsonElement>(exportContentText);
            netlist = exportResult.GetProperty("netlist").GetString() ?? "";
        }
        else
        {
            // If it's not JSON, it's likely the raw netlist text
            netlist = exportContentText;
        }

        Assert.Contains("Xspk", netlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", netlist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubcircuitLibraryServiceWiring_ACAnalysis_ShouldWorkWithSubcircuit()
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
                arguments = new { circuit_id = "test" }
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

        // Step 3: Add speaker subcircuit (THIS IS THE CRITICAL TEST)
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

        // If HTTP request fails, we've reproduced the bug
        if (!addSubcircuitResponse.IsSuccessStatusCode)
        {
            Assert.Fail(
                $"❌ BUG REPRODUCED: Cannot add subcircuit component (HTTP error)\n" +
                $"Response: {addSubcircuitText}\n\n" +
                $"This prevents AC analysis with real speaker models.");
        }

        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);
        
        // Check for JSON-RPC errors
        if (addSubcircuitJson.TryGetProperty("error", out var addSubError))
        {
            var errorMsg = addSubError.TryGetProperty("message", out var msg) ? msg.GetString() : addSubError.ToString();
            Assert.Fail(
                $"❌ BUG REPRODUCED: Cannot add subcircuit component\n" +
                $"Error: {errorMsg}\n" +
                $"Response: {addSubcircuitText}\n\n" +
                $"This prevents AC analysis with real speaker models.");
        }

        // Extract result and check status
        var addSubcircuitContent = addSubcircuitJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var addSubcircuitResult = JsonSerializer.Deserialize<JsonElement>(addSubcircuitContent!);
        
        if (addSubcircuitResult.TryGetProperty("status", out var addStatus) && 
            addStatus.GetString() != "added")
        {
            Assert.Fail(
                $"❌ BUG REPRODUCED: Cannot add subcircuit component\n" +
                $"Status: {addStatus.GetString()}\n" +
                $"Response: {addSubcircuitContent}\n\n" +
                $"This prevents AC analysis with real speaker models.");
        }

        // Step 4: Verify it was added (export netlist)
        var exportRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "export_netlist",
                arguments = new { circuit_id = "test" }
            }
        };

        var exportResponse = await client.PostAsJsonAsync("/mcp", exportRequest);
        var exportText = await exportResponse.Content.ReadAsStringAsync();
        var exportJson = JsonSerializer.Deserialize<JsonElement>(exportText);
        
        var exportContentText = exportJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";
        
        // Try to parse as JSON first (for structured responses)
        string netlist;
        if (exportContentText.TrimStart().StartsWith("{"))
        {
            var exportResult = JsonSerializer.Deserialize<JsonElement>(exportContentText);
            netlist = exportResult.GetProperty("netlist").GetString() ?? "";
        }
        else
        {
            // If it's not JSON, it's likely the raw netlist text
            netlist = exportContentText;
        }

        Assert.Contains("Xspk", netlist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("275_030", netlist, StringComparison.OrdinalIgnoreCase);

        // Step 5: Run AC analysis (should work with speaker impedance)
        var acRequest = new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "tools/call",
            @params = new
            {
                name = "run_ac_analysis",
                arguments = new
                {
                    circuit_id = "test",
                    signals = new[] { "v(in)" },
                    start_frequency = 20,
                    stop_frequency = 20000,
                    number_of_points = 100
                }
            }
        };

        var acResponse = await client.PostAsJsonAsync("/mcp", acRequest);
        var acText = await acResponse.Content.ReadAsStringAsync();
        
        Assert.True(acResponse.IsSuccessStatusCode,
            $"AC analysis should succeed. Response: {acText}");

        var acJson = JsonSerializer.Deserialize<JsonElement>(acText);
        
        // Extract the actual result from MCP content format
        var acContent = acJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var acResult = JsonSerializer.Deserialize<JsonElement>(acContent!);
        var acStatus = acResult.TryGetProperty("Status", out var statusUpper) 
            ? statusUpper.GetString() 
            : (acResult.TryGetProperty("status", out var statusLower) ? statusLower.GetString() : null);

        Assert.True(acStatus == "Success" || acStatus == "success",
            $"AC analysis should succeed. Status: {acStatus}, Response: {acText}");

        Assert.True(acResult.TryGetProperty("Frequencies", out _),
            "AC analysis result should contain Frequencies");
        Assert.True(acResult.TryGetProperty("MagnitudeDb", out _),
            "AC analysis result should contain MagnitudeDb");
    }
}

