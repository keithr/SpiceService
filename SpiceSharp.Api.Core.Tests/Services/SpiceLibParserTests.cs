using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for SpiceLibParser - covers Phase 1A.4 from implementation plan
/// </summary>
public class SpiceLibParserTests
{
    [Fact]
    public void ParseLibFile_WithSimpleModel_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
* Simple diode model
.MODEL D1N4001 D (IS=1E-14 RS=0.5 N=1.5)
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.Equal("D1N4001", model.ModelName);
        Assert.Equal("diode", model.ModelType);
        Assert.NotNull(model.Parameters);
        Assert.True(Math.Abs(model.Parameters["IS"] - 1e-14) < 1e-20, $"Expected IS ≈ 1e-14, got {model.Parameters["IS"]}");
        Assert.True(Math.Abs(model.Parameters["RS"] - 0.5) < 0.01, $"Expected RS ≈ 0.5, got {model.Parameters["RS"]}");
        Assert.True(Math.Abs(model.Parameters["N"] - 1.5) < 0.01, $"Expected N ≈ 1.5, got {model.Parameters["N"]}");
    }

    [Fact]
    public void ParseLibFile_WithMultiLineModel_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
* Multi-line model with continuation
.MODEL Q2N3904 NPN (
+ IS=1E-16
+ BF=100
+ VAF=100
+ )
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.Equal("Q2N3904", model.ModelName);
        Assert.Equal("bjt_npn", model.ModelType);
        Assert.NotNull(model.Parameters);
        Assert.True(Math.Abs(model.Parameters["IS"] - 1e-16) < 1e-20, $"Expected IS ≈ 1e-16, got {model.Parameters["IS"]}");
        Assert.True(Math.Abs(model.Parameters["BF"] - 100.0) < 0.1, $"Expected BF ≈ 100.0, got {model.Parameters["BF"]}");
        Assert.True(Math.Abs(model.Parameters["VAF"] - 100.0) < 0.1, $"Expected VAF ≈ 100.0, got {model.Parameters["VAF"]}");
    }

    [Fact]
    public void ParseLibFile_WithComments_IgnoresComments()
    {
        // Arrange
        var libContent = @"
* This is a comment line
* Another comment
.MODEL D1N4148 D (
+ IS=2E-14    * Saturation current
+ RS=0.5      * Series resistance
+ N=1.8       * Ideality factor
+ )
* End comment
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.Equal("D1N4148", model.ModelName);
        Assert.Equal("diode", model.ModelType);
        Assert.NotNull(model.Parameters);
        Assert.True(Math.Abs(model.Parameters["IS"] - 2e-14) < 1e-20, $"Expected IS ≈ 2e-14, got {model.Parameters["IS"]}");
        Assert.True(Math.Abs(model.Parameters["RS"] - 0.5) < 0.01, $"Expected RS ≈ 0.5, got {model.Parameters["RS"]}");
        Assert.True(Math.Abs(model.Parameters["N"] - 1.8) < 0.01, $"Expected N ≈ 1.8, got {model.Parameters["N"]}");
    }

    [Fact]
    public void ParseLibFile_WithMultipleModels_ParsesAll()
    {
        // Arrange
        var libContent = @"
.MODEL D1N4001 D (IS=1E-14 RS=0.5)
.MODEL D1N4002 D (IS=2E-14 RS=0.6)
.MODEL Q2N3904 NPN (IS=1E-16 BF=100)
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Equal(3, models.Count);
        var d1 = models.First(m => m.ModelName == "D1N4001");
        var d2 = models.First(m => m.ModelName == "D1N4002");
        var q1 = models.First(m => m.ModelName == "Q2N3904");
        
        Assert.Equal("diode", d1.ModelType);
        Assert.Equal("diode", d2.ModelType);
        Assert.Equal("bjt_npn", q1.ModelType);
    }

    [Fact]
    public void ParseLibFile_WithScientificNotation_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.MODEL TEST_MODEL D (
+ IS=1.5E-15
+ RS=2.3E-2
+ N=1.5E0
+ )
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.True(Math.Abs(model.Parameters["IS"] - 1.5e-15) < 1e-20, $"Expected IS ≈ 1.5e-15, got {model.Parameters["IS"]}");
        Assert.True(Math.Abs(model.Parameters["RS"] - 2.3e-2) < 1e-4, $"Expected RS ≈ 2.3e-2, got {model.Parameters["RS"]}");
        Assert.True(Math.Abs(model.Parameters["N"] - 1.5) < 0.01, $"Expected N ≈ 1.5, got {model.Parameters["N"]}");
    }

    [Fact]
    public void ParseLibFile_WithInvalidFormat_HandlesGracefully()
    {
        // Arrange
        var libContent = @"
* Invalid model line (no .MODEL)
INVALID LINE
.MODEL VALID_MODEL D (IS=1E-14)
* Another invalid line
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        // Should parse the valid model and ignore invalid lines
        Assert.Single(models);
        Assert.Equal("VALID_MODEL", models.First().ModelName);
    }

    [Fact]
    public void ParseLibFile_WithEmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var libContent = @"
