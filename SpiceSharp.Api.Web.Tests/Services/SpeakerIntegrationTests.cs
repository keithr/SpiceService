using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Services;
using SpiceSharp.Api.Web.Models;
using Xunit;
using System.Text.Json;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Integration tests for end-to-end speaker workflow
/// </summary>
public class SpeakerIntegrationTests
{
    [Fact]
    public void FullWorkflow_SearchSpeakers_CalculateEnclosure_CheckCompatibility()
    {
        // Arrange - Use isolated database for this test
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        
        var enclosureService = new EnclosureDesignService();
        var crossoverService = new CrossoverCompatibilityService();
        
        // Create test speakers
        var woofer = new SubcircuitDefinition
        {
            Name = "TEST_WOOFER_6.5",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "Test" },
                { "TYPE", "woofers" },
                { "DIAMETER", "6.5" },
                { "IMPEDANCE", "8" },
                { "SENSITIVITY", "88" }
            },
            TsParameters = new Dictionary<string, double>
            {
                { "FS", 42.18 },
                { "QTS", 0.35 },
                { "QES", 0.38 },
                { "QMS", 4.92 },
                { "VAS", 11.2 },
                { "RE", 2.73 },
                { "LE", 0.65 },
                { "BL", 8.27 },
                { "XMAX", 8.2 },
                { "MMS", 35.3 },
                { "CMS", 0.4667 },
                { "SD", 214.0 }
            }
        };

        var tweeter = new SubcircuitDefinition
        {
            Name = "TEST_TWEETER_1",
            Metadata = new Dictionary<string, string>
            {
                { "MANUFACTURER", "Test" },
                { "TYPE", "tweeters" },
                { "DIAMETER", "1" },
                { "IMPEDANCE", "8" },
                { "SENSITIVITY", "90" }
            },
            TsParameters = new Dictionary<string, double>
            {
                { "FS", 800.0 },
                { "QTS", 0.5 },
                { "QES", 0.6 },
                { "QMS", 2.5 },
                { "VAS", 0.1 },
                { "RE", 3.5 },
                { "LE", 0.1 },
                { "BL", 3.0 },
                { "XMAX", 0.5 },
                { "MMS", 0.5 },
                { "CMS", 0.0001 },
                { "SD", 5.0 }
            }
        };

        // Populate database
        speakerDb.PopulateFromSubcircuits(new[] { woofer, tweeter });

        // Act & Assert: Search for woofer
        var searchParams = new SpeakerSearchParameters
        {
            DriverType = new List<string> { "woofers" },
            DiameterMin = 6.0,
            DiameterMax = 7.0,
            Limit = 10
        };
        var searchResults = speakerDb.SearchSpeakersByParameters(searchParams);
        Assert.NotEmpty(searchResults);
        var foundWoofer = searchResults.FirstOrDefault(s => s.SubcircuitName == "TEST_WOOFER_6.5");
        Assert.NotNull(foundWoofer);

        // Act & Assert: Calculate enclosure design
        var wooferTs = ConvertToTsParameters(foundWoofer);
        var sealedDesign = enclosureService.CalculateSealedBox(wooferTs, 0.707);
        Assert.True(sealedDesign.VolumeLiters > 0);
        Assert.True(sealedDesign.Qtc > 0);
        Assert.True(sealedDesign.F3 > 0);

        // Act & Assert: Check crossover compatibility
        var tweeterResult = speakerDb.GetSpeakerByName("TEST_TWEETER_1");
        Assert.NotNull(tweeterResult);
        var tweeterTs = ConvertToTsParameters(tweeterResult);
        var compatibility = crossoverService.CheckCompatibility(wooferTs, tweeterTs, 2500.0, 2);
        Assert.True(compatibility.CompatibilityScore >= 0 && compatibility.CompatibilityScore <= 100);
        Assert.NotNull(compatibility.Recommendations);

        // Cleanup
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch { }
    }

    [Fact]
    public void FullWorkflow_RealLibraryFile_ParsesAndIndexes()
    {
        // Arrange - Use isolated database and test directory
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var parser = new SpiceLibParser();
        var libraryService = new LibraryService(speakerDb);

        // Create a test library file with speaker data
        var testLibContent = @"
* SPICE Model: INTEGRATION_TEST_SPEAKER
* MANUFACTURER: TestManufacturer
* TYPE: woofers
* DIAMETER: 8.0
* IMPEDANCE: 8
* SENSITIVITY: 89.5
* FS: 35.0
* QTS: 0.40
* QES: 0.45
* QMS: 3.5
* VAS: 25.0
* RE: 6.5
* LE: 1.2
* BL: 7.5
* XMAX: 6.5
* MMS: 45.0
* CMS: 0.35
* SD: 320.0
* PRICE: 125.00
*
.SUBCKT INTEGRATION_TEST_SPEAKER PLUS MINUS
Re PLUS 1 6.5
Le 1 2 1.2mH
Rms 2 3 0.5
Lms 3 4 0.05H
Cms 4 MINUS 0.35F
.ENDS INTEGRATION_TEST_SPEAKER
";

        // Create a dedicated test directory to avoid permission issues
        var testDir = Path.Combine(Path.GetTempPath(), $"spice_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        var tempFile = Path.Combine(testDir, "test_speaker.lib");
        File.WriteAllText(tempFile, testLibContent);

        try
        {
            // Act: Parse and index
            var subcircuits = parser.ParseSubcircuits(testLibContent);
            Assert.Single(subcircuits);
            var sub = subcircuits.First();
            Assert.Equal("INTEGRATION_TEST_SPEAKER", sub.Name);
            Assert.Equal("TestManufacturer", sub.Metadata["MANUFACTURER"]);
            Assert.Equal(35.0, sub.TsParameters["FS"]);

            // Index libraries - use the test directory
            libraryService.IndexLibraries(new[] { testDir });

            // Verify database contains the speaker
            var speaker = speakerDb.GetSpeakerByName("INTEGRATION_TEST_SPEAKER");
            Assert.NotNull(speaker);
            Assert.Equal("TestManufacturer", speaker.Manufacturer);
            Assert.Equal(8.0, speaker.Diameter);
            Assert.Equal(35.0, speaker.TsParameters["FS"]);
        }
        finally
        {
            // Clean up test directory and database
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void FullWorkflow_MultipleSpeakers_SearchAndDesign()
    {
        // Arrange - Use a unique database file for this test to ensure isolation
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDb = new SpeakerDatabaseService(dbPath);
        speakerDb.InitializeDatabase();
        var enclosureService = new EnclosureDesignService();

        var speakers = new[]
        {
            new SubcircuitDefinition
            {
                Name = "WOOFER_A",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 40.0 }, { "QTS", 0.35 }, { "VAS", 15.0 }, { "SD", 200.0 }
                }
            },
            new SubcircuitDefinition
            {
                Name = "WOOFER_B",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 50.0 }, { "QTS", 0.45 }, { "VAS", 20.0 }, { "SD", 250.0 }
                }
            },
            new SubcircuitDefinition
            {
                Name = "TWEETER_X",
                Metadata = new Dictionary<string, string> { { "TYPE", "tweeters" } },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 1000.0 }, { "QTS", 0.5 }, { "SD", 8.0 }
                }
            }
        };

        speakerDb.PopulateFromSubcircuits(speakers);

        // Act: Search for woofers with specific QTS range
        var searchParams = new SpeakerSearchParameters
        {
            DriverType = new List<string> { "woofers" },
            QtsMin = 0.30,
            QtsMax = 0.40,
            Limit = 10
        };
        var results = speakerDb.SearchSpeakersByParameters(searchParams);

        // Assert: Should find WOOFER_A but not WOOFER_B
        Assert.Single(results);
        Assert.Equal("WOOFER_A", results[0].SubcircuitName);

        // Act: Calculate enclosure for found woofer
        var wooferTs = ConvertToTsParameters(results[0]);
        var design = enclosureService.CalculateSealedBox(wooferTs, 0.707);
        Assert.True(design.VolumeLiters > 0);

        // Cleanup
        try
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
        catch { }
    }

    private static SpeakerTsParameters ConvertToTsParameters(SpeakerSearchResult speaker)
    {
        return new SpeakerTsParameters
        {
            Fs = speaker.TsParameters.TryGetValue("FS", out var fs) ? fs : 0,
            Qts = speaker.TsParameters.TryGetValue("QTS", out var qts) ? qts : 0,
            Qes = speaker.TsParameters.TryGetValue("QES", out var qes) ? qes : 0,
            Qms = speaker.TsParameters.TryGetValue("QMS", out var qms) ? qms : 0,
            Vas = speaker.TsParameters.TryGetValue("VAS", out var vas) ? vas : 0,
            Re = speaker.TsParameters.TryGetValue("RE", out var re) ? re : 0,
            Le = speaker.TsParameters.TryGetValue("LE", out var le) ? le : 0,
            Bl = speaker.TsParameters.TryGetValue("BL", out var bl) ? bl : 0,
            Xmax = speaker.TsParameters.TryGetValue("XMAX", out var xmax) ? xmax : 0,
            Mms = speaker.TsParameters.TryGetValue("MMS", out var mms) ? mms : 0,
            Cms = speaker.TsParameters.TryGetValue("CMS", out var cms) ? cms : 0,
            Sd = speaker.TsParameters.TryGetValue("SD", out var sd) ? sd : 0
        };
    }
}

