# SpiceService Library Setup Guide

## Overview

SpiceService supports multiple locations for SPICE component library (.lib) files, with a priority system that allows users to override installed libraries with their own.

## Library Search Priority

Libraries are searched in the following order (first match wins for duplicate model names):

1. **User Libraries Directory** (Highest Priority)
   - Location: `%USERPROFILE%\Documents\SpiceService\libraries`
   - Purpose: User's custom libraries that override installed libraries
   - Status: Auto-created if it doesn't exist (on first library search)
   - Example: `C:\Users\YourName\Documents\SpiceService\libraries\my_models.lib`

2. **Configuration File Paths** (Future)
   - Location: `%APPDATA%\SpiceService\config.json`
   - Purpose: User-configurable library paths
   - Status: Not yet implemented (can be added if needed)

3. **Installed Libraries** (Included in MSI)
   - Location: `%LOCALAPPDATA%\SpiceService\Tray\libraries`
   - Purpose: Libraries included with the SpiceService installation
   - Status: Automatically included when MSI is installed
   - Example: `C:\Users\YourName\AppData\Local\SpiceService\Tray\libraries\sample_components.lib`

4. **Sample Libraries** (Development Only)
   - Location: `sample_libraries` directory relative to source/build
   - Purpose: For development and testing
   - Status: Only available in development builds

## Adding Your Own Libraries

### Option 1: User Libraries Directory (Recommended)

1. Create the directory: `%USERPROFILE%\Documents\SpiceService\libraries`
2. Place your `.lib` files in that directory
3. Restart SpiceService Tray application
4. Your libraries will be indexed and available for search

**Advantages:**
- Survives application updates
- Easy to find and manage
- Takes priority over installed libraries
- Standard Windows location

### Option 2: Next to Executable

1. Navigate to: `%LOCALAPPDATA%\SpiceService\Tray`
2. Create a `libraries` subdirectory if it doesn't exist
3. Place your `.lib` files there
4. Restart SpiceService Tray application

**Note:** This location may be overwritten during updates.

## Library File Format

SPICE library files use the `.MODEL` statement format:

```
* Comments start with *
.MODEL ModelName ModelType (
+ Parameter1=Value1    * Inline comments supported
+ Parameter2=Value2
+ )
```

**Supported Model Types:**
- `D` → `diode`
- `NPN` → `bjt_npn`
- `PNP` → `bjt_pnp`
- `NMOS` → `mosfet_n`
- `PMOS` → `mosfet_p`
- `NJF` / `JFETN` → `jfet_n`
- `PJF` / `JFETP` → `jfet_p`

**Parameter Value Formats:**
- Scientific notation: `1E-14`, `2.5E-3`
- Unit suffixes: `1.5n` (nano), `2.3m` (milli), `100k` (kilo)
- Regular numbers: `1.5`, `100`, `0.5`

## Example Library File

See `sample_components.lib` for a complete example with:
- Diode models (D1N4001, D1N4002, D1N4148)
- Transistor models (Q2N3904 NPN, Q2N3906 PNP)
- MOSFET models (M2N7000, M2N7002)

## Verifying Library Setup

1. Start SpiceService Tray application
2. Check the log for library path messages:
   - Should show: "Library paths configured: [paths]"
3. Use `library_search` tool:
   - Empty query: `library_search` with `query=""` should return models
   - If no libraries: Returns helpful error message with setup instructions

## Troubleshooting

**Problem:** `library_search` returns "Library service is not configured"

**Solution:**
1. Ensure at least one library directory exists with `.lib` files
2. Check that `.lib` files are valid SPICE format
3. Restart the tray application after adding libraries
4. Check the tray application log for library indexing messages

**Problem:** My custom library models aren't showing up

**Solution:**
1. Verify your library is in `Documents\SpiceService\libraries` (highest priority)
2. Check that the `.lib` file format is correct
3. Ensure model names don't conflict (first match wins)
4. Restart the tray application

**Problem:** Installed libraries missing after update

**Solution:**
- Installed libraries are in `%LOCALAPPDATA%\SpiceService\Tray\libraries`
- These are reinstalled with each MSI update
- Your user libraries in `Documents\SpiceService\libraries` are preserved
