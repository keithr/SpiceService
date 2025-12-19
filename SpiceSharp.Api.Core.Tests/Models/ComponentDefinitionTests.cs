using Xunit;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Tests.Models;

public class ComponentDefinitionTests
{
    [Fact]
    public void ComponentDefinition_Creation_ShouldSetProperties()
    {
        // Arrange & Act
        var component = new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Value = 1000.0,
            Nodes = new List<string> { "node1", "node2" }
        };

        // Assert
        Assert.Equal("resistor", component.ComponentType);
        Assert.Equal("R1", component.Name);
        Assert.Equal(1000.0, component.Value);
        Assert.Equal(2, component.Nodes.Count);
    }

    [Fact]
    public void ComponentDefinition_WithModel_ShouldSetModelProperty()
    {
        // Arrange & Act
        var component = new ComponentDefinition
        {
            ComponentType = "diode",
            Name = "D1",
            Model = "1N4148",
            Nodes = new List<string> { "anode", "cathode" }
        };

        // Assert
        Assert.Equal("diode", component.ComponentType);
        Assert.Equal("D1", component.Name);
        Assert.Equal("1N4148", component.Model);
        Assert.Equal(2, component.Nodes.Count);
    }

    [Fact]
    public void ComponentDefinition_DefaultParameters_ShouldBeEmpty()
    {
        // Arrange & Act
        var component = new ComponentDefinition();

        // Assert
        Assert.NotNull(component.Parameters);
        Assert.Empty(component.Parameters);
    }
}

