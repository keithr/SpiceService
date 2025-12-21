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

    [Fact]
    public void SubcircuitDefinition_Metadata_IsInitialized()
    {
        // Arrange & Act
        var subcircuit = new SubcircuitDefinition();

        // Assert
        Assert.NotNull(subcircuit.Metadata);
    }

    [Fact]
    public void SubcircuitDefinition_TsParameters_IsInitialized()
    {
        // Arrange & Act
        var subcircuit = new SubcircuitDefinition();

        // Assert
        Assert.NotNull(subcircuit.TsParameters);
    }

    [Fact]
    public void SubcircuitDefinition_CanSetAndRetrieveMetadata()
    {
        // Arrange
        var subcircuit = new SubcircuitDefinition();

        // Act
        subcircuit.Metadata["MANUFACTURER"] = "Peerless";
        subcircuit.Metadata["TYPE"] = "woofers";
        subcircuit.Metadata["PRICE"] = "59.98";

        // Assert
        Assert.Equal("Peerless", subcircuit.Metadata["MANUFACTURER"]);
        Assert.Equal("woofers", subcircuit.Metadata["TYPE"]);
        Assert.Equal("59.98", subcircuit.Metadata["PRICE"]);
    }

    [Fact]
    public void SubcircuitDefinition_CanSetAndRetrieveTsParameters()
    {
        // Arrange
        var subcircuit = new SubcircuitDefinition();

        // Act
        subcircuit.TsParameters["FS"] = 42.18;
        subcircuit.TsParameters["QTS"] = 0.35;
        subcircuit.TsParameters["VAS"] = 11.2;

        // Assert
        Assert.Equal(42.18, subcircuit.TsParameters["FS"]);
        Assert.Equal(0.35, subcircuit.TsParameters["QTS"]);
        Assert.Equal(11.2, subcircuit.TsParameters["VAS"]);
    }

    [Fact]
    public void SubcircuitDefinition_ExistingPropertiesStillWork()
    {
        // Arrange & Act
        var subcircuit = new SubcircuitDefinition
        {
            Name = "264_1148",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = "Re PLUS 1 2.73\nLe 1 2 0.65mH"
        };

        // Assert - existing properties
        Assert.Equal("264_1148", subcircuit.Name);
        Assert.Equal(2, subcircuit.Nodes.Count);
        Assert.Contains("PLUS", subcircuit.Nodes);
        Assert.Contains("MINUS", subcircuit.Nodes);
        Assert.Equal("Re PLUS 1 2.73\nLe 1 2 0.65mH", subcircuit.Definition);

        // Assert - new properties are initialized
        Assert.NotNull(subcircuit.Metadata);
        Assert.NotNull(subcircuit.TsParameters);
    }
}

