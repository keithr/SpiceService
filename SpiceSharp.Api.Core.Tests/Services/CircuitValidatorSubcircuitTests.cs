using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Components;
using System.Reflection;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for CircuitValidator subcircuit validation functionality
/// </summary>
public class CircuitValidatorSubcircuitTests
{
    private readonly CircuitValidator _validator;
    private readonly ComponentService _componentService;
    private readonly CircuitManager _circuitManager;
    private readonly LibraryService _libraryService;

    public CircuitValidatorSubcircuitTests()
    {
        _libraryService = new LibraryService();
        _componentService = new ComponentService(_libraryService);
        _validator = new CircuitValidator();
        _circuitManager = new CircuitManager();
    }

    [Fact]
    public void ValidateCircuit_SubcircuitWithoutDefinition_ReportsError()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Use reflection to access internal properties for testing
        var internalCircuitProp = typeof(CircuitModel).GetProperty("InternalCircuit", BindingFlags.NonPublic | BindingFlags.Instance);
        var componentDefinitionsProp = typeof(CircuitModel).GetProperty("ComponentDefinitions", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var internalCircuit = internalCircuitProp?.GetValue(circuit) as SpiceSharp.Circuit;
        var componentDefinitions = componentDefinitionsProp?.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
        
        // Manually add a subcircuit instance without a definition
        // This simulates the case where a subcircuit instance exists but the definition wasn't registered
        var subcircuitDef = new ComponentDefinition
        {
            Name = "X1",
            ComponentType = "subcircuit",
            Model = "nonexistent_sub",
            Nodes = new List<string> { "n1", "n2" }
        };
        
        // Add the component definition to the circuit's ComponentDefinitions
        if (componentDefinitions != null)
        {
            componentDefinitions[subcircuitDef.Name] = subcircuitDef;
        }
        
        // Create a temporary subcircuit definition to use as a template
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT dummy_sub 1 2
R1 1 2 1K
.ENDS
");

        try
        {
            _libraryService.IndexLibraries(new[] { tempDir });
            
            // Create a dummy definition to get a SubcircuitDefinition object
            var dummyDef = new ComponentDefinition
            {
                Name = "X_DUMMY",
                ComponentType = "subcircuit",
                Model = "dummy_sub",
                Nodes = new List<string> { "d1", "d2" }
            };
            _componentService.AddComponent(circuit, dummyDef);
            
            // Get the dummy definition
            var spiceCircuit = circuit.GetSpiceSharpCircuit();
            var hasDummyDef = spiceCircuit.TryGetEntity("dummy_sub", out var dummyEntity);
            Assert.True(hasDummyDef);
            
            // Get the ISubcircuitDefinition from the entity
            ISubcircuitDefinition? dummySubcircuitDef = null;
            if (dummyEntity is ISubcircuitDefinition directDef)
            {
                dummySubcircuitDef = directDef;
            }
            else
            {
                // Try to get it via reflection if it's wrapped
                var definitionProp = dummyEntity?.GetType().GetProperty("Definition", BindingFlags.Public | BindingFlags.Instance);
                dummySubcircuitDef = definitionProp?.GetValue(dummyEntity) as ISubcircuitDefinition;
            }
            
            Assert.NotNull(dummySubcircuitDef);
            
            // Create a Subcircuit instance that references a non-existent definition name
            var brokenSubcircuit = new Subcircuit(
                "X1",
                dummySubcircuitDef, // Use dummy definition temporarily
                subcircuitDef.Nodes.ToArray()
            );
            internalCircuit?.Add(brokenSubcircuit);
            
            // Remove the dummy definition so X1's definition doesn't exist
            internalCircuit?.Remove(dummyEntity!);
            
            // Act
            var result = _validator.Validate(circuit);
            
            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            // The error should mention the missing subcircuit definition
            var hasSubcircuitError = result.Errors.Any(e => 
                e.Contains("subcircuit", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("X1", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("definition", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasSubcircuitError, $"Expected subcircuit-related error. Errors: {string.Join(", ", result.Errors)}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateCircuit_SubcircuitNodeCountMismatch_ReportsError()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Use reflection to access internal properties
        var internalCircuitProp = typeof(CircuitModel).GetProperty("InternalCircuit", BindingFlags.NonPublic | BindingFlags.Instance);
        var componentDefinitionsProp = typeof(CircuitModel).GetProperty("ComponentDefinitions", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var internalCircuit = internalCircuitProp?.GetValue(circuit) as SpiceSharp.Circuit;
        var componentDefinitions = componentDefinitionsProp?.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
        
        // Set up a test library with a subcircuit that has 2 nodes
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT test_sub 1 2
R1 1 2 1K
.ENDS
");

        try
        {
            _libraryService.IndexLibraries(new[] { tempDir });

            // Add the subcircuit definition (this creates the definition with 2 nodes)
            var definition = new ComponentDefinition
            {
                Name = "X1",
                ComponentType = "subcircuit",
                Model = "test_sub",
                Nodes = new List<string> { "n1", "n2" }
            };
            _componentService.AddComponent(circuit, definition);
            
            // Now manually modify the instance to have 3 nodes (mismatch)
            // Get the subcircuit instance
            var spiceCircuit = circuit.GetSpiceSharpCircuit();
            var subcircuitInstance = spiceCircuit.TryGetEntity("X1", out var entity) 
                ? entity as Subcircuit 
                : null;
            
            Assert.NotNull(subcircuitInstance);
            
            // Remove the old instance
            internalCircuit?.Remove(subcircuitInstance);
            
            // Get the definition
            var hasDef = spiceCircuit.TryGetEntity("test_sub", out var defEntity);
            Assert.True(hasDef);
            
            ISubcircuitDefinition? subcircuitDef = null;
            if (defEntity is ISubcircuitDefinition directDef)
            {
                subcircuitDef = directDef;
            }
            else
            {
                var definitionProp = defEntity?.GetType().GetProperty("Definition", BindingFlags.Public | BindingFlags.Instance);
                subcircuitDef = definitionProp?.GetValue(defEntity) as ISubcircuitDefinition;
            }
            
            Assert.NotNull(subcircuitDef);
            
            // Create a new instance with wrong number of nodes (3 instead of 2)
            var wrongSubcircuit = new Subcircuit(
                "X1",
                subcircuitDef,
                new[] { "n1", "n2", "n3" } // 3 nodes, but definition expects 2
            );
            internalCircuit?.Add(wrongSubcircuit);
            
            // Update the component definition to reflect 3 nodes
            if (componentDefinitions != null && componentDefinitions.ContainsKey("X1"))
            {
                componentDefinitions["X1"].Nodes = new List<string> { "n1", "n2", "n3" };
            }
            
            // Act
            var result = _validator.Validate(circuit);
            
            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            // The error should mention node count mismatch
            var hasNodeCountError = result.Errors.Any(e => 
                e.Contains("node", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("X1", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasNodeCountError, $"Expected node count error. Errors: {string.Join(", ", result.Errors)}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateCircuit_SubcircuitWithValidDefinition_Passes()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Set up a test library with a subcircuit
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT test_sub 1 2
R1 1 2 1K
.ENDS
");

        try
        {
            _libraryService.IndexLibraries(new[] { tempDir });

            var definition = new ComponentDefinition
            {
                Name = "X1",
                ComponentType = "subcircuit",
                Model = "test_sub",
                Nodes = new List<string> { "n1", "n2" }
            };

            _componentService.AddComponent(circuit, definition);

            // Act
            var result = _validator.Validate(circuit);

            // Assert
            // Should be valid (no errors related to subcircuits)
            // May have warnings (like missing ground), but no subcircuit errors
            var subcircuitErrors = result.Errors.Where(e => 
                e.Contains("subcircuit", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("X1", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Empty(subcircuitErrors);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateCircuit_MultipleSubcircuits_ValidatesAll()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Use reflection to access internal properties
        var internalCircuitProp = typeof(CircuitModel).GetProperty("InternalCircuit", BindingFlags.NonPublic | BindingFlags.Instance);
        var componentDefinitionsProp = typeof(CircuitModel).GetProperty("ComponentDefinitions", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var internalCircuit = internalCircuitProp?.GetValue(circuit) as SpiceSharp.Circuit;
        var componentDefinitions = componentDefinitionsProp?.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
        
        // Set up a test library with multiple subcircuits
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT valid_sub 1 2
R1 1 2 1K
.ENDS
.SUBCKT another_sub 1 2 3
R1 1 2 1K
R2 2 3 2K
.ENDS
");

        try
        {
            _libraryService.IndexLibraries(new[] { tempDir });

            // Add a valid subcircuit
            var validDef = new ComponentDefinition
            {
                Name = "X1",
                ComponentType = "subcircuit",
                Model = "valid_sub",
                Nodes = new List<string> { "a", "b" }
            };
            _componentService.AddComponent(circuit, validDef);
            
            // Add another valid subcircuit
            var validDef2 = new ComponentDefinition
            {
                Name = "X2",
                ComponentType = "subcircuit",
                Model = "another_sub",
                Nodes = new List<string> { "c", "d", "e" }
            };
            _componentService.AddComponent(circuit, validDef2);
            
            // Manually add an invalid subcircuit instance (wrong node count)
            var invalidSubcircuit = new ComponentDefinition
            {
                Name = "X3",
                ComponentType = "subcircuit",
                Model = "valid_sub", // Definition expects 2 nodes
                Nodes = new List<string> { "f", "g", "h" } // But we provide 3
            };
            if (componentDefinitions != null)
            {
                componentDefinitions[invalidSubcircuit.Name] = invalidSubcircuit;
            }
            
            // Get the definition and create instance with wrong node count
            var spiceCircuit = circuit.GetSpiceSharpCircuit();
            var hasDef = spiceCircuit.TryGetEntity("valid_sub", out var defEntity);
            Assert.True(hasDef);
            
            ISubcircuitDefinition? subcircuitDef = null;
            if (defEntity is ISubcircuitDefinition directDef)
            {
                subcircuitDef = directDef;
            }
            else
            {
                var definitionProp = defEntity?.GetType().GetProperty("Definition", BindingFlags.Public | BindingFlags.Instance);
                subcircuitDef = definitionProp?.GetValue(defEntity) as ISubcircuitDefinition;
            }
            
            Assert.NotNull(subcircuitDef);
            
            var wrongSubcircuit = new Subcircuit(
                "X3",
                subcircuitDef,
                new[] { "f", "g", "h" } // 3 nodes, but definition expects 2
            );
            internalCircuit?.Add(wrongSubcircuit);

            // Act
            var result = _validator.Validate(circuit);

            // Assert
            // Should have at least one error for X3 (node count mismatch)
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            
            // Should have error mentioning X3
            var hasX3Error = result.Errors.Any(e => 
                e.Contains("X3", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasX3Error, $"Expected error for X3. Errors: {string.Join(", ", result.Errors)}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