* Empty file with only comments
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Empty(models);
    }

    [Fact]
    public void ParseLibFile_WithPNPModel_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.MODEL Q2N3906 PNP (IS=1E-16 BF=50)
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.Equal("Q2N3906", model.ModelName);
        Assert.Equal("bjt_pnp", model.ModelType);
    }

    [Fact]
    public void ParseLibFile_WithMOSFETModel_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.MODEL M2N7000 NMOS (VTO=2.0 KP=0.1)
";
        var parser = new SpiceLibParser();

        // Act
        var models = parser.ParseLibFile(libContent);

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.Equal("M2N7000", model.ModelName);
        Assert.Equal("mosfet_n", model.ModelType);
        Assert.True(Math.Abs(model.Parameters["VTO"] - 2.0) < 0.1, $"Expected VTO ≈ 2.0, got {model.Parameters["VTO"]}");
        Assert.True(Math.Abs(model.Parameters["KP"] - 0.1) < 0.01, $"Expected KP ≈ 0.1, got {model.Parameters["KP"]}");
    }

    [Fact]
    public void ParseSubcircuits_WithSimpleSubcircuit_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.SUBCKT irf1010n 1 2 3
M1 9 7 8 8 MM L=100u W=100u
.MODEL MM NMOS LEVEL=1 VTO=3.74111
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits.First();
        Assert.Equal("irf1010n", sub.Name);
        Assert.Equal(3, sub.Nodes.Count);
        Assert.Contains("1", sub.Nodes);
        Assert.Contains("2", sub.Nodes);
        Assert.Contains("3", sub.Nodes);
        Assert.Contains("M1 9 7 8 8 MM L=100u W=100u", sub.Definition);
        Assert.Contains(".MODEL MM NMOS LEVEL=1 VTO=3.74111", sub.Definition);
    }

    [Fact]
    public void ParseSubcircuits_WithMultipleExternalNodes_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.SUBCKT opamp_ideal in+ in- out vcc vee
R1 in+ in- 1MEG
E1 out 0 in+ in- 1000000
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits.First();
        Assert.Equal("opamp_ideal", sub.Name);
        Assert.Equal(5, sub.Nodes.Count);
        Assert.Contains("in+", sub.Nodes);
        Assert.Contains("in-", sub.Nodes);
        Assert.Contains("out", sub.Nodes);
        Assert.Contains("vcc", sub.Nodes);
        Assert.Contains("vee", sub.Nodes);
    }

    [Fact]
    public void ParseSubcircuits_WithContinuationLines_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.SUBCKT complex_sub 1 2 3
