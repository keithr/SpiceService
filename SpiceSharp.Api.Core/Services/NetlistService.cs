using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for exporting circuits to SPICE netlist format.
/// </summary>
public class NetlistService : INetlistService
{
    /// <inheritdoc/>
    public string ExportNetlist(CircuitModel circuit, bool includeComments = true)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        var output = new List<string>();

        // Add header comment
        if (includeComments)
        {
            output.Add($"* SPICE Netlist");
            output.Add($"* Circuit: {circuit.Id}");
            if (!string.IsNullOrWhiteSpace(circuit.Description))
            {
                output.Add($"* Description: {circuit.Description}");
            }
            output.Add($"* Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            output.Add(string.Empty);
        }

        // Add title line
        output.Add($".TITLE {circuit.Id}");

        // Add model definitions
        var models = ExportModels(circuit, includeComments);
        if (models.Any())
        {
            output.AddRange(models);
            output.Add(string.Empty);
        }

        // Add component definitions
        var components = ExportComponents(circuit);
        if (components.Any())
        {
            output.AddRange(components);
        }

        // Add end statement
        output.Add(string.Empty);
        output.Add(".END");

        return string.Join(Environment.NewLine, output);
    }

    /// <summary>
    /// Exports model definitions from the circuit.
    /// </summary>
    private static List<string> ExportModels(CircuitModel circuit, bool includeComments)
    {
        var lines = new List<string>();

        if (includeComments && circuit.ModelDefinitions.Count > 0)
        {
            lines.Add("* Model definitions");
        }

        foreach (var kvp in circuit.ModelDefinitions)
        {
            var model = kvp.Value;
            lines.Add(FormatModel(model));
        }

        return lines;
    }

    /// <summary>
    /// Formats a model definition as a SPICE model statement.
    /// </summary>
    private static string FormatModel(ModelDefinition model)
    {
        var modelTypeKeyword = GetModelTypeKeyword(model.ModelType);
        var paramString = FormatParameters(model.Parameters);
        
        return $".MODEL {model.ModelName} {modelTypeKeyword}{paramString}";
    }

    /// <summary>
    /// Gets the SPICE keyword for a model type.
    /// </summary>
    private static string GetModelTypeKeyword(string modelType)
    {
        return modelType.ToLower() switch
        {
            "diode" => "D",
            "bjt_npn" => "NPN",
            "bjt_pnp" => "PNP",
            "mosfet_n" => "NMOS",
            "mosfet_p" => "PMOS",
            "jfet_n" => "NJF",
            "jfet_p" => "PJF",
            _ => "D" // Default to diode if unknown
        };
    }

    /// <summary>
    /// Formats parameters dictionary into SPICE format.
    /// </summary>
    private static string FormatParameters(Dictionary<string, double> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return string.Empty;

        var paramList = parameters.Select(kvp => $"{kvp.Key}={kvp.Value:G}").ToList();
        return $"({string.Join(" ", paramList)})";
    }

    /// <summary>
    /// Exports component definitions from the circuit.
    /// </summary>
    private static List<string> ExportComponents(CircuitModel circuit)
    {
        var lines = new List<string>();

        foreach (var kvp in circuit.ComponentDefinitions)
        {
            var component = kvp.Value;
            lines.Add(FormatComponent(component));
        }

        return lines;
    }

    /// <summary>
    /// Formats a component definition as a SPICE component line.
    /// </summary>
    private static string FormatComponent(ComponentDefinition component)
    {
        var nodesString = string.Join(" ", component.Nodes);
        
        return component.ComponentType.ToLower() switch
        {
            "resistor" => $"{component.Name} {nodesString} {component.Value:G}",
            "capacitor" => $"C{component.Name} {nodesString} {component.Value:G}",
            "inductor" => $"L{component.Name} {nodesString} {component.Value:G}",
            "diode" => $"{component.Name} {nodesString} {component.Model}",
            "voltage_source" => FormatVoltageSource(component),
            "current_source" => FormatCurrentSource(component),
            "bjt_npn" or "bjt_pnp" => $"Q{component.Name} {nodesString} {component.Model}",
            "mosfet_n" or "mosfet_p" => $"M{component.Name} {nodesString} {component.Model}",
            "jfet_n" or "jfet_p" => $"J{component.Name} {nodesString} {component.Model}",
            "vcvs" => FormatVCVS(component),
            "vccs" => FormatVCCS(component),
            "ccvs" => FormatCCVS(component),
            "cccs" => FormatCCCS(component),
            "behavioral_voltage_source" => FormatBehavioralVoltageSource(component),
            "behavioral_current_source" => FormatBehavioralCurrentSource(component),
            "mutual_inductance" => FormatMutualInductance(component),
            "voltage_switch" => FormatVoltageSwitch(component),
            "current_switch" => FormatCurrentSwitch(component),
            "subcircuit" => FormatSubcircuit(component),
            _ => $"* Unknown component type: {component.ComponentType}"
        };
    }

