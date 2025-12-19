# SpiceService Component Libraries

This directory contains SPICE component library (.lib) files that are included with the SpiceService installation.

## Included Libraries

SpiceService includes over 500 SPICE component models from multiple sources:

- **KiCad Spice Library**: Comprehensive collection of 545 component library files with thousands of models including:
  - Digital logic ICs (74xx, 54xx series)
  - Transistors (BJT, MOSFET, JFET)
  - Diodes and Zener diodes
  - Operational amplifiers
  - Manufacturer-specific models (Texas Instruments, Infineon, Maxim, etc.)
  - Passive components
  - And many more component types

- **sample_components.lib**: Sample diode, transistor, and MOSFET models for testing and demonstration

## License Information

The KiCad Spice Library models are licensed under the **GNU General Public License version 3 (GPL-3.0)**.

For full license terms, see:
- **KICAD_LICENSE.txt** in this directory
- Original repository: https://github.com/KiCad/KiCad-Spice-Library

**Important**: When distributing SpiceService, you must comply with the GPL-3.0 license requirements for the KiCad Spice Library models. This includes:
- Providing access to the source code
- Including the GPL-3.0 license text
- Preserving copyright notices

## Adding Your Own Libraries

You can add your own library files in one of these locations (checked in order):

1. **User Libraries Directory** (Recommended):
   - `%USERPROFILE%\Documents\SpiceService\libraries`
   - Create this directory and place your .lib files here
   - These libraries take priority over installed libraries

2. **Configuration File**:
   - Edit `%APPDATA%\SpiceService\config.json` (if it exists)
   - Add `libraryPaths` array with your custom library directories

3. **Next to Executable**:
   - Place .lib files in `libraries` subdirectory next to SpiceServiceTray.exe
   - Location: `%LOCALAPPDATA%\SpiceService\Tray\libraries`

## Library File Format

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

## Library Priority

When multiple libraries contain models with the same name, the first one found is used. Search order:
1. User libraries directory (`Documents\SpiceService\libraries`)
2. Config file specified paths
3. Executable-relative libraries directory
4. Installed libraries (this directory)

This allows you to override installed models with your own versions.