+ 4 5
R1 1 2 1K
R2 3 4 2K
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits.First();
        Assert.Equal("complex_sub", sub.Name);
        Assert.Equal(5, sub.Nodes.Count);
        Assert.Contains("1", sub.Nodes);
        Assert.Contains("2", sub.Nodes);
        Assert.Contains("3", sub.Nodes);
        Assert.Contains("4", sub.Nodes);
        Assert.Contains("5", sub.Nodes);
    }

    [Fact]
    public void ParseSubcircuits_WithInternalComponents_ParsesCorrectly()
    {
        // Arrange
        var libContent = @"
.SUBCKT test_sub 1 2
R1 1 2 1K
C1 1 0 1uF
L1 2 0 1mH
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits.First();
        Assert.Equal("test_sub", sub.Name);
        Assert.Contains("R1 1 2 1K", sub.Definition);
        Assert.Contains("C1 1 0 1uF", sub.Definition);
        Assert.Contains("L1 2 0 1mH", sub.Definition);
    }

    [Fact]
    public void ParseSubcircuits_WithComments_IgnoresComments()
    {
        // Arrange
        var libContent = @"
* This is a comment line
.SUBCKT commented_sub 1 2
* Internal comment
R1 1 2 1K * Inline comment
* Another comment
.ENDS
* End comment
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits.First();
        Assert.Equal("commented_sub", sub.Name);
        Assert.Equal(2, sub.Nodes.Count);
        // Definition should not contain comment-only lines
        Assert.DoesNotContain("* This is a comment line", sub.Definition);
        Assert.DoesNotContain("* Internal comment", sub.Definition);
        // But should contain the component line (with inline comment removed)
        Assert.Contains("R1 1 2 1K", sub.Definition);
    }

    [Fact]
    public void ParseSubcircuits_WithMultipleSubcircuits_ParsesAll()
    {
        // Arrange
        var libContent = @"
.SUBCKT sub1 1 2
R1 1 2 1K
.ENDS
.SUBCKT sub2 3 4
R2 3 4 2K
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Equal(2, subcircuits.Count);
        var sub1 = subcircuits.First(s => s.Name == "sub1");
        var sub2 = subcircuits.First(s => s.Name == "sub2");
        Assert.Equal(2, sub1.Nodes.Count);
        Assert.Equal(2, sub2.Nodes.Count);
        Assert.Contains("R1 1 2 1K", sub1.Definition);
        Assert.Contains("R2 3 4 2K", sub2.Definition);
    }

    [Fact]
    public void ParseSubcircuits_WithBothModelsAndSubcircuits_ParsesBoth()
    {
        // Arrange
        var libContent = @"
.MODEL D1N4148 D (IS=1E-14)
.SUBCKT test_sub 1 2
R1 1 2 1K
.ENDS
.MODEL Q2N3904 NPN (IS=1E-16)
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits.First();
        Assert.Equal("test_sub", sub.Name);
        Assert.Contains("R1 1 2 1K", sub.Definition);
    }

    [Fact]
    public void ParseSubcircuits_WithMalformedSubcircuit_HandlesGracefully()
    {
        // Arrange
        var libContent = @"
* Invalid subcircuit line (no .SUBCKT)
INVALID LINE
.SUBCKT valid_sub 1 2
R1 1 2 1K
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        // Should parse the valid subcircuit and ignore invalid lines
        Assert.Single(subcircuits);
        Assert.Equal("valid_sub", subcircuits.First().Name);
    }

    [Fact]
    public void ParseSubcircuits_WithMissingEnds_HandlesGracefully()
    {
        // Arrange
        var libContent = @"
.SUBCKT incomplete_sub 1 2
R1 1 2 1K
.SUBCKT complete_sub 3 4
R2 3 4 2K
.ENDS
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        // Should parse complete_sub, and incomplete_sub should end at next .SUBCKT
        Assert.Equal(2, subcircuits.Count);
        var incomplete = subcircuits.First(s => s.Name == "incomplete_sub");
        var complete = subcircuits.First(s => s.Name == "complete_sub");
        Assert.Contains("R1 1 2 1K", incomplete.Definition);
        Assert.Contains("R2 3 4 2K", complete.Definition);
    }

    [Fact]
    public void ParseSubcircuits_WithEmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var libContent = @"
* Empty file with only comments
";
        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Empty(subcircuits);
    }
}
