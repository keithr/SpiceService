# Speaker Design Support Implementation Plan

## Overview

This plan implements speaker design support for the SpiceService MCP server, extending the existing subcircuit infrastructure to parse speaker metadata, enable parameter-based search, and provide design tools.

**Priority**: Library parsing first (Phase 1-3), then search tools (Phase 4-5), then design tools (Phase 6-8)

**Approach**: Test-Driven Development (TDD) - Write tests first, then implement

**Quality Gates**: 
- ✅ All tests pass
- ✅ Zero compiler errors
- ✅ Zero compiler warnings
- ✅ All linter errors resolved
- ✅ Update this plan with completion status

---

## Phase 1: Foundation - Extend SubcircuitDefinition for Speakers

**Goal**: Add metadata and T/S parameter support to SubcircuitDefinition

### Step 1.1: Extend SubcircuitDefinition Model
**File**: `SpiceSharp.Api.Core/Models/SubcircuitDefinition.cs`

**Checklist**:
- [x] Add `Metadata` property (Dictionary<string, string>)
- [x] Add `TsParameters` property (Dictionary<string, double>)
- [x] Keep existing properties (Name, Nodes, Definition)
- [x] Add XML documentation comments
- [x] Ensure backward compatibility (optional properties)

**TDD**:
- [x] Extend `SpiceSharp.Api.Core.Tests/Models/SubcircuitDefinitionTests.cs`
- [x] Test: Metadata dictionary is initialized (not null)
- [x] Test: TsParameters dictionary is initialized (not null)
- [x] Test: Can set and retrieve metadata values
- [x] Test: Can set and retrieve T/S parameter values
- [x] Test: Existing properties still work (backward compatibility)

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

---

## Phase 2: Parser - Extract Comment Metadata

**Goal**: Extend `SpiceLibParser` to parse comment metadata before `.SUBCKT` statements

### Step 2.1: Extend ParseSubcircuits to Parse Comment Metadata
**File**: `SpiceSharp.Api.Core/Services/SpiceLibParser.cs`

**Checklist**:
- [x] Collect comment lines before each `.SUBCKT` statement
- [x] Parse `* KEY: VALUE` format from comments
- [x] Extract T/S parameters (FS, QTS, QES, QMS, VAS, RE, LE, BL, XMAX, MMS, CMS, SD)
- [x] Extract metadata (MANUFACTURER, PART_NUMBER, TYPE, DIAMETER, IMPEDANCE, POWER_RMS, SENSITIVITY, PRICE)
- [x] Handle missing values gracefully (empty string or 0.0)
- [x] Store metadata in SubcircuitDefinition.Metadata
- [x] Store T/S parameters in SubcircuitDefinition.TsParameters
- [x] **ENHANCED**: Handle units in metadata values (strips "in", "ohms", "dB", "watts")
- [x] **ENHANCED**: Handle extra whitespace in metadata values
- [x] **ENHANCED**: Case-insensitive field matching

**TDD**:
- [x] Extend `SpiceSharp.Api.Core.Tests/Services/SpiceLibParserTests.cs`
- [x] Test: ParseSubcircuits extracts comment metadata before .SUBCKT
- [x] Test: ParseSubcircuits extracts T/S parameters from comments
- [x] Test: ParseSubcircuits handles missing metadata gracefully
- [x] Test: ParseSubcircuits handles numeric values correctly
- [x] Test: ParseSubcircuits handles zero values correctly
- [x] Test: ParseSubcircuits extracts all T/S parameters (FS, QTS, QES, QMS, VAS, RE, LE, BL, XMAX, MMS, CMS, SD)
- [x] Test: ParseSubcircuits extracts all metadata fields
- [x] Test: ParseSubcircuits handles multiple subcircuits with different metadata

**Example Test Case**:
```csharp
[Fact]
public void ParseSubcircuits_WithCommentMetadata_ExtractsCorrectly()
{
    var libContent = @"
* SPICE Model: 264_1148
* MANUFACTURER: Peerless
* TYPE: woofers
* FS: 42.18
* QTS: 0.35
* QES: 0.38
* VAS: 11.2
* RE: 2.73
* PRICE: 59.98
.SUBCKT 264_1148 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.65mH
.ENDS
";
    var parser = new SpiceLibParser();
    var subcircuits = parser.ParseSubcircuits(libContent);
    
    Assert.Single(subcircuits);
    var sub = subcircuits.First();
    Assert.Equal("264_1148", sub.Name);
    Assert.Equal("Peerless", sub.Metadata["MANUFACTURER"]);
    Assert.Equal("woofers", sub.Metadata["TYPE"]);
    Assert.Equal(42.18, sub.TsParameters["FS"]);
    Assert.Equal(0.35, sub.TsParameters["QTS"]);
    Assert.Equal(59.98, double.Parse(sub.Metadata["PRICE"]));
}
```

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Test with real library file (e.g., `libraries/woofers.lib`)

