using SpiceSharp.Api.Core.Models;
using System.Text.RegularExpressions;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Parser for SPICE netlists
/// </summary>
public class NetlistParser : INetlistParser
{
    private readonly SpiceLibParser _modelParser = new();

    // Regex patterns for component parsing
    private static readonly Regex ResistorRegex = new Regex(
        @"^\s*R(\w+)\s+(\w+)\s+(\w+)\s+([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex CapacitorRegex = new Regex(
        @"^\s*C(\w+)\s+(\w+)\s+(\w+)\s+([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?[HFVAWOS]?)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex InductorRegex = new Regex(
        @"^\s*L(\w+)\s+(\w+)\s+(\w+)\s+([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?[HFVAWOS]?)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex VoltageSourceRegex = new Regex(
        @"^\s*V(\w+)\s+(\w+)\s+(\w+)\s+(?:DC\s+)?([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?)(?:\s+AC\s+([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?))?\s*$",
        RegexOptions.IgnoreCase);
    
    // Voltage source with AC specification: V<name> <node1> <node2> AC <value>
    private static readonly Regex VoltageSourceACRegex = new Regex(
        @"^\s*V(\w+)\s+(\w+)\s+(\w+)\s+AC\s+([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex CurrentSourceRegex = new Regex(
        @"^\s*I(\w+)\s+(\w+)\s+(\w+)\s+(?:DC\s+)?([+-]?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|[+-]?\d+\.?\d*[munpfaKMGT]?)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex DiodeRegex = new Regex(
        @"^\s*D(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex BJTRegex = new Regex(
        @"^\s*Q(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex MOSFETRegex = new Regex(
        @"^\s*M(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex JFETRegex = new Regex(
        @"^\s*J(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s+(\w+)\s*$",
        RegexOptions.IgnoreCase);

    // Subcircuit instantiation: X<name> <node1> <node2> ... <subcircuit_name>
    // Example: Xtweeter tw_out 0 275_030
    private static readonly Regex SubcircuitRegex = new Regex(
        @"^\s*X(\w+)\s+(.+?)\s+(\w+)\s*$",
        RegexOptions.IgnoreCase);

    /// <inheritdoc/>
    public ParsedNetlist ParseNetlist(string netlist)
    {
        if (string.IsNullOrWhiteSpace(netlist))
            return new ParsedNetlist();

        var result = new ParsedNetlist();
        var lines = netlist.Split('\n');
        var modelLines = new List<string>();
        var inModel = false;
        var isFirstLine = true; // Track first line for title handling

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (inModel && modelLines.Count > 0)
                {
                    // End of model block
                    var models = _modelParser.ParseLibFile(string.Join("\n", modelLines));
                    result.Models.AddRange(models);
                    modelLines.Clear();
                    inModel = false;
                }
                continue;
            }

            // Skip comment lines
            if (trimmed.StartsWith("*"))
                continue;

            // Handle .MODEL statements
            if (trimmed.StartsWith(".MODEL", StringComparison.OrdinalIgnoreCase))
            {
                if (inModel && modelLines.Count > 0)
                {
                    // Process previous model
                    var models = _modelParser.ParseLibFile(string.Join("\n", modelLines));
                    result.Models.AddRange(models);
                    modelLines.Clear();
                }
                modelLines.Add(trimmed);
                inModel = true;
                continue;
            }

            // Handle continuation lines for models
            if (inModel)
            {
                if (trimmed.StartsWith("+"))
                {
                    modelLines.Add(trimmed);
                    continue;
                }
                // Check if this line closes the model (ends with ) or is just )
                if (trimmed == ")" || trimmed.EndsWith(")"))
                {
                    // Add closing paren if not already in the last line
                    var lastLine = modelLines.LastOrDefault();
                    if (lastLine != null && !lastLine.TrimEnd().EndsWith(")"))
                    {
                        if (trimmed != ")")
                        {
                            // Add the line with the closing paren
                            modelLines.Add(trimmed);
                        }
                        else
                        {
                            // Just add the closing paren as a continuation
                            modelLines.Add("+ " + trimmed);
                        }
                    }
                    // Process the model
                    var models = _modelParser.ParseLibFile(string.Join("\n", modelLines));
                    result.Models.AddRange(models);
                    modelLines.Clear();
                    inModel = false;
                    continue;
                }
                // If we hit a non-continuation, non-closing line, process the model
                if (modelLines.Count > 0)
                {
                    var models = _modelParser.ParseLibFile(string.Join("\n", modelLines));
                    result.Models.AddRange(models);
                    modelLines.Clear();
                    inModel = false;
                }
            }

