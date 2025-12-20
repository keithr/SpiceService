# McpRemote

A lightweight stdio ↔ HTTP proxy that enables IDEs using the stdio-based MCP (Model Context Protocol) to connect to SpiceService's HTTP-based MCP server.

## Overview

`McpRemote.exe` is a console application that acts as a bridge between:
- **IDEs** (Claude Desktop, Cursor AI, Windsurf, VS Code) that communicate via **stdio** (standard input/output)
- **SpiceService** MCP server that communicates via **HTTP**

This proxy eliminates the need for Node.js or other external dependencies, making SpiceService integration seamless for enterprise deployments.

## Why McpRemote?

Many AI-powered IDEs use the MCP protocol over stdio (standard input/output) for communication. However, SpiceService's MCP server uses HTTP for better scalability and integration with web-based architectures. `McpRemote` bridges this gap by:

1. **Reading JSON-RPC messages** from stdin (from the IDE)
2. **Converting them to HTTP POST requests** to the SpiceService MCP server
3. **Reading HTTP responses** from the server
4. **Writing JSON-RPC responses** to stdout (back to the IDE)

This allows IDEs to connect to SpiceService without requiring any changes to their MCP implementation.

## Features

- ✅ **Bidirectional Proxy**: Handles both requests and responses
- ✅ **JSON-RPC 2.0 Compliant**: Properly handles requests, responses, and notifications
- ✅ **Auto-Discovery**: Automatically finds the SpiceService endpoint
- ✅ **Error Handling**: Robust error handling with proper JSON-RPC error responses
- ✅ **No Dependencies**: Single-file executable, no Node.js or external dependencies
- ✅ **Notification Support**: Correctly handles JSON-RPC notifications (fire-and-forget)

## Usage

### Auto-Discovery Mode (Recommended)

```bash
McpRemote.exe auto
# or
McpRemote.exe --discover
```

The proxy will automatically discover the SpiceService MCP endpoint by querying common ports (8081-8090) for the `/discovery` endpoint.

### Explicit URL Mode

```bash
McpRemote.exe http://localhost:8081/mcp
```

Provide the full HTTP endpoint URL of the SpiceService MCP server.

## How It Works

### Architecture

```
IDE (stdio)  ←→  McpRemote.exe  ←→  SpiceService (HTTP)
```

1. **IDE sends JSON-RPC message** → McpRemote reads from stdin
2. **McpRemote converts to HTTP POST** → Sends to SpiceService `/mcp` endpoint
3. **SpiceService processes request** → Returns JSON-RPC response via HTTP
4. **McpRemote reads HTTP response** → Writes JSON-RPC response to stdout
5. **IDE receives response** → Reads from stdout

### JSON-RPC Protocol Handling

- **Requests** (with `id`): Waits for HTTP response and forwards to stdout
- **Notifications** (without `id`): Performs fire-and-forget HTTP request, no stdout output
- **Errors**: Converts HTTP errors to JSON-RPC error responses with proper error codes

### Auto-Discovery Mechanism

When using `auto` or `--discover` mode:

1. Queries `http://localhost:{port}/discovery` for ports 8081-8090
2. Checks if the response contains a valid MCP endpoint URL
3. Uses the first discovered endpoint
4. Falls back to error if no endpoint is found

## Integration with IDEs

`McpRemote.exe` is automatically configured by the SpiceService tray application's "Configure IDE Integration" dialog. The dialog:

1. Detects installed IDEs (Claude Desktop, Cursor AI, Windsurf, VS Code)
2. Configures their MCP settings to use `McpRemote.exe auto`
3. Ensures `McpRemote.exe` is colocated with `SpiceServiceTray.exe`

### Example IDE Configuration

**Claude Desktop** (`%APPDATA%\Claude\claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe",
      "args": ["auto"]
    }
  }
}
```

**Cursor AI** (`%USERPROFILE%\.cursor\mcp.json`):
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe",
      "args": ["auto"]
    }
  }
}
```

## Building

```bash
dotnet publish McpRemote/McpRemote.csproj --configuration Release --runtime win-x64 --self-contained false -p:PublishSingleFile=true
```

The executable will be output to:
```
McpRemote/bin/Release/net8.0/win-x64/publish/McpRemote.exe
```

## Requirements

- **.NET 8.0 Runtime** (included with Windows 10/11 or can be installed separately)
- **SpiceService** must be running and accessible via HTTP

## Technical Details

### Error Handling

- **HTTP Errors**: Converted to JSON-RPC error responses with code -32000
- **Network Errors**: Converted to JSON-RPC error responses with code -32603
- **Timeouts**: Converted to JSON-RPC error responses with code -32603
- **Invalid JSON**: Server handles gracefully, proxy forwards errors

### Notification Handling

JSON-RPC notifications (messages without an `id` field) are handled specially:
- Performs HTTP POST request in background (fire-and-forget)
- Does not wait for response
- Does not write anything to stdout
- Prevents IDEs from receiving unexpected EOF

### Auto-Discovery Endpoint

The `/discovery` endpoint returns:
```json
{
  "endpoint": "http://localhost:8081/mcp",
  "port": 8081,
  "host": "localhost",
  "networkVisible": false
}
```

## Troubleshooting

### "Could not discover SpiceService MCP endpoint"

- Ensure SpiceService tray application is running
- Check that the server is listening on ports 8081-8090
- Try using explicit URL mode: `McpRemote.exe http://localhost:8081/mcp`

### "Unexpected end of JSON input"

- This usually indicates a notification handling issue
- Ensure you're using the latest version of `McpRemote.exe`
- Check that the SpiceService server correctly handles notifications

### Connection Timeouts

- Verify SpiceService is running and accessible
- Check firewall settings
- Ensure the endpoint URL is correct

## License

Part of the SpiceService project. See the main project LICENSE file for details.

