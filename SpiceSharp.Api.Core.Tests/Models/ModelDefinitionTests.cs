using Xunit;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Tests.Models;

public class ModelDefinitionTests
{
    [Fact]
    public void ModelDefinition_Creation_ShouldSetProperties()
    {
        // Arrange & Act
        var model = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "1N4148",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-12 },
                { "N", 1.0 },
                { "RS", 1.0 }
            }
        };

        // Assert
        Assert.Equal("diode", model.ModelType);
        Assert.Equal("1N4148", model.ModelName);
        Assert.Equal(3, model.Parameters.Count);
        Assert.Equal(1e-12, model.Parameters["IS"]);
    }

    [Fact]
    public void ModelDefinition_DefaultParameters_ShouldBeEmpty()
    {
        // Arrange & Act
        var model = new ModelDefinition();

        // Assert
        Assert.NotNull(model.Parameters);
        Assert.Empty(model.Parameters);
    }
}

