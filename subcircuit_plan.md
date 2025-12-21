# Subcircuit Support Implementation Plan

## Overview

This plan implements subcircuit support for the SpiceService MCP server, prioritizing library subcircuit support first, then expanding to full user-defined subcircuit support.

**Priority**: Library subcircuits first (Phase 1-3), then user-defined subcircuits (Phase 4-5)

**Approach**: Test-Driven Development (TDD) - Write tests first, then implement

**Quality Gates**: 
- ✅ All tests pass
- ✅ Zero compiler errors
- ✅ Zero compiler warnings
- ✅ All linter errors resolved
- ✅ Update this plan with completion status

---

## Phase 1: Foundation - SubcircuitDefinition Model

**Goal**: Create the data model for subcircuit definitions

### Step 1.1: Create SubcircuitDefinition Model
**File**: `SpiceSharp.Api.Core/Models/SubcircuitDefinition.cs`

**Checklist**:
- [x] Create `SubcircuitDefinition` class
- [x] Properties: `Name` (string), `Nodes` (List<string>), `Definition` (string - internal netlist)
- [x] Add XML documentation comments
- [x] Follow same pattern as `ModelDefinition`

**TDD**:
- [x] Create test file: `SpiceSharp.Api.Core.Tests/Models/SubcircuitDefinitionTests.cs`
- [x] Test: Constructor initializes properties correctly
- [x] Test: Nodes list is initialized (not null)
- [x] Test: Definition string is initialized (not null)

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

---

## Phase 2: Library Parser - Parse .SUBCKT Definitions

**Goal**: Extend `SpiceLibParser` to parse `.SUBCKT` statements from library files

### Step 2.1: Extend SpiceLibParser Interface
**File**: `SpiceSharp.Api.Core/Services/SpiceLibParser.cs`

**Checklist**:
- [x] Add `ParseSubcircuits()` method (returns `List<SubcircuitDefinition>`)
- [x] Add regex pattern for `.SUBCKT` line: `^\s*\.SUBCKT\s+(\w+)\s+(.+)$`
- [x] Handle continuation lines (starting with `+`)
- [x] Parse until `.ENDS` statement
- [x] Extract subcircuit name and external nodes
- [x] Store internal definition (all lines between `.SUBCKT` and `.ENDS`)
- [x] Handle comments (lines starting with `*`)
- [x] Handle inline comments (after `*` in line)

**TDD**:
- [x] Extend `SpiceSharp.Api.Core.Tests/Services/SpiceLibParserTests.cs`
- [x] Test: Parse simple subcircuit (single line definition)
- [x] Test: Parse subcircuit with multiple external nodes
- [x] Test: Parse subcircuit with continuation lines
- [x] Test: Parse subcircuit with internal components
- [x] Test: Parse subcircuit with comments
- [x] Test: Parse multiple subcircuits in one file
- [x] Test: Parse file with both models and subcircuits
- [x] Test: Handle malformed .SUBCKT gracefully
- [x] Test: Handle missing .ENDS gracefully (or until next .SUBCKT/.MODEL)
- [x] Test: Empty file returns empty list

**Example Test Case**:
```csharp
[Fact]
public void ParseSubcircuits_WithSimpleSubcircuit_ParsesCorrectly()
{
    var libContent = @"
.SUBCKT irf1010n 1 2 3
M1 9 7 8 8 MM L=100u W=100u
.MODEL MM NMOS LEVEL=1 VTO=3.74111
.ENDS
";
    var parser = new SpiceLibParser();
    var subcircuits = parser.ParseSubcircuits(libContent);
    
    Assert.Single(subcircuits);
    var sub = subcircuits.First();
    Assert.Equal("irf1010n", sub.Name);
    Assert.Equal(3, sub.Nodes.Count);
    Assert.Contains("1", sub.Nodes);
    Assert.Contains("2", sub.Nodes);
    Assert.Contains("3", sub.Nodes);
    Assert.Contains("M1 9 7 8 8 MM L=100u W=100u", sub.Definition);
}
```

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Test with real library file (e.g., `libraries/kicad_INTRNTL2.LIB`)

