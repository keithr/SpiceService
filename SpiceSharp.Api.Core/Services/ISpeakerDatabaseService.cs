using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing speaker data in SQLite database
/// </summary>
public interface ISpeakerDatabaseService
{
    /// <summary>
    /// Initializes the database schema (creates tables and indexes)
    /// </summary>
    void InitializeDatabase();

    /// <summary>
    /// Populates the database with speaker data from subcircuit definitions
    /// </summary>
    /// <param name="subcircuits">List of subcircuit definitions to populate from</param>
    void PopulateFromSubcircuits(IEnumerable<SubcircuitDefinition> subcircuits);

    /// <summary>
    /// Searches for speakers matching the specified parameters
    /// </summary>
    /// <param name="parameters">Search parameters</param>
    /// <returns>List of matching speakers</returns>
    List<SpeakerSearchResult> SearchSpeakersByParameters(SpeakerSearchParameters parameters);

    /// <summary>
    /// Gets a speaker by subcircuit name
    /// </summary>
    /// <param name="subcircuitName">The subcircuit name to look up</param>
    /// <returns>Speaker search result if found, null otherwise</returns>
    SpeakerSearchResult? GetSpeakerByName(string subcircuitName);
}

