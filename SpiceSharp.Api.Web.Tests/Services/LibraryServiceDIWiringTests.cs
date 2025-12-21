using Microsoft.Extensions.DependencyInjection;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests that verify the actual DI container setup from Program.cs
/// This tests the REAL wiring that happens in the application, not manual construction.
/// 
/// These tests should FAIL if there's a DI wiring issue where LibraryService
/// exists but ComponentService doesn't get it.
/// </summary>
public class LibraryServiceDIWiringTests
{
    /// <summary>
    /// Test the actual DI setup from Program.cs
    /// This reproduces the exact scenario from the bug report:
    /// - LibraryService exists (library_search works)
    /// - But ComponentService doesn't have access to it
    /// </summary>
    [Fact]
    public async Task DIWiring_LibraryServiceExistsButComponentServiceDoesntGetIt_ShouldFail()
    {
        // Arrange - Set up DI container exactly like Program.cs does
        var services = new ServiceCollection();
        
        // Register CircuitManager
        services.AddSingleton<ICircuitManager, CircuitManager>();
        
        // Register ComponentService EXACTLY like Program.cs does (line 26-30)
        // This is BEFORE LibraryService is registered!
        services.AddSingleton<IComponentService>(sp =>
        {
            var libraryService = sp.GetService<ILibraryService>();
            return new ComponentService(libraryService);
        });
        
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
        services.AddSingleton<IResponseMeasurementService>(sp => 
            new ResponseMeasurementService(sp.GetRequiredService<CircuitResultsCache>()));
        services.AddSingleton<IGroupDelayService>(sp => 
            new GroupDelayService(sp.GetRequiredService<CircuitResultsCache>()));
        services.AddSingleton<INetlistParser, NetlistParser>();
        services.AddSingleton<CircuitResultsCache>();
        
        // Create test library
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);
        var subcircuitDef = @"
* Test Subcircuit
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.001
.ENDS
";
        File.WriteAllText(Path.Combine(tempLibPath, "test.lib"), subcircuitDef);
        
        var mcpConfig = new MCPServerConfig { LibraryPaths = new[] { tempLibPath } };
        services.AddSingleton(mcpConfig);
        
        // Register SpeakerDatabaseService
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        services.AddSingleton<ISpeakerDatabaseService>(sp => 
        {
            var db = new SpeakerDatabaseService(dbPath);
            db.InitializeDatabase();
            return db;
        });
        
        // Register LibraryService EXACTLY like Program.cs does (line 160-172)
        // This is AFTER ComponentService registration!
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
        
        // Register MCPService
        services.AddSingleton<MCPService>();
        
        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Get services from DI (like the real application does)
        var libraryService = serviceProvider.GetRequiredService<ILibraryService>();
        var componentService = serviceProvider.GetRequiredService<IComponentService>();
        var mcpService = serviceProvider.GetRequiredService<MCPService>();
        
        // Assert - LibraryService should exist and work
        var searchArgs = JsonSerializer.SerializeToElement(new { query = "275_030" });
        var searchResult = await mcpService.ExecuteTool("library_search", searchArgs);
        Assert.NotNull(searchResult);
        var searchText = searchResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        var searchJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(searchText);
        Assert.NotNull(searchJson);
        Assert.True(searchJson["count"].GetInt32() >= 1, "LibraryService should find subcircuits");
        
        // THIS IS THE KEY TEST - ComponentService should have access to LibraryService
        // If this fails, it means ComponentService was created before LibraryService was registered
        // and got null instead of the actual LibraryService instance
        
        // Test: Try to add a subcircuit component
        var circuitManager = serviceProvider.GetRequiredService<ICircuitManager>();
        var createArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "di_wiring_test",
            make_active = true
        });
        await mcpService.ExecuteTool("create_circuit", createArgs);
        
        var addArgs = JsonSerializer.SerializeToElement(new
        {
            name = "Xspk",
            component_type = "subcircuit",
            model = "275_030",
            nodes = new[] { "in", "0" }
        });
        
        // This should SUCCEED if ComponentService has LibraryService
        // This should FAIL if ComponentService got null LibraryService
        var addResult = await mcpService.ExecuteTool("add_component", addArgs);
        Assert.NotNull(addResult);
        var addText = addResult.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        
        // If ComponentService doesn't have LibraryService, we'll get an error
        // Check if the error message indicates library service is not available
        if (addText.Contains("library service is not available", StringComparison.OrdinalIgnoreCase))
        {
            // THIS IS THE BUG - ComponentService exists, LibraryService exists,
            // but ComponentService doesn't have access to LibraryService
            Assert.Fail(
                "BUG REPRODUCED: LibraryService exists (library_search works) but ComponentService " +
                "doesn't have access to it. ComponentService was likely created before LibraryService " +
                "was registered in DI container, or DI resolution order is wrong.");
        }
        
        // If we get here without error, ComponentService successfully used LibraryService
        var addJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(addText);
        Assert.NotNull(addJson);
        Assert.Equal("added", addJson["status"].GetString());
    }

    /// <summary>
    /// Test that verifies the DI registration order matters
    /// ComponentService registration references LibraryService, but LibraryService
    /// is registered AFTER ComponentService. This should still work because DI
    /// resolves lazily, but let's verify.
    /// </summary>
    [Fact]
    public void DIWiring_ComponentServiceGetsLibraryService_WhenResolvedAfterRegistration()
    {
        // Arrange - Set up DI container
        var services = new ServiceCollection();
        
        // Register ComponentService FIRST (like Program.cs does)
        services.AddSingleton<IComponentService>(sp =>
        {
            var libraryService = sp.GetService<ILibraryService>();
            return new ComponentService(libraryService);
        });
        
        // Register LibraryService AFTER
        services.AddSingleton<ILibraryService>(sp =>
        {
            var speakerDb = new SpeakerDatabaseService(Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db"));
            speakerDb.InitializeDatabase();
            return new LibraryService(speakerDb);
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Resolve ComponentService (this triggers the factory)
        var componentService = serviceProvider.GetRequiredService<IComponentService>();
        var libraryService = serviceProvider.GetRequiredService<ILibraryService>();
        
        // Assert - ComponentService should have gotten LibraryService
        // We can't directly check the private field, but we can test behavior
        // If ComponentService has LibraryService, it can add subcircuits
        // If it doesn't, it will fail with "library service is not available"
        
        // This test verifies that DI lazy resolution works correctly
        // ComponentService factory is called when resolved, at which point
        // LibraryService should already be registered
        Assert.NotNull(componentService);
        Assert.NotNull(libraryService);
        
        // The actual test is in the async test above - this just verifies DI setup
    }
}

