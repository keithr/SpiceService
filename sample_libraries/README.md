# Sample SPICE Library Files

This directory contains sample SPICE library (.lib) files for testing the `library_search` tool.

## Usage

To use these library files with SpiceService:

1. **Configure Library Paths**: Add the path to this directory (or any directory containing .lib files) to `MCPServerConfig.LibraryPaths` in your configuration.

2. **Example Configuration**:
   ```csharp
   var config = new MCPServerConfig
   {
       LibraryPaths = new[] { @"C:\path\to\sample_libraries" }
   };
   ```

3. **Automatic Indexing**: The library service will automatically scan all .lib files in the configured directories on startup and index all model definitions.

4. **Search Models**: Use the `library_search` tool to find models:
   - Search by name: `library_search` with `query="D1N"`
   - Filter by type: `library_search` with `type="diode"`
   - Get all models: `library_search` with `query=""`

## File Format

SPICE library files use the `.MODEL` statement format:
```
.MODEL ModelName ModelType (
+ Parameter1=Value1
+ Parameter2=Value2
+ )
```

- Lines starting with `*` are comments
- Continuation lines start with `+`
- Parameters can use scientific notation (1E-14) or unit suffixes (1.5n, 2.3m, etc.)

## Sample Files

- `sample_components.lib`: Contains sample diode, transistor, and MOSFET models for testing
