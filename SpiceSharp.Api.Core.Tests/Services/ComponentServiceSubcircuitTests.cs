using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Entities;
using SpiceSharp.Components;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ComponentService subcircuit functionality
/// </summary>
public class ComponentServiceSubcircuitTests
{
    private readonly ComponentService _componentService;
    private readonly CircuitManager _circuitManager;
    private readonly LibraryService _libraryService;

    public ComponentServiceSubcircuitTests()
    {
        _libraryService = new LibraryService();
        _componentService = new ComponentService(_libraryService);
        _circuitManager = new CircuitManager();
    }

    [Fact]
    public void AddSubcircuitComponent_WithLibraryDefinition_RegistersDefinitionInCircuit()
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

            // Act
            var entity = _componentService.AddComponent(circuit, definition);

            // Assert
            Assert.NotNull(entity);
            Assert.Equal("X1", entity.Name);
            
            // Verify definition is registered in circuit's InternalCircuit
            var spiceCircuit = circuit.GetSpiceSharpCircuit();
            var definitionRegistered = spiceCircuit.TryGetEntity("test_sub", out var definitionEntity);
            Assert.True(definitionRegistered, "Subcircuit definition should be registered in circuit");
            Assert.NotNull(definitionEntity);
            Assert.IsAssignableFrom<ISubcircuitDefinition>(definitionEntity);
            
            // Verify definition can be retrieved by name
            var retrievedDefinition = definitionEntity as ISubcircuitDefinition;
            Assert.NotNull(retrievedDefinition);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddSubcircuitComponent_MultipleInstances_ReusesSameDefinition()
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

            var definition1 = new ComponentDefinition
            {
                Name = "X1",
                ComponentType = "subcircuit",
                Model = "test_sub",
                Nodes = new List<string> { "n1", "n2" }
            };

            var definition2 = new ComponentDefinition
            {
                Name = "X2",
                ComponentType = "subcircuit",
                Model = "test_sub",
                Nodes = new List<string> { "n3", "n4" }
            };

            // Act - Add first instance
            var entity1 = _componentService.AddComponent(circuit, definition1);
            Assert.NotNull(entity1);
            
            // Verify definition exists after first instance
            var spiceCircuit1 = circuit.GetSpiceSharpCircuit();
            var definitionExistsAfterFirst = spiceCircuit1.TryGetEntity("test_sub", out var definitionAfterFirst);
            Assert.True(definitionExistsAfterFirst, "Definition should exist after first instance");
            
            // Add second instance
            var entity2 = _componentService.AddComponent(circuit, definition2);
            Assert.NotNull(entity2);
            
            // Assert - Verify only one definition exists (reused)
            var spiceCircuit2 = circuit.GetSpiceSharpCircuit();
            var definitionCount = spiceCircuit2
                .OfType<ISubcircuitDefinition>()
                .Count();
            
            Assert.Equal(1, definitionCount);
            
            // Verify both instances reference the same definition
            var definitionEntity = spiceCircuit2.TryGetEntity("test_sub", out var def) ? def as ISubcircuitDefinition : null;
            Assert.NotNull(definitionEntity);
            
            // Both instances should be subcircuits
            var instance1 = entity1 as Subcircuit;
            var instance2 = entity2 as Subcircuit;
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddSubcircuitComponent_DefinitionRegistered_BeforeInstanceCreated()
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

            // Track circuit state before adding component
            var spiceCircuitBefore = circuit.GetSpiceSharpCircuit();
            var definitionExistsBefore = spiceCircuitBefore.TryGetEntity("test_sub", out _);
            Assert.False(definitionExistsBefore, "Definition should not exist before adding component");

            // Act
            var entity = _componentService.AddComponent(circuit, definition);

            // Assert
            Assert.NotNull(entity);
            
            // Verify definition exists in circuit (it should be registered before instance creation)
            var spiceCircuitAfter = circuit.GetSpiceSharpCircuit();
            var definitionExists = spiceCircuitAfter.TryGetEntity("test_sub", out var definitionEntity);
            Assert.True(definitionExists, "Definition should be registered in circuit");
            Assert.NotNull(definitionEntity);
            Assert.IsAssignableFrom<ISubcircuitDefinition>(definitionEntity);
            
            // Verify instance can reference the definition
            var subcircuitInstance = entity as Subcircuit;
            Assert.NotNull(subcircuitInstance);
            
            // The instance should have been created successfully, which means it could reference the definition
            var instanceRetrieved = _componentService.GetComponent(circuit, "X1");
            Assert.NotNull(instanceRetrieved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

