using Microsoft.Data.Sqlite;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests to validate speaker database service handles real-world scenarios correctly
/// </summary>
public class SpeakerDatabaseServiceValidationTests
{
    private string GetTestDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
    }

    private void CleanupDatabase(string dbPath)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

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

    [Fact]
    public void PopulateFromSubcircuits_ShouldOnlyContainSpeakers_NotTubesOrDiodes()
    {
        // Arrange - Simulate real library with mixed content
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            // Speaker (should be included)
            new SubcircuitDefinition
            {
                Name = "264_1148",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "MANUFACTURER", "Peerless" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 42.18 },
                    { "QTS", 0.35 }
                }
            },
            // Tube (should be excluded)
            new SubcircuitDefinition
            {
                Name = "12AT7A",
                Nodes = new List<string> { "1", "2", "3" },
                Definition = "R1 1 2 1K",
                Metadata = new Dictionary<string, string>(),
                TsParameters = new Dictionary<string, double>()
            },
            // Diode (should be excluded)
            new SubcircuitDefinition
            {
                Name = "1N5817",
                Nodes = new List<string> { "A", "K" },
                Definition = "D1 A K D1N5817",
                Metadata = new Dictionary<string, string>(),
                TsParameters = new Dictionary<string, double>()
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert - Only speaker should be in database
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT subcircuit_name FROM speakers";
                using var reader = command.ExecuteReader();
                var names = new List<string>();
                while (reader.Read())
                {
                    names.Add(reader.GetString(0));
                }

                Assert.Single(names);
                Assert.Equal("264_1148", names[0]);
                Assert.DoesNotContain("12AT7A", names);
                Assert.DoesNotContain("1N5817", names);
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_ShouldParseDiameterCorrectly()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_6_5",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "DIAMETER", "6.5" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker_8",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "DIAMETER", "8.0" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 45.0 } }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT subcircuit_name, diameter FROM speakers WHERE subcircuit_name = 'speaker_6_5'";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("speaker_6_5", reader.GetString(0));
                Assert.False(reader.IsDBNull(1));
                Assert.Equal(6.5, reader.GetDouble(1));
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_ShouldParseImpedanceCorrectly()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_8ohm",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "IMPEDANCE", "8" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker_4ohm",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "IMPEDANCE", "4" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 45.0 } }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT subcircuit_name, impedance FROM speakers WHERE subcircuit_name = 'speaker_8ohm'";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("speaker_8ohm", reader.GetString(0));
                Assert.False(reader.IsDBNull(1));
                Assert.Equal(8, reader.GetInt32(1));
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_ShouldParseSensitivityCorrectly()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_88db",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "SENSITIVITY", "88.5" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert - Sensitivity should be 88.5, not 1.0
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT subcircuit_name, sensitivity FROM speakers WHERE subcircuit_name = 'speaker_88db'";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("speaker_88db", reader.GetString(0));
                Assert.False(reader.IsDBNull(1));
                var sensitivity = reader.GetDouble(1);
                Assert.NotEqual(1.0, sensitivity);
                Assert.Equal(88.5, sensitivity);
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_ShouldParseSDCorrectly()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_test",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 40.0 },
                    { "SD", 214.0 } // Real piston area in cm²
                }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert - SD should be 214.0, not 2.0
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT subcircuit_name, sd FROM speakers WHERE subcircuit_name = 'speaker_test'";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("speaker_test", reader.GetString(0));
                Assert.False(reader.IsDBNull(1));
                var sd = reader.GetDouble(1);
                Assert.NotEqual(2.0, sd);
                Assert.Equal(214.0, sd);
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_ShouldHandleCaseInsensitiveParameterNames()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_test",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 42.18 },
                    { "QTS", 0.35 },
                    { "VAS", 11.2 }
                }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act - Search using lowercase parameter names (as MCP tool would)
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                FsMin = 40.0,
                FsMax = 45.0,
                QtsMin = 0.3,
                QtsMax = 0.4,
                VasMin = 10.0,
                VasMax = 12.0
            });

            // Assert - Should find the speaker
            Assert.Single(results);
            Assert.Equal("speaker_test", results.First().SubcircuitName);
            Assert.True(results.First().TsParameters.ContainsKey("FS"));
            Assert.Equal(42.18, results.First().TsParameters["FS"]);
            Assert.True(results.First().TsParameters.ContainsKey("QTS"));
            Assert.Equal(0.35, results.First().TsParameters["QTS"]);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_ShouldFilterByDiameter()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_6_5",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "DIAMETER", "6.5" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker_8",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "DIAMETER", "8.0" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 45.0 } }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act - Search for 6-7 inch speakers
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                DiameterMin = 6.0,
                DiameterMax = 7.0
            });

            // Assert - Should find only 6.5" speaker
            Assert.Single(results);
            Assert.Equal("speaker_6_5", results.First().SubcircuitName);
            Assert.Equal(6.5, results.First().Diameter);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_ShouldFilterByImpedance()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_8ohm",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "IMPEDANCE", "8" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker_4ohm",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "IMPEDANCE", "4" }
                },
                TsParameters = new Dictionary<string, double> { { "FS", 45.0 } }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act - Search for 8 ohm speakers
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                Impedance = 8
            });

            // Assert - Should find only 8 ohm speaker
            Assert.Single(results);
            Assert.Equal("speaker_8ohm", results.First().SubcircuitName);
            Assert.Equal(8, results.First().Impedance);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_ShouldFilterByQts()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker_low_qts",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 40.0 },
                    { "QTS", 0.35 }
                }
            },
            new SubcircuitDefinition
            {
                Name = "speaker_high_qts",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 45.0 },
                    { "QTS", 0.7 }
                }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act - Search for QTS 0.3-0.4
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                QtsMin = 0.3,
                QtsMax = 0.4
            });

            // Assert - Should find only low QTS speaker
            Assert.Single(results);
            Assert.Equal("speaker_low_qts", results.First().SubcircuitName);
            Assert.True(results.First().TsParameters.ContainsKey("QTS"));
            Assert.Equal(0.35, results.First().TsParameters["QTS"]);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }
}