---

## Phase 3: Library Service - Index Speaker Metadata

**Goal**: Extend `LibraryService` to index speaker metadata alongside subcircuits

### Step 3.1: Verify LibraryService Handles Extended SubcircuitDefinition
**File**: `SpiceSharp.Api.Core/Services/LibraryService.cs`

**Checklist**:
- [x] Verify existing IndexLibraries() works with extended SubcircuitDefinition
- [x] Verify SearchSubcircuits() returns subcircuits with metadata
- [x] No changes needed (should work automatically)

**TDD**:
- [x] Extend `SpiceSharp.Api.Core.Tests/Services/LibraryServiceSubcircuitTests.cs`
- [x] Test: IndexLibraries indexes subcircuits with metadata
- [x] Test: SearchSubcircuits returns subcircuits with T/S parameters
- [x] Test: SearchSubcircuits returns subcircuits with metadata

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

---

## Phase 4: Database - SQLite for Parameter Queries

**Goal**: Add SQLite database for fast parameter-based queries

### Step 4.1: Create SpeakerDatabaseService
**File**: `SpiceSharp.Api.Core/Services/SpeakerDatabaseService.cs`

**Checklist**:
- [x] Create `ISpeakerDatabaseService` interface
- [x] Create `SpeakerDatabaseService` implementation
- [x] Add SQLite dependency (Microsoft.Data.Sqlite)
- [x] Create database schema (speakers table with all T/S parameters and metadata)
- [x] Add indexes for common query fields (type, diameter, fs, qts, vas, price)
- [x] Implement `InitializeDatabase()` method
- [x] Implement `PopulateFromSubcircuits()` method
- [x] Handle database file path configuration
- [x] **ENHANCED**: Filter to only include speakers (requires TYPE metadata: woofers, tweeters, midrange, etc.)
- [x] **ENHANCED**: Case-insensitive metadata and T/S parameter lookups

**TDD**:
- [x] Create test file: `SpiceSharp.Api.Core.Tests/Services/SpeakerDatabaseServiceTests.cs`
- [x] Test: InitializeDatabase creates schema correctly
- [x] Test: PopulateFromSubcircuits inserts speaker data
- [x] Test: PopulateFromSubcircuits handles missing T/S parameters
- [x] Test: PopulateFromSubcircuits handles duplicate names (update vs insert)
- [x] Test: Database indexes are created
- [x] **ADDED**: Validation tests for metadata parsing (diameter, impedance, sensitivity, SD)
- [x] **ADDED**: Tests for filtering non-speaker components (tubes, diodes)
- [x] **ADDED**: Tests for case-insensitive parameter lookups

**Database Schema**:
```sql
CREATE TABLE speakers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    subcircuit_name TEXT UNIQUE NOT NULL,
    manufacturer TEXT,
    part_number TEXT,
    type TEXT,
    diameter REAL,
    impedance INTEGER,
    power_rms INTEGER,
    sensitivity REAL,
    price REAL,
    fs REAL,
    qts REAL,
    qes REAL,
    qms REAL,
    vas REAL,
    re REAL,
    le REAL,
    bl REAL,
    xmax REAL,
    mms REAL,
    cms REAL,
    sd REAL,
    source_file TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_type ON speakers(type);
CREATE INDEX idx_diameter ON speakers(diameter);
CREATE INDEX idx_impedance ON speakers(impedance);
CREATE INDEX idx_fs ON speakers(fs);
CREATE INDEX idx_qts ON speakers(qts);
CREATE INDEX idx_vas ON speakers(vas);
CREATE INDEX idx_price ON speakers(price);
```

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

### Step 4.2: Integrate Database with LibraryService
**File**: `SpiceSharp.Api.Core/Services/LibraryService.cs`

