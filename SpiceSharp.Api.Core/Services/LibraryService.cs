using SpiceSharp.Api.Core.Models;
using System.Collections.Concurrent;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for indexing and searching SPICE component libraries
/// </summary>
public class LibraryService : ILibraryService
{
    private readonly ConcurrentDictionary<string, ModelDefinition> _modelIndex = new();
    private readonly ConcurrentDictionary<string, SubcircuitDefinition> _subcircuitIndex = new();
    private readonly SpiceLibParser _parser = new();
    private readonly ISpeakerDatabaseService? _speakerDatabaseService;

    /// <summary>
    /// Initializes a new instance of the LibraryService
    /// </summary>
    /// <param name="speakerDatabaseService">Optional speaker database service for indexing speaker metadata</param>
    public LibraryService(ISpeakerDatabaseService? speakerDatabaseService = null)
    {
        _speakerDatabaseService = speakerDatabaseService;
    }

    /// <inheritdoc/>
    public void IndexLibraries(IEnumerable<string> libraryPaths)
    {
        foreach (var path in libraryPaths)
        {
            if (!Directory.Exists(path))
                continue;

            // Recursively find all .lib files
            // Handle permission errors gracefully
            string[] libFiles;
            try
            {
                libFiles = Directory.GetFiles(path, "*.lib", SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have access to
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted between check and access
                continue;
            }

            foreach (var libFile in libFiles)
            {
                try
                {
                    var content = File.ReadAllText(libFile);
                    var models = _parser.ParseLibFile(content);
                    var subcircuits = _parser.ParseSubcircuits(content);

                    foreach (var model in models)
                    {
                        // Only add if not already indexed (first wins for duplicates)
                        _modelIndex.TryAdd(model.ModelName, model);
                    }

                    foreach (var subcircuit in subcircuits)
                    {
                        // Only add if not already indexed (first wins for duplicates)
                        _subcircuitIndex.TryAdd(subcircuit.Name, subcircuit);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue processing other files
                    System.Diagnostics.Debug.WriteLine($"Error parsing library file {libFile}: {ex.Message}");
                }
            }
        }

        // Populate database with speaker subcircuits after indexing
        if (_speakerDatabaseService != null)
        {
            try
            {
                _speakerDatabaseService.InitializeDatabase();
                _speakerDatabaseService.PopulateFromSubcircuits(_subcircuitIndex.Values);
            }
            catch (Exception ex)
            {
                // Log but don't fail indexing if database population fails
                System.Diagnostics.Debug.WriteLine($"Error populating speaker database: {ex.Message}");
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

    /// <inheritdoc/>
    public List<SubcircuitDefinition> SearchSubcircuits(string query, string? typeFilter, int limit)
    {
        var queryLower = query?.ToLowerInvariant() ?? string.Empty;
        var typeFilterLower = typeFilter?.ToLowerInvariant();

        var results = _subcircuitIndex.Values
            .Where(subcircuit =>
            {
                // Filter by query - search subcircuit name and metadata fields
                if (!string.IsNullOrEmpty(queryLower))
                {
                    bool matchesQuery = false;

                    // Search subcircuit name
                    if (subcircuit.Name.ToLowerInvariant().Contains(queryLower))
                    {
                        matchesQuery = true;
                    }
                    // Search PRODUCT_NAME metadata
                    else if (subcircuit.Metadata.TryGetValue("PRODUCT_NAME", out var productName) &&
                             !string.IsNullOrEmpty(productName) &&
                             productName.ToLowerInvariant().Contains(queryLower))
                    {
                        matchesQuery = true;
                    }
                    // Search PART_NUMBER metadata
                    else if (subcircuit.Metadata.TryGetValue("PART_NUMBER", out var partNumber) &&
                             !string.IsNullOrEmpty(partNumber) &&
                             partNumber.ToLowerInvariant().Contains(queryLower))
                    {
                        matchesQuery = true;
                    }
                    // Search MANUFACTURER metadata
                    else if (subcircuit.Metadata.TryGetValue("MANUFACTURER", out var manufacturer) &&
                             !string.IsNullOrEmpty(manufacturer) &&
                             manufacturer.ToLowerInvariant().Contains(queryLower))
                    {
                        matchesQuery = true;
                    }

                    if (!matchesQuery)
                    {
                        return false;
                    }
                }

                // Filter by type if specified (searches metadata TYPE field)
                if (!string.IsNullOrEmpty(typeFilterLower))
                {
                    if (!subcircuit.Metadata.TryGetValue("TYPE", out var subcircuitType) ||
                        string.IsNullOrEmpty(subcircuitType) ||
                        !subcircuitType.ToLowerInvariant().Equals(typeFilterLower))
                    {
                        return false;
                    }
                }

                return true;
            })
            .OrderBy(s => s.Name)
            .Take(limit)
            .ToList();

        return results;
    }

    /// <inheritdoc/>
    public SubcircuitDefinition? GetSubcircuitByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _subcircuitIndex.TryGetValue(name, out var subcircuit) ? subcircuit : null;
    }
}
