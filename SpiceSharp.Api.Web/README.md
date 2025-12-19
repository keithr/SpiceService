# SpiceSharp MCP Server

MCP (Model Context Protocol) server for circuit simulation using SpiceSharp.

## Quick Start

### Run the MCP Server
```bash
dotnet run --project SpiceSharp.Api.Web
```

The MCP server will be available at:
- **MCP Endpoint**: `http://localhost:8081/mcp` (or check console output for actual port)

### Command-Line Options
- `--port=<number>` - Specify the port (default: auto-detect starting from 8081)
- `--discovery-port=<number>` - Specify UDP discovery port (default: 8080)
- `--no-discovery` - Disable UDP discovery broadcasting

## MCP Protocol

This server implements the MCP JSON-RPC 2.0 protocol with the following methods:

- `initialize` - Initialize the MCP connection
- `tools/list` - List available circuit simulation tools
- `tools/call` - Execute a tool with arguments

## Available Tools

The server provides various tools for circuit simulation including:
- Circuit creation and management
- Component addition (resistors, capacitors, diodes, transistors, etc.)
- Model definition (semiconductor models)
- Analysis execution (DC operating point, DC sweep, transient, AC)
- Netlist export
- Schematic rendering (SVG)

See the MCP tools endpoint for the complete list of available tools and their schemas.

## Discovery Service

The server includes a UDP discovery service that broadcasts its availability on the network. This allows MCP clients to automatically discover and connect to the server.
