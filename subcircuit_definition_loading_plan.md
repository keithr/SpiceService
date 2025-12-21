# Subcircuit Definition Loading - Bug Resolution Plan

## Problem Summary

**Status**: ✅ Subcircuit instances are created successfully  
**Status**: ✅ Subcircuit instances appear in exported netlists  
**Status**: ❌ AC analysis fails with "2 rule violations"  
**Status**: ❌ Subcircuit internal definitions may not be properly accessible during simulation

## Root Cause Analysis

### Current Flow
1. `ComponentService.AddSubcircuitComponent()` is called
2. It checks if subcircuit definition exists in circuit (via `SubcircuitDefinitionEntity` wrapper)
3. If not found, it loads from `LibraryService.GetSubcircuitByName()`
4. It creates `SpiceSharp.Components.SubcircuitDefinition` via `CreateSpiceSharpSubcircuitDefinition()`
5. It wraps it in `SubcircuitDefinitionEntity` and adds to circuit
6. It creates the `Subcircuit` instance

### The Issue
The subcircuit definition's **internal circuit** (created in `CreateSpiceSharpSubcircuitDefinition`) may have:
- Floating nodes (nodes not connected to any component)
- Missing DC paths to ground (required for AC analysis)
- Inductor loops without resistance
- Other SpiceSharp validation rule violations

OR the subcircuit definition isn't being properly registered/accessible during SpiceSharp's internal validation.

## Investigation Steps

### Step 1: Verify Subcircuit Definition Registration
**File**: `SpiceSharp.Api.Core/Services/ComponentService.cs`

**Action**: Add logging/debugging to verify:
- Subcircuit definition is added to circuit before instance creation
- Subcircuit definition can be retrieved by name during validation
- The `SubcircuitDefinitionEntity` wrapper is working correctly

**Code Location**: Lines 405-407
```csharp
var definitionWrapper = new SubcircuitDefinitionEntity(subcircuitName, subcircuitDef);
circuit.InternalCircuit.Add(definitionWrapper);
existingDefinition = subcircuitDef;
```

### Step 2: Validate Subcircuit Internal Circuit
**File**: `SpiceSharp.Api.Core/Services/ComponentService.cs`

**Action**: Add validation of the subcircuit's internal circuit before creating the definition

**Code Location**: `CreateSpiceSharpSubcircuitDefinition()` method (lines 472-521)

**Check**:
- Does the internal circuit have a ground node (node "0")?
- Are all nodes connected to at least 2 components?
- Do inductors have series resistance?
- Are there DC paths from all nodes to ground?

### Step 3: Test with Known Good Subcircuit
**Action**: Create a test with a minimal, known-good subcircuit definition:
```spice
.SUBCKT TEST_SUB PLUS MINUS
R1 PLUS MINUS 1000
.ENDS
```

This should work without validation errors. If it fails, the issue is in definition registration, not internal circuit validation.

### Step 4: Check SpiceSharp SubcircuitDefinition Requirements
**Action**: Verify how SpiceSharp expects subcircuit definitions to be registered:
- Does `SubcircuitDefinition` need to be added directly to circuit, not wrapped?
- Does the name need to be set differently?
- Are there specific requirements for the internal circuit?

## Fix Strategy

### Option A: Fix Internal Circuit Validation Issues
If the issue is in the subcircuit's internal circuit:

1. **Add DC Path Resistors**: Add large resistors (1e9-1e12 ohms) from floating nodes to ground
2. **Add Series Resistors to Inductors**: Add small resistors (1e-6 ohms) in series with inductors
3. **Ensure Ground Node**: Verify all subcircuit definitions have a ground node (node "0")

**Implementation**: Modify `CreateSpiceSharpSubcircuitDefinition()` to:
- Parse the subcircuit definition
- Validate the internal circuit
- Add missing DC paths and series resistors automatically
- Log warnings for any modifications made

### Option B: Fix Definition Registration
If the issue is in how definitions are registered:

1. **Direct Registration**: Try adding `SubcircuitDefinition` directly to circuit instead of wrapping
2. **Name Setting**: Ensure the definition's name is set correctly (may require reflection)
3. **Registration Order**: Ensure definitions are registered before instances

