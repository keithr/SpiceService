using Xunit;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Tests.Models;

public class SubcircuitDefinitionTests
{
    [Fact]
    public void SubcircuitDefinition_Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var subcircuit = new SubcircuitDefinition
        {
            Name = "irf1010n",
            Nodes = new List<string> { "1", "2", "3" },
            Definition = "M1 9 7 8 8 MM L=100u W=100u"
        };

        // Assert
        Assert.Equal("irf1010n", subcircuit.Name);
        Assert.Equal(3, subcircuit.Nodes.Count);
        Assert.Contains("1", subcircuit.Nodes);
        Assert.Contains("2", subcircuit.Nodes);
        Assert.Contains("3", subcircuit.Nodes);
        Assert.Equal("M1 9 7 8 8 MM L=100u W=100u", subcircuit.Definition);
    }

    [Fact]
    public void SubcircuitDefinition_NodesList_IsInitialized()
    {
        // Arrange & Act
        var subcircuit = new SubcircuitDefinition();

        // Assert
        Assert.NotNull(subcircuit.Nodes);
    }

    [Fact]
    public void SubcircuitDefinition_DefinitionString_IsInitialized()
    {
        // Arrange & Act
        var subcircuit = new SubcircuitDefinition();

        // Assert
        Assert.NotNull(subcircuit.Definition);
    }
}