            // Skip directives (they're handled elsewhere)
            if (trimmed.StartsWith("."))
            {
                continue;
            }

            // Try to parse as component first
            ComponentDefinition? component = null;
            try
            {
                component = ParseComponentLine(trimmed);
                if (component != null)
                {
                    result.Components.Add(component);
                    isFirstLine = false;
                }
            }
            catch (ArgumentException)
            {
                // Check if line starts with a component prefix - if so, it's an error, not a title
                var upperTrimmed = trimmed.ToUpperInvariant();
                var startsWithComponentPrefix = upperTrimmed.Length > 0 && 
                    (upperTrimmed[0] == 'R' || upperTrimmed[0] == 'C' || upperTrimmed[0] == 'L' ||
                     upperTrimmed[0] == 'V' || upperTrimmed[0] == 'I' || upperTrimmed[0] == 'D' ||
                     upperTrimmed[0] == 'Q' || upperTrimmed[0] == 'M' || upperTrimmed[0] == 'J' ||
                     upperTrimmed[0] == 'X');
                
                // If parsing fails and it's the first non-comment line, treat it as a title (SPICE convention)
                // UNLESS it starts with a component prefix, in which case it's an invalid component format
                if (isFirstLine && !startsWithComponentPrefix)
                {
                    result.Title = trimmed;
                    isFirstLine = false;
                }
                else
                {
                    // Not the first line, or starts with component prefix - re-throw the exception
                    throw;
                }
            }
        }

        // Process any remaining model
        if (inModel && modelLines.Count > 0)
        {
            var models = _modelParser.ParseLibFile(string.Join("\n", modelLines));
            result.Models.AddRange(models);
        }

        return result;
    }

    private ComponentDefinition? ParseComponentLine(string line)
    {
        // Try each component type
        var         match = ResistorRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "R" + match.Groups[1].Value,
                ComponentType = "resistor",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Value = ParseValue(match.Groups[4].Value)
            };
        }

        match = CapacitorRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "C" + match.Groups[1].Value,
                ComponentType = "capacitor",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Value = ParseValue(match.Groups[4].Value)
            };
        }

        match = InductorRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "L" + match.Groups[1].Value,
                ComponentType = "inductor",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Value = ParseValue(match.Groups[4].Value)
            };
        }

        // Try AC format first (V<name> <node1> <node2> AC <value>)
        match = VoltageSourceACRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "V" + match.Groups[1].Value,
                ComponentType = "voltage_source",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Value = ParseValue(match.Groups[4].Value),
                Parameters = new Dictionary<string, object> { { "ac", ParseValue(match.Groups[4].Value) } }
            };
        }

        match = VoltageSourceRegex.Match(line);
        if (match.Success)
        {
            var component = new ComponentDefinition
            {
                Name = "V" + match.Groups[1].Value,
                ComponentType = "voltage_source",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Value = ParseValue(match.Groups[4].Value),
                Parameters = new Dictionary<string, object>()
            };

            // Parse AC parameter if present
            if (match.Groups.Count > 5 && !string.IsNullOrWhiteSpace(match.Groups[5].Value))
            {
                component.Parameters["ac"] = ParseValue(match.Groups[5].Value);
            }

            return component;
        }

        match = CurrentSourceRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "I" + match.Groups[1].Value,
                ComponentType = "current_source",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Value = ParseValue(match.Groups[4].Value)
            };
        }

        match = DiodeRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "D" + match.Groups[1].Value,
                ComponentType = "diode",
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value },
                Model = match.Groups[4].Value
            };
        }

        match = BJTRegex.Match(line);
        if (match.Success)
        {
            // Determine type from model (will be set when model is added)
            // For now, default to npn
            return new ComponentDefinition
            {
                Name = "Q" + match.Groups[1].Value,
                ComponentType = "bjt_npn", // Will be updated if model specifies pnp
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value },
                Model = match.Groups[5].Value
            };
        }

        match = MOSFETRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "M" + match.Groups[1].Value,
                ComponentType = "mosfet_n", // Will be updated if model specifies p
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value, match.Groups[5].Value },
                Model = match.Groups[6].Value
            };
        }

        match = JFETRegex.Match(line);
        if (match.Success)
        {
            return new ComponentDefinition
            {
                Name = "J" + match.Groups[1].Value,
                ComponentType = "jfet_n", // Will be updated if model specifies p
                Nodes = new List<string> { match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value },
                Model = match.Groups[5].Value
            };
        }

        match = SubcircuitRegex.Match(line);
        if (match.Success)
        {
            // Parse nodes from group 2 (everything between component name and subcircuit name)
            var nodesText = match.Groups[2].Value.Trim();
            var nodes = nodesText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var subcircuitName = match.Groups[3].Value;

            return new ComponentDefinition
            {
                Name = "X" + match.Groups[1].Value,
                ComponentType = "subcircuit",
                Nodes = nodes,
                Model = subcircuitName
            };
        }

        // If no match, check if it's a valid directive (like .TITLE, .END) - ignore those
        var trimmed = line.Trim();
        if (trimmed.StartsWith(".") || trimmed == ")" || string.IsNullOrWhiteSpace(trimmed))
        {
            return null; // Directive or closing paren, not a component
        }

        // Unknown format - throw exception
        throw new ArgumentException($"Unable to parse component line: {line}");
    }

    private double ParseValue(string valueStr)
    {
        if (string.IsNullOrWhiteSpace(valueStr))
            return 0.0;

        valueStr = valueStr.Trim();

        // Strip unit letters (H=henry, F=farad, A=ampere, V=volt, W=watt, O=ohm, S=siemens)
        // SPICE typically omits these, but some files include them for clarity (e.g., "0.05mH", "0.143682H")
        // We strip them before parsing engineering notation
        // Engineering notation letters (m, u, n, p, f, a, k, M, G, T) are handled below
        if (valueStr.Length > 1)
        {
            var lastChar = valueStr[valueStr.Length - 1];
            // Unit letters that should be stripped (case-insensitive)
            if ((lastChar == 'H' || lastChar == 'h') || // Henry
                (lastChar == 'F' || lastChar == 'f') || // Farad
                (lastChar == 'V' || lastChar == 'v') || // Volt
                (lastChar == 'W' || lastChar == 'w') || // Watt
                (lastChar == 'O' || lastChar == 'o') || // Ohm
                (lastChar == 'S' || lastChar == 's'))   // Siemens
            {
                // Strip the unit letter (e.g., "0.05mH" -> "0.05m", "0.143682H" -> "0.143682")
                valueStr = valueStr.Substring(0, valueStr.Length - 1);
            }
            // Note: 'A' and 'a' are ambiguous (could be atto or ampere)
            // We don't strip them - let engineering notation parsing handle 'a' as atto
        }

        // Handle unit suffixes (engineering notation)
        var multiplier = 1.0;
        if (valueStr.EndsWith("T", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e12;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("G", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e9;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("MEG", StringComparison.OrdinalIgnoreCase) || valueStr.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            // Check for MEG (megohm) vs M (milli)
            if (valueStr.EndsWith("MEG", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1e6;
                valueStr = valueStr.Substring(0, valueStr.Length - 3);
            }
            else
            {
                // Could be milli or mega - default to milli for SPICE compatibility
                // But check if it's likely mega (large number)
                if (double.TryParse(valueStr.Substring(0, valueStr.Length - 1), out var num) && Math.Abs(num) < 1000)
                {
                    multiplier = 1e-3; // milli
                }
                else
                {
                    multiplier = 1e6; // mega
                }
                valueStr = valueStr.Substring(0, valueStr.Length - 1);
            }
        }
        else if (valueStr.EndsWith("K", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e3;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("M", StringComparison.OrdinalIgnoreCase) && valueStr.Length > 1 && char.IsDigit(valueStr[valueStr.Length - 2]))
        {
            // Already handled above, but check for milli
            multiplier = 1e-3;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("U", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e-6;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("N", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e-9;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("P", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e-12;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("F", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e-15;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }
        else if (valueStr.EndsWith("A", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e-18;
            valueStr = valueStr.Substring(0, valueStr.Length - 1);
        }

        if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value * multiplier;
        }

        throw new ArgumentException($"Unable to parse value: {valueStr}");
    }
}