**Checklist**:
- [x] Add `ISpeakerDatabaseService` dependency (constructor injection)
- [x] In `IndexLibraries()`, populate database after indexing subcircuits
- [x] Handle database initialization errors gracefully

**TDD**:
- [x] Extend `SpiceSharp.Api.Core.Tests/Services/LibraryServiceSubcircuitTests.cs`
- [x] Test: IndexLibraries populates database
- [x] Test: Database contains all indexed speakers

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

---

## Phase 5: MCP Tool - search_speakers_by_parameters

**Goal**: Add parameter-based speaker search tool

### Step 5.1: Implement SearchSpeakersByParameters
**File**: `SpiceSharp.Api.Core/Services/ISpeakerDatabaseService.cs` and `SpeakerDatabaseService.cs`

**Checklist**:
- [x] Add `SearchSpeakersByParameters()` method to interface
- [x] Implement SQL query builder for parameter ranges
- [x] Support filtering by: driver_type, diameter_min/max, impedance, fs_min/max, qts_min/max, qes_min/max, qms_min/max, vas_min/max, sensitivity_min/max, power_min/max, xmax_min/max, manufacturer, price_max
- [x] Support sorting by: sensitivity, price, fs, qts, vas
- [x] Support limit parameter
- [x] Return List<SpeakerSearchResult> with all fields

**TDD**:
- [x] Extend `SpiceSharp.Api.Core.Tests/Services/SpeakerDatabaseServiceTests.cs`
- [x] Test: SearchSpeakersByParameters filters by driver_type
- [x] Test: SearchSpeakersByParameters filters by diameter range
- [x] Test: SearchSpeakersByParameters filters by fs range
- [x] Test: SearchSpeakersByParameters filters by qts range
- [x] Test: SearchSpeakersByParameters filters by multiple parameters
- [x] Test: SearchSpeakersByParameters respects limit
- [x] Test: SearchSpeakersByParameters sorts correctly
- [x] Test: SearchSpeakersByParameters handles empty results

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

### Step 5.2: Add MCP Tool
**File**: `SpiceSharp.Api.Web/Services/MCPService.cs`

**Checklist**:
- [x] Add `search_speakers_by_parameters` tool definition
- [x] Implement `SearchSpeakersByParameters()` handler method
- [x] Parse all filter parameters from JSON
- [x] Call `_speakerDatabaseService.SearchSpeakersByParameters()`
- [x] Format response JSON with results
- [x] Handle errors gracefully
- [x] **FIXED**: library_search tool now returns metadata and tsParameters in subcircuit results

**TDD**:
- [ ] Create test file: `SpiceSharp.Api.Web.Tests/Services/SpeakerSearchToolTests.cs`
- [ ] Test: search_speakers_by_parameters returns matching speakers
- [ ] Test: search_speakers_by_parameters filters by parameters
- [ ] Test: search_speakers_by_parameters respects limit
- [ ] Test: search_speakers_by_parameters handles empty results
- [ ] Test: search_speakers_by_parameters validates input parameters

**Example Response Structure**:
```json
{
  "count": 15,
  "results": [
    {
      "subcircuit_name": "264_1148",
      "manufacturer": "Peerless",
      "type": "woofers",
      "diameter": 0.0,
      "impedance": 8,
      "ts_parameters": {
        "fs": 42.18,
        "qts": 0.35,
        "qes": 0.38,
        "qms": 4.92,
        "vas": 11.2,
        "re": 2.73,
        "le": 0.65,
        "bl": 8.27,
        "xmax": 8.2,
        "mms": 35.3,
        "cms": 0.4667,
        "sd": 2.0
      },
      "sensitivity": 1.0,
      "power_rms": 75,
      "price": 59.98
    }
  ]
}
```

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Manual test: Call search_speakers_by_parameters via MCP

---

## Phase 6: Design Tools - Enclosure Calculator

**Goal**: Add enclosure design calculation tool

### Step 6.1: Create EnclosureDesignService
**File**: `SpiceSharp.Api.Core/Services/EnclosureDesignService.cs`

**Checklist**:
- [x] Create `IEnclosureDesignService` interface
- [x] Create `EnclosureDesignService` implementation
- [x] Implement sealed box calculations (Butterworth, Bessel, Critically Damped)
- [x] Implement vented box calculations (QB3, B4, SBB4, C4)
- [x] Calculate port dimensions for vented boxes
- [x] Calculate predicted F3, Qtc, Fb
- [x] Handle unit conversions (liters, cubic feet, inches, cm)