    /// <summary>
    /// Formats a voltage source with optional waveform and AC specification.
    /// </summary>
    private static string FormatVoltageSource(ComponentDefinition component)
    {
        var nodesString = string.Join(" ", component.Nodes);
        var waveformString = FormatWaveform(component);
        
        // Check for AC parameters
        var acMagnitude = GetDoubleParam(component.Parameters, "ac", 0.0);
        if (acMagnitude == 0.0)
        {
            acMagnitude = GetDoubleParam(component.Parameters, "acmag", 0.0);
        }
        var acPhase = GetDoubleParam(component.Parameters, "acphase", 0.0);
        
        // Build the source specification
        var parts = new List<string>();
        
        // DC value (always present)
        parts.Add($"DC {component.Value:G}");
        
        // AC specification (if present)
        if (acMagnitude > 0)
        {
            if (acPhase != 0)
            {
                parts.Add($"AC {acMagnitude:G} {acPhase:G}");
            }
            else
            {
                parts.Add($"AC {acMagnitude:G}");
            }
        }
        
        // Waveform (for transient analysis)
        if (!string.IsNullOrEmpty(waveformString))
        {
            parts.Add(waveformString);
        }
        
        return $"{component.Name} {nodesString} {string.Join(" ", parts)}";
    }

    /// <summary>
    /// Formats a current source with optional waveform and AC specification.
    /// </summary>
    private static string FormatCurrentSource(ComponentDefinition component)
    {
        var nodesString = string.Join(" ", component.Nodes);
        var waveformString = FormatWaveform(component);
        
        // Check for AC parameters
        var acMagnitude = GetDoubleParam(component.Parameters, "ac", 0.0);
        if (acMagnitude == 0.0)
        {
            acMagnitude = GetDoubleParam(component.Parameters, "acmag", 0.0);
        }
        var acPhase = GetDoubleParam(component.Parameters, "acphase", 0.0);
        
        // Build the source specification
        var parts = new List<string>();
        
        // DC value (always present)
        parts.Add($"DC {component.Value:G}");
        
        // AC specification (if present)
        if (acMagnitude > 0)
        {
            if (acPhase != 0)
            {
                parts.Add($"AC {acMagnitude:G} {acPhase:G}");
            }
            else
            {
                parts.Add($"AC {acMagnitude:G}");
            }
        }
        
        // Waveform (for transient analysis)
        if (!string.IsNullOrEmpty(waveformString))
        {
            parts.Add(waveformString);
        }
        
        return $"I{component.Name} {nodesString} {string.Join(" ", parts)}";
    }

