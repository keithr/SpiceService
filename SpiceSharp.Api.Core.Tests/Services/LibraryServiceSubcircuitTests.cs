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
        var results = service.SearchSubcircuits("test", 10);

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
            var results = service.SearchSubcircuits("irf1010n", 10);

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
            var results = service.SearchSubcircuits("sub", 2);

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
            var results = service.SearchSubcircuits("mysubcircuit", 10);

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
            var results = service.SearchSubcircuits("test_sub", 10);
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
            var results = service.SearchSubcircuits("", 10);
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
            var results = service.SearchSubcircuits("duplicate", 10);
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
            var subcircuits = service.SearchSubcircuits("test_sub", 10);
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
}

