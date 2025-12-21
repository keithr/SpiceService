using Microsoft.Data.Sqlite;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

public class SpeakerDatabaseServiceTests
{
    private string GetTestDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"test_speakers_{Guid.NewGuid()}.db");
    }

    private void CleanupDatabase(string dbPath)
    {
        // Force garbage collection to ensure connections are disposed
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Retry deletion in case file is still locked
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
    public void InitializeDatabase_CreatesSchemaCorrectly()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);

        try
        {
            // Act
            service.InitializeDatabase();

            // Assert - verify table exists
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='speakers'";
                var result = command.ExecuteScalar();

                Assert.NotNull(result);
                Assert.Equal("speakers", result.ToString());

                // Verify indexes exist
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'idx_%'";
                using var reader = command.ExecuteReader();
                var indexes = new List<string>();
                while (reader.Read())
                {
                    indexes.Add(reader.GetString(0));
                }

                Assert.Contains("idx_type", indexes);
                Assert.Contains("idx_diameter", indexes);
                Assert.Contains("idx_impedance", indexes);
                Assert.Contains("idx_fs", indexes);
                Assert.Contains("idx_qts", indexes);
                Assert.Contains("idx_vas", indexes);
                Assert.Contains("idx_price", indexes);
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_InsertsSpeakerData()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "264_1148",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "MANUFACTURER", "Peerless" },
                    { "TYPE", "woofers" },
                    { "DIAMETER", "6.5" },
                    { "IMPEDANCE", "8" },
                    { "PRICE", "59.98" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 42.18 },
                    { "QTS", 0.35 },
                    { "VAS", 11.2 },
                    { "RE", 2.73 }
                }
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
                command.CommandText = "SELECT COUNT(*) FROM speakers";
                var count = Convert.ToInt32(command.ExecuteScalar());
                Assert.Equal(1, count);

                command.CommandText = @"
                    SELECT subcircuit_name, manufacturer, type, fs, qts, vas, re
                    FROM speakers WHERE subcircuit_name = '264_1148'
                ";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("264_1148", reader.GetString(0));
                Assert.Equal("Peerless", reader.GetString(1));
                Assert.Equal("woofers", reader.GetString(2));
                Assert.Equal(42.18, reader.GetDouble(3));
                Assert.Equal(0.35, reader.GetDouble(4));
                Assert.Equal(11.2, reader.GetDouble(5));
                Assert.Equal(2.73, reader.GetDouble(6));
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_HandlesMissingTsParameters()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "test_speaker",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string>
                {
                    { "TYPE", "woofers" },
                    { "MANUFACTURER", "Test" }
                },
                TsParameters = new Dictionary<string, double>
                {
                    { "FS", 42.18 }
                    // Missing other T/S parameters
                }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert - should not throw and should insert with NULL for missing values
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT fs, qts, vas FROM speakers WHERE subcircuit_name = 'test_speaker'
                ";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal(42.18, reader.GetDouble(0));
                Assert.True(reader.IsDBNull(1)); // qts should be NULL
                Assert.True(reader.IsDBNull(2)); // vas should be NULL
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_HandlesDuplicateNames()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "duplicate",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "MANUFACTURER", "First" } },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "duplicate",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "MANUFACTURER", "Second" } },
                TsParameters = new Dictionary<string, double> { { "FS", 50.0 } }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert - should have only one record (last one wins due to INSERT OR REPLACE)
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM speakers WHERE subcircuit_name = 'duplicate'";
                var count = Convert.ToInt32(command.ExecuteScalar());
                Assert.Equal(1, count);

                // Should be the second one (last one wins)
                command.CommandText = "SELECT manufacturer, fs FROM speakers WHERE subcircuit_name = 'duplicate'";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("Second", reader.GetString(0));
                Assert.Equal(50.0, reader.GetDouble(1));
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void PopulateFromSubcircuits_SkipsSubcircuitsWithoutMetadataOrTsParameters()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "regular_sub",
                Nodes = new List<string> { "1", "2" },
                Definition = "R1 1 2 1K"
                // No metadata or T/S parameters
            },
            new SubcircuitDefinition
            {
                Name = "speaker_sub",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double> { { "FS", 42.18 } }
            }
        };

        try
        {
            // Act
            service.PopulateFromSubcircuits(subcircuits);

            // Assert - should only have the speaker subcircuit
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM speakers";
                var count = Convert.ToInt32(command.ExecuteScalar());
                Assert.Equal(1, count);

                command.CommandText = "SELECT subcircuit_name FROM speakers";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("speaker_sub", reader.GetString(0));
            }
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_FiltersByDriverType()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "woofer1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "tweeter1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "tweeters" } },
                TsParameters = new Dictionary<string, double> { { "FS", 2000.0 } }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                DriverType = new List<string> { "woofers" }
            });

            // Assert
            Assert.Single(results);
            Assert.Equal("woofer1", results.First().SubcircuitName);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_FiltersByDiameterRange()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "DIAMETER", "6.5" } },
                TsParameters = new Dictionary<string, double>()
            },
            new SubcircuitDefinition
            {
                Name = "speaker2",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "DIAMETER", "8.0" } },
                TsParameters = new Dictionary<string, double>()
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                DiameterMin = 6.0,
                DiameterMax = 7.0
            });

            // Assert
            Assert.Single(results);
            Assert.Equal("speaker1", results.First().SubcircuitName);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_FiltersByFsRange()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker2",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double> { { "FS", 60.0 } }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                FsMin = 35.0,
                FsMax = 45.0
            });

            // Assert
            Assert.Single(results);
            Assert.Equal("speaker1", results.First().SubcircuitName);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_FiltersByQtsRange()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double> { { "QTS", 0.35 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker2",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
                TsParameters = new Dictionary<string, double> { { "QTS", 0.7 } }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                QtsMin = 0.3,
                QtsMax = 0.4
            });

            // Assert
            Assert.Single(results);
            Assert.Equal("speaker1", results.First().SubcircuitName);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_FiltersByMultipleParameters()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "DIAMETER", "6.5" } },
                TsParameters = new Dictionary<string, double> { { "FS", 40.0 }, { "QTS", 0.35 } }
            },
            new SubcircuitDefinition
            {
                Name = "speaker2",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "tweeters" }, { "DIAMETER", "1.0" } },
                TsParameters = new Dictionary<string, double> { { "FS", 2000.0 }, { "QTS", 0.5 } }
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                DriverType = new List<string> { "woofers" },
                DiameterMin = 6.0,
                FsMin = 35.0,
                FsMax = 45.0,
                QtsMin = 0.3,
                QtsMax = 0.4
            });

            // Assert
            Assert.Single(results);
            Assert.Equal("speaker1", results.First().SubcircuitName);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_RespectsLimit()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = Enumerable.Range(1, 10).Select(i => new SubcircuitDefinition
        {
            Name = $"speaker{i}",
            Nodes = new List<string> { "PLUS", "MINUS" },
            Definition = "Re PLUS 1 2.73",
            Metadata = new Dictionary<string, string> { { "TYPE", "woofers" } },
            TsParameters = new Dictionary<string, double> { { "FS", 40.0 + i } }
        }).ToList();

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                Limit = 5
            });

            // Assert
            Assert.Equal(5, results.Count);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_SortsCorrectly()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        var subcircuits = new List<SubcircuitDefinition>
        {
            new SubcircuitDefinition
            {
                Name = "speaker1",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 2.73",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "PRICE", "100.0" } },
                TsParameters = new Dictionary<string, double>()
            },
            new SubcircuitDefinition
            {
                Name = "speaker2",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 3.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "PRICE", "50.0" } },
                TsParameters = new Dictionary<string, double>()
            },
            new SubcircuitDefinition
            {
                Name = "speaker3",
                Nodes = new List<string> { "PLUS", "MINUS" },
                Definition = "Re PLUS 1 4.0",
                Metadata = new Dictionary<string, string> { { "TYPE", "woofers" }, { "PRICE", "75.0" } },
                TsParameters = new Dictionary<string, double>()
            }
        };

        service.PopulateFromSubcircuits(subcircuits);

        try
        {
            // Act - sort by price ascending
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                SortBy = "price",
                SortDirection = "asc"
            });

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("speaker2", results[0].SubcircuitName);
            Assert.Equal("speaker3", results[1].SubcircuitName);
            Assert.Equal("speaker1", results[2].SubcircuitName);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }

    [Fact]
    public void SearchSpeakersByParameters_HandlesEmptyResults()
    {
        // Arrange
        var dbPath = GetTestDatabasePath();
        var service = new SpeakerDatabaseService(dbPath);
        service.InitializeDatabase();

        try
        {
            // Act
            var results = service.SearchSpeakersByParameters(new SpeakerSearchParameters
            {
                DriverType = new List<string> { "nonexistent" }
            });

            // Assert
            Assert.Empty(results);
        }
        finally
        {
            CleanupDatabase(dbPath);
        }
    }
}

