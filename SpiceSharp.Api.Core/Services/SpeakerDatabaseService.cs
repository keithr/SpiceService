using Microsoft.Data.Sqlite;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing speaker data in SQLite database
/// </summary>
public class SpeakerDatabaseService : ISpeakerDatabaseService
{
    private readonly string _databasePath;

    /// <summary>
    /// Initializes a new instance of the SpeakerDatabaseService
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file. If null, uses default path.</param>
    public SpeakerDatabaseService(string? databasePath = null)
    {
        _databasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpiceService",
            "speakers.db");
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc/>
    public void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS speakers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                subcircuit_name TEXT UNIQUE NOT NULL,
                manufacturer TEXT,
                part_number TEXT,
                type TEXT,
                diameter REAL,
                impedance INTEGER,
                power_rms INTEGER,
                sensitivity REAL,
                price REAL,
                fs REAL,
                qts REAL,
                qes REAL,
                qms REAL,
                vas REAL,
                re REAL,
                le REAL,
                bl REAL,
                xmax REAL,
                mms REAL,
                cms REAL,
                sd REAL,
                source_file TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_type ON speakers(type);
            CREATE INDEX IF NOT EXISTS idx_diameter ON speakers(diameter);
            CREATE INDEX IF NOT EXISTS idx_impedance ON speakers(impedance);
            CREATE INDEX IF NOT EXISTS idx_fs ON speakers(fs);
            CREATE INDEX IF NOT EXISTS idx_qts ON speakers(qts);
            CREATE INDEX IF NOT EXISTS idx_vas ON speakers(vas);
            CREATE INDEX IF NOT EXISTS idx_price ON speakers(price);
        ";

