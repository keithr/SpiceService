using SpiceSharp.Api.Core.Models;
using System.Collections.Concurrent;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for indexing and searching SPICE component libraries
/// </summary>
public class LibraryService : ILibraryService
{
    private readonly ConcurrentDictionary<string, ModelDefinition> _modelIndex = new();
    private readonly SpiceLibParser _parser = new();

    /// <inheritdoc/>
    public void IndexLibraries(IEnumerable<string> libraryPaths)
    {
        foreach (var path in libraryPaths)
        {
            if (!Directory.Exists(path))
                continue;

            // Recursively find all .lib files
            var libFiles = Directory.GetFiles(path, "*.lib", SearchOption.AllDirectories);

            foreach (var libFile in libFiles)
            {
                try
                {
                    var content = File.ReadAllText(libFile);
                    var models = _parser.ParseLibFile(content);

                    foreach (var model in models)
                    {
                        // Only add if not already indexed (first wins for duplicates)
                        _modelIndex.TryAdd(model.ModelName, model);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue processing other files
                    System.Diagnostics.Debug.WriteLine($"Error parsing library file {libFile}: {ex.Message}");
                }
            }
        }
    }

    /// <inheritdoc/>
    public List<ModelDefinition> SearchModels(string query, string? typeFilter, int limit)
    {
        var queryLower = query?.ToLowerInvariant() ?? string.Empty;
        var typeFilterLower = typeFilter?.ToLowerInvariant();

        var results = _modelIndex.Values
            .Where(model =>
            {
                // Filter by query (model name substring)
                if (!string.IsNullOrEmpty(queryLower) && 
                    !model.ModelName.ToLowerInvariant().Contains(queryLower))
                {
                    return false;
                }

                // Filter by type
                if (!string.IsNullOrEmpty(typeFilterLower) &&
                    !model.ModelType.ToLowerInvariant().Equals(typeFilterLower))
                {
                    return false;
                }

                return true;
            })
            .OrderBy(m => m.ModelName)
            .Take(limit)
            .ToList();

        return results;
    }
}
