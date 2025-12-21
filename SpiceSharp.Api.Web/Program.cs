// Copyright (c) 2025 Keith Rule
// This software is free for personal use. Commercial use requires a commercial license.

using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add CORS support for MCP endpoint
builder.Services.AddCors(options =>
{
    options.AddPolicy("MCPPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register SpiceSharp services as singletons
builder.Services.AddSingleton<ICircuitManager, CircuitManager>();
builder.Services.AddSingleton<IComponentService, ComponentService>();
builder.Services.AddSingleton<IModelService, ModelService>();
builder.Services.AddSingleton<IOperatingPointService, OperatingPointService>();
builder.Services.AddSingleton<IDCAnalysisService, DCAnalysisService>();
builder.Services.AddSingleton<ITransientAnalysisService, TransientAnalysisService>();
builder.Services.AddSingleton<IACAnalysisService, ACAnalysisService>();
builder.Services.AddSingleton<INetlistService, NetlistService>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddSingleton<IParameterSweepService, ParameterSweepService>();
builder.Services.AddSingleton<INoiseAnalysisService, NoiseAnalysisService>();
builder.Services.AddSingleton<ITemperatureSweepService, TemperatureSweepService>();
builder.Services.AddSingleton<IImpedanceAnalysisService>(sp => 
    new ImpedanceAnalysisService(sp.GetRequiredService<IACAnalysisService>()));
builder.Services.AddSingleton<IResponseMeasurementService>(sp => 
    new ResponseMeasurementService(sp.GetRequiredService<CircuitResultsCache>()));
builder.Services.AddSingleton<IGroupDelayService>(sp => 
    new GroupDelayService(sp.GetRequiredService<CircuitResultsCache>()));
builder.Services.AddSingleton<INetlistParser, NetlistParser>();

// MCP Server Configuration
var mcpConfig = new MCPServerConfig();
var discoveryConfig = new DiscoveryConfig();

// Handle command-line arguments for port and discovery
var portArg = args.FirstOrDefault(arg => arg.StartsWith("--port="));
if (portArg != null && int.TryParse(portArg.Split('=')[1], out var port))
{
    mcpConfig.Port = port;
    builder.WebHost.UseUrls($"http://*:{port}");
}

var discoveryPortArg = args.FirstOrDefault(arg => arg.StartsWith("--discovery-port="));
if (discoveryPortArg != null && int.TryParse(discoveryPortArg.Split('=')[1], out var discoveryPort))
{
    discoveryConfig.Port = discoveryPort;
}

if (args.Contains("--no-discovery"))
{
    discoveryConfig.Enabled = false;
}

// Auto-detect local IP if not set
if (string.IsNullOrEmpty(mcpConfig.LocalIp))
{
    mcpConfig.LocalIp = GetLocalIpAddress();
}

// Auto-port selection if port not specified
if (!args.Any(arg => arg.StartsWith("--port=")))
{
    mcpConfig.Port = FindAvailablePort(8081);
    builder.WebHost.UseUrls($"http://*:{mcpConfig.Port}");
}
else
{
    // Verify specified port is available
    if (!IsPortAvailable(mcpConfig.Port))
    {
        Console.WriteLine($"Warning: Port {mcpConfig.Port} is not available, finding alternative...");
        mcpConfig.Port = FindAvailablePort(mcpConfig.Port + 1);
        builder.WebHost.UseUrls($"http://*:{mcpConfig.Port}");
        Console.WriteLine($"Using port {mcpConfig.Port} instead");
    }
}

builder.Services.AddSingleton(mcpConfig);
builder.Services.AddSingleton(discoveryConfig);
builder.Services.AddSingleton<CircuitResultsCache>();

// Register speaker database service (optional, for speaker search functionality)
builder.Services.AddSingleton<ISpeakerDatabaseService, SpeakerDatabaseService>();

// Register enclosure design service (optional, for enclosure design calculations)
builder.Services.AddSingleton<IEnclosureDesignService, EnclosureDesignService>();

// Register crossover compatibility service (optional, for crossover compatibility checking)
builder.Services.AddSingleton<ICrossoverCompatibilityService, CrossoverCompatibilityService>();

builder.Services.AddSingleton<MCPService>();
builder.Services.AddSingleton<DiscoveryService>();

// Add minimal controllers (only MCP)
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("MCPPolicy");
app.UseAuthorization();
app.MapControllers();

// Start discovery service if enabled
if (discoveryConfig.Enabled)
{
    var discoveryService = app.Services.GetRequiredService<DiscoveryService>();
    _ = Task.Run(() => discoveryService.StartBroadcasting(discoveryConfig));
    Console.WriteLine($"Discovery service started on UDP port {discoveryConfig.Port}");
}

Console.WriteLine($"MCP Server running on port {mcpConfig.Port}");
Console.WriteLine($"MCP endpoint: http://{mcpConfig.LocalIp}:{mcpConfig.Port}/mcp");

app.Run();

// Helper methods
static int FindAvailablePort(int startPort)
{
    for (int port = startPort; port < startPort + 100; port++)
    {
        if (IsPortAvailable(port))
        {
            return port;
        }
    }
    throw new InvalidOperationException($"Could not find available port starting from {startPort}");
}

static bool IsPortAvailable(int port)
{
    try
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        int foundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(IPAddress.Loopback, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
            if (success)
            {
                client.EndConnect(result);
                return false;
            }
        }
        catch
        {
            // Connection failed, port is likely available
        }
        
        return foundPort == port;
    }
    catch (SocketException)
    {
        return false;
    }
    catch
    {
        return false;
    }
}

static string GetLocalIpAddress()
{
    try
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
            {
                return ip.ToString();
            }
        }
    }
    catch
    {
        // Fallback to localhost
    }
    return "127.0.0.1";
}