**TDD**:
- [x] Create test file: `SpiceSharp.Api.Core.Tests/Services/EnclosureDesignServiceTests.cs`
- [x] Test: CalculateSealedBox returns correct volume for Butterworth alignment
- [x] Test: CalculateSealedBox returns correct Qtc
- [x] Test: CalculateSealedBox returns correct F3
- [x] Test: CalculateVentedBox returns correct volume for QB3
- [x] Test: CalculateVentedBox returns correct tuning frequency
- [x] Test: CalculateVentedBox returns correct port dimensions
- [x] Test: CalculateVentedBox handles port velocity warnings

**Formulas**:
- Sealed: Vb = Vas / (α² - 1) where α = Qtc / Qts
- Vented QB3: Vb = 15 × Qts³ × Vas, Fb = Fs / (Qts × 1.4)

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

### Step 6.2: Add MCP Tool - calculate_enclosure_design
**File**: `SpiceSharp.Api.Web/Services/MCPService.cs`

**Checklist**:
- [x] Add `calculate_enclosure_design` tool definition
- [x] Implement `CalculateEnclosureDesign()` handler method
- [x] Look up speaker by model name
- [x] Call `_enclosureDesignService.CalculateSealedBox()` or `CalculateVentedBox()`
- [x] Format response JSON

**TDD**:
- [ ] Extend `SpiceSharp.Api.Web.Tests/Services/SpeakerSearchToolTests.cs` or create new file
- [ ] Test: calculate_enclosure_design returns design for sealed box
- [ ] Test: calculate_enclosure_design returns design for vented box
- [ ] Test: calculate_enclosure_design handles missing speaker gracefully

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

---

## Phase 7: Design Tools - Crossover Compatibility

**Goal**: Add crossover compatibility checking tool

### Step 7.1: Create CrossoverCompatibilityService
**File**: `SpiceSharp.Api.Core/Services/CrossoverCompatibilityService.cs`

**Checklist**:
- [x] Create `ICrossoverCompatibilityService` interface
- [x] Create `CrossoverCompatibilityService` implementation
- [x] Implement woofer beaming check: `crossover_freq < 13750 / diameter_inches`
- [x] Implement tweeter Fs check: `tweeter_fs < 0.5 × crossover_freq`
- [x] Implement sensitivity match check (within 3dB ideal)
- [x] Implement impedance match check
- [x] Calculate compatibility score
- [x] Generate recommendations for adjustments

**TDD**:
- [x] Create test file: `SpiceSharp.Api.Core.Tests/Services/CrossoverCompatibilityServiceTests.cs`
- [x] Test: CheckCompatibility validates woofer beaming
- [x] Test: CheckCompatibility validates tweeter Fs
- [x] Test: CheckCompatibility validates sensitivity match
- [x] Test: CheckCompatibility validates impedance match
- [x] Test: CheckCompatibility calculates compatibility score
- [x] Test: CheckCompatibility generates recommendations

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

### Step 7.2: Add MCP Tool - check_crossover_compatibility
**File**: `SpiceSharp.Api.Web/Services/MCPService.cs`

**Checklist**:
- [x] Add `check_crossover_compatibility` tool definition
- [x] Implement `CheckCrossoverCompatibility()` handler method
- [x] Look up woofer and tweeter by model name
- [x] Call `_crossoverCompatibilityService.CheckCompatibility()`
- [x] Format response JSON

**TDD**:
- [ ] Extend speaker tool tests
- [ ] Test: check_crossover_compatibility returns compatibility results
- [ ] Test: check_crossover_compatibility handles missing speakers

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

---

## Phase 8: Integration Testing - End-to-End Speaker Support

**Goal**: Verify complete speaker workflow

### Step 8.1: Integration Tests
**Checklist**:
- [x] Test: Parse library → Index → Search by parameters → Calculate box → Check compatibility
- [x] Test: Real library file (woofers.lib) parses correctly
- [x] Test: Database queries return correct results
- [x] Test: Design calculations match expected values
- [x] Test: Multiple speakers in same workflow

**TDD**:
- [x] Create `SpiceSharp.Api.Web.Tests/Services/SpeakerIntegrationTests.cs`
- [x] Test: Full workflow: library_search → search_speakers_by_parameters → calculate_enclosure_design
- [x] Test: Full workflow with crossover compatibility check
- [x] Test: Real library file parsing and indexing

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Manual end-to-end test via MCP client

