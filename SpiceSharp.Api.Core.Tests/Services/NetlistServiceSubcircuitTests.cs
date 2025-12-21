using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for NetlistService subcircuit export functionality
/// </summary>
public class NetlistServiceSubcircuitTests
{
    private readonly NetlistService _netlistService;
    private readonly ComponentService _componentService;
    private readonly CircuitManager _circuitManager;
    private readonly LibraryService _libraryService;

    public NetlistServiceSubcircuitTests()
    {
        _libraryService = new LibraryService();
        _componentService = new ComponentService(_libraryService);
        _netlistService = new NetlistService();
        _circuitManager = new CircuitManager();
    }

    [Fact]
    public void ExportNetlist_WithSubcircuitInstance_IncludesXLine()
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
            var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);

            // Assert
            Assert.Contains("X1", netlist);
            Assert.Contains("n1", netlist);
            Assert.Contains("n2", netlist);
            Assert.Contains("test_sub", netlist);
            
            // Verify X-line format: X<name> <node1> <node2> ... <subcircuit_name>
            var lines = netlist.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var xLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("X1", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(xLine);
            
            // Format should be: X1 n1 n2 test_sub
            var parts = xLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("X1", parts[0]);
            Assert.Equal("n1", parts[1]);
            Assert.Equal("n2", parts[2]);
            Assert.Equal("test_sub", parts[3]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportNetlist_WithMultipleSubcircuits_ExportsAll()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Set up a test library with multiple subcircuits
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT sub1 1 2
R1 1 2 1K
.ENDS
.SUBCKT sub2 1 2 3
R1 1 2 1K
R2 2 3 2K
.ENDS
");

        try
        {
            _libraryService.IndexLibraries(new[] { tempDir });

            var definition1 = new ComponentDefinition
            {
                Name = "X1",
                ComponentType = "subcircuit",
                Model = "sub1",
                Nodes = new List<string> { "a", "b" }
            };

            var definition2 = new ComponentDefinition
            {
                Name = "X2",
                ComponentType = "subcircuit",
                Model = "sub2",
                Nodes = new List<string> { "c", "d", "e" }
            };

            _componentService.AddComponent(circuit, definition1);
            _componentService.AddComponent(circuit, definition2);

            // Act
            var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);

            // Assert
            Assert.Contains("X1", netlist);
            Assert.Contains("sub1", netlist);
            Assert.Contains("X2", netlist);
            Assert.Contains("sub2", netlist);
            
            // Verify both X-lines are present
            var lines = netlist.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var x1Line = lines.FirstOrDefault(l => l.TrimStart().StartsWith("X1", StringComparison.OrdinalIgnoreCase));
            var x2Line = lines.FirstOrDefault(l => l.TrimStart().StartsWith("X2", StringComparison.OrdinalIgnoreCase));
            
            Assert.NotNull(x1Line);
            Assert.NotNull(x2Line);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportNetlist_SubcircuitWithMultipleNodes_ExportsCorrectly()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Set up a test library with a subcircuit that has 3 nodes
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT test_sub 1 2 3
R1 1 2 1K
R2 2 3 2K
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
                Nodes = new List<string> { "node1", "node2", "node3" }
            };

            _componentService.AddComponent(circuit, definition);

            // Act
            var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);

            // Assert
            var lines = netlist.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var xLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("X1", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(xLine);
            
            // Format should be: X1 node1 node2 node3 test_sub
            var parts = xLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("X1", parts[0]);
            Assert.Equal("node1", parts[1]);
            Assert.Equal("node2", parts[2]);
            Assert.Equal("node3", parts[3]);
            Assert.Equal("test_sub", parts[4]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportNetlist_RoundTrip_PreservesSubcircuits()
    {
        // Arrange
        var circuit1 = _circuitManager.CreateCircuit("test_circuit1", "Test circuit 1");
        
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

            _componentService.AddComponent(circuit1, definition);

            // Act - Export netlist
            var exportedNetlist = _netlistService.ExportNetlist(circuit1, includeComments: false);
            
            // Import the exported netlist
            var netlistParser = new NetlistParser();
            var parsedNetlist = netlistParser.ParseNetlist(exportedNetlist);
            
            var circuit2 = _circuitManager.CreateCircuit("test_circuit2", "Test circuit 2");
            foreach (var componentDef in parsedNetlist.Components)
            {
                _componentService.AddComponent(circuit2, componentDef);
            }
            
            // Export again
            var reExportedNetlist = _netlistService.ExportNetlist(circuit2, includeComments: false);

            // Assert
            // Both netlists should contain the subcircuit
            Assert.Contains("X1", exportedNetlist);
            Assert.Contains("test_sub", exportedNetlist);
            Assert.Contains("X1", reExportedNetlist);
            Assert.Contains("test_sub", reExportedNetlist);
            
            // Verify the subcircuit is still present in the circuit
            var component = _componentService.GetComponent(circuit2, "X1");
            Assert.NotNull(component);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

