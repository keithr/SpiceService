using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using System.IO;

namespace SpiceSharp.Api.Web.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory for integration testing through the actual HTTP interface.
/// This allows us to test the real DI wiring and service resolution that happens in Program.cs.
/// </summary>
public class SpiceServiceWebApplicationFactory : WebApplicationFactory<SpiceSharp.Api.Web.ProgramMarker>
{
    private readonly string? _testLibraryPath;
    private readonly string? _testDatabasePath;

    /// <summary>
    /// Create a factory with optional test library and database paths
    /// </summary>
    public SpiceServiceWebApplicationFactory(string? testLibraryPath = null, string? testDatabasePath = null)
    {
        _testLibraryPath = testLibraryPath;
        _testDatabasePath = testDatabasePath;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Logging:LogLevel:Default", "Warning" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Override library paths if test library path is provided
            if (!string.IsNullOrEmpty(_testLibraryPath))
            {
                // Remove the default MCPServerConfig registration
                var configDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MCPServerConfig));
                if (configDescriptor != null)
                {
                    services.Remove(configDescriptor);
                }

                // Register test configuration
                services.AddSingleton(new MCPServerConfig
                {
                    LibraryPaths = new[] { _testLibraryPath },
                    Port = 0, // Let the system assign a port
                    Version = "1.0.0"
                });
            }

            // Override database path if test database path is provided
            if (!string.IsNullOrEmpty(_testDatabasePath))
            {
                // Remove the default SpeakerDatabaseService registration
                var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISpeakerDatabaseService));
                if (dbDescriptor != null)
                {
                    services.Remove(dbDescriptor);
                }

                // Register test database service
                services.AddSingleton<ISpeakerDatabaseService>(sp =>
                {
                    var db = new SpeakerDatabaseService(_testDatabasePath);
                    db.InitializeDatabase();
                    return db;
                });
            }
        });

        // Use test environment
        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Create a factory with a temporary test library directory
    /// </summary>
    public static SpiceServiceWebApplicationFactory CreateWithTestLibrary(Action<string>? setupLibrary = null)
    {
        var tempLibPath = Path.Combine(Path.GetTempPath(), $"test_lib_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempLibPath);

        setupLibrary?.Invoke(tempLibPath);

        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");

        return new SpiceServiceWebApplicationFactory(tempLibPath, tempDbPath);
    }
}

