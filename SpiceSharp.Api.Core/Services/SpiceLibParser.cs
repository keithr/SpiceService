using SpiceSharp.Api.Core.Models;
using System.Text.RegularExpressions;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Parser for SPICE library (.lib) files
/// </summary>
public class SpiceLibParser
{
    private static readonly Regex ModelLineRegex = new Regex(
        @"^\s*\.MODEL\s+(\w+)\s+(\w+)\s*\(([^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex ParameterRegex = new Regex(
        @"(\w+)\s*=\s*([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfa]?)",
        RegexOptions.IgnoreCase);

    private static readonly Regex SubcircuitLineRegex = new Regex(
        @"^\s*\.SUBCKT\s+(\w+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>
    /// Parses a SPICE library file content and extracts model definitions
    /// </summary>
    /// <param name="libContent">The content of the .lib file</param>
    /// <returns>List of parsed model definitions</returns>
    public List<ModelDefinition> ParseLibFile(string libContent)
    {
        var models = new List<ModelDefinition>();

        if (string.IsNullOrWhiteSpace(libContent))
            return models;

        // Remove comments (lines starting with *)
        var lines = libContent.Split('\n');
        var cleanedLines = new List<string>();
        var currentModelLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip empty lines and comment-only lines
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("*"))
                continue;

            // Check if this is a .MODEL line
            if (trimmed.StartsWith(".MODEL", StringComparison.OrdinalIgnoreCase))
            {
                // If we have accumulated continuation lines, process them
                if (currentModelLines.Count > 0)
                {
                    var model = ParseModelLines(currentModelLines);
                    if (model != null)
                        models.Add(model);
                    currentModelLines.Clear();
                }
                currentModelLines.Add(trimmed);
            }
            // Check if this is a continuation line (starts with +)
            else if (trimmed.StartsWith("+", StringComparison.OrdinalIgnoreCase))
            {
                currentModelLines.Add(trimmed);
            }
            // If we have accumulated lines and hit a non-continuation line, process them
            else if (currentModelLines.Count > 0)
            {
                var model = ParseModelLines(currentModelLines);
                if (model != null)
                    models.Add(model);
                currentModelLines.Clear();
            }
        }

        // Process any remaining accumulated lines
        if (currentModelLines.Count > 0)
        {
            var model = ParseModelLines(currentModelLines);
            if (model != null)
                models.Add(model);
        }