---

## Phase 3: Library Service - Index and Search Subcircuits

**Goal**: Extend `LibraryService` to index and search subcircuits alongside models

### Step 3.1: Extend ILibraryService Interface
**File**: `SpiceSharp.Api.Core/Services/ILibraryService.cs`

**Checklist**:
- [x] Add `SearchSubcircuits(string query, int limit)` method
- [x] Add XML documentation

**TDD**:
- [x] Create test file: `SpiceSharp.Api.Core.Tests/Services/LibraryServiceSubcircuitTests.cs`
- [x] Test: SearchSubcircuits returns empty list when no subcircuits indexed
- [x] Test: SearchSubcircuits finds subcircuit by name
- [x] Test: SearchSubcircuits respects limit
- [x] Test: SearchSubcircuits is case-insensitive

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors

### Step 3.2: Extend LibraryService Implementation
**File**: `SpiceSharp.Api.Core/Services/LibraryService.cs`

**Checklist**:
- [x] Add `_subcircuitIndex` (ConcurrentDictionary<string, SubcircuitDefinition>)
- [x] In `IndexLibraries()`, call `_parser.ParseSubcircuits()` for each library file
- [x] Index subcircuits (similar to models)
- [x] Implement `SearchSubcircuits()` method
- [x] Handle duplicate subcircuit names (first wins, like models)

**TDD**:
- [x] Extend `LibraryServiceSubcircuitTests.cs`
- [x] Test: IndexLibraries indexes subcircuits from library files
- [x] Test: IndexLibraries handles multiple library files
- [x] Test: IndexLibraries handles duplicate subcircuit names (first wins)
- [x] Test: SearchSubcircuits finds indexed subcircuits
- [x] Test: SearchSubcircuits with query filters by name
- [x] Test: SearchSubcircuits with limit returns correct count
- [x] Test: IndexLibraries handles files with both models and subcircuits

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Test with real library directory

---

## Phase 4: MCP Tool - library_search Returns Subcircuits

**Goal**: Extend `library_search` MCP tool to return subcircuits in results

### Step 4.1: Update library_search Tool
**File**: `SpiceSharp.Api.Web/Services/MCPService.cs`

**Checklist**:
- [x] In `LibrarySearch()`, call `_libraryService.SearchSubcircuits()`
- [x] Add subcircuits to response JSON
- [x] Include `type: "subcircuit"` field in results
- [ ] Allow filtering by type (include subcircuits in type filter)
- [x] Update tool description to mention subcircuits

**TDD**:
- [x] Extend `SpiceSharp.Api.Web.Tests/Services/LibrarySearchToolTests.cs` (or create if needed)
- [x] Test: library_search returns subcircuits when query matches
- [x] Test: library_search returns both models and subcircuits
- [ ] Test: library_search with type filter works for subcircuits
- [x] Test: library_search response includes correct fields for subcircuits
- [x] Test: library_search handles empty results gracefully

**Example Response Structure**:
```json
{
  "query": "irf1010",
  "count": 2,
  "models": [...],
  "subcircuits": [
    {
      "name": "irf1010n",
      "type": "subcircuit",
      "nodes": ["1", "2", "3"],
      "node_count": 3
    }
  ]
}
```

**Validation**:
- [x] `dotnet build` - zero errors, zero warnings
- [x] `dotnet test` - all tests pass
- [x] `read_lints` - zero linter errors
- [ ] Manual test: Call library_search via MCP and verify subcircuits appear

---

## Phase 5: Component Factory - Create Subcircuit Instances

**Goal**: Add support for creating `SpiceSharp.Components.Subcircuit` instances from library subcircuits

### Step 5.1: Research SpiceSharp.Subcircuit API
**Checklist**:
- [ ] Review SpiceSharp documentation for `Subcircuit` class
- [ ] Understand constructor parameters
- [ ] Understand node mapping (subcircuit nodes → main circuit nodes)
- [ ] Test manually: Create simple subcircuit instance in test circuit

