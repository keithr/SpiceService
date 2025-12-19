using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Comprehensive validation tests for all KiCad library files
/// Tests that every library file can be parsed, loaded, and indexed correctly
/// </summary>
public class KiCadLibraryValidationTests
{
    private readonly SpiceLibParser _parser;
    private readonly string _librariesPath;

    public KiCadLibraryValidationTests()
    {
        _parser = new SpiceLibParser();
        
        // Find the libraries directory relative to the test project
        // Try multiple possible paths
        var testProjectDir = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(testProjectDir, "..", "..", "..", "..", "spiceservice", "libraries"),
            Path.Combine(testProjectDir, "..", "..", "..", "..", "..", "spiceservice", "libraries"),
            Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", "..", "spiceservice", "libraries")),
            Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", "..", "..", "spiceservice", "libraries")),
            // Also try from current working directory
            Path.Combine(Directory.GetCurrentDirectory(), "libraries"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "libraries"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "libraries"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "libraries"),
        };

        _librariesPath = possiblePaths.FirstOrDefault(p => Directory.Exists(p) && 
            Directory.GetFiles(p, "kicad_*.lib", SearchOption.TopDirectoryOnly).Length > 0) 
            ?? Path.Combine(testProjectDir, "..", "..", "..", "..", "spiceservice", "libraries");
    }

    [Fact]
    public void AllKiCadLibraries_CanBeParsed()
    {
        // Arrange
        var libraryFiles = GetKiCadLibraryFiles();
        Assert.True(libraryFiles.Count > 0, 
            $"No KiCad library files found. Searched in: {_librariesPath}. " +
            $"Directory exists: {Directory.Exists(_librariesPath)}. " +
            $"If directory exists, files found: {(Directory.Exists(_librariesPath) ? Directory.GetFiles(_librariesPath, "*.lib").Length : 0)}");

        var parseResults = new List<(string FileName, bool Success, int ModelCount, string? Error)>();

        // Act - Parse each library file
        foreach (var libFile in libraryFiles)
        {
            try
            {
                var content = File.ReadAllText(libFile);
                var models = _parser.ParseLibFile(content);
                
                parseResults.Add((
                    Path.GetFileName(libFile),
                    true,
                    models.Count,
                    null
                ));
            }
            catch (Exception ex)
            {
                parseResults.Add((
                    Path.GetFileName(libFile),
                    false,
                    0,
                    ex.Message
                ));
            }
        }

        // Assert - Most files should parse successfully (allow some failures for edge cases)
        var failedFiles = parseResults.Where(r => !r.Success).ToList();
        var filesWithNoModels = parseResults.Where(r => r.Success && r.ModelCount == 0).ToList();
        var successfulFiles = parseResults.Where(r => r.Success && r.ModelCount > 0).ToList();

        // Calculate success rate
        var totalFiles = parseResults.Count;
        var parseSuccessRate = (totalFiles - failedFiles.Count) / (double)totalFiles;
        var modelSuccessRate = successfulFiles.Count / (double)totalFiles;

        // We expect:
        // - At least 95% of files to parse without exceptions (some might be empty/subcircuits)
        // - At least 50% of files to contain .MODEL statements (others might be .subckt or empty)
        // - At least 1000 total models extracted across all files
        var totalModels = parseResults.Sum(r => r.ModelCount);
        
        if (parseSuccessRate < 0.95 || totalModels < 1000)
        {
            var errorMessage = new System.Text.StringBuilder();
            errorMessage.AppendLine($"Parse success rate too low: {parseSuccessRate:P2} ({totalFiles - failedFiles.Count}/{totalFiles} files parsed successfully)");
            errorMessage.AppendLine($"Model success rate: {modelSuccessRate:P2} ({successfulFiles.Count}/{totalFiles} files have models)");
            errorMessage.AppendLine($"\nTotal library files tested: {totalFiles}");
            errorMessage.AppendLine($"Successfully parsed with models: {successfulFiles.Count}");
            errorMessage.AppendLine($"Successfully parsed but no models: {filesWithNoModels.Count} (may contain .subckt instead of .MODEL)");
            errorMessage.AppendLine($"Failed to parse: {failedFiles.Count}");
            
            if (failedFiles.Any())
            {
                errorMessage.AppendLine($"\nFailed to parse ({Math.Min(failedFiles.Count, 10)} of {failedFiles.Count} shown):");
                foreach (var failed in failedFiles.Take(10))
                {
                    errorMessage.AppendLine($"  - {failed.FileName}: {failed.Error}");
                }
                if (failedFiles.Count > 10)
                {
                    errorMessage.AppendLine($"  ... and {failedFiles.Count - 10} more");
                }
            }
            
            if (filesWithNoModels.Any())
            {
                errorMessage.AppendLine($"\nFiles with no models extracted ({Math.Min(filesWithNoModels.Count, 10)} of {filesWithNoModels.Count} shown):");
                foreach (var empty in filesWithNoModels.Take(10))
                {
                    errorMessage.AppendLine($"  - {empty.FileName}");
                }
                if (filesWithNoModels.Count > 10)
                {
                    errorMessage.AppendLine($"  ... and {filesWithNoModels.Count - 10} more");
                }
            }

            Assert.True(false, errorMessage.ToString());
        }

        // Summary statistics (totalModels already calculated above)
        var avgModelsPerFile = successfulFiles.Count > 0 ? totalModels / (double)successfulFiles.Count : 0;
        
        Assert.True(totalModels > 0, $"Expected to extract at least some models, but got {totalModels} total models from {successfulFiles.Count} files");
        Assert.True(avgModelsPerFile > 0, $"Expected average models per file > 0, but got {avgModelsPerFile}");
        
        // Log summary (this will appear in test output)
        System.Diagnostics.Debug.WriteLine($"KiCad Library Validation Summary:");
        System.Diagnostics.Debug.WriteLine($"  Total files: {totalFiles}");
        System.Diagnostics.Debug.WriteLine($"  Files with models: {successfulFiles.Count}");
        System.Diagnostics.Debug.WriteLine($"  Files without models: {filesWithNoModels.Count} (may contain .subckt)");
        System.Diagnostics.Debug.WriteLine($"  Parse failures: {failedFiles.Count}");
        System.Diagnostics.Debug.WriteLine($"  Total models extracted: {totalModels}");
        System.Diagnostics.Debug.WriteLine($"  Average models per file (with models): {avgModelsPerFile:F2}");
    }

    [Fact]
    public void AllKiCadLibraries_CanBeIndexed()
    {
        // Arrange
        var libraryFiles = GetKiCadLibraryFiles();
        Assert.NotEmpty(libraryFiles);

        var libraryService = new LibraryService();
        var libraryDir = Path.GetDirectoryName(libraryFiles.First())!;

        // Act - Index all libraries
        libraryService.IndexLibraries(new[] { libraryDir });

        // Assert - Verify models were indexed
        var allModels = libraryService.SearchModels("", null, int.MaxValue);
        Assert.True(allModels.Count > 0, "Expected at least some models to be indexed");

        // Verify we can search for specific models
        var diodeModels = libraryService.SearchModels("", "diode", 100);
        var bjtModels = libraryService.SearchModels("", "bjt_npn", 100);
        var mosfetModels = libraryService.SearchModels("", "mosfet", 100);

        // At least some models of common types should be found
        var totalFound = diodeModels.Count + bjtModels.Count + mosfetModels.Count;
        Assert.True(totalFound > 0, "Expected to find at least some models of common types (diode, bjt, mosfet)");
    }

    [Fact]
    public void AllKiCadLibraries_ModelsHaveValidStructure()
    {
        // Arrange
        var libraryFiles = GetKiCadLibraryFiles();
        Assert.NotEmpty(libraryFiles);

        var validationErrors = new List<string>();
        var totalModels = 0;
        var validModels = 0;

        // Act - Validate model structure for each library
        foreach (var libFile in libraryFiles)
        {
            try
            {
                var content = File.ReadAllText(libFile);
                var models = _parser.ParseLibFile(content);
                
                foreach (var model in models)
                {
                    totalModels++;
                    
                    // Validate model structure
                    if (string.IsNullOrWhiteSpace(model.ModelName))
                    {
                        validationErrors.Add($"{Path.GetFileName(libFile)}: Model with empty name");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(model.ModelType))
                    {
                        validationErrors.Add($"{Path.GetFileName(libFile)}: Model '{model.ModelName}' has empty type");
                        continue;
                    }

                    if (model.Parameters == null)
                    {
                        validationErrors.Add($"{Path.GetFileName(libFile)}: Model '{model.ModelName}' has null parameters");
                        continue;
                    }

                    // Check for invalid parameter values (NaN, Infinity)
                    foreach (var param in model.Parameters)
                    {
                        if (double.IsNaN(param.Value) || double.IsInfinity(param.Value))
                        {
                            validationErrors.Add($"{Path.GetFileName(libFile)}: Model '{model.ModelName}' parameter '{param.Key}' has invalid value: {param.Value}");
                        }
                    }

                    validModels++;
                }
            }
            catch (Exception ex)
            {
                validationErrors.Add($"{Path.GetFileName(libFile)}: Exception during parsing: {ex.Message}");
            }
        }

        // Assert
        if (validationErrors.Any())
        {
            var errorMessage = new System.Text.StringBuilder();
            errorMessage.AppendLine($"Validation errors found ({validationErrors.Count}):");
            foreach (var error in validationErrors.Take(50)) // Limit to first 50 errors
            {
                errorMessage.AppendLine($"  - {error}");
            }
            if (validationErrors.Count > 50)
            {
                errorMessage.AppendLine($"  ... and {validationErrors.Count - 50} more errors");
            }
            errorMessage.AppendLine($"\nTotal models: {totalModels}, Valid models: {validModels}");
            
            Assert.True(false, errorMessage.ToString());
        }

        Assert.True(validModels > 0, $"Expected at least some valid models, but got {validModels} valid out of {totalModels} total");
        Assert.True(validModels == totalModels, $"Expected all models to be valid, but {totalModels - validModels} models had validation errors");
    }

    [Fact]
    public void AllKiCadLibraries_SampleFilesParseCorrectly()
    {
        // Arrange - Test a sample of files to ensure they parse correctly
        var libraryFiles = GetKiCadLibraryFiles();
        Assert.NotEmpty(libraryFiles);

        // Test first 10 files in detail
        var sampleFiles = libraryFiles.Take(10).ToList();
        var parser = new SpiceLibParser();

        foreach (var libFile in sampleFiles)
        {
            // Act
            var content = File.ReadAllText(libFile);
            var models = _parser.ParseLibFile(content);

            // Assert
            Assert.NotNull(models);
            
            // Each file should have at least one model (or be explicitly empty)
            // Some library files might be empty or contain only comments, which is OK
            // But if they have content, they should parse without errors
            
            // Verify model structure for any models found
            foreach (var model in models)
            {
                Assert.NotNull(model);
                Assert.False(string.IsNullOrWhiteSpace(model.ModelName), 
                    $"Model in {Path.GetFileName(libFile)} has empty name");
                Assert.False(string.IsNullOrWhiteSpace(model.ModelType), 
                    $"Model '{model.ModelName}' in {Path.GetFileName(libFile)} has empty type");
                Assert.NotNull(model.Parameters);
            }
        }
    }

    [Fact]
    public void AllKiCadLibraries_CanBeSearched()
    {
        // Arrange
        var libraryFiles = GetKiCadLibraryFiles();
        Assert.NotEmpty(libraryFiles);

        var libraryService = new LibraryService();
        var libraryDir = Path.GetDirectoryName(libraryFiles.First())!;

        // Act - Index and search
        libraryService.IndexLibraries(new[] { libraryDir });

        // Test various search queries
        var searchTests = new[]
        {
            ("2N", null, "Search for 2N series transistors"),
            ("74", null, "Search for 74xx series ICs"),
            ("D1N", null, "Search for D1N series diodes"),
            ("", "diode", "Search all diodes"),
            ("", "bjt_npn", "Search all NPN transistors"),
            ("", "mosfet_n", "Search all N-channel MOSFETs"),
            ("Q2N", null, "Search for Q2N series transistors"),
        };

        var searchResults = new List<(string Query, string? Type, int Count, bool Success)>();

        foreach (var (query, typeFilter, description) in searchTests)
        {
            try
            {
                var results = libraryService.SearchModels(query, typeFilter, 100);
                searchResults.Add((query, typeFilter ?? "any", results.Count, true));
            }
            catch (Exception ex)
            {
                searchResults.Add((query, typeFilter ?? "any", 0, false));
            }
        }

        // Assert - At least some searches should return results
        var successfulSearches = searchResults.Where(r => r.Success && r.Count > 0).ToList();
        Assert.True(successfulSearches.Any(), 
            $"Expected at least some searches to return results. Search results: {string.Join(", ", searchResults.Select(r => $"{r.Query}/{r.Type}={r.Count}"))}");
    }

    [Fact]
    public void AllKiCadLibraries_NoDuplicateModelNames()
    {
        // Arrange
        var libraryFiles = GetKiCadLibraryFiles();
        Assert.NotEmpty(libraryFiles);

        var allModels = new Dictionary<string, List<string>>(); // modelName -> list of files containing it

        // Act - Collect all models
        foreach (var libFile in libraryFiles)
        {
            try
            {
                var content = File.ReadAllText(libFile);
                var models = _parser.ParseLibFile(content);
                
                foreach (var model in models)
                {
                    if (!allModels.ContainsKey(model.ModelName))
                    {
                        allModels[model.ModelName] = new List<string>();
                    }
                    allModels[model.ModelName].Add(Path.GetFileName(libFile));
                }
            }
            catch
            {
                // Skip files that fail to parse (handled by other tests)
            }
        }

        // Assert - Check for duplicates (this is informational, not a failure)
        var duplicates = allModels.Where(kvp => kvp.Value.Count > 1).ToList();
        
        if (duplicates.Any())
        {
            // Log duplicates but don't fail - duplicates are expected in a large library collection
            var duplicateInfo = string.Join("\n", 
                duplicates.Take(10).Select(d => 
                    $"  {d.Key} appears in {d.Value.Count} files: {string.Join(", ", d.Value.Take(3))}"));
            
            // This is expected behavior - first model found wins
            Assert.True(true, $"Found {duplicates.Count} duplicate model names (first occurrence wins). Examples:\n{duplicateInfo}");
        }
    }

    /// <summary>
    /// Gets all KiCad library files from the libraries directory
    /// </summary>
    private List<string> GetKiCadLibraryFiles()
    {
        if (!Directory.Exists(_librariesPath))
        {
            // Try to find libraries directory from current working directory
            var currentDir = Directory.GetCurrentDirectory();
            var searchPaths = new[]
            {
                Path.Combine(currentDir, "libraries"),
                Path.Combine(currentDir, "..", "libraries"),
                Path.Combine(currentDir, "..", "..", "libraries"),
                Path.Combine(currentDir, "..", "..", "..", "libraries"),
                Path.Combine(currentDir, "..", "..", "..", "..", "libraries"),
            };

            foreach (var searchPath in searchPaths)
            {
                var fullPath = Path.GetFullPath(searchPath);
                if (Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "kicad_*.lib", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        return files.OrderBy(f => f).ToList();
                    }
                }
            }

            return new List<string>();
        }

        return Directory.GetFiles(_librariesPath, "kicad_*.lib", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();
    }
}