---

## Phase 9: Documentation and Cleanup ✅ COMPLETE

**Goal**: Document speaker support and clean up code

### Step 9.1: Update Documentation
**Checklist**:
- [x] Update README.md with speaker support information
- [x] Add examples of using speaker search tools
- [x] Document search_speakers_by_parameters tool
- [x] Document calculate_enclosure_design tool
- [x] Document check_crossover_compatibility tool

### Step 9.2: Code Cleanup
**Checklist**:
- [x] Review all TODO comments (one remaining in CircuitValidator.cs - unrelated to speaker support)
- [x] Ensure consistent naming conventions
- [x] Add XML documentation where missing (all speaker services have XML docs)
- [x] Review error messages for clarity

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Documentation is complete and accurate

---

## Progress Tracking

### Phase 1: Foundation
- [x] Step 1.1: Extend SubcircuitDefinition Model

### Phase 2: Parser
- [x] Step 2.1: Extend ParseSubcircuits to Parse Comment Metadata

### Phase 3: Library Service
- [x] Step 3.1: Verify LibraryService Handles Extended SubcircuitDefinition

### Phase 4: Database
- [x] Step 4.1: Create SpeakerDatabaseService
- [x] Step 4.2: Integrate Database with LibraryService
- [x] **FIXED**: Database filtering now only includes speakers (requires TYPE metadata)
- [x] **FIXED**: Case-insensitive metadata and T/S parameter lookups

### Phase 5: MCP Tool - search_speakers_by_parameters
- [x] Step 5.1: Implement SearchSpeakersByParameters
- [x] Step 5.2: Add MCP Tool
- [x] **FIXED**: library_search now returns metadata and tsParameters in subcircuit results
- [x] **FIXED**: Enhanced metadata parsing to handle units (in, ohms, dB, watts) and whitespace

### Phase 6: Design Tools - Enclosure Calculator
- [x] Step 6.1: Create EnclosureDesignService
- [x] Step 6.2: Add MCP Tool - calculate_enclosure_design

### Phase 7: Design Tools - Crossover Compatibility
- [x] Step 7.1: Create CrossoverCompatibilityService
- [x] Step 7.2: Add MCP Tool - check_crossover_compatibility

### Phase 8: Integration Testing
- [x] Step 8.1: Integration Tests

### Phase 9: Documentation
- [ ] Step 9.1: Update Documentation
- [ ] Step 9.2: Code Cleanup

---

## Notes

- **TDD Approach**: Write tests first, see them fail, then implement to make them pass
- **Quality Gates**: Never move to next step until current step passes all validation
- **Update Plan**: Mark checkboxes as you complete items
- **Error Handling**: Always provide helpful error messages
- **Backward Compatibility**: Ensure existing functionality continues to work
- **Database Location**: Use configurable path (default: `speakers.db` in app data directory)

---

## Example Library File Format

```
* SPICE Model: 264_1148
* MANUFACTURER: Peerless
* PART_NUMBER: 
* TYPE: woofers
* DIAMETER: 0.0
* IMPEDANCE: 8.0
* POWER_RMS: 75.0
* POWER_MAX: 0.0
* SENSITIVITY: 1.0
* FS: 42.18
* QTS: 0.35
* QES: 0.38
* QMS: 4.92
* VAS: 11.2
* RE: 2.73
* LE: 0.65
* BL: 8.27
* XMAX: 8.2
* MMS: 35.3
* CMS: 0.4667
* SD: 2.0
* PRICE: 59.98
*
.SUBCKT 264_1148 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.65mH
Rms 2 3 0.210854
Lms 3 4 0.027108H
Cms 4 MINUS 0.000525F
.ENDS 264_1148
```

**MCP Tool Usage Examples**:

**Search by parameters**:
```json
{
  "driver_type": ["woofer"],
  "diameter_min": 6.0,
  "diameter_max": 8.0,
  "qts_min": 0.4,
  "qts_max": 0.7,
  "limit": 20
}
```

**Calculate enclosure**:
```json
{
  "model": "264_1148",
  "enclosure_type": "vented",
  "alignment": "QB3"
}
```

**Check crossover compatibility**:
```json
{
  "woofer_model": "264_1148",
  "tweeter_model": "Dayton_ND25FA-4",
  "crossover_frequency": 2500,
  "crossover_order": 2
}
```