        command.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public void PopulateFromSubcircuits(IEnumerable<SubcircuitDefinition> subcircuits)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        // Valid speaker types
        var validSpeakerTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "woofers", "tweeters", "midrange", "subwoofers", "fullrange", "midwoofers"
        };

        foreach (var subcircuit in subcircuits)
        {
            // Only process subcircuits that are actually speakers
            // Check for TYPE metadata indicating it's a speaker
            var type = GetMetadataValue(subcircuit, "TYPE");
            if (string.IsNullOrWhiteSpace(type) || !validSpeakerTypes.Contains(type))
            {
                // Skip if no TYPE metadata or TYPE is not a valid speaker type
                continue;
            }

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO speakers (
                    subcircuit_name, manufacturer, part_number, type, diameter, impedance,
                    power_rms, sensitivity, price, fs, qts, qes, qms, vas, re, le, bl,
                    xmax, mms, cms, sd, source_file
                ) VALUES (
                    @subcircuit_name, @manufacturer, @part_number, @type, @diameter, @impedance,
                    @power_rms, @sensitivity, @price, @fs, @qts, @qes, @qms, @vas, @re, @le, @bl,
                    @xmax, @mms, @cms, @sd, @source_file
                )
            ";

            // Set parameters (use DBNull.Value for null values)
            command.Parameters.AddWithValue("@subcircuit_name", subcircuit.Name);
            AddParameterOrNull(command, "@manufacturer", GetMetadataValue(subcircuit, "MANUFACTURER"));
            AddParameterOrNull(command, "@part_number", GetMetadataValue(subcircuit, "PART_NUMBER"));
            AddParameterOrNull(command, "@type", GetMetadataValue(subcircuit, "TYPE"));
            AddParameterOrNull(command, "@diameter", GetMetadataDouble(subcircuit, "DIAMETER"));
            AddParameterOrNull(command, "@impedance", GetMetadataInt(subcircuit, "IMPEDANCE"));
            AddParameterOrNull(command, "@power_rms", GetMetadataInt(subcircuit, "POWER_RMS"));
            AddParameterOrNull(command, "@sensitivity", GetMetadataDouble(subcircuit, "SENSITIVITY"));
            AddParameterOrNull(command, "@price", GetMetadataDouble(subcircuit, "PRICE"));
            AddParameterOrNull(command, "@fs", GetTsParameter(subcircuit, "FS"));
            AddParameterOrNull(command, "@qts", GetTsParameter(subcircuit, "QTS"));
            AddParameterOrNull(command, "@qes", GetTsParameter(subcircuit, "QES"));
            AddParameterOrNull(command, "@qms", GetTsParameter(subcircuit, "QMS"));
            AddParameterOrNull(command, "@vas", GetTsParameter(subcircuit, "VAS"));
            AddParameterOrNull(command, "@re", GetTsParameter(subcircuit, "RE"));
            AddParameterOrNull(command, "@le", GetTsParameter(subcircuit, "LE"));
            AddParameterOrNull(command, "@bl", GetTsParameter(subcircuit, "BL"));
            AddParameterOrNull(command, "@xmax", GetTsParameter(subcircuit, "XMAX"));
            AddParameterOrNull(command, "@mms", GetTsParameter(subcircuit, "MMS"));
            AddParameterOrNull(command, "@cms", GetTsParameter(subcircuit, "CMS"));
            AddParameterOrNull(command, "@sd", GetTsParameter(subcircuit, "SD"));
            command.Parameters.AddWithValue("@source_file", DBNull.Value); // Could be enhanced to track source file

            command.ExecuteNonQuery();
        }
    }

    private static string? GetMetadataValue(SubcircuitDefinition subcircuit, string key)
    {
        // Try exact match first
        if (subcircuit.Metadata.TryGetValue(key, out var value))
            return value;
        
        // Try case-insensitive lookup
        var keyUpper = key.ToUpperInvariant();
        var match = subcircuit.Metadata.FirstOrDefault(kvp => 
            kvp.Key.ToUpperInvariant() == keyUpper);
        
        return match.Key != null ? match.Value : null;
    }

    private static double? GetMetadataDouble(SubcircuitDefinition subcircuit, string key)
    {
        // Try exact match first
        if (subcircuit.Metadata.TryGetValue(key, out var value))
        {
            return ParseDoubleValue(value);
        }
        
        // Try case-insensitive lookup
        var keyUpper = key.ToUpperInvariant();
        var match = subcircuit.Metadata.FirstOrDefault(kvp => 
            kvp.Key.ToUpperInvariant() == keyUpper);
        
        if (match.Key != null)
        {
            return ParseDoubleValue(match.Value);
        }

        return null;
    }

    private static double? ParseDoubleValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }

    private static int? GetMetadataInt(SubcircuitDefinition subcircuit, string key)
    {
        // Try exact match first
        if (subcircuit.Metadata.TryGetValue(key, out var value))
        {
            return ParseIntValue(value);
        }
        
        // Try case-insensitive lookup
        var keyUpper = key.ToUpperInvariant();
        var match = subcircuit.Metadata.FirstOrDefault(kvp => 
            kvp.Key.ToUpperInvariant() == keyUpper);
        
        if (match.Key != null)
        {
            return ParseIntValue(match.Value);
        }

        return null;
    }

    private static int? ParseIntValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Try parsing as int first
        if (int.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var intResult))
        {
            return intResult;
        }

        // If that fails, try parsing as double and converting to int (handles "8.0" -> 8)
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var doubleResult))
        {
            return (int)Math.Round(doubleResult);
        }

        return null;
    }

    private static double? GetTsParameter(SubcircuitDefinition subcircuit, string key)
    {
        // Try exact match first
        if (subcircuit.TsParameters.TryGetValue(key, out var value))
            return value;
        
        // Try case-insensitive lookup
        var keyUpper = key.ToUpperInvariant();
        var match = subcircuit.TsParameters.FirstOrDefault(kvp => 
            kvp.Key.ToUpperInvariant() == keyUpper);
        
        return match.Key != null ? match.Value : null;
    }

    private static void AddParameterOrNull(SqliteCommand command, string parameterName, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    /// <inheritdoc/>
    public List<SpeakerSearchResult> SearchSpeakersByParameters(SpeakerSearchParameters parameters)
    {
        var results = new List<SpeakerSearchResult>();

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        // Build WHERE clause
        var whereConditions = new List<string>();
        var command = connection.CreateCommand();

        // Driver type filter
        if (parameters.DriverType != null && parameters.DriverType.Count > 0)
        {
            var placeholders = string.Join(",", parameters.DriverType.Select((_, i) => $"@type{i}"));
            whereConditions.Add($"type IN ({placeholders})");
            for (int i = 0; i < parameters.DriverType.Count; i++)
            {
                command.Parameters.AddWithValue($"@type{i}", parameters.DriverType[i]);
            }
        }

        // Diameter range
        if (parameters.DiameterMin.HasValue)
        {
            whereConditions.Add("diameter >= @diameter_min");
            command.Parameters.AddWithValue("@diameter_min", parameters.DiameterMin.Value);
        }
        if (parameters.DiameterMax.HasValue)
        {
            whereConditions.Add("diameter <= @diameter_max");
            command.Parameters.AddWithValue("@diameter_max", parameters.DiameterMax.Value);
        }

        // Impedance
        if (parameters.Impedance.HasValue)
        {
            whereConditions.Add("impedance = @impedance");
            command.Parameters.AddWithValue("@impedance", parameters.Impedance.Value);
        }

        // FS range
        if (parameters.FsMin.HasValue)
        {
            whereConditions.Add("fs >= @fs_min");
            command.Parameters.AddWithValue("@fs_min", parameters.FsMin.Value);
        }
        if (parameters.FsMax.HasValue)
        {
            whereConditions.Add("fs <= @fs_max");
            command.Parameters.AddWithValue("@fs_max", parameters.FsMax.Value);
        }

        // QTS range
        if (parameters.QtsMin.HasValue)
        {
            whereConditions.Add("qts >= @qts_min");
            command.Parameters.AddWithValue("@qts_min", parameters.QtsMin.Value);
        }
        if (parameters.QtsMax.HasValue)
        {
            whereConditions.Add("qts <= @qts_max");
            command.Parameters.AddWithValue("@qts_max", parameters.QtsMax.Value);
        }

        // QES range
        if (parameters.QesMin.HasValue)
        {
            whereConditions.Add("qes >= @qes_min");
            command.Parameters.AddWithValue("@qes_min", parameters.QesMin.Value);
        }
        if (parameters.QesMax.HasValue)
        {
            whereConditions.Add("qes <= @qes_max");
            command.Parameters.AddWithValue("@qes_max", parameters.QesMax.Value);
        }

        // QMS range
        if (parameters.QmsMin.HasValue)
        {
            whereConditions.Add("qms >= @qms_min");
            command.Parameters.AddWithValue("@qms_min", parameters.QmsMin.Value);
        }
        if (parameters.QmsMax.HasValue)
        {
            whereConditions.Add("qms <= @qms_max");
            command.Parameters.AddWithValue("@qms_max", parameters.QmsMax.Value);
        }

        // VAS range
        if (parameters.VasMin.HasValue)
        {
            whereConditions.Add("vas >= @vas_min");
            command.Parameters.AddWithValue("@vas_min", parameters.VasMin.Value);
        }
        if (parameters.VasMax.HasValue)
        {
            whereConditions.Add("vas <= @vas_max");
            command.Parameters.AddWithValue("@vas_max", parameters.VasMax.Value);
        }

        // Sensitivity range
        if (parameters.SensitivityMin.HasValue)
        {
            whereConditions.Add("sensitivity >= @sensitivity_min");
            command.Parameters.AddWithValue("@sensitivity_min", parameters.SensitivityMin.Value);
        }
        if (parameters.SensitivityMax.HasValue)
        {
            whereConditions.Add("sensitivity <= @sensitivity_max");
            command.Parameters.AddWithValue("@sensitivity_max", parameters.SensitivityMax.Value);
        }

        // Power range
        if (parameters.PowerMin.HasValue)
        {
            whereConditions.Add("power_rms >= @power_min");
            command.Parameters.AddWithValue("@power_min", parameters.PowerMin.Value);
        }
        if (parameters.PowerMax.HasValue)
        {
            whereConditions.Add("power_rms <= @power_max");
            command.Parameters.AddWithValue("@power_max", parameters.PowerMax.Value);
        }

        // XMAX range
        if (parameters.XmaxMin.HasValue)
        {
            whereConditions.Add("xmax >= @xmax_min");
            command.Parameters.AddWithValue("@xmax_min", parameters.XmaxMin.Value);
        }
        if (parameters.XmaxMax.HasValue)
        {
            whereConditions.Add("xmax <= @xmax_max");
            command.Parameters.AddWithValue("@xmax_max", parameters.XmaxMax.Value);
        }

        // Manufacturer
        if (!string.IsNullOrWhiteSpace(parameters.Manufacturer))
        {
            whereConditions.Add("manufacturer LIKE @manufacturer");
            command.Parameters.AddWithValue("@manufacturer", $"%{parameters.Manufacturer}%");
        }

        // Price max
        if (parameters.PriceMax.HasValue)
        {
            whereConditions.Add("price <= @price_max");
            command.Parameters.AddWithValue("@price_max", parameters.PriceMax.Value);
        }

        // Build ORDER BY clause
        var orderBy = "subcircuit_name";
        if (!string.IsNullOrWhiteSpace(parameters.SortBy))
        {
            var sortField = parameters.SortBy.ToLowerInvariant() switch
            {
                "sensitivity" => "sensitivity",
                "price" => "price",
                "fs" => "fs",
                "qts" => "qts",
                "vas" => "vas",
                _ => "subcircuit_name"
            };
            var sortDir = parameters.SortDirection?.ToLowerInvariant() == "desc" ? "DESC" : "ASC";
            orderBy = $"{sortField} {sortDir}";
        }

        // Build SQL query
        var whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";
        var limit = Math.Max(1, Math.Min(parameters.Limit, 1000)); // Cap at 1000

        command.CommandText = $@"
            SELECT 
                subcircuit_name, manufacturer, part_number, type, diameter, impedance,
                power_rms, sensitivity, price, fs, qts, qes, qms, vas, re, le, bl,
                xmax, mms, cms, sd
            FROM speakers
            {whereClause}
            ORDER BY {orderBy}
            LIMIT @limit
        ";

        command.Parameters.AddWithValue("@limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var result = CreateSpeakerSearchResult(reader);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc/>
    public SpeakerSearchResult? GetSpeakerByName(string subcircuitName)
    {
        if (string.IsNullOrWhiteSpace(subcircuitName))
            return null;

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                subcircuit_name, manufacturer, part_number, type, diameter, impedance,
                power_rms, sensitivity, price, fs, qts, qes, qms, vas, re, le, bl,
                xmax, mms, cms, sd
            FROM speakers
            WHERE subcircuit_name = @name
            LIMIT 1
        ";

        command.Parameters.AddWithValue("@name", subcircuitName);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return CreateSpeakerSearchResult(reader);
    }

    private static SpeakerSearchResult CreateSpeakerSearchResult(SqliteDataReader reader)
    {
        var result = new SpeakerSearchResult
        {
            SubcircuitName = reader.GetString(0),
            Manufacturer = reader.IsDBNull(1) ? null : reader.GetString(1),
            PartNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
            Type = reader.IsDBNull(3) ? null : reader.GetString(3),
            Diameter = reader.IsDBNull(4) ? null : reader.GetDouble(4),
            Impedance = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            PowerRms = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            Sensitivity = reader.IsDBNull(7) ? null : reader.GetDouble(7),
            Price = reader.IsDBNull(8) ? null : reader.GetDouble(8),
            TsParameters = new Dictionary<string, double>()
        };

        // Add T/S parameters
        if (!reader.IsDBNull(9)) result.TsParameters["FS"] = reader.GetDouble(9);
        if (!reader.IsDBNull(10)) result.TsParameters["QTS"] = reader.GetDouble(10);
        if (!reader.IsDBNull(11)) result.TsParameters["QES"] = reader.GetDouble(11);
        if (!reader.IsDBNull(12)) result.TsParameters["QMS"] = reader.GetDouble(12);
        if (!reader.IsDBNull(13)) result.TsParameters["VAS"] = reader.GetDouble(13);
        if (!reader.IsDBNull(14)) result.TsParameters["RE"] = reader.GetDouble(14);
        if (!reader.IsDBNull(15)) result.TsParameters["LE"] = reader.GetDouble(15);
        if (!reader.IsDBNull(16)) result.TsParameters["BL"] = reader.GetDouble(16);
        if (!reader.IsDBNull(17)) result.TsParameters["XMAX"] = reader.GetDouble(17);
        if (!reader.IsDBNull(18)) result.TsParameters["MMS"] = reader.GetDouble(18);
        if (!reader.IsDBNull(19)) result.TsParameters["CMS"] = reader.GetDouble(19);
        if (!reader.IsDBNull(20)) result.TsParameters["SD"] = reader.GetDouble(20);

        return result;
    }
}