**TDD**:
- [ ] Create exploratory test: `SpiceSharp.Api.Core.Tests/Services/SubcircuitExploratoryTests.cs`
- [ ] Test: Create SpiceSharp.Subcircuit instance manually
- [ ] Test: Verify node mapping works
- [ ] Test: Verify subcircuit can be added to circuit
- [ ] Test: Verify circuit with subcircuit can be simulated

**Validation**:
- [ ] Exploratory tests pass
- [ ] Understand SpiceSharp.Subcircuit API

### Step 5.2: Extend ComponentFactory
**File**: `SpiceSharp.Api.Core/Services/ComponentFactory.cs`

**Checklist**:
- [ ] Add `"subcircuit"` case to `CreateComponent()` switch
- [ ] Add `ILibraryService` dependency (constructor injection)
- [ ] Look up subcircuit definition from library
- [ ] Create `SpiceSharp.Components.Subcircuit` instance
- [ ] Map nodes: `definition.Nodes[i]` → `componentDefinition.Nodes[i]`
- [ ] Handle missing subcircuit definition (throw helpful error)
- [ ] Handle node count mismatch (throw helpful error)

**TDD**:
- [ ] Extend `SpiceSharp.Api.Core.Tests/Services/ComponentFactoryTests.cs`
- [ ] Test: CreateComponent with subcircuit type creates Subcircuit instance
- [ ] Test: CreateComponent maps nodes correctly
- [ ] Test: CreateComponent throws when subcircuit not found in library
- [ ] Test: CreateComponent throws when node count mismatch
- [ ] Test: CreateComponent handles empty nodes list

**Validation**:
- [ ] `dotnet build` - zero errors, zero warnings
- [ ] `dotnet test` - all tests pass
- [ ] `read_lints` - zero linter errors

### Step 5.3: Update ComponentService
**File**: `SpiceSharp.Api.Core/Services/ComponentService.cs`

**Checklist**:
- [ ] Ensure ComponentService can handle subcircuit component type
- [ ] Verify ComponentFactory is called correctly
- [ ] No special handling needed (should work through existing flow)

**TDD**:
- [ ] Extend `SpiceSharp.Api.Core.Tests/Services/ComponentServiceTests.cs`
- [ ] Test: AddComponent with subcircuit type adds to circuit
- [ ] Test: AddComponent with subcircuit creates correct entity

**Validation**:
- [ ] `dotnet build` - zero errors, zero warnings
- [ ] `dotnet test` - all tests pass
- [ ] `read_lints` - zero linter errors

---

## Phase 6: MCP Tool - add_component Supports Subcircuits

**Goal**: Enable `add_component` MCP tool to accept subcircuit component type

### Step 6.1: Update add_component Tool
**File**: `SpiceSharp.Api.Web/Services/MCPService.cs`

**Checklist**:
- [ ] Add `"subcircuit"` to component_type enum in tool description
- [ ] Update tool description to explain subcircuit usage
- [ ] Ensure `AddComponent()` call works (should already work via ComponentService)

**TDD**:
- [ ] Extend `SpiceSharp.Api.Web.Tests/Services/ComponentInfoToolTests.cs` or create new test file
- [ ] Test: add_component with component_type="subcircuit" and model="irf1010n" works
- [ ] Test: add_component with subcircuit validates node count
- [ ] Test: add_component with subcircuit throws helpful error if subcircuit not found

**Example Usage**:
```json
{
  "name": "X1",
  "component_type": "subcircuit",
  "nodes": ["drain", "gate", "source"],
  "model": "irf1010n"
}
```

**Validation**:
- [ ] `dotnet build` - zero errors, zero warnings
- [ ] `dotnet test` - all tests pass
- [ ] `read_lints` - zero linter errors
- [ ] Manual test: Add subcircuit via MCP tool, verify it appears in circuit

---

## Phase 7: Integration Testing - End-to-End Library Subcircuit Support

**Goal**: Verify complete library subcircuit workflow

### Step 7.1: Integration Tests
**Checklist**:
- [ ] Test: Index library with subcircuits → Search finds them → Add to circuit → Simulate
- [ ] Test: Multiple subcircuit instances in same circuit
- [ ] Test: Subcircuit with different node mappings
- [ ] Test: Real library file (e.g., `kicad_INTRNTL2.LIB`)