        return models;
    }

    /// <summary>
    /// Parses accumulated model lines (including continuation lines) into a ModelDefinition
    /// </summary>
    private ModelDefinition? ParseModelLines(List<string> lines)
    {
        if (lines.Count == 0)
            return null;

        // Combine all lines (remove + continuation markers and inline comments)
        var cleanedLines = lines.Select(l =>
        {
            var trimmed = l.TrimStart('+', ' ').Trim();
            // Remove inline comments
            var commentIndex = trimmed.IndexOf('*');
            if (commentIndex >= 0)
            {
                trimmed = trimmed.Substring(0, commentIndex).Trim();
            }
            return trimmed;
        }).Where(l => !string.IsNullOrWhiteSpace(l));
        
        var combinedLine = string.Join(" ", cleanedLines);

        // Match .MODEL statement
        var match = ModelLineRegex.Match(combinedLine);
        if (!match.Success)
            return null;

        var modelName = match.Groups[1].Value;
        var modelTypeRaw = match.Groups[2].Value.ToUpper();
        var parametersText = match.Groups[3].Value;

        // Map SPICE model types to our internal types
        var modelType = MapModelType(modelTypeRaw);

        // Parse parameters
        var parameters = ParseParameters(parametersText);

        return new ModelDefinition
        {
            ModelName = modelName,
            ModelType = modelType,
            Parameters = parameters
        };
    }

    /// <summary>
    /// Maps SPICE model type strings to internal model type strings
    /// </summary>
    private string MapModelType(string spiceType)
    {
        return spiceType.ToUpper() switch
        {
            "D" => "diode",
            "NPN" => "bjt_npn",
            "PNP" => "bjt_pnp",
            "NMOS" => "mosfet_n",
            "PMOS" => "mosfet_p",
            "NJF" or "JFETN" => "jfet_n",
            "PJF" or "JFETP" => "jfet_p",
            _ => spiceType.ToLower() // Default: use lowercase version
        };
    }

    /// <summary>
    /// Parses parameter string (e.g., "IS=1E-14 RS=0.5 N=1.5") into dictionary
    /// </summary>
    private Dictionary<string, double> ParseParameters(string parametersText)
    {
        var parameters = new Dictionary<string, double>();

        if (string.IsNullOrWhiteSpace(parametersText))
            return parameters;

        // Parameters text should already have comments removed from ParseModelLines
        // But handle it here too for safety
        var cleaned = parametersText;
        var commentIndex = cleaned.IndexOf('*');
        if (commentIndex >= 0)
        {
            cleaned = cleaned.Substring(0, commentIndex).Trim();
        }

        // Match all parameter assignments
        var matches = ParameterRegex.Matches(cleaned);
        foreach (Match match in matches)
        {
            var paramName = match.Groups[1].Value;
            var paramValueStr = match.Groups[2].Value;

            if (TryParseParameterValue(paramValueStr, out var paramValue))
            {
                parameters[paramName] = paramValue;
            }
        }

        return parameters;
    }

    /// <summary>
    /// Parses a parameter value string, handling scientific notation and unit suffixes
    /// </summary>
    private bool TryParseParameterValue(string valueStr, out double value)
    {
        value = 0.0;

        if (string.IsNullOrWhiteSpace(valueStr))
            return false;

        valueStr = valueStr.Trim();

        // Handle unit suffixes (m, u, n, p, f, a, k, M, G, T)
        var multiplier = 1.0;
        if (valueStr.Length > 0)
        {
            var lastChar = valueStr[valueStr.Length - 1];
            multiplier = lastChar switch
            {
                'a' or 'A' => 1e-18,
                'f' or 'F' => 1e-15,
                'p' or 'P' => 1e-12,
                'n' or 'N' => 1e-9,
                'u' or 'U' => 1e-6,
                'm' or 'M' => 1e-3,
                'k' or 'K' => 1e3,
                'g' or 'G' => 1e9,
                't' or 'T' => 1e12,
                _ => 1.0
            };

            // Remove unit suffix if present
            if (multiplier != 1.0)
            {
                valueStr = valueStr.Substring(0, valueStr.Length - 1);
            }
        }

        // Try to parse as double (handles scientific notation)
        if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture, out var baseValue))
        {
            value = baseValue * multiplier;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a SPICE library file content and extracts subcircuit definitions
    /// </summary>
    /// <param name="libContent">The content of the .lib file</param>
    /// <returns>List of parsed subcircuit definitions</returns>
    public List<SubcircuitDefinition> ParseSubcircuits(string libContent)
    {
        var subcircuits = new List<SubcircuitDefinition>();

        if (string.IsNullOrWhiteSpace(libContent))
            return subcircuits;

        var lines = libContent.Split('\n');
        var currentSubcircuitLines = new List<string>();
        var currentCommentLines = new List<string>();
        string? currentSubcircuitName = null;
        List<string>? currentSubcircuitNodes = null;
        Dictionary<string, string>? currentMetadata = null;
        Dictionary<string, double>? currentTsParameters = null;
        bool inSubcircuit = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Collect comment lines before .SUBCKT
            if (trimmed.StartsWith("*"))
            {
                if (!inSubcircuit)
                {
                    currentCommentLines.Add(trimmed);
                }
                continue;
            }

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Check if this is a .SUBCKT line
            if (trimmed.StartsWith(".SUBCKT", StringComparison.OrdinalIgnoreCase))
            {
                // If we have an incomplete subcircuit, save it
                if (inSubcircuit && currentSubcircuitName != null && currentSubcircuitNodes != null)
                {
                    var subcircuit = CreateSubcircuitDefinition(currentSubcircuitName, currentSubcircuitNodes, currentSubcircuitLines, currentMetadata, currentTsParameters);
                    if (subcircuit != null)
                        subcircuits.Add(subcircuit);
                }

                // Start new subcircuit
                var match = SubcircuitLineRegex.Match(trimmed);
                if (match.Success)
                {
                    // Parse comment metadata before this .SUBCKT
                    var (metadata, tsParameters) = ParseCommentMetadata(currentCommentLines);
                    currentMetadata = metadata;
                    currentTsParameters = tsParameters;
                    currentCommentLines.Clear();

                    currentSubcircuitName = match.Groups[1].Value;
                    var nodesText = match.Groups[2].Value;
                    // Remove inline comments
                    var commentIndex = nodesText.IndexOf('*');
                    if (commentIndex >= 0)
                    {
                        nodesText = nodesText.Substring(0, commentIndex).Trim();
                    }
                    currentSubcircuitNodes = nodesText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    currentSubcircuitLines.Clear();
                    inSubcircuit = true;
                }
            }
            // Check if this is a .ENDS line
            else if (trimmed.StartsWith(".ENDS", StringComparison.OrdinalIgnoreCase))
            {
                if (inSubcircuit && currentSubcircuitName != null && currentSubcircuitNodes != null)
                {
                    var subcircuit = CreateSubcircuitDefinition(currentSubcircuitName, currentSubcircuitNodes, currentSubcircuitLines, currentMetadata, currentTsParameters);
                    if (subcircuit != null)
                        subcircuits.Add(subcircuit);
                }
                inSubcircuit = false;
                currentSubcircuitName = null;
                currentSubcircuitNodes = null;
                currentSubcircuitLines.Clear();
                currentCommentLines.Clear();
                currentMetadata = null;
                currentTsParameters = null;
            }
            // Check if this is a continuation line (starts with +)
            else if (trimmed.StartsWith("+", StringComparison.OrdinalIgnoreCase))
            {
                if (inSubcircuit)
                {
                    // If we're in a subcircuit and this is a continuation line, it could be:
                    // 1. Continuation of .SUBCKT line (more nodes)
                    // 2. Continuation of an internal component line
                    if (currentSubcircuitLines.Count == 0)
                    {
                        // This is continuation of .SUBCKT line - add nodes
                        var continuationText = trimmed.TrimStart('+', ' ').Trim();
                        var commentIndex = continuationText.IndexOf('*');
                        if (commentIndex >= 0)
                        {
                            continuationText = continuationText.Substring(0, commentIndex).Trim();
                        }
                        var additionalNodes = continuationText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (currentSubcircuitNodes != null)
                        {
                            currentSubcircuitNodes.AddRange(additionalNodes);
                        }
                    }
                    else
                    {
                        // This is continuation of an internal component line
                        currentSubcircuitLines.Add(trimmed);
                    }
                }
            }
            // Regular line inside subcircuit
            else if (inSubcircuit)
            {
                currentSubcircuitLines.Add(trimmed);
            }
        }

        // Process any remaining incomplete subcircuit
        if (inSubcircuit && currentSubcircuitName != null && currentSubcircuitNodes != null)
        {
            var subcircuit = CreateSubcircuitDefinition(currentSubcircuitName, currentSubcircuitNodes, currentSubcircuitLines, currentMetadata, currentTsParameters);
            if (subcircuit != null)
                subcircuits.Add(subcircuit);
        }

        return subcircuits;
    }

    /// <summary>
    /// Parses comment lines to extract metadata and T/S parameters
    /// </summary>
    /// <param name="commentLines">List of comment lines (starting with *)</param>
    /// <returns>Tuple of (metadata dictionary, T/S parameters dictionary)</returns>
    private (Dictionary<string, string> metadata, Dictionary<string, double> tsParameters) ParseCommentMetadata(List<string> commentLines)
    {
        var metadata = new Dictionary<string, string>();
        var tsParameters = new Dictionary<string, double>();

        // T/S parameter names (numeric values)
        var tsParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FS", "QTS", "QES", "QMS", "VAS", "RE", "LE", "BL", "XMAX", "MMS", "CMS", "SD"
        };

        // Metadata field names (string values)
        var metadataFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MANUFACTURER", "PART_NUMBER", "PRODUCT_NAME", "MODEL_NAME", "TYPE", "DIAMETER", "IMPEDANCE", "POWER_RMS", "POWER_MAX", "SENSITIVITY", "PRICE"
        };

        foreach (var commentLine in commentLines)
        {
            // Remove leading * and trim
            var line = commentLine.TrimStart('*').Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse format: KEY: VALUE (handle multiple colons, spaces, etc.)
            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                continue;

            var key = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;

            // Check if it's a T/S parameter (numeric)
            if (tsParameterNames.Contains(key))
            {
                // For numeric T/S parameters, handle units or additional info after a space
                // e.g., "SENSITIVITY: 88.5 dB" -> extract "88.5"
                var numericValueStr = value;
                if (value.Contains(' '))
                {
                    var firstPart = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstPart))
                    {
                        numericValueStr = firstPart;
                    }
                }
                
                if (double.TryParse(numericValueStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
                {
                    tsParameters[key.ToUpper()] = numericValue;
                }
            }
            // Check if it's a string metadata field (keep full value including spaces)
            else if (metadataFieldNames.Contains(key))
            {
                // For string metadata like PRODUCT_NAME, keep the full value
                metadata[key.ToUpper()] = value;
            }
            // Otherwise, treat as generic metadata (keep full value)
            else
            {
                metadata[key.ToUpper()] = value;
            }
        }

        return (metadata, tsParameters);
    }

    /// <summary>
    /// Creates a SubcircuitDefinition from parsed data
    /// </summary>
    private SubcircuitDefinition? CreateSubcircuitDefinition(string name, List<string> nodes, List<string> definitionLines, Dictionary<string, string>? metadata, Dictionary<string, double>? tsParameters)
    {
        if (string.IsNullOrWhiteSpace(name) || nodes == null || nodes.Count == 0)
            return null;

        // Clean up definition lines: remove inline comments and continuation markers
        var cleanedDefinitionLines = definitionLines.Select(l =>
        {
            var trimmed = l.TrimStart('+', ' ').Trim();
            // Remove inline comments
            var commentIndex = trimmed.IndexOf('*');
            if (commentIndex >= 0)
            {
                trimmed = trimmed.Substring(0, commentIndex).Trim();
            }
            return trimmed;
        }).Where(l => !string.IsNullOrWhiteSpace(l));

        var definition = string.Join("\n", cleanedDefinitionLines);

        return new SubcircuitDefinition
        {
            Name = name,
            Nodes = nodes,
            Definition = definition,
            Metadata = metadata ?? new Dictionary<string, string>(),
            TsParameters = tsParameters ?? new Dictionary<string, double>()
        };
    }
}
