using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for indexing and searching SPICE component libraries
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Indexes all .lib files in the specified directories (recursively)
    /// </summary>
    /// <param name="libraryPaths">Directories to scan for .lib files</param>
    void IndexLibraries(IEnumerable<string> libraryPaths);

    /// <summary>
    /// Searches for models matching the query
    /// </summary>
    /// <param name="query">Search query (model name substring, case-insensitive)</param>
    /// <param name="typeFilter">Optional model type filter (e.g., "diode", "bjt_npn")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>List of matching model definitions</returns>
    List<ModelDefinition> SearchModels(string query, string? typeFilter, int limit);

    /// <summary>
    /// Searches for subcircuits matching the query
    /// </summary>
    /// <param name="query">Search query (searches subcircuit name, PRODUCT_NAME, PART_NUMBER, and MANUFACTURER metadata fields, case-insensitive)</param>
    /// <param name="typeFilter">Optional type filter (e.g., "woofers", "tweeters", "midrange") - filters by metadata TYPE field</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>List of matching subcircuit definitions</returns>
    List<SubcircuitDefinition> SearchSubcircuits(string query, string? typeFilter, int limit);

    /// <summary>
    /// Gets a subcircuit definition by exact name
    /// </summary>
    /// <param name="name">Exact subcircuit name (case-sensitive)</param>
    /// <returns>Subcircuit definition if found, null otherwise</returns>
    SubcircuitDefinition? GetSubcircuitByName(string name);
}
