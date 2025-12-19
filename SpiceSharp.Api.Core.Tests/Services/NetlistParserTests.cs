using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for NetlistParser
/// </summary>
public class NetlistParserTests
{
    private readonly NetlistParser _parser;

    public NetlistParserTests()
    {
        _parser = new NetlistParser();
    }

    [Fact]
    public void ParseNetlist_WithSimpleResistor_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
* Simple resistor circuit
R1 node1 node2 1k
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Components);
        var component = result.Components[0];
        Assert.Equal("R1", component.Name);
        Assert.Equal("resistor", component.ComponentType);
        Assert.Equal(2, component.Nodes.Count);
        Assert.Contains("node1", component.Nodes);
        Assert.Contains("node2", component.Nodes);
        Assert.Equal(1000.0, component.Value);
    }

    [Fact]
    public void ParseNetlist_WithCapacitorAndInductor_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
C1 node1 0 100n
L1 node1 node2 10m
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Components.Count);
        
        var capacitor = result.Components.First(c => c.Name == "C1");
        Assert.Equal("capacitor", capacitor.ComponentType);
        // 100n = 100 * 1e-9 = 1e-7
        Assert.True(Math.Abs((double)(capacitor.Value ?? 0) - 1e-7) < 1e-10, $"Expected ~1e-7, got {capacitor.Value}");
        
        var inductor = result.Components.First(c => c.Name == "L1");
        Assert.Equal("inductor", inductor.ComponentType);
        Assert.Equal(10e-3, inductor.Value);
    }

    [Fact]
    public void ParseNetlist_WithVoltageSource_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
V1 in 0 DC 5 AC 1
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Components);
        var component = result.Components[0];
        Assert.Equal("V1", component.Name);
        Assert.Equal("voltage_source", component.ComponentType);
        Assert.Equal(5.0, component.Value);
        Assert.True(component.Parameters.ContainsKey("ac"));
        Assert.Equal(1.0, component.Parameters["ac"]);
    }

    [Fact]
    public void ParseNetlist_WithDiodeAndModel_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
D1 anode cathode DMODEL
.MODEL DMODEL D (IS=1e-14 N=1.05)
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Components);
        var component = result.Components[0];
        Assert.Equal("D1", component.Name);
        Assert.Equal("diode", component.ComponentType);
        Assert.Equal("DMODEL", component.Model);
        
        Assert.Single(result.Models);
        var model = result.Models[0];
        Assert.Equal("DMODEL", model.ModelName);
        Assert.Equal("diode", model.ModelType);
        Assert.True(model.Parameters.ContainsKey("IS"));
        Assert.Equal(1e-14, model.Parameters["IS"]);
        Assert.True(model.Parameters.ContainsKey("N"));
        Assert.Equal(1.05, model.Parameters["N"]);
    }

    [Fact]
    public void ParseNetlist_WithTransistorAndModel_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
Q1 c b e QMODEL
.MODEL QMODEL NPN (BF=100 IS=1e-15)
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Components);
        var component = result.Components[0];
        Assert.Equal("Q1", component.Name);
        Assert.Equal("bjt_npn", component.ComponentType);
        Assert.Equal("QMODEL", component.Model);
        
        Assert.Single(result.Models);
        var model = result.Models[0];
        Assert.Equal("QMODEL", model.ModelName);
        Assert.Equal("bjt_npn", model.ModelType);
    }

    [Fact]
    public void ParseNetlist_WithComments_IgnoresComments()
    {
        // Arrange
        var netlist = @"
* This is a comment
R1 node1 node2 1k
* Another comment
C1 node1 0 100n
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Components.Count);
        Assert.Equal("R1", result.Components[0].Name);
        Assert.Equal("C1", result.Components[1].Name);
    }

    [Fact]
    public void ParseNetlist_WithContinuationLines_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
.MODEL DMODEL D (
+ IS=1e-14
+ N=1.05
+ RS=0.5
)
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Models);
        var model = result.Models[0];
        Assert.Equal("DMODEL", model.ModelName);
        Assert.True(model.Parameters.ContainsKey("IS"));
        Assert.True(model.Parameters.ContainsKey("N"));
        Assert.True(model.Parameters.ContainsKey("RS"));
    }

    [Fact]
    public void ParseNetlist_WithInvalidFormat_ThrowsException()
    {
        // Arrange
        var netlist = @"
R1 invalid line format
";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseNetlist(netlist));
    }

    [Fact]
    public void ParseNetlist_WithEmptyNetlist_ReturnsEmptyResult()
    {
        // Arrange
        var netlist = @"
* Empty netlist
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Components);
        Assert.Empty(result.Models);
    }

    [Fact]
    public void ParseNetlist_WithCurrentSource_ParsesCorrectly()
    {
        // Arrange
        var netlist = @"
I1 node1 0 10m
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Components);
        var component = result.Components[0];
        Assert.Equal("I1", component.Name);
        Assert.Equal("current_source", component.ComponentType);
        Assert.Equal(10e-3, component.Value);
    }

    [Fact]
    public void ParseNetlist_WithMultipleModels_ParsesAll()
    {
        // Arrange
        var netlist = @"
D1 a c DMODEL1
D2 b d DMODEL2
.MODEL DMODEL1 D (IS=1e-14)
.MODEL DMODEL2 D (IS=2e-14)
";

        // Act
        var result = _parser.ParseNetlist(netlist);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Components.Count);
        Assert.Equal(2, result.Models.Count);
        Assert.True(result.Models.Any(m => m.ModelName == "DMODEL1"));
        Assert.True(result.Models.Any(m => m.ModelName == "DMODEL2"));
    }
}