**Implementation**: Modify `AddSubcircuitComponent()` to:
- Register definition directly if SpiceSharp supports it
- Or ensure `SubcircuitDefinitionEntity` properly implements all required interfaces

### Option C: Pre-validate Before Simulation
If definitions are registered correctly but validation fails during simulation:

1. **Pre-validation**: Validate subcircuit definitions when they're loaded
2. **Auto-fix**: Automatically add DC paths and series resistors to fix validation issues
3. **Error Reporting**: Provide clear error messages about what's missing

## Implementation Plan

### Phase 1: Diagnosis (Current)
- [x] Understand current code flow
- [ ] Add diagnostic logging to `AddSubcircuitComponent()`
- [ ] Add diagnostic logging to `CreateSpiceSharpSubcircuitDefinition()`
- [ ] Create unit test that reproduces the AC analysis failure
- [ ] Verify subcircuit definition is accessible during validation

### Phase 2: Fix Internal Circuit Issues
- [ ] Add validation for subcircuit internal circuits
- [ ] Auto-add DC path resistors for floating nodes
- [ ] Auto-add series resistors for inductors
- [ ] Ensure ground node exists in all subcircuits

### Phase 3: Fix Definition Registration (if needed)
- [ ] Verify `SubcircuitDefinitionEntity` implements all required interfaces
- [ ] Test direct registration of `SubcircuitDefinition` (if supported)
- [ ] Ensure definition name is set correctly

### Phase 4: Testing
- [ ] Unit test: Simple subcircuit with AC analysis
- [ ] Unit test: Complex subcircuit (speaker model) with AC analysis
- [ ] Integration test: Overnight Sensation crossover with AC analysis
- [ ] Verify all existing tests still pass

## Code Changes Required

### File 1: `SpiceSharp.Api.Core/Services/ComponentService.cs`

**Location**: `CreateSpiceSharpSubcircuitDefinition()` method

**Changes**:
1. After creating `subcircuitCircuit`, validate it
2. Add DC path resistors if needed
3. Add series resistors to inductors if needed
4. Log any modifications made

### File 2: `SpiceSharp.Api.Core/Services/ComponentService.cs`

**Location**: `AddSubcircuitComponent()` method

**Changes**:
1. Add logging when definition is loaded from library
2. Add logging when definition is registered in circuit
3. Verify definition is accessible after registration

### File 3: New helper class (optional)

**File**: `SpiceSharp.Api.Core/Services/SubcircuitCircuitValidator.cs`

**Purpose**: Validate and fix subcircuit internal circuits

**Methods**:
- `ValidateAndFix(Circuit subcircuitCircuit, IReadOnlyList<string> pins)`
- `AddDCPaths(Circuit circuit)`
- `AddSeriesResistorsToInductors(Circuit circuit)`

## Test Cases

### Test 1: Simple Subcircuit AC Analysis
```csharp
[Fact]
public async Task SubcircuitDefinition_SimpleACAnalysis_ShouldWork()
{
    // Create circuit with simple subcircuit
    // Run AC analysis
    // Verify no validation errors
}
```

### Test 2: Speaker Subcircuit AC Analysis
```csharp
[Fact]
public async Task SubcircuitDefinition_SpeakerACAnalysis_ShouldWork()
{
    // Create circuit with speaker subcircuit (275_030)
    // Run AC analysis
    // Verify no validation errors
    // Verify frequency response is reasonable
}
```

### Test 3: Overnight Sensation Full Workflow
```csharp
[Fact]
public async Task SubcircuitDefinition_OvernightSensationACAnalysis_ShouldWork()
{
    // Create Overnight Sensation crossover
    // Add both tweeter and woofer subcircuits
    // Run AC analysis
    // Verify no validation errors
    // Verify frequency response shows crossover behavior
}
```

## Success Criteria

1. ✅ Subcircuit instances can be added via `add_component`
2. ✅ Subcircuit instances appear in exported netlists
3. ✅ AC analysis runs successfully with subcircuits
4. ✅ No validation errors during simulation
5. ✅ Frequency response shows expected behavior (for speaker models)

## Next Steps

1. **Create diagnostic test** to reproduce the exact failure
2. **Add logging** to understand what's happening during definition loading
3. **Validate subcircuit internal circuits** before creating definitions
4. **Fix validation issues** automatically (DC paths, series resistors)
5. **Test with real speaker models** to verify fix works