    /// <summary>
    /// Formats waveform parameters into SPICE syntax.
    /// </summary>
    private static string FormatWaveform(ComponentDefinition component)
    {
        if (component.Parameters == null || component.Parameters.Count == 0)
            return string.Empty;

        // Check for explicit waveform type
        var waveformParam = component.Parameters.FirstOrDefault(p => 
            p.Key.Equals("waveform", StringComparison.OrdinalIgnoreCase));
        var waveformType = waveformParam.Key != null ? waveformParam.Value?.ToString()?.ToLower() : null;

        // Auto-detect sine waveform if sine parameters are present
        if (string.IsNullOrWhiteSpace(waveformType))
        {
            var hasSineParams = component.Parameters.Any(p => 
                p.Key.Equals("sine_amplitude", StringComparison.OrdinalIgnoreCase) ||
                p.Key.Equals("sine_frequency", StringComparison.OrdinalIgnoreCase) ||
                (p.Key.Equals("amplitude", StringComparison.OrdinalIgnoreCase) && 
                 component.Parameters.Any(p2 => p2.Key.Equals("frequency", StringComparison.OrdinalIgnoreCase))));
            
            if (hasSineParams)
            {
                waveformType = "sine";
            }
        }

        return waveformType?.ToLower() switch
        {
            "sine" => FormatSineWaveform(component.Parameters),
            "pulse" => FormatPulseWaveform(component.Parameters),
            "pwl" => FormatPwlWaveform(component.Parameters),
            "sffm" => FormatSffmWaveform(component.Parameters),
            "am" => FormatAmWaveform(component.Parameters),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Formats sine waveform parameters into SPICE SIN syntax.
    /// Format: SIN(offset amplitude frequency delay damping)
    /// </summary>
    private static string FormatSineWaveform(Dictionary<string, object> parameters)
    {
        // Get parameters - support both naming conventions
        var offset = GetDoubleParam(parameters, "offset", 0.0);
        
        if (TryGetDoubleParam(parameters, "amplitude", out var amplitude) ||
            TryGetDoubleParam(parameters, "sine_amplitude", out amplitude))
        {
            if (TryGetDoubleParam(parameters, "frequency", out var frequency) ||
                TryGetDoubleParam(parameters, "sine_frequency", out frequency))
            {
                var delay = GetDoubleParam(parameters, "delay", 0.0);
                var damping = GetDoubleParam(parameters, "damping", 0.0);
                
                // SPICE format: SIN(offset amplitude frequency delay damping)
                return $"SIN({offset:G} {amplitude:G} {frequency:G} {delay:G} {damping:G})";
            }
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Formats pulse waveform parameters into SPICE PULSE syntax.
    /// Format: PULSE(v1 v2 td tr tf pw per)
    /// </summary>
    private static string FormatPulseWaveform(Dictionary<string, object> parameters)
    {
        // Required parameters: v1, v2, td, tr, tf, pw, per
        if (TryGetDoubleParam(parameters, "v1", out var v1) &&
            TryGetDoubleParam(parameters, "v2", out var v2) &&
            TryGetDoubleParam(parameters, "td", out var td) &&
            TryGetDoubleParam(parameters, "tr", out var tr) &&
            TryGetDoubleParam(parameters, "tf", out var tf) &&
            TryGetDoubleParam(parameters, "pw", out var pw) &&
            TryGetDoubleParam(parameters, "per", out var per))
        {
            // SPICE format: PULSE(v1 v2 td tr tf pw per)
            return $"PULSE({v1:G} {v2:G} {td:G} {tr:G} {tf:G} {pw:G} {per:G})";
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Formats PWL (Piecewise Linear) waveform parameters into SPICE PWL syntax.
    /// Format: PWL(t1 v1 t2 v2 ...)
    /// </summary>
    private static string FormatPwlWaveform(Dictionary<string, object> parameters)
    {
        // PWL requires a "points" parameter which is an array of [time, voltage] pairs
        var pointsParam = parameters.FirstOrDefault(p => p.Key.Equals("points", StringComparison.OrdinalIgnoreCase));
        if (pointsParam.Key == null)
            return string.Empty;

        // Convert points to list of (time, value) tuples
        var points = new List<(double time, double value)>();
        
        if (pointsParam.Value is not object[] pointsArray || pointsArray.Length == 0)
            return string.Empty;

        foreach (var point in pointsArray)
        {
            if (point is not object[] pointPair || pointPair.Length != 2)
                return string.Empty;

            try
            {
                var time = Convert.ToDouble(pointPair[0]);
                var value = Convert.ToDouble(pointPair[1]);
                points.Add((time, value));
            }
            catch
            {
                return string.Empty;
            }
        }

        if (points.Count == 0)
            return string.Empty;

        // Format as PWL(t1 v1 t2 v2 ...)
        var pointStrings = points.Select(p => $"{p.time:G} {p.value:G}");
        return $"PWL({string.Join(" ", pointStrings)})";
    }

    /// <summary>
    /// Formats SFFM (Single-Frequency FM) waveform parameters into SPICE SFFM syntax.
    /// Format: SFFM(vo va fc mdi fs)
    /// </summary>
    private static string FormatSffmWaveform(Dictionary<string, object> parameters)
    {
        // Required parameters: vo, va, fc, mdi, fs
        if (TryGetDoubleParam(parameters, "vo", out var vo) &&
            TryGetDoubleParam(parameters, "va", out var va) &&
            TryGetDoubleParam(parameters, "fc", out var fc) &&
            TryGetDoubleParam(parameters, "mdi", out var mdi) &&
            TryGetDoubleParam(parameters, "fs", out var fs))
        {
            // SPICE format: SFFM(vo va fc mdi fs)
            return $"SFFM({vo:G} {va:G} {fc:G} {mdi:G} {fs:G})";
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Formats AM (Amplitude Modulation) waveform parameters into SPICE AM syntax.
    /// Format: AM(vo va mf fc)
    /// </summary>
    private static string FormatAmWaveform(Dictionary<string, object> parameters)
    {
        // Required parameters: vo, va, mf, fc
        if (TryGetDoubleParam(parameters, "vo", out var vo) &&
            TryGetDoubleParam(parameters, "va", out var va) &&
            TryGetDoubleParam(parameters, "mf", out var mf) &&
            TryGetDoubleParam(parameters, "fc", out var fc))
        {
            // SPICE format: AM(vo va mf fc)
            return $"AM({vo:G} {va:G} {mf:G} {fc:G})";
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Tries to get a double parameter from the parameters dictionary.
    /// </summary>
    private static bool TryGetDoubleParam(Dictionary<string, object> parameters, string key, out double value)
    {
        value = 0.0;
        var param = parameters.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (param.Key == null)
            return false;

        try
        {
            value = Convert.ToDouble(param.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a double parameter from the parameters dictionary, or returns the default value if not found.
    /// </summary>
    private static double GetDoubleParam(Dictionary<string, object> parameters, string key, double defaultValue)
    {
        if (TryGetDoubleParam(parameters, key, out var value))
            return value;
        return defaultValue;
    }

    /// <summary>
    /// Formats a VCVS (Voltage Controlled Voltage Source) component.
    /// Format: E{name} {out+} {out-} {in+} {in-} {gain}
    /// </summary>
    private static string FormatVCVS(ComponentDefinition component)
    {
        if (component.Nodes.Count < 4)
            return $"* Invalid VCVS {component.Name}: requires 4 nodes";
        
        var outputPos = component.Nodes[0];
        var outputNeg = component.Nodes[1];
        var inputPos = component.Nodes[2];
        var inputNeg = component.Nodes[3];
        
        var gain = GetGainParam(component.Parameters);
        
        return $"{component.Name} {outputPos} {outputNeg} {inputPos} {inputNeg} {gain:G}";
    }

    /// <summary>
    /// Formats a VCCS (Voltage Controlled Current Source) component.
    /// Format: G{name} {out+} {out-} {in+} {in-} {gain}
    /// </summary>
    private static string FormatVCCS(ComponentDefinition component)
    {
        if (component.Nodes.Count < 4)
            return $"* Invalid VCCS {component.Name}: requires 4 nodes";
        
        var outputPos = component.Nodes[0];
        var outputNeg = component.Nodes[1];
        var inputPos = component.Nodes[2];
        var inputNeg = component.Nodes[3];
        
        var gain = GetGainParam(component.Parameters);
        
        return $"{component.Name} {outputPos} {outputNeg} {inputPos} {inputNeg} {gain:G}";
    }

    /// <summary>
    /// Formats a CCVS (Current Controlled Voltage Source) component.
    /// Format: H{name} {out+} {out-} {V{name}} {gain}
    /// Note: CCVS references a voltage source name, not nodes directly.
    /// We reconstruct the voltage source name from control nodes to match ComponentFactory.
    /// </summary>
    private static string FormatCCVS(ComponentDefinition component)
    {
        if (component.Nodes.Count < 4)
            return $"* Invalid CCVS {component.Name}: requires 4 nodes";
        
        var outputPos = component.Nodes[0];
        var outputNeg = component.Nodes[1];
        var controlPos = component.Nodes[2];
        var controlNeg = component.Nodes[3];
        
        // Reconstruct voltage source name matching ComponentFactory convention
        var controlSourceName = $"V_CTRL_{controlPos}_{controlNeg}".Replace("-", "_").Replace("+", "P");
        
        var gain = GetGainParam(component.Parameters);
        
        return $"{component.Name} {outputPos} {outputNeg} {controlSourceName} {gain:G}";
    }

    /// <summary>
    /// Formats a CCCS (Current Controlled Current Source) component.
    /// Format: F{name} {out+} {out-} {V{name}} {gain}
    /// Note: CCCS references a voltage source name, not nodes directly.
    /// We reconstruct the voltage source name from control nodes to match ComponentFactory.
    /// </summary>
    private static string FormatCCCS(ComponentDefinition component)
    {
        if (component.Nodes.Count < 4)
            return $"* Invalid CCCS {component.Name}: requires 4 nodes";
        
        var outputPos = component.Nodes[0];
        var outputNeg = component.Nodes[1];
        var controlPos = component.Nodes[2];
        var controlNeg = component.Nodes[3];
        
        // Reconstruct voltage source name matching ComponentFactory convention
        var controlSourceName = $"V_CTRL_{controlPos}_{controlNeg}".Replace("-", "_").Replace("+", "P");
        
        var gain = GetGainParam(component.Parameters);
        
        return $"{component.Name} {outputPos} {outputNeg} {controlSourceName} {gain:G}";
    }

    /// <summary>
    /// Formats a Mutual Inductance component.
    /// Format: K{name} {inductor1} {inductor2} {coupling}
    /// </summary>
    private static string FormatMutualInductance(ComponentDefinition component)
    {
        if (component.Parameters == null || component.Parameters.Count == 0)
            return $"* Invalid mutual inductance {component.Name}: requires inductor1, inductor2, and coupling parameters";
        
        var inductor1Param = component.Parameters.FirstOrDefault(p => p.Key.Equals("inductor1", StringComparison.OrdinalIgnoreCase));
        var inductor2Param = component.Parameters.FirstOrDefault(p => p.Key.Equals("inductor2", StringComparison.OrdinalIgnoreCase));
        var couplingParam = component.Parameters.FirstOrDefault(p => p.Key.Equals("coupling", StringComparison.OrdinalIgnoreCase));
        
        if (inductor1Param.Key == null || inductor2Param.Key == null || couplingParam.Key == null)
            return $"* Invalid mutual inductance {component.Name}: missing required parameters";
        
        var inductor1 = inductor1Param.Value?.ToString() ?? string.Empty;
        var inductor2 = inductor2Param.Value?.ToString() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(inductor1) || string.IsNullOrWhiteSpace(inductor2))
            return $"* Invalid mutual inductance {component.Name}: inductor names cannot be empty";
        
        double coupling;
        try
        {
            coupling = Convert.ToDouble(couplingParam.Value);
        }
        catch
        {
            return $"* Invalid mutual inductance {component.Name}: coupling must be numeric";
        }
        
        // SPICE format: K1 L1 L2 0.95
        return $"{component.Name} {inductor1} {inductor2} {coupling:G}";
    }

    /// <summary>
    /// Formats a Voltage Controlled Switch component.
    /// Format: S{name} {switch+} {switch-} {control+} {control-} {modelName}
    /// </summary>
    private static string FormatVoltageSwitch(ComponentDefinition component)
    {
        if (component.Nodes == null || component.Nodes.Count < 2)
            return $"* Invalid voltage switch {component.Name}: requires 2 nodes";

        if (component.Parameters == null || component.Parameters.Count == 0)
            return $"* Invalid voltage switch {component.Name}: requires controlNodes and model parameters";

        var modelParam = component.Parameters.FirstOrDefault(p => p.Key.Equals("model", StringComparison.OrdinalIgnoreCase));
        if (modelParam.Key == null)
            return $"* Invalid voltage switch {component.Name}: requires model parameter";

        var modelName = modelParam.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modelName))
            return $"* Invalid voltage switch {component.Name}: model name cannot be empty";

        var controlNodesParam = component.Parameters.FirstOrDefault(p => p.Key.Equals("controlNodes", StringComparison.OrdinalIgnoreCase));
        if (controlNodesParam.Key == null)
            return $"* Invalid voltage switch {component.Name}: requires controlNodes parameter";

        string[] controlNodes;
        if (controlNodesParam.Value is string[] nodesArray)
        {
            controlNodes = nodesArray;
        }
        else if (controlNodesParam.Value is object[] objArray)
        {
            controlNodes = objArray.Select(o => o?.ToString() ?? string.Empty).ToArray();
        }
        else
        {
            return $"* Invalid voltage switch {component.Name}: controlNodes must be an array";
        }

        if (controlNodes.Length != 2)
            return $"* Invalid voltage switch {component.Name}: controlNodes must have exactly 2 nodes";

        var switchPos = component.Nodes[0];
        var switchNeg = component.Nodes[1];
        var controlPos = controlNodes[0];
        var controlNeg = controlNodes[1];

        // SPICE format: S1 out+ out- ctrl+ ctrl- SW_MODEL
        return $"{component.Name} {switchPos} {switchNeg} {controlPos} {controlNeg} {modelName}";
    }

    /// <summary>
    /// Formats a Current Controlled Switch component.
    /// Format: W{name} {switch+} {switch-} {voltageSourceName} {modelName}
    /// </summary>
    private static string FormatCurrentSwitch(ComponentDefinition component)
    {
        if (component.Nodes == null || component.Nodes.Count < 2)
            return $"* Invalid current switch {component.Name}: requires 2 nodes";

        if (component.Parameters == null || component.Parameters.Count == 0)
            return $"* Invalid current switch {component.Name}: requires controlSource and model parameters";

        var modelParam = component.Parameters.FirstOrDefault(p => p.Key.Equals("model", StringComparison.OrdinalIgnoreCase));
        if (modelParam.Key == null)
            return $"* Invalid current switch {component.Name}: requires model parameter";

        var modelName = modelParam.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modelName))
            return $"* Invalid current switch {component.Name}: model name cannot be empty";

        var controlSourceParam = component.Parameters.FirstOrDefault(p => 
            p.Key.Equals("controlSource", StringComparison.OrdinalIgnoreCase) ||
            p.Key.Equals("control_source", StringComparison.OrdinalIgnoreCase));
        
        if (controlSourceParam.Key == null)
            return $"* Invalid current switch {component.Name}: requires controlSource parameter";

        var controlSourceName = controlSourceParam.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(controlSourceName))
            return $"* Invalid current switch {component.Name}: controlSource name cannot be empty";

        var switchPos = component.Nodes[0];
        var switchNeg = component.Nodes[1];

        // SPICE format: W1 out+ out- V_CTRL CSW_MODEL
        return $"{component.Name} {switchPos} {switchNeg} {controlSourceName} {modelName}";
    }

    /// <summary>
    /// Gets the gain parameter from the parameters dictionary.
    /// </summary>
    private static double GetGainParam(Dictionary<string, object> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return 0.0;
        
        var gainParam = parameters.FirstOrDefault(p => p.Key.Equals("gain", StringComparison.OrdinalIgnoreCase));
        if (gainParam.Key == null)
            return 0.0;
        
        try
        {
            return Convert.ToDouble(gainParam.Value);
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Formats a behavioral voltage source with expression.
    /// SPICE syntax: Bname n+ n- V={expression}
    /// </summary>
    private static string FormatBehavioralVoltageSource(ComponentDefinition component)
    {
        if (component.Parameters == null || component.Parameters.Count == 0)
            return $"* Invalid behavioral voltage source {component.Name}: requires expression parameter";

        var expressionParam = component.Parameters.FirstOrDefault(p => 
            p.Key.Equals("expression", StringComparison.OrdinalIgnoreCase));
        
        if (expressionParam.Key == null || expressionParam.Value == null)
            return $"* Invalid behavioral voltage source {component.Name}: requires expression parameter";

        var expression = expressionParam.Value.ToString();
        
        if (string.IsNullOrWhiteSpace(expression))
            return $"* Invalid behavioral voltage source {component.Name}: expression cannot be empty";

        var nodesString = string.Join(" ", component.Nodes);
        return $"{component.Name} {nodesString} V={{{expression}}}";
    }

    /// <summary>
    /// Formats a behavioral current source with expression.
    /// SPICE syntax: Bname n+ n- I={expression}
    /// </summary>
    private static string FormatBehavioralCurrentSource(ComponentDefinition component)
    {
        if (component.Parameters == null || component.Parameters.Count == 0)
            return $"* Invalid behavioral current source {component.Name}: requires expression parameter";

        var expressionParam = component.Parameters.FirstOrDefault(p => 
            p.Key.Equals("expression", StringComparison.OrdinalIgnoreCase));
        
        if (expressionParam.Key == null || expressionParam.Value == null)
            return $"* Invalid behavioral current source {component.Name}: requires expression parameter";

        var expression = expressionParam.Value.ToString();
        
        if (string.IsNullOrWhiteSpace(expression))
            return $"* Invalid behavioral current source {component.Name}: expression cannot be empty";

        var nodesString = string.Join(" ", component.Nodes);
        return $"{component.Name} {nodesString} I={{{expression}}}";
    }

    /// <summary>
    /// Formats a subcircuit instance.
    /// SPICE syntax: X{name} {node1} {node2} ... {subcircuit_name}
    /// </summary>
    private static string FormatSubcircuit(ComponentDefinition component)
    {
        if (component.Nodes == null || component.Nodes.Count == 0)
            return $"* Invalid subcircuit {component.Name}: requires at least one node";

        if (string.IsNullOrWhiteSpace(component.Model))
            return $"* Invalid subcircuit {component.Name}: requires model (subcircuit name) parameter";

        var nodesString = string.Join(" ", component.Nodes);
        
        // SPICE format: X<name> <node1> <node2> ... <subcircuit_name>
        return $"{component.Name} {nodesString} {component.Model}";
    }
}

