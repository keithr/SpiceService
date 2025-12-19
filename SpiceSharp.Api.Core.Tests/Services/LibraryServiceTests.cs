using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using System.IO;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for LibraryService - covers Phase 1A.4 from implementation plan
/// </summary>
public class LibraryServiceTests : IDisposable
{
    private readonly string _testLibDir;
    private readonly LibraryService _service;

    public LibraryServiceTests()
    {
        // Create a temporary directory for test library files
        _testLibDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLibDir);
        
        _service = new LibraryService();
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testLibDir))
        {
            Directory.Delete(_testLibDir, recursive: true);
        }
    }

    [Fact]
    public void IndexLibraries_WithValidDirectory_IndexesAllModels()
    {
        // Arrange
        var libFile1 = Path.Combine(_testLibDir, "diodes.lib");
        File.WriteAllText(libFile1, @"
.MODEL D1N4001 D (IS=1E-14 RS=0.5 N=1.5)
.MODEL D1N4002 D (IS=2E-14 RS=0.6 N=1.6)
");

        var libFile2 = Path.Combine(_testLibDir, "transistors.lib");
        File.WriteAllText(libFile2, @"
.MODEL Q2N3904 NPN (IS=1E-16 BF=100)
");

        // Act
        _service.IndexLibraries(new[] { _testLibDir });

        // Assert
        var models = _service.SearchModels("", null, 100);
        Assert.Equal(3, models.Count);
        Assert.Contains(models, m => m.ModelName == "D1N4001");
        Assert.Contains(models, m => m.ModelName == "D1N4002");
        Assert.Contains(models, m => m.ModelName == "Q2N3904");
    }

    [Fact]
    public void IndexLibraries_WithMultipleDirectories_MergesIndex()
    {
        // Arrange
        var dir1 = Path.Combine(_testLibDir, "dir1");
        var dir2 = Path.Combine(_testLibDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        File.WriteAllText(Path.Combine(dir1, "lib1.lib"), @"
.MODEL D1 D (IS=1E-14)
");

        File.WriteAllText(Path.Combine(dir2, "lib2.lib"), @"
.MODEL D2 D (IS=2E-14)
");

        // Act
        _service.IndexLibraries(new[] { dir1, dir2 });

        // Assert
        var models = _service.SearchModels("", null, 100);
        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.ModelName == "D1");
        Assert.Contains(models, m => m.ModelName == "D2");
    }

    [Fact]
    public void IndexLibraries_WithDuplicateModels_UsesFirst()
    {
        // Arrange
        var libFile1 = Path.Combine(_testLibDir, "lib1.lib");
        File.WriteAllText(libFile1, @"
.MODEL DUP_MODEL D (IS=1E-14 RS=0.5)
");

        var libFile2 = Path.Combine(_testLibDir, "lib2.lib");
        File.WriteAllText(libFile2, @"
.MODEL DUP_MODEL D (IS=2E-14 RS=0.6)
");

        // Act
        _service.IndexLibraries(new[] { _testLibDir });

        // Assert
        var models = _service.SearchModels("DUP_MODEL", null, 100);
        Assert.Single(models);
        // Should use first one (IS=1E-14)
        Assert.True(Math.Abs(models.First().Parameters["IS"] - 1e-14) < 1e-20, 
            $"Expected IS ≈ 1e-14, got {models.First().Parameters["IS"]}");
    }

    [Fact]
    public void IndexLibraries_WithSubdirectories_IndexesRecursively()
    {
        // Arrange
        var subDir = Path.Combine(_testLibDir, "subdir");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(_testLibDir, "root.lib"), @"
.MODEL ROOT_MODEL D (IS=1E-14)
");

        File.WriteAllText(Path.Combine(subDir, "sub.lib"), @"
.MODEL SUB_MODEL D (IS=2E-14)
");

        // Act
        _service.IndexLibraries(new[] { _testLibDir });

        // Assert
        var models = _service.SearchModels("", null, 100);
        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.ModelName == "ROOT_MODEL");
        Assert.Contains(models, m => m.ModelName == "SUB_MODEL");
    }

    [Fact]
    public void SearchModels_WithQuery_FiltersByName()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testLibDir, "test.lib"), @"
.MODEL D1N4001 D (IS=1E-14)
.MODEL D1N4002 D (IS=2E-14)
.MODEL Q2N3904 NPN (IS=1E-16)
");

        _service.IndexLibraries(new[] { _testLibDir });

        // Act
        var results = _service.SearchModels("D1N", null, 100);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, m => Assert.Contains("D1N", m.ModelName));
    }

    [Fact]
    public void SearchModels_WithTypeFilter_FiltersByType()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testLibDir, "test.lib"), @"
.MODEL D1N4001 D (IS=1E-14)
.MODEL Q2N3904 NPN (IS=1E-16)
.MODEL Q2N3906 PNP (IS=1E-16)
");

        _service.IndexLibraries(new[] { _testLibDir });

        // Act
        var diodeResults = _service.SearchModels("", "diode", 100);
        var npnResults = _service.SearchModels("", "bjt_npn", 100);

        // Assert
        Assert.Single(diodeResults);
        Assert.Equal("D1N4001", diodeResults.First().ModelName);
        
        Assert.Single(npnResults);
        Assert.Equal("Q2N3904", npnResults.First().ModelName);
    }

    [Fact]
    public void SearchModels_WithLimit_RespectsLimit()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testLibDir, "test.lib"), @"
.MODEL D1 D (IS=1E-14)
.MODEL D2 D (IS=2E-14)
.MODEL D3 D (IS=3E-14)
.MODEL D4 D (IS=4E-14)
.MODEL D5 D (IS=5E-14)
");

        _service.IndexLibraries(new[] { _testLibDir });

        // Act
        var results = _service.SearchModels("", null, 3);

        // Assert
        Assert.Equal(3, results.Count);
    }
}
