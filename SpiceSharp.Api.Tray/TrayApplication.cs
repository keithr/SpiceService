using System.Linq;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using SpiceSharp.Api.Tray.Services;
using SpiceSharp.Api.Tray.Models;

namespace SpiceSharp.Api.Tray;

public class TrayApplication : ApplicationContext
{
    private NotifyIcon? _notifyIcon;
    private readonly ICircuitManager _circuitManager;
    private readonly MCPService _mcpService;
    private readonly CircularLogBuffer _logBuffer;
    private readonly DiscoveryService? _discoveryService;
    private WebApplication? _webApp;
    private readonly MCPServerConfig _mcpConfig;
    private bool _autoStartEnabled;
    private bool _networkVisible; // true = network accessible, false = localhost only
    private System.Windows.Forms.Timer? _statusTimer;
    private LogDialog? _logDialog;

    public TrayApplication()
    {
        // Initialize log buffer (circular buffer with 1000 entries)
        _logBuffer = new CircularLogBuffer(1000);
        
        // Initialize services directly (in-process)
        _circuitManager = new CircuitManager();
        var componentService = new ComponentService();
        var modelService = new ModelService();
        var operatingPointService = new OperatingPointService();
        var dcAnalysisService = new DCAnalysisService();
        var transientAnalysisService = new TransientAnalysisService();
        var acAnalysisService = new ACAnalysisService();
        var netlistService = new NetlistService();
        
        // Initialize MCP config and find available port
        _mcpConfig = new MCPServerConfig();
        var availablePort = FindAvailablePort(8081);
        _mcpConfig.Port = availablePort;
        
        // Configure library paths with priority order:
        // 1. User libraries directory (Documents\SpiceService\libraries) - highest priority
        // 2. Config file paths (if config file exists and specifies paths)
        // 3. Installed libraries (next to executable) - included in MSI
        // 4. Sample libraries (development/testing only)
        var libraryPaths = new List<string>();
        
        // 1. User libraries directory (Documents\SpiceService\libraries)
        var userLibPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SpiceService", "libraries");
        if (Directory.Exists(userLibPath))
        {
            libraryPaths.Add(userLibPath);
        }
        
        // 2. Config file paths (TODO: implement config file loading)
        // For now, skip - can be added later if needed
        
        // 3. Installed libraries (next to executable in libraries subdirectory)
        var installedLibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libraries");
        if (Directory.Exists(installedLibPath))
        {
            libraryPaths.Add(installedLibPath);
        }
        
        // 4. Development libraries (in source directory - for development builds)
        // Try multiple paths relative to executable to find source directory
        var devLibPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "libraries"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "libraries"),
            Path.Combine(Directory.GetCurrentDirectory(), "libraries"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "libraries"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "libraries"),
        };
        
        foreach (var devLibPath in devLibPaths)
        {
            var fullPath = Path.GetFullPath(devLibPath);
            if (Directory.Exists(fullPath) && Directory.GetFiles(fullPath, "kicad_*.lib", SearchOption.TopDirectoryOnly).Length > 0)
            {
                libraryPaths.Add(fullPath);
                break; // Only add first valid path found
            }
        }
        
        // 5. Sample libraries (development/testing - check relative to source)
        var sampleLibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "sample_libraries");
        if (Directory.Exists(sampleLibPath))
        {
            libraryPaths.Add(sampleLibPath);
        }
        else
        {
            // Also try relative to current directory
            var relativeLibPath = Path.Combine(Directory.GetCurrentDirectory(), "sample_libraries");
            if (Directory.Exists(relativeLibPath))
            {
                libraryPaths.Add(relativeLibPath);
            }
        }
        
        if (libraryPaths.Any())
        {
            _mcpConfig.LibraryPaths = libraryPaths;
            var totalFiles = libraryPaths.Sum(p => Directory.GetFiles(p, "*.lib", SearchOption.AllDirectories).Length);
            _logBuffer.Add(Services.LogLevel.Information, 
                $"Library paths configured ({libraryPaths.Count} paths, {totalFiles} .lib files): {string.Join(", ", libraryPaths)}");
        }
        else
        {
            _logBuffer.Add(Services.LogLevel.Warning, 
                "No library paths found. Library search will not be available. " +
                "Place .lib files in Documents\\SpiceService\\libraries or next to the executable.");
        }
        
        _mcpConfig.LocalIp = GetLocalIpAddress();
        
        // Create a logger factory that writes to our buffer
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new Services.LogBufferLoggerProvider(_logBuffer));
        });
        var mcpLogger = loggerFactory.CreateLogger<SpiceSharp.Api.Web.Services.MCPService>();
        var discoveryLogger = loggerFactory.CreateLogger<SpiceSharp.Api.Web.Services.DiscoveryService>();
        
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
        // Create library service if paths are configured
        ILibraryService? libraryService = null;
        if (_mcpConfig.LibraryPaths != null && _mcpConfig.LibraryPaths.Any())
        {
            libraryService = new LibraryService();
            libraryService.IndexLibraries(_mcpConfig.LibraryPaths);
        }

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
            _mcpConfig,
            libraryService,
            mcpLogger);
        
        // Log startup
        _logBuffer.Add(Services.LogLevel.Information, "SpiceService Tray Application started");
        
        try
        {
            InitializeTrayIcon();
            LoadAutoStartStatus();
            LoadNetworkVisibility();
            
            // Initialize discovery service AFTER loading network visibility setting
            _discoveryService = new SpiceSharp.Api.Web.Services.DiscoveryService(_mcpConfig, _mcpService, discoveryLogger, _networkVisible);
            _logBuffer.Add(Services.LogLevel.Information, "Tray icon initialized successfully");
            
            // Start HTTP server
            StartHttpServer();
            
            // Start discovery service
            StartDiscoveryService();
        }
        catch (Exception ex)
        {
            _logBuffer.Add(Services.LogLevel.Error, "Failed to initialize tray application", ex);
            // Re-throw to allow Program.cs to show error message to user
            throw;
        }
    }
    
    private void StartHttpServer()
    {
        try
        {
            var builder = WebApplication.CreateBuilder();
            
            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MCPPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            
            // Configure JSON options
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
            });
            
            // Verify port is still available before binding
            if (!IsPortAvailable(_mcpConfig.Port))
            {
                _logBuffer.Add(Services.LogLevel.Warning, $"Port {_mcpConfig.Port} is no longer available, finding new port...");
                var newPort = FindAvailablePort(_mcpConfig.Port + 1);
                _mcpConfig.Port = newPort;
                _logBuffer.Add(Services.LogLevel.Information, $"Switched to port {newPort}");
                _logBuffer.Add(Services.LogLevel.Information, $"Discovery service will use updated port {newPort}");
            }
            
            // Set the URL to use the configured port
            var listenUrl = _networkVisible 
                ? $"http://*:{_mcpConfig.Port}" 
                : $"http://127.0.0.1:{_mcpConfig.Port}";
            builder.WebHost.UseUrls(listenUrl);
            
            _webApp = builder.Build();
            
            // Log the actual port that will be used (in case it changed)
            _logBuffer.Add(Services.LogLevel.Information, $"HTTP server configured to listen on port {_mcpConfig.Port}");
            
            // Configure middleware
            _webApp.UseCors("MCPPolicy");
            
            // Map discovery endpoint (for McpRemote.exe to find current MCP endpoint)
            _webApp.MapGet("/discovery", async (HttpContext context) =>
            {
                var host = _networkVisible 
                    ? (_mcpConfig.LocalIp ?? "127.0.0.1") 
                    : "127.0.0.1";
                var endpointUrl = $"http://{host}:{_mcpConfig.Port}/mcp";
                
                await context.Response.WriteAsJsonAsync(new
                {
                    mcpEndpoint = endpointUrl,
                    port = _mcpConfig.Port,
                    host = host,
                    networkVisible = _networkVisible
                });
            });
            
            // Map MCP endpoint
            _webApp.MapPost("/mcp", async (HttpContext context) =>
            {
                var remoteEndpoint = $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}";
                _logBuffer.Add(Services.LogLevel.Information, $"Incoming connection from {remoteEndpoint} to /mcp endpoint");
                
                JsonElement request = default;
                try
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var jsonText = await reader.ReadToEndAsync();
                    _logBuffer.Add(Services.LogLevel.Debug, $"Received request body: {jsonText.Substring(0, Math.Min(200, jsonText.Length))}...");
                    request = JsonSerializer.Deserialize<JsonElement>(jsonText);
                    
                    // Validate JSON-RPC version
                    if (!request.TryGetProperty("jsonrpc", out var jsonrpcElement) ||
                        jsonrpcElement.GetString() != "2.0")
                    {
                        _logBuffer.Add(Services.LogLevel.Warning, $"Invalid JSON-RPC version from {remoteEndpoint}");
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            jsonrpc = "2.0",
                            error = new { code = -32600, message = "Invalid Request", data = "jsonrpc must be '2.0'" },
                            id = (object?)null
                        });
                        return;
                    }
                    
                    // Get method
                    if (!request.TryGetProperty("method", out var methodElement))
                    {
                        _logBuffer.Add(Services.LogLevel.Warning, $"Missing method in request from {remoteEndpoint}");
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            jsonrpc = "2.0",
                            error = new { code = -32600, message = "Invalid Request", data = "method is required" },
                            id = (object?)null
                        });
                        return;
                    }
                    
                    var method = methodElement.GetString() ?? throw new InvalidOperationException("method must be a string");
                    bool hasId = request.TryGetProperty("id", out var idElement);
                    bool isNotification = !hasId;
                    JsonElement? id = hasId ? idElement : null;
                    var @params = request.TryGetProperty("params", out var paramsElement) ? paramsElement : default(JsonElement);
                    
                    _logBuffer.Add(Services.LogLevel.Information, $"Processing MCP method: {method} from {remoteEndpoint} (ID: {(id.HasValue ? id.Value.ToString() : isNotification ? "notification" : "null")})");
                    
                    // Route to appropriate handler
                    object? result = method switch
                    {
                        "initialize" => HandleInitialize(),
                        "tools/list" => HandleToolsList(),
                        "tools/call" => await HandleToolsCall(@params),
                        "notifications/initialized" => null, // Notification - no response
                        _ => throw new InvalidOperationException($"Method not found: {method}")
                    };
                    
                    object HandleInitialize()
                    {
                        _logBuffer.Add(Services.LogLevel.Information, $"Handling initialize method from {remoteEndpoint}");
                        return new
                        {
                            protocolVersion = _mcpConfig.ProtocolVersion,
                            serverInfo = new { name = _mcpConfig.Name, version = _mcpConfig.Version },
                            capabilities = new { }
                        };
                    }
                    
                    object HandleToolsList()
                    {
                        _logBuffer.Add(Services.LogLevel.Information, $"Handling tools/list method from {remoteEndpoint}");
                        var tools = _mcpService.GetTools();
                        _logBuffer.Add(Services.LogLevel.Information, $"Returning {tools.Count} tools to {remoteEndpoint}");
                        return new
                        {
                            tools = tools.Select(t => new
                            {
                                name = t.Name,
                                description = t.Description,
                                inputSchema = t.InputSchema
                            })
                        };
                    }
                    
                    async Task<object> HandleToolsCall(JsonElement @params)
                    {
                        var toolName = @params.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? throw new ArgumentException("name is required") : throw new ArgumentException("name is required");
                        var toolArgs = @params.TryGetProperty("arguments", out var argsEl) ? argsEl : default(JsonElement);
                        _logBuffer.Add(Services.LogLevel.Information, $"Handling tools/call for tool: {toolName} from {remoteEndpoint}");
                        _logBuffer.Add(Services.LogLevel.Debug, $"Tool arguments: {(toolArgs.ValueKind != JsonValueKind.Undefined ? toolArgs.ToString() : "none")}");
                        var toolResult = await _mcpService.ExecuteTool(toolName, toolArgs);
                        _logBuffer.Add(Services.LogLevel.Information, $"Tool {toolName} executed successfully for {remoteEndpoint}");
                        return toolResult;
                    }
                    
                    // JSON-RPC spec: Notifications (no id) should not receive responses
                    if (isNotification)
                    {
                        _logBuffer.Add(Services.LogLevel.Debug, $"Skipping response for notification: {method}");
                        return; // Don't send any response for notifications
                    }
                    
                    // Build response for requests (must have id)
                    var response = new Dictionary<string, object> { { "jsonrpc", "2.0" } };
                    
                    // Include id if present (should always be present for requests, but handle null case)
                    if (id.HasValue)
                    {
                        if (id.Value.ValueKind == JsonValueKind.Null)
                        {
                            // Request had "id": null - include null in response (only for error responses per spec)
                            // But for successful responses, we should have a valid id
                            response["id"] = null!;
                        }
                        else
                        {
                            response["id"] = id.Value;
                        }
                    }
                    
                    if (result != null)
                        response["result"] = result;
                    
                    _logBuffer.Add(Services.LogLevel.Debug, $"Sending response for method {method} (Status: 200)");
                    await context.Response.WriteAsJsonAsync(response);
                }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("Method not found"))
                {
                    JsonElement? id = request.ValueKind != JsonValueKind.Undefined && request.TryGetProperty("id", out var idElement) ? idElement : null;
                    _logBuffer.Add(Services.LogLevel.Warning, $"Method not found from {remoteEndpoint}: {ex.Message}");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        jsonrpc = "2.0",
                        error = new { code = -32601, message = "Method not found", data = ex.Message },
                        id = id.HasValue ? id.Value : (object?)null
                    });
                }
                catch (ArgumentException ex)
                {
                    JsonElement? id = request.ValueKind != JsonValueKind.Undefined && request.TryGetProperty("id", out var idElement) ? idElement : null;
                    _logBuffer.Add(Services.LogLevel.Warning, $"Invalid params from {remoteEndpoint}: {ex.Message}");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        jsonrpc = "2.0",
                        error = new { code = -32602, message = "Invalid params", data = ex.Message },
                        id = id.HasValue ? id.Value : (object?)null
                    });
                }
                catch (Exception ex)
                {
                    JsonElement? id = request.ValueKind != JsonValueKind.Undefined && request.TryGetProperty("id", out var idElement) ? idElement : null;
                    _logBuffer.Add(Services.LogLevel.Error, $"Error processing request from {remoteEndpoint}: {ex.Message}", ex);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        jsonrpc = "2.0",
                        error = new { code = -32603, message = "Internal error", data = ex.Message },
                        id = id.HasValue ? id.Value : (object?)null
                    });
                }
                finally
                {
                    _logBuffer.Add(Services.LogLevel.Debug, $"Request from {remoteEndpoint} completed");
                }
            });
            
            // Start the server in the background with proper error handling
            var serverTask = Task.Run(async () =>
            {
                try
                {
                    _logBuffer.Add(Services.LogLevel.Debug, $"Attempting to start HTTP server on port {_mcpConfig.Port}...");
                    _logBuffer.Add(Services.LogLevel.Debug, $"Server URL: {(_networkVisible ? $"http://*:{_mcpConfig.Port}" : $"http://127.0.0.1:{_mcpConfig.Port}")}");
                    _logBuffer.Add(Services.LogLevel.Debug, $"Network visible: {_networkVisible}");
                    _logBuffer.Add(Services.LogLevel.Debug, $"Local IP: {_mcpConfig.LocalIp}");
                    
                    await _webApp!.RunAsync();
                }
                catch (Exception ex)
                {
                    _logBuffer.Add(Services.LogLevel.Error, $"HTTP server failed to start or crashed", ex);
                    _logBuffer.Add(Services.LogLevel.Error, $"Exception type: {ex.GetType().Name}");
                    _logBuffer.Add(Services.LogLevel.Error, $"Exception message: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        _logBuffer.Add(Services.LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                    }
                    
                    // Try to restart on a new port if binding failed
                    if (ex is HttpRequestException || ex.Message.Contains("address already in use") || ex.Message.Contains("port") || ex.Message.Contains("bind"))
                    {
                        _logBuffer.Add(Services.LogLevel.Warning, $"Port binding failed, attempting to find new port...");
                        try
                        {
                            var newPort = FindAvailablePort(_mcpConfig.Port + 1);
                            _mcpConfig.Port = newPort;
                            _logBuffer.Add(Services.LogLevel.Information, $"Found new port: {newPort}, restarting server...");
                            // Note: Server restart would need to be handled separately
                            // For now, just log the error
                        }
                        catch (Exception restartEx)
                        {
                            _logBuffer.Add(Services.LogLevel.Error, "Failed to find alternative port", restartEx);
                        }
                    }
                }
            });
            
            // Verify the server actually started by checking if it's listening
            _ = Task.Run(async () =>
            {
                // Wait a moment for the server to start
                await Task.Delay(1000);
                
                // Verify the server is actually listening
                if (!await VerifyServerIsListening(_mcpConfig.Port))
                {
                    _logBuffer.Add(Services.LogLevel.Error, $"Server verification failed - port {_mcpConfig.Port} is not responding. Server may have failed to start.");
                }
                else
                {
                    _logBuffer.Add(Services.LogLevel.Information, $"Server verified - listening on port {_mcpConfig.Port}");
                }
            });
            
            _logBuffer.Add(Services.LogLevel.Information, $"HTTP server starting on port {_mcpConfig.Port} ({( _networkVisible ? "network accessible" : "localhost only")})");
            _logBuffer.Add(Services.LogLevel.Information, $"MCP endpoint: http://{_mcpConfig.LocalIp}:{_mcpConfig.Port}/mcp");
        }
        catch (Exception ex)
        {
            _logBuffer.Add(Services.LogLevel.Error, "Failed to start HTTP server", ex);
        }
    }
    
    private static int FindAvailablePort(int startPort)
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
    
    private static bool IsPortAvailable(int port)
    {
        try
        {
            // Try to bind to the port to verify it's available
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            int foundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            
            // Double-check: verify the port is actually available by trying to connect
            // This helps catch cases where the port is reserved but not actively listening
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(IPAddress.Loopback, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                if (success)
                {
                    client.EndConnect(result);
                    // Port is in use (connection succeeded)
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
            // Port is in use or cannot bind
            return false;
        }
        catch
        {
            // Other error, assume port is not available
            return false;
        }
    }
    
    private async Task<bool> VerifyServerIsListening(int port)
    {
        _logBuffer.Add(Services.LogLevel.Debug, $"Verifying server is listening on port {port}...");
        
        // Try to connect to verify the server is actually listening
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                _logBuffer.Add(Services.LogLevel.Debug, $"Connection attempt {attempt + 1}/5 to localhost:{port}");
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
                var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500));
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == connectTask && client.Connected)
                {
                    _logBuffer.Add(Services.LogLevel.Debug, $"Successfully connected to port {port} on attempt {attempt + 1}");
                    return true;
                }
                else
                {
                    _logBuffer.Add(Services.LogLevel.Debug, $"Connection attempt {attempt + 1} timed out or failed");
                }
            }
            catch (Exception ex)
            {
                _logBuffer.Add(Services.LogLevel.Debug, $"Connection attempt {attempt + 1} exception: {ex.Message}");
                // Connection failed, server might still be starting
            }
            
            // Wait a bit before retrying
            await Task.Delay(200);
        }
        
        _logBuffer.Add(Services.LogLevel.Warning, $"Failed to verify server is listening on port {port} after 5 attempts");
        return false;
    }
    
    private static string GetLocalIpAddress()
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
    
    private void StartDiscoveryService()
    {
        try
        {
            var discoveryConfig = new SpiceSharp.Api.Web.Models.DiscoveryConfig
            {
                Enabled = true,
                Port = 19847,
                IntervalSeconds = 30
            };
            
            // Verify tools are available
            var tools = _mcpService.GetTools();
            _logBuffer.Add(Services.LogLevel.Information, $"Starting discovery service on UDP port {discoveryConfig.Port}");
            _logBuffer.Add(Services.LogLevel.Information, $"Discovery will announce HTTP server on port {_mcpConfig.Port}");
            _logBuffer.Add(Services.LogLevel.Information, $"MCP Service has {tools.Count} tools available: {string.Join(", ", tools.Select(t => t.Name))}");
            
            // Verify the port in the config matches what we're actually using
            if (_mcpConfig.Port <= 0)
            {
                _logBuffer.Add(Services.LogLevel.Error, $"Invalid port in MCP config: {_mcpConfig.Port}");
            }
            else
            {
                _logBuffer.Add(Services.LogLevel.Debug, $"Discovery announcement will include port: {_mcpConfig.Port}, host: {_mcpConfig.LocalIp}");
            }
            
            _ = Task.Run(async () => await _discoveryService!.StartBroadcasting(discoveryConfig));
        }
        catch (Exception ex)
        {
            _logBuffer.Add(Services.LogLevel.Error, "Failed to start discovery service", ex);
        }
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "SpiceService - Circuit Simulation API",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
    }

    private Icon CreateIcon()
    {
        try
        {
            // Try to load from embedded resource first (50x50 for tray icon)
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            
            // Try different possible resource name formats
            var possibleNames = new[]
            {
                "SpiceSharp.Api.Tray.Resources.spice_50x50.png",
                "SpiceSharp.Api.Tray.resources.spice_50x50.png",
                "resources.spice_50x50.png",
                "spice_50x50.png"
            };
            
            foreach (var resourceName in possibleNames)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var bitmap = new Bitmap(stream);
                    // Convert bitmap to icon
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
            
            // If not found, try enumerating all resources to find it
            var allResources = assembly.GetManifestResourceNames();
            var iconResource = allResources.FirstOrDefault(r => r.Contains("spice_50x50") || r.Contains("spice"));
            if (iconResource != null)
            {
                using var stream = assembly.GetManifestResourceStream(iconResource);
                if (stream != null)
                {
                    using var bitmap = new Bitmap(stream);
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue to fallback
            System.Diagnostics.Debug.WriteLine($"Failed to load embedded icon: {ex.Message}");
        }

        // Try to load from file system as fallback
        try
        {
            var resourcePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "resources", "spice_50x50.png");
            
            if (File.Exists(resourcePath))
            {
                using var bitmap = new Bitmap(resourcePath);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch { }

        // Final fallback: create simple icon programmatically
        using var fallbackBitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(fallbackBitmap);
        graphics.Clear(Color.Blue);
        graphics.FillEllipse(new SolidBrush(Color.White), 2, 2, 12, 12);
        graphics.DrawString("S", new Font("Arial", 8, FontStyle.Bold), 
            new SolidBrush(Color.Blue), 4, 2);
        return Icon.FromHandle(fallbackBitmap.GetHicon());
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("Status: Starting...")
        {
            Enabled = false
        };
        menu.Items.Add(statusItem);

        menu.Items.Add(new ToolStripSeparator());

        var autoStartItem = new ToolStripMenuItem("Auto-start on Login")
        {
            Checked = _autoStartEnabled
        };
        autoStartItem.Click += (s, e) =>
        {
            _autoStartEnabled = !_autoStartEnabled;
            autoStartItem.Checked = _autoStartEnabled;
            SetAutoStart(_autoStartEnabled);
        };
        menu.Items.Add(autoStartItem);

        var networkVisibilityItem = new ToolStripMenuItem("Network Accessible")
        {
            Checked = _networkVisible
        };
        networkVisibilityItem.Click += (s, e) =>
        {
            _networkVisible = !_networkVisible;
            networkVisibilityItem.Checked = _networkVisible;
            SetNetworkVisibility(_networkVisible);
        };
        menu.Items.Add(networkVisibilityItem);

        var configureIDEItem = new ToolStripMenuItem("Configure IDE Integration...");
        configureIDEItem.Click += (s, e) => ShowIDEConfigurationDialog();
        menu.Items.Add(configureIDEItem);

        menu.Items.Add(new ToolStripSeparator());

        var circuitsItem = new ToolStripMenuItem("List Circuits...");
        circuitsItem.Click += async (s, e) => await ShowCircuitsDialog();
        menu.Items.Add(circuitsItem);

        var exportItem = new ToolStripMenuItem("Export Circuit...");
        exportItem.Click += async (s, e) => await ShowExportDialog();
        menu.Items.Add(exportItem);

        var logItem = new ToolStripMenuItem("View Logs...");
        logItem.Click += (s, e) => ShowLogDialog();
        menu.Items.Add(logItem);

        menu.Items.Add(new ToolStripSeparator());

        // Remove "Open Server URL" menu item - no REST server anymore

        var aboutItem = new ToolStripMenuItem("About...");
        aboutItem.Click += (s, e) => ShowAboutDialog();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();
        menu.Items.Add(exitItem);

        // Update status periodically
        _statusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _statusTimer.Tick += async (s, e) =>
        {
            var status = await CheckServerStatus();
            statusItem.Text = $"Status: {status}";
        };
        _statusTimer.Start();

        return menu;
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        // No Swagger UI - show about dialog instead
        ShowAboutDialog();
    }

    private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // No Swagger UI - show about dialog instead
            ShowAboutDialog();
        }
    }

    private async Task<string> CheckServerStatus()
    {
        try
        {
            // Direct service call - no HTTP, no network overhead
            var result = await _mcpService.ExecuteTool("get_service_status", default);
            var visibility = _networkVisible ? "Network" : "Local";
            return $"Running ({visibility})";
        }
        catch (Exception ex)
        {
            _logBuffer.Add(Services.LogLevel.Warning, "Failed to check server status", ex);
            return "Error";
        }
    }

    private void LoadAutoStartStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            _autoStartEnabled = key?.GetValue("SpiceServiceTray") != null;
        }
        catch
        {
            _autoStartEnabled = false;
        }
    }

    private void LoadNetworkVisibility()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\SpiceService\Tray", false);
            if (key?.GetValue("NetworkVisible") != null)
            {
                // Registry DWord values come back as int (0 or 1)
                var value = key.GetValue("NetworkVisible");
                if (value is int intValue)
                {
                    _networkVisible = intValue != 0;
                }
                else
                {
                    _networkVisible = Convert.ToBoolean(value);
                }
            }
            else
            {
                // Default to localhost only for security
                _networkVisible = false;
                // Save the default value so it persists
                SetNetworkVisibility(false, silent: true);
            }
        }
        catch
        {
            _networkVisible = false;
        }
    }

    private void SetNetworkVisibility(bool networkVisible, bool silent = false)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"SOFTWARE\SpiceService\Tray", true);
            
            if (key == null) return;

            // Store as DWord (0 or 1) for better compatibility
            key.SetValue("NetworkVisible", networkVisible ? 1 : 0, RegistryValueKind.DWord);
            
            _networkVisible = networkVisible;
            
            if (!silent)
            {
                var visibilityText = networkVisible ? "Network Accessible" : "Localhost Only";
                ShowNotification("Network Visibility Changed", 
                    $"SpiceService is now set to: {visibilityText}\n" +
                    (networkVisible 
                        ? "Services will be accessible from other devices on the network."
                        : "Services will only be accessible from this computer.") +
                    "\n\nNote: Restart the application for network visibility changes to take effect.", 
                    ToolTipIcon.Info);
                
                _logBuffer.Add(Services.LogLevel.Information, $"Network visibility changed to: {visibilityText}");
                _logBuffer.Add(Services.LogLevel.Warning, "Application restart required for network visibility changes to take effect");
            }
            
            // Note: Restarting the HTTP server and discovery service would require more complex logic
            // For now, inform the user that a restart is needed
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show($"Failed to set network visibility: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            
            if (key == null) return;

            if (enable)
            {
                var exePath = Application.ExecutablePath;
                key.SetValue("SpiceServiceTray", $"\"{exePath}\"");
                ShowNotification("Auto-start Enabled", 
                    "SpiceService will start automatically when you log in.", ToolTipIcon.Info);
            }
            else
            {
                key.DeleteValue("SpiceServiceTray", false);
                ShowNotification("Auto-start Disabled", 
                    "SpiceService will not start automatically.", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set auto-start: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ShowCircuitsDialog()
    {
        try
        {
            _logBuffer.Add(Services.LogLevel.Debug, "Opening circuits dialog");
            // Direct service call
            var result = await _mcpService.ExecuteTool("list_circuits", default);
            var circuits = ParseCircuitsFromMCPResult(result);

            if (circuits == null || circuits.Count == 0)
            {
                _logBuffer.Add(Services.LogLevel.Information, "No circuits found");
                MessageBox.Show("No circuits found.", "Circuits", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _logBuffer.Add(Services.LogLevel.Information, $"Found {circuits.Count} circuit(s)");
            using var dialog = new CircuitsDialog(circuits, _mcpService);
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logBuffer.Add(Services.LogLevel.Error, "Error retrieving circuits", ex);
            MessageBox.Show($"Error retrieving circuits: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ShowExportDialog()
    {
        try
        {
            _logBuffer.Add(Services.LogLevel.Debug, "Opening export circuit dialog");
            // Direct service call
            var result = await _mcpService.ExecuteTool("list_circuits", default);
            var circuits = ParseCircuitsFromMCPResult(result);

            if (circuits == null || circuits.Count == 0)
            {
                _logBuffer.Add(Services.LogLevel.Information, "No circuits found to export");
                MessageBox.Show("No circuits found to export.", "Export Circuit", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new ExportCircuitDialog(circuits, _mcpService);
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logBuffer.Add(Services.LogLevel.Error, "Error in export dialog", ex);
            MessageBox.Show($"Error: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<CircuitInfo> ParseCircuitsFromMCPResult(MCPToolResult result)
    {
        // Extract JSON from MCP result and deserialize
        if (result.Content.Count > 0 && result.Content[0].Type == "text")
        {
            var jsonText = result.Content[0].Text;
            var jsonDoc = JsonDocument.Parse(jsonText);
            var circuits = new List<CircuitInfo>();
            
            foreach (var circuitElement in jsonDoc.RootElement.EnumerateArray())
            {
                circuits.Add(new CircuitInfo
                {
                    Id = circuitElement.GetProperty("id").GetString() ?? "",
                    Description = circuitElement.GetProperty("description").GetString() ?? "",
                    IsActive = circuitElement.GetProperty("is_active").GetBoolean()
                });
            }
            
            return circuits;
        }
        
        return new List<CircuitInfo>();
    }

    private void ShowAboutDialog()
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog();
    }

    private void ShowLogDialog()
    {
        if (_logDialog == null || _logDialog.IsDisposed)
        {
            _logDialog = new LogDialog(_logBuffer);
            _logDialog.FormClosed += (s, e) => _logDialog = null;
            _logDialog.Show();
        }
        else
        {
            _logDialog.BringToFront();
            _logDialog.Focus();
        }
    }


    private void ShowNotification(string title, string message, ToolTipIcon icon)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
    }

    private void ExitApplication()
    {
        // Hide tray icon first
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
        }
        
        // Stop status timer
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        _statusTimer = null;
        
        // Close log dialog if open
        if (_logDialog != null && !_logDialog.IsDisposed)
        {
            _logDialog.Close();
            _logDialog.Dispose();
            _logDialog = null;
        }
        
        // Stop HTTP server
        try
        {
            if (_webApp != null)
            {
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                _webApp.StopAsync(cts.Token).Wait(cts.Token);
                _webApp.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
                _webApp = null;
            }
        }
        catch
        {
            // Ignore errors during shutdown
        }
        
        // Stop discovery service
        try
        {
            _discoveryService?.StopBroadcasting();
        }
        catch
        {
            // Ignore errors during shutdown
        }
        
        // Dispose tray icon
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        
        // Exit the application
        Application.Exit();
        Environment.Exit(0); // Force exit if Application.Exit doesn't work
    }

    protected override void ExitThreadCore()
    {
        // Ensure cleanup happens
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        _statusTimer = null;
        
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        
        // Stop HTTP server
        try
        {
            if (_webApp != null)
            {
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                _webApp.StopAsync(cts.Token).Wait(cts.Token);
                _webApp.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
                _webApp = null;
            }
        }
        catch
        {
            // Ignore errors during shutdown
        }
        
        // Stop discovery service
        try
        {
            _discoveryService?.StopBroadcasting();
        }
        catch
        {
            // Ignore errors during shutdown
        }
        
        base.ExitThreadCore();
    }
    
    /// <summary>
    /// Get the current MCP endpoint URL
    /// </summary>
    public string GetEndpointUrl()
    {
        var host = _networkVisible 
            ? (_mcpConfig.LocalIp ?? "127.0.0.1") 
            : "127.0.0.1";
        return $"http://{host}:{_mcpConfig.Port}/mcp";
    }
    
    /// <summary>
    /// Check if the server is healthy/running
    /// </summary>
    public bool IsServerHealthy()
    {
        return _webApp != null;
    }
    
    /// <summary>
    /// Show the IDE configuration dialog
    /// </summary>
    private void ShowIDEConfigurationDialog()
    {
        // Determine McpRemote.exe path (same directory as executable)
        var proxyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "McpRemote.exe");
        
        var input = new IDEConfigurationInput
        {
            McpEndpointUrl = GetEndpointUrl(),
            ProxyExecutablePath = proxyPath,
            IsServerRunning = IsServerHealthy()
        };
        
        using var dialog = new IDEConfigurationDialog(input);
        dialog.ShowDialog();
    }
}

public class CircuitInfo
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