**TDD**:
- [ ] Create `SpiceSharp.Api.Web.Tests/Services/SubcircuitIntegrationTests.cs`
- [ ] Test: Full workflow: library_search → add_component → run_dc_analysis
- [ ] Test: Multiple instances of same subcircuit
- [ ] Test: Subcircuit with 2, 3, 4+ nodes
- [ ] Test: Error handling (missing subcircuit, wrong node count)

**Validation**:
- [ ] `dotnet build` - zero errors, zero warnings
- [ ] `dotnet test` - all tests pass
- [ ] `read_lints` - zero linter errors
- [ ] Manual end-to-end test via MCP client

---

## Phase 8: Documentation and Cleanup

**Goal**: Document subcircuit support and clean up code

### Step 8.1: Update Documentation
**Checklist**:
- [ ] Update README.md with subcircuit support information
- [ ] Add examples of using library subcircuits
- [ ] Document subcircuit component type in add_component tool docs
- [ ] Update library_search tool docs to mention subcircuits

### Step 8.2: Code Cleanup
**Checklist**:
- [ ] Remove exploratory test files (or mark as integration tests)
- [ ] Review all TODO comments
- [ ] Ensure consistent naming conventions
- [ ] Add XML documentation where missing
- [ ] Review error messages for clarity

**Validation**:
- [ ] `dotnet build` - zero errors, zero warnings
- [ ] `dotnet test` - all tests pass
- [ ] `read_lints` - zero linter errors
- [ ] Documentation is complete and accurate

---

## Phase 9: Future - User-Defined Subcircuits (Out of Scope for Initial Implementation)

**Note**: This phase is for future work, not part of the initial library subcircuit support.

**Future Steps** (not implemented in this plan):
- Parse user-defined subcircuits from netlist import
- Support `.SUBCKT` definitions in user circuits
- Support subcircuit instantiation (X-prefixed lines) in netlist parser
- MCP tool: `define_subcircuit` for programmatic subcircuit creation
- Nested subcircuits support

---

## Progress Tracking

### Phase 1: Foundation
- [x] Step 1.1: SubcircuitDefinition Model

### Phase 2: Library Parser
- [x] Step 2.1: Extend SpiceLibParser

### Phase 3: Library Service
- [x] Step 3.1: Extend ILibraryService Interface
- [x] Step 3.2: Extend LibraryService Implementation

### Phase 4: MCP Tool - library_search
- [x] Step 4.1: Update library_search Tool

### Phase 5: Component Factory
- [ ] Step 5.1: Research SpiceSharp.Subcircuit API
- [ ] Step 5.2: Extend ComponentFactory
- [ ] Step 5.3: Update ComponentService

### Phase 6: MCP Tool - add_component
- [ ] Step 6.1: Update add_component Tool

### Phase 7: Integration Testing
- [ ] Step 7.1: Integration Tests

### Phase 8: Documentation
- [ ] Step 8.1: Update Documentation
- [ ] Step 8.2: Code Cleanup

---

## Notes

- **TDD Approach**: Write tests first, see them fail, then implement to make them pass
- **Quality Gates**: Never move to next step until current step passes all validation
- **Update Plan**: Mark checkboxes as you complete items
- **Error Handling**: Always provide helpful error messages
- **Backward Compatibility**: Ensure existing functionality continues to work

---

## Example Library Subcircuit Format

```
.SUBCKT irf1010n 1 2 3
* External nodes: 1=Drain, 2=Gate, 3=Source
M1 9 7 8 8 MM L=100u W=100u
.MODEL MM NMOS LEVEL=1 VTO=3.74111
RS 8 3 0.00744759
D1 3 1 MD
.MODEL MD D IS=3.76689e-10
.ENDS
```

**Usage in circuit**:
```
X1 drain gate source irf1010n
```

**MCP Tool Usage**:
```json
{
  "name": "X1",
  "component_type": "subcircuit",
  "nodes": ["drain", "gate", "source"],
  "model": "irf1010n"
}
```

