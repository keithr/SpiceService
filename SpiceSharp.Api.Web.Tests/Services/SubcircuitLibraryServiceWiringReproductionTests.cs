using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using SpiceSharp.Api.Web.Tests.Infrastructure;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests that reproduce the exact DI configuration issue from Program.cs.
/// 
/// The bug: ComponentService is registered BEFORE LibraryService (lines 26-30 vs 160-172),
/// and ComponentService uses sp.GetService&lt;ILibraryService&gt;() which returns null
/// if LibraryService hasn't been registered yet.
/// 
/// This test creates a WebApplicationFactory that replicates the EXACT problematic
/// DI registration order from Program.cs to reproduce the bug.
/// </summary>
public class SubcircuitLibraryServiceWiringReproductionTests
{
    /// <summary>
    /// WebApplicationFactory that replicates the EXACT problematic DI order from Program.cs
    /// </summary>
    private class ProblematicDIOrderFactory : WebApplicationFactory<SpiceSharp.Api.Web.ProgramMarker>
    {
        private readonly string? _testLibraryPath;
        private readonly string? _testDatabasePath;

        public ProblematicDIOrderFactory(string? testLibraryPath = null, string? testDatabasePath = null)
        {
            _testLibraryPath = testLibraryPath;
            _testDatabasePath = testDatabasePath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Logging:LogLevel:Default", "Warning" }
                });
            });

            builder.ConfigureServices(services =>
            {
                // REPRODUCE THE EXACT PROBLEMATIC ORDER FROM Program.cs
                
                // Step 1: Register CircuitManager (line 24)
                services.AddSingleton<ICircuitManager, CircuitManager>();
                
                // Step 2: Register ComponentService BEFORE LibraryService (lines 26-30)
                // THIS IS THE BUG - ComponentService tries to get ILibraryService which doesn't exist yet!
                // The issue: When ComponentService factory is called, LibraryService doesn't exist yet,
                // so sp.GetService<ILibraryService>() returns null, and ComponentService is created with null.
                // Even though LibraryService is registered later, ComponentService already has the null reference.
                
                // To reproduce the bug, we need to force ComponentService to be instantiated DURING ConfigureServices
                // before LibraryService is registered. We'll do this by creating a temporary service provider.
                var tempServices = new ServiceCollection();
                tempServices.AddSingleton<ICircuitManager, CircuitManager>();
                
                // Register ComponentService factory that will be called immediately
                IComponentService? capturedComponentService = null;
                tempServices.AddSingleton<IComponentService>(sp =>
                {
                    // This will return NULL because LibraryService isn't registered yet!
                    var libraryService = sp.GetService<ILibraryService>();
                    System.Diagnostics.Debug.WriteLine($"🔍 ComponentService factory called during temp setup. LibraryService is null: {libraryService == null}");
                    var componentService = new ComponentService(libraryService);
                    capturedComponentService = componentService;
                    return componentService;
                });
                
                // Build temp provider to force ComponentService creation
                var tempProvider = tempServices.BuildServiceProvider();
                var forcedComponentService = tempProvider.GetRequiredService<IComponentService>();
                
                // Now register the pre-created ComponentService (with null LibraryService) in the real services
                services.AddSingleton<IComponentService>(_ => forcedComponentService);
                
                // Step 3: Register other services (lines 31-47)
                services.AddSingleton<IModelService, ModelService>();
                services.AddSingleton<IOperatingPointService, OperatingPointService>();
                services.AddSingleton<IDCAnalysisService, DCAnalysisService>();
                services.AddSingleton<ITransientAnalysisService, TransientAnalysisService>();
                services.AddSingleton<IACAnalysisService, ACAnalysisService>();
                services.AddSingleton<INetlistService, NetlistService>();
                services.AddSingleton<IExportService, ExportService>();
                services.AddSingleton<IParameterSweepService, ParameterSweepService>();
                services.AddSingleton<INoiseAnalysisService, NoiseAnalysisService>();
                services.AddSingleton<ITemperatureSweepService, TemperatureSweepService>();
                services.AddSingleton<IImpedanceAnalysisService>(sp => 
                    new ImpedanceAnalysisService(sp.GetRequiredService<IACAnalysisService>()));
                services.AddSingleton<INetlistParser, NetlistParser>();
                
                // Step 4: Configure MCP config (lines 50-151)
                MCPServerConfig mcpConfig;
                if (!string.IsNullOrEmpty(_testLibraryPath))
                {
                    mcpConfig = new MCPServerConfig
                    {
                        LibraryPaths = new[] { _testLibraryPath },
                        Port = 0,
                        Version = "1.0.0"
                    };
                }
                else
                {
                    mcpConfig = new MCPServerConfig { Port = 0, Version = "1.0.0" };
                }
                
                services.AddSingleton(mcpConfig);
                services.AddSingleton(new DiscoveryConfig { Enabled = false });
                services.AddSingleton<CircuitResultsCache>();
                
                // Step 5: Register SpeakerDatabaseService (line 156)
                if (!string.IsNullOrEmpty(_testDatabasePath))
                {
                    services.AddSingleton<ISpeakerDatabaseService>(sp =>
                    {
                        var db = new SpeakerDatabaseService(_testDatabasePath);
                        db.InitializeDatabase();
                        return db;
                    });
                }
                else
                {
                    services.AddSingleton<ISpeakerDatabaseService, SpeakerDatabaseService>();
                }
                
                // Step 6: Register LibraryService AFTER ComponentService (lines 160-172)
                // THIS IS TOO LATE - ComponentService was already created with null LibraryService!
                services.AddSingleton<ILibraryService>(sp =>
                {
                    var speakerDb = sp.GetRequiredService<ISpeakerDatabaseService>();
                    var libraryService = new LibraryService(speakerDb);
                    
                    // Index libraries on startup if paths are configured
                    if (mcpConfig.LibraryPaths != null && mcpConfig.LibraryPaths.Any())
                    {
                        libraryService.IndexLibraries(mcpConfig.LibraryPaths);
                    }
                    
                    return libraryService;
                });
                
                // Step 7: Register remaining services (lines 174-181)
                services.AddSingleton<IEnclosureDesignService, EnclosureDesignService>();
                services.AddSingleton<ICrossoverCompatibilityService, CrossoverCompatibilityService>();
                
                // CRITICAL: MCPService depends on IComponentService, so when MCPService is created,
                // it will trigger ComponentService creation. If this happens before LibraryService is registered,
                // ComponentService will be created with null LibraryService.
                // However, since MCPService is registered AFTER LibraryService in our test (matching Program.cs),
                // LibraryService should be available. But let's try to force early instantiation.
                services.AddSingleton<MCPService>(sp =>
                {
                    // Force ComponentService to be resolved NOW (before LibraryService is fully set up)
                    // Actually wait - LibraryService is already registered above, so this won't work.
                    // The real issue might be that ComponentService is created during app startup
                    // before LibraryService.IndexLibraries() is called, or there's a race condition.
                    return ActivatorUtilities.CreateInstance<MCPService>(sp);
                });
            });
            
            builder.UseEnvironment("Testing");
        }
    }

    [Fact]
    public async Task ReproduceBug_ComponentServiceRegisteredBeforeLibraryService_ShouldFail()
    {
        // Setup: Create test library with subcircuit
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
        
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        
        // Create factory with PROBLEMATIC DI order (ComponentService before LibraryService)
        using var factory = new ProblematicDIOrderFactory(tempLibPath, tempDbPath);
        using var client = factory.CreateClient();

        // Test 1: Library Search Works ✅ (LibraryService exists and works)
        var librarySearchRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "library_search",
                arguments = new { query = "275_030", limit = 5 }
            }
        };

        var librarySearchResponse = await client.PostAsJsonAsync("/mcp", librarySearchRequest);
        var librarySearchText = await librarySearchResponse.Content.ReadAsStringAsync();
        
        Assert.True(librarySearchResponse.IsSuccessStatusCode, 
            $"Library search should succeed. Response: {librarySearchText}");

        var librarySearchJson = JsonSerializer.Deserialize<JsonElement>(librarySearchText);
        var librarySearchContent = librarySearchJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var librarySearchResult = JsonSerializer.Deserialize<JsonElement>(librarySearchContent!);
        Assert.Equal(1, librarySearchResult.GetProperty("count").GetInt32());
        
        System.Diagnostics.Debug.WriteLine($"✅ Test 1 PASSED: Library search works - LibraryService is available");

        // Test 2: Create Circuit ✅
        var createCircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = "create_circuit",
                arguments = new { circuit_id = "test_bug_reproduction" }
            }
        };

        var createCircuitResponse = await client.PostAsJsonAsync("/mcp", createCircuitRequest);
        var createCircuitText = await createCircuitResponse.Content.ReadAsStringAsync();
        
        Assert.True(createCircuitResponse.IsSuccessStatusCode,
            $"Create circuit should succeed. Response: {createCircuitText}");

        // Test 3: Add Voltage Source ✅
        var addVoltageRequest = new
        {
            jsonrpc = "2.0",
            id = 3,
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

        var addVoltageResponse = await client.PostAsJsonAsync("/mcp", addVoltageRequest);
        var addVoltageText = await addVoltageResponse.Content.ReadAsStringAsync();
        
        Assert.True(addVoltageResponse.IsSuccessStatusCode,
            $"Add voltage source should succeed. Response: {addVoltageText}");

        // Test 4: Add Subcircuit Component ❌ THIS SHOULD FAIL DUE TO BUG
        var addSubcircuitRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
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
        
        System.Diagnostics.Debug.WriteLine($"🔍 DEBUG: add_component(subcircuit) response: {addSubcircuitText}");

        var addSubcircuitJson = JsonSerializer.Deserialize<JsonElement>(addSubcircuitText);
        
        // Check for JSON-RPC errors
        if (addSubcircuitJson.TryGetProperty("error", out var error))
        {
            var errorMessage = error.TryGetProperty("message", out var msg) 
                ? msg.GetString() 
                : error.ToString();
            
            // Check if it's the "library service is not available" error
            if (errorMessage?.Contains("library service is not available", StringComparison.OrdinalIgnoreCase) == true ||
                errorMessage?.Contains("LibraryService", StringComparison.OrdinalIgnoreCase) == true)
            {
                Assert.Fail(
                    $"❌ BUG REPRODUCED: add_component with subcircuit FAILED with 'library service is not available'\n" +
                    $"Library search works (Test 1 passed), but ComponentService doesn't have access to LibraryService.\n" +
                    $"Error: {errorMessage}\n" +
                    $"Full Response: {addSubcircuitText}\n\n" +
                    $"Root Cause: ComponentService was registered BEFORE LibraryService in DI container.\n" +
                    $"When ComponentService was created, sp.GetService<ILibraryService>() returned null.\n" +
                    $"Even though LibraryService is now registered, ComponentService still has the null reference.\n\n" +
                    $"Fix: Register LibraryService BEFORE ComponentService in Program.cs.");
            }
            
            Assert.Fail(
                $"❌ BUG REPRODUCED: add_component with subcircuit FAILED\n" +
                $"Error: {errorMessage}\n" +
                $"Full Response: {addSubcircuitText}\n\n" +
                $"This indicates ComponentService doesn't have access to LibraryService.");
        }

        // If no error, check the result
        var addSubcircuitContent = addSubcircuitJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var addSubcircuitResult = JsonSerializer.Deserialize<JsonElement>(addSubcircuitContent!);
        
        // Check if status is not "added"
        if (addSubcircuitResult.TryGetProperty("status", out var statusProp))
        {
            var status = statusProp.GetString();
            if (status != "added")
            {
                Assert.Fail(
                    $"❌ BUG REPRODUCED: add_component with subcircuit FAILED\n" +
                    $"Library search works (Test 1 passed), but ComponentService doesn't have access to LibraryService.\n" +
                    $"Response: {addSubcircuitContent}\n\n" +
                    $"Expected: {{'status': 'added'}}\n" +
                    $"Actual: {{'status': '{status}'}}\n\n" +
                    $"Root Cause: ComponentService was registered BEFORE LibraryService.\n" +
                    $"Fix: Register LibraryService BEFORE ComponentService in Program.cs.");
            }
        }
        else
        {
            Assert.Fail(
                $"❌ BUG REPRODUCED: add_component with subcircuit FAILED - no status in response\n" +
                $"Response: {addSubcircuitContent}\n\n" +
                $"This indicates ComponentService doesn't have access to LibraryService.");
        }
        
        // If we get here, the bug is NOT reproduced - this test intentionally uses problematic DI order
        // The actual application should work correctly now that Program.cs is fixed.
        // This test serves as documentation of what happens with the wrong DI order.
        Assert.Fail(
            $"⚠️ BUG NOT REPRODUCED: add_component with subcircuit succeeded unexpectedly.\n" +
            $"This test intentionally uses problematic DI order (ComponentService before LibraryService).\n" +
            $"If this test passes, it means the problematic DI order somehow works (unexpected).\n" +
            $"The actual application uses the CORRECT order (LibraryService before ComponentService) and should work.\n\n" +
            $"Response: {addSubcircuitContent}");
    }

    [Fact]
    public async Task ReproduceBug_ImportNetlistWithSubcircuit_ShouldFail()
    {
        // Setup: Create test library with subcircuit
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
        
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        
        // Create factory with PROBLEMATIC DI order
        using var factory = new ProblematicDIOrderFactory(tempLibPath, tempDbPath);
        using var client = factory.CreateClient();

        // Test: Import Netlist with Subcircuit ❌ THIS SHOULD FAIL DUE TO BUG
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
        
        System.Diagnostics.Debug.WriteLine($"🔍 DEBUG: import_netlist response: {importText}");

        var importJson = JsonSerializer.Deserialize<JsonElement>(importText);
        
        // Check for JSON-RPC errors
        if (importJson.TryGetProperty("error", out var error))
        {
            var errorMessage = error.TryGetProperty("message", out var msg) 
                ? msg.GetString() 
                : error.ToString();
            
            if (errorMessage?.Contains("library service is not available", StringComparison.OrdinalIgnoreCase) == true)
            {
                Assert.Fail(
                    $"❌ BUG REPRODUCED: import_netlist with subcircuit FAILED with 'library service is not available'\n" +
                    $"Error: {errorMessage}\n" +
                    $"Full Response: {importText}\n\n" +
                    $"Root Cause: ComponentService was registered BEFORE LibraryService.\n" +
                    $"Fix: Register LibraryService BEFORE ComponentService in Program.cs.");
            }
        }

        // Extract result
        var importContent = importJson.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        
        var importResult = JsonSerializer.Deserialize<JsonElement>(importContent!);
        
        var componentsAdded = importResult.GetProperty("components_added").GetInt32();
        var totalComponents = importResult.GetProperty("total_components").GetInt32();
        var status = importResult.GetProperty("status").GetString();

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
                fc.TryGetProperty("error", out var fcError) &&
                fcError.GetString()?.Contains("library service is not available", StringComparison.OrdinalIgnoreCase) == true);

            if (hasLibraryServiceError)
            {
                Assert.Fail(
                    $"❌ BUG REPRODUCED: import_netlist with subcircuit FAILED\n" +
                    $"Library search works, but import_netlist doesn't have access to LibraryService.\n" +
                    $"Response: {importContent}\n\n" +
                    $"Expected: components_added = {totalComponents}, status = 'Success'\n" +
                    $"Actual: components_added = {componentsAdded}, status = '{status}'\n" +
                    $"Error: 'library service is not available'\n\n" +
                    $"Root Cause: ComponentService was registered BEFORE LibraryService.\n" +
                    $"Fix: Register LibraryService BEFORE ComponentService in Program.cs.");
            }
        }
        
        // If we get here and it succeeded, the bug is NOT reproduced
        // This test intentionally uses problematic DI order to demonstrate the bug.
        // The actual application should work correctly now that Program.cs is fixed.
        if (componentsAdded == totalComponents && status == "Success")
        {
            Assert.Fail(
                $"⚠️ BUG NOT REPRODUCED: import_netlist succeeded unexpectedly.\n" +
                $"This test intentionally uses problematic DI order (ComponentService before LibraryService).\n" +
                $"If this test passes, it means the problematic DI order somehow works (unexpected).\n" +
                $"The actual application uses the CORRECT order (LibraryService before ComponentService) and should work.\n\n" +
                $"Response: {importContent}");
        }
    }
}

