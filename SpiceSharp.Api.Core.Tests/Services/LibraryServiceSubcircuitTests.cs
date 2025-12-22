using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

public class LibraryServiceSubcircuitTests
{
    [Fact]
    public void SearchSubcircuits_WhenNoSubcircuitsIndexed_ReturnsEmptyList()
    {
        // Arrange
        var service = new LibraryService();

        // Act
        var results = service.SearchSubcircuits("test", null, 10);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void SearchSubcircuits_FindsSubcircuitByName()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT irf1010n 1 2 3
M1 9 7 8 8 MM L=100u W=100u
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act
            var results = service.SearchSubcircuits("irf1010n", null, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("irf1010n", results.First().Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_RespectsLimit()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT sub1 1 2
R1 1 2 1K
.ENDS
.SUBCKT sub2 3 4
R2 3 4 2K
.ENDS
.SUBCKT sub3 5 6
R3 5 6 3K
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act
            var results = service.SearchSubcircuits("sub", null, 2);

            // Assert
            Assert.Equal(2, results.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_IsCaseInsensitive()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.SUBCKT MySubcircuit 1 2
R1 1 2 1K
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act
            var results = service.SearchSubcircuits("mysubcircuit", null, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("MySubcircuit", results.First().Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IndexLibraries_IndexesSubcircuitsFromLibraryFiles()
    {
        // Arrange
        var service = new LibraryService();
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
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert
            var results = service.SearchSubcircuits("test_sub", null, 10);
            Assert.Single(results);
            var sub = results.First();
            Assert.Equal("test_sub", sub.Name);
            Assert.Equal(3, sub.Nodes.Count);
            Assert.Contains("R1 1 2 1K", sub.Definition);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IndexLibraries_HandlesMultipleLibraryFiles()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "lib1.lib"), @"
.SUBCKT sub1 1 2
R1 1 2 1K
.ENDS
");
        File.WriteAllText(Path.Combine(tempDir, "lib2.lib"), @"
.SUBCKT sub2 3 4
R2 3 4 2K
.ENDS
");

        try
        {
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert
            var results = service.SearchSubcircuits("", null, 10);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, s => s.Name == "sub1");
            Assert.Contains(results, s => s.Name == "sub2");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IndexLibraries_HandlesDuplicateSubcircuitNames_FirstWins()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "lib1.lib"), @"
.SUBCKT duplicate 1 2
R1 1 2 1K
.ENDS
");
        File.WriteAllText(Path.Combine(tempDir, "lib2.lib"), @"
.SUBCKT duplicate 3 4
R2 3 4 2K
.ENDS
");

        try
        {
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert
            var results = service.SearchSubcircuits("duplicate", null, 10);
            Assert.Single(results);
            // First one should win (lib1.lib)
            Assert.Contains("R1 1 2 1K", results.First().Definition);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IndexLibraries_HandlesFilesWithBothModelsAndSubcircuits()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
.MODEL D1N4148 D (IS=1E-14)
.SUBCKT test_sub 1 2
R1 1 2 1K
.ENDS
.MODEL Q2N3904 NPN (IS=1E-16)
");

        try
        {
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert
            var subcircuits = service.SearchSubcircuits("test_sub", null, 10);
            var models = service.SearchModels("D1N4148", null, 10);
            
            Assert.Single(subcircuits);
            Assert.Single(models);
            Assert.Equal("test_sub", subcircuits.First().Name);
            Assert.Equal("D1N4148", models.First().ModelName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IndexLibraries_IndexesSubcircuitsWithMetadata()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Peerless
* TYPE: woofers
* DIAMETER: 6.5
* IMPEDANCE: 8
* PRICE: 59.98
.SUBCKT 264_1148 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.65mH
.ENDS
");

        try
        {
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert
            var results = service.SearchSubcircuits("264_1148", null, 10);
            Assert.Single(results);
            var sub = results.First();
            Assert.Equal("264_1148", sub.Name);
            Assert.Equal("Peerless", sub.Metadata["MANUFACTURER"]);
            Assert.Equal("woofers", sub.Metadata["TYPE"]);
            Assert.Equal("6.5", sub.Metadata["DIAMETER"]);
            Assert.Equal("8", sub.Metadata["IMPEDANCE"]);
            Assert.Equal("59.98", sub.Metadata["PRICE"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_ReturnsSubcircuitsWithTsParameters()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* FS: 42.18
* QTS: 0.35
* QES: 0.38
* QMS: 4.92
* VAS: 11.2
* RE: 2.73
* LE: 0.65
* BL: 8.27
.SUBCKT speaker_test PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.65mH
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act
            var results = service.SearchSubcircuits("speaker_test", null, 10);

            // Assert
            Assert.Single(results);
            var sub = results.First();
            Assert.Equal("speaker_test", sub.Name);
            Assert.Equal(42.18, sub.TsParameters["FS"]);
            Assert.Equal(0.35, sub.TsParameters["QTS"]);
            Assert.Equal(0.38, sub.TsParameters["QES"]);
            Assert.Equal(4.92, sub.TsParameters["QMS"]);
            Assert.Equal(11.2, sub.TsParameters["VAS"]);
            Assert.Equal(2.73, sub.TsParameters["RE"]);
            Assert.Equal(0.65, sub.TsParameters["LE"]);
            Assert.Equal(8.27, sub.TsParameters["BL"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_ReturnsSubcircuitsWithMetadata()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Peerless
* PART_NUMBER: 264-1148
* TYPE: woofers
* DIAMETER: 6.5
* IMPEDANCE: 8
* POWER_RMS: 75
* SENSITIVITY: 88.5
* PRICE: 59.98
.SUBCKT 264_1148 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.65mH
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act
            var results = service.SearchSubcircuits("264_1148", null, 10);

            // Assert
            Assert.Single(results);
            var sub = results.First();
            Assert.Equal("264_1148", sub.Name);
            Assert.NotNull(sub.Metadata);
            Assert.Equal("Peerless", sub.Metadata["MANUFACTURER"]);
            Assert.Equal("264-1148", sub.Metadata["PART_NUMBER"]);
            Assert.Equal("woofers", sub.Metadata["TYPE"]);
            Assert.Equal("6.5", sub.Metadata["DIAMETER"]);
            Assert.Equal("8", sub.Metadata["IMPEDANCE"]);
            Assert.Equal("75", sub.Metadata["POWER_RMS"]);
            Assert.Equal("88.5", sub.Metadata["SENSITIVITY"]);
            Assert.Equal("59.98", sub.Metadata["PRICE"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IndexLibraries_PopulatesDatabase()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDbService = new SpeakerDatabaseService(dbPath);
        var service = new LibraryService(speakerDbService);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Peerless
* TYPE: woofers
* FS: 42.18
* QTS: 0.35
.SUBCKT 264_1148 PLUS MINUS
Re PLUS 1 2.73
Le 1 2 0.65mH
.ENDS
");

        try
        {
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert - verify database was populated
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM speakers WHERE subcircuit_name = '264_1148'";
                var count = Convert.ToInt32(command.ExecuteScalar());
                Assert.Equal(1, count);
            }

            // Force cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        finally
        {
            Directory.Delete(tempDir, true);
            // Retry deletion
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (File.Exists(dbPath))
                    {
                        File.Delete(dbPath);
                    }
                    break;
                }
                catch (IOException)
                {
                    if (i < 4)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }
    }

    [Fact]
    public void IndexLibraries_DatabaseContainsAllIndexedSpeakers()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
        var speakerDbService = new SpeakerDatabaseService(dbPath);
        var service = new LibraryService(speakerDbService);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "lib1.lib"), @"
* TYPE: woofers
* FS: 40.0
.SUBCKT speaker1 PLUS MINUS
Re PLUS 1 2.0
.ENDS
");
        File.WriteAllText(Path.Combine(tempDir, "lib2.lib"), @"
* TYPE: tweeters
* FS: 2000.0
.SUBCKT speaker2 PLUS MINUS
Re PLUS 1 3.0
.ENDS
");

        try
        {
            // Act
            service.IndexLibraries(new[] { tempDir });

            // Assert - verify both speakers are in database
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM speakers";
                var count = Convert.ToInt32(command.ExecuteScalar());
                Assert.Equal(2, count);

                command.CommandText = "SELECT subcircuit_name FROM speakers ORDER BY subcircuit_name";
                using var reader = command.ExecuteReader();
                var names = new List<string>();
                while (reader.Read())
                {
                    names.Add(reader.GetString(0));
                }
                Assert.Contains("speaker1", names);
                Assert.Contains("speaker2", names);
            }

            // Force cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        finally
        {
            Directory.Delete(tempDir, true);
            // Retry deletion
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (File.Exists(dbPath))
                    {
                        File.Delete(dbPath);
                    }
                    break;
                }
                catch (IOException)
                {
                    if (i < 4)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }
    }

    [Fact]
    public void SearchSubcircuits_SearchesProductNameMetadata()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Dayton Audio
* PART_NUMBER: 275-030
* PRODUCT_NAME: Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* TYPE: tweeters
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 6.0
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act - Search by product name
            var results = service.SearchSubcircuits("ND20FA", null, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("275_030", results.First().Name);
            Assert.Equal("Dayton Audio ND20FA-6 3/4\" Soft Dome Neodymium Tweeter", results.First().Metadata["PRODUCT_NAME"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_SearchesPartNumberMetadata()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: HiVi
* PART_NUMBER: 297-429
* PRODUCT_NAME: HiVi B4N 4"" Aluminum Cone Woofer
* TYPE: woofers
.SUBCKT 297_429 PLUS MINUS
Re PLUS 1 8.0
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act - Search by part number
            var results = service.SearchSubcircuits("297-429", null, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("297_429", results.First().Name);
            Assert.Equal("297-429", results.First().Metadata["PART_NUMBER"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_SearchesManufacturerMetadata()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Dayton Audio
* PART_NUMBER: 275-030
* PRODUCT_NAME: Dayton Audio ND20FA-6 3/4"" Soft Dome Neodymium Tweeter
* TYPE: tweeters
.SUBCKT 275_030 PLUS MINUS
Re PLUS 1 6.0
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act - Search by manufacturer
            var results = service.SearchSubcircuits("Dayton", null, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("275_030", results.First().Name);
            Assert.Equal("Dayton Audio", results.First().Metadata["MANUFACTURER"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_FiltersByType()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "woofer.lib"), @"
* MANUFACTURER: HiVi
* PART_NUMBER: 297-429
* PRODUCT_NAME: HiVi B4N 4"" Aluminum Cone Woofer
* TYPE: woofers
.SUBCKT 297_429 PLUS MINUS
Re PLUS 1 8.0
.ENDS
");
        File.WriteAllText(Path.Combine(tempDir, "mosfet.lib"), @"
* MANUFACTURER: Infineon
* TYPE: mosfet
.SUBCKT B4N PLUS MINUS GATE
M1 PLUS GATE MINUS MINUS NMOS
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act - Search for "B4N" with type filter "woofers"
            var wooferResults = service.SearchSubcircuits("B4N", "woofers", 10);
            
            // Act - Search for "B4N" with type filter "mosfet"
            var mosfetResults = service.SearchSubcircuits("B4N", "mosfet", 10);

            // Assert
            Assert.Single(wooferResults);
            Assert.Equal("297_429", wooferResults.First().Name);
            Assert.Equal("woofers", wooferResults.First().Metadata["TYPE"]);
            
            Assert.Single(mosfetResults);
            Assert.Equal("B4N", mosfetResults.First().Name);
            Assert.Equal("mosfet", mosfetResults.First().Metadata["TYPE"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_TypeFilterIsCaseInsensitive()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Peerless
* TYPE: woofers
.SUBCKT test_woofer PLUS MINUS
Re PLUS 1 8.0
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act - Search with different case type filter
            var results1 = service.SearchSubcircuits("test", "WOOFERS", 10);
            var results2 = service.SearchSubcircuits("test", "Woofers", 10);
            var results3 = service.SearchSubcircuits("test", "woofers", 10);

            // Assert - All should find the same result
            Assert.Single(results1);
            Assert.Single(results2);
            Assert.Single(results3);
            Assert.Equal("test_woofer", results1.First().Name);
            Assert.Equal("test_woofer", results2.First().Name);
            Assert.Equal("test_woofer", results3.First().Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchSubcircuits_ReturnsEmptyWhenTypeFilterDoesNotMatch()
    {
        // Arrange
        var service = new LibraryService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var libFile = Path.Combine(tempDir, "test.lib");
        File.WriteAllText(libFile, @"
* MANUFACTURER: Peerless
* TYPE: woofers
.SUBCKT test_woofer PLUS MINUS
Re PLUS 1 8.0
.ENDS
");

        try
        {
            service.IndexLibraries(new[] { tempDir });

            // Act - Search with wrong type filter
            var results = service.SearchSubcircuits("test", "tweeters", 10);

            // Assert
            Assert.Empty(results);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

