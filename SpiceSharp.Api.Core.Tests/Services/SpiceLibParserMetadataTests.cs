using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests to verify metadata parsing handles real-world variations
/// </summary>
public class SpiceLibParserMetadataTests
{
    [Fact]
    public void ParseSubcircuits_HandlesMetadataWithUnits()
    {
        // Arrange - Simulate real .lib file with units in metadata
        var libContent = @"
* MANUFACTURER: Peerless
* TYPE: woofers
* DIAMETER: 6.5 in
* IMPEDANCE: 8 ohms
* SENSITIVITY: 88.5 dB
* POWER_RMS: 50 watts
* FS: 42.18
* QTS: 0.35
* VAS: 11.2
.SUBCKT 264_1148 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.001
.ENDS
";

        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits[0];
        
        // Verify metadata is extracted correctly (should strip units)
        Assert.True(sub.Metadata.ContainsKey("MANUFACTURER"));
        Assert.Equal("Peerless", sub.Metadata["MANUFACTURER"]);
        
        Assert.True(sub.Metadata.ContainsKey("TYPE"));
        Assert.Equal("woofers", sub.Metadata["TYPE"]);
        
        Assert.True(sub.Metadata.ContainsKey("DIAMETER"));
        Assert.Equal("6.5", sub.Metadata["DIAMETER"]); // Should strip "in"
        
        Assert.True(sub.Metadata.ContainsKey("IMPEDANCE"));
        Assert.Equal("8", sub.Metadata["IMPEDANCE"]); // Should strip "ohms"
        
        Assert.True(sub.Metadata.ContainsKey("SENSITIVITY"));
        Assert.Equal("88.5", sub.Metadata["SENSITIVITY"]); // Should strip "dB"
        
        Assert.True(sub.Metadata.ContainsKey("POWER_RMS"));
        Assert.Equal("50", sub.Metadata["POWER_RMS"]); // Should strip "watts"
    }

    [Fact]
    public void ParseSubcircuits_HandlesMetadataWithoutUnits()
    {
        // Arrange - Metadata without units
        var libContent = @"
* MANUFACTURER: Dayton Audio
* TYPE: woofers
* DIAMETER: 8.0
* IMPEDANCE: 4
* SENSITIVITY: 91.6
* POWER_RMS: 75
* FS: 45.0
* QTS: 0.4
.SUBCKT DAYTON_8 PLUS MINUS
Re PLUS 1 3.0
.ENDS
";

        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits[0];
        
        Assert.True(sub.Metadata.ContainsKey("DIAMETER"));
        Assert.Equal("8.0", sub.Metadata["DIAMETER"]);
        
        Assert.True(sub.Metadata.ContainsKey("IMPEDANCE"));
        Assert.Equal("4", sub.Metadata["IMPEDANCE"]);
        
        Assert.True(sub.Metadata.ContainsKey("SENSITIVITY"));
        Assert.Equal("91.6", sub.Metadata["SENSITIVITY"]);
    }

    [Fact]
    public void ParseSubcircuits_HandlesCaseVariations()
    {
        // Arrange - Different case variations
        var libContent = @"
* manufacturer: Peerless
* type: woofers
* diameter: 6.5
* impedance: 8
* sensitivity: 88.5
* FS: 42.18
* qts: 0.35
.SUBCKT TEST_SPEAKER PLUS MINUS
Re PLUS 1 2.73
.ENDS
";

        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits[0];
        
        // Should normalize to uppercase keys
        Assert.True(sub.Metadata.ContainsKey("MANUFACTURER"));
        Assert.True(sub.Metadata.ContainsKey("TYPE"));
        Assert.True(sub.Metadata.ContainsKey("DIAMETER"));
        Assert.True(sub.Metadata.ContainsKey("IMPEDANCE"));
        Assert.True(sub.Metadata.ContainsKey("SENSITIVITY"));
        
        // T/S parameters should also normalize
        Assert.True(sub.TsParameters.ContainsKey("FS"));
        Assert.True(sub.TsParameters.ContainsKey("QTS"));
    }

    [Fact]
    public void ParseSubcircuits_HandlesExtraWhitespace()
    {
        // Arrange - Extra whitespace in values
        var libContent = @"
* MANUFACTURER:  Peerless  
* TYPE: woofers
* DIAMETER:  6.5  in
* IMPEDANCE:  8  ohms
* SENSITIVITY:  88.5  dB
* FS:  42.18
.SUBCKT TEST PLUS MINUS
Re PLUS 1 2.73
.ENDS
";

        var parser = new SpiceLibParser();

        // Act
        var subcircuits = parser.ParseSubcircuits(libContent);

        // Assert
        Assert.Single(subcircuits);
        var sub = subcircuits[0];
        
        Assert.True(sub.Metadata.ContainsKey("DIAMETER"));
        Assert.Equal("6.5", sub.Metadata["DIAMETER"]);
        
        Assert.True(sub.Metadata.ContainsKey("IMPEDANCE"));
        Assert.Equal("8", sub.Metadata["IMPEDANCE"]);
        
        Assert.True(sub.Metadata.ContainsKey("SENSITIVITY"));
        Assert.Equal("88.5", sub.Metadata["SENSITIVITY"]);
    }
}

