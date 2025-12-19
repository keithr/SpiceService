using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Components;
using SpiceSharpBehavioral;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Factory for creating SpiceSharp component entities from component definitions.
/// </summary>
public class ComponentFactory
{
    /// <summary>
    /// Creates a SpiceSharp entity from a component definition.
    /// </summary>
    /// <param name="definition">The component definition</param>
    /// <returns>The created SpiceSharp entity</returns>
    /// <exception cref="ArgumentException">Thrown if component type is unsupported or validation fails</exception>
    public IEntity CreateComponent(ComponentDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.ComponentType))
            throw new ArgumentException("Component type is required.", nameof(definition));

        ValidateNodes(definition);

        // Handle component creation based on type
        return definition.ComponentType.ToLower() switch
        {
            "resistor" => CreateResistor(definition),
            "capacitor" => CreateCapacitor(definition),
            "inductor" => CreateInductor(definition),
            "diode" => CreateDiode(definition),
            "voltage_source" => CreateVoltageSource(definition),
            "current_source" => CreateCurrentSource(definition),
            // Semiconductors - basic support for now
            "bjt_npn" or "bjt_pnp" => CreateBJT(definition),
            "mosfet_n" or "mosfet_p" => CreateMOSFET(definition),
            "jfet_n" or "jfet_p" => CreateJFET(definition),
            // Dependent sources
            "vcvs" => CreateVCVS(definition),
            "vccs" => CreateVCCS(definition),
            "ccvs" => CreateCCVS(definition),
            "cccs" => CreateCCCS(definition),
            // Mutual inductance
            "mutual_inductance" => CreateMutualInductance(definition),
            // Switches
            "voltage_switch" => CreateVoltageSwitch(definition),
            "current_switch" => CreateCurrentSwitch(definition),
            // Behavioral sources (with expressions)
            "behavioral_voltage_source" => CreateBehavioralVoltageSource(definition),
            "behavioral_current_source" => CreateBehavioralCurrentSource(definition),
            "opamp" => throw new NotImplementedException("OpAmp component not yet implemented"),
            _ => throw new ArgumentException($"Unsupported component type: {definition.ComponentType}")
        };
    }

    private void ValidateNodes(ComponentDefinition definition)
    {
        // Mutual inductance doesn't use nodes - it references inductor names
        if (definition.ComponentType.Equals("mutual_inductance", StringComparison.OrdinalIgnoreCase))
            return; // Skip node validation for mutual inductance

        if (definition.Nodes == null || definition.Nodes.Count == 0)
            throw new ArgumentException("At least one node is required.");

        // Minimum node count validation based on type
        var minNodes = definition.ComponentType.ToLower() switch
        {
            "resistor" or "capacitor" or "inductor" or "diode" or "voltage_source" or "current_source" or "voltage_switch" or "current_switch" or "behavioral_voltage_source" or "behavioral_current_source" => 2,
            "bjt_npn" or "bjt_pnp" or "jfet_n" or "jfet_p" => 3,
            "mosfet_n" or "mosfet_p" or "vcvs" or "vccs" or "ccvs" or "cccs" => 4,
            _ => 2
        };

        if (definition.Nodes.Count < minNodes)
            throw new ArgumentException($"Component type '{definition.ComponentType}' requires at least {minNodes} nodes, but only {definition.Nodes.Count} provided.");
    }

    private IEntity CreateResistor(ComponentDefinition definition)
    {
        ValidateValue(definition);
        return new Resistor(definition.Name, definition.Nodes[0], definition.Nodes[1], definition.Value!.Value);
    }

    private IEntity CreateCapacitor(ComponentDefinition definition)
    {
        ValidateValue(definition);
        return new Capacitor(definition.Name, definition.Nodes[0], definition.Nodes[1], definition.Value!.Value);
    }

    private IEntity CreateInductor(ComponentDefinition definition)
    {
        ValidateValue(definition);
        return new Inductor(definition.Name, definition.Nodes[0], definition.Nodes[1], definition.Value!.Value);
    }

    private IEntity CreateDiode(ComponentDefinition definition)
    {
        // Diode requires a model
        if (string.IsNullOrWhiteSpace(definition.Model))
            throw new ArgumentException("Diode components require a model to be specified.");

        return new Diode(definition.Name, definition.Nodes[0], definition.Nodes[1], definition.Model);
    }

    private IEntity CreateVoltageSource(ComponentDefinition definition)
    {
        ValidateValue(definition);
        var source = new VoltageSource(definition.Name, definition.Nodes[0], definition.Nodes[1], definition.Value!.Value);
        
        // Configure waveform and AC parameters if provided
        if (definition.Parameters != null && definition.Parameters.Count > 0)
        {
            // Check for waveform parameter first (for transient analysis)
            var waveformParam = definition.Parameters
                .FirstOrDefault(p => p.Key.Equals("waveform", StringComparison.OrdinalIgnoreCase));
            var waveformType = waveformParam.Key != null ? waveformParam.Value?.ToString()?.ToLower() : null;
            
            // Auto-detect sine waveform if sine_amplitude or sine_frequency parameters are present
            if (string.IsNullOrWhiteSpace(waveformType))
            {
                var hasSineAmplitude = definition.Parameters.Any(p => 
                    p.Key.Equals("sine_amplitude", StringComparison.OrdinalIgnoreCase) ||
                    p.Key.Equals("amplitude", StringComparison.OrdinalIgnoreCase));
                var hasSineFrequency = definition.Parameters.Any(p => 
                    p.Key.Equals("sine_frequency", StringComparison.OrdinalIgnoreCase) ||
                    p.Key.Equals("frequency", StringComparison.OrdinalIgnoreCase));
                
                if (hasSineAmplitude || hasSineFrequency)
                {
                    waveformType = "sine";
                }
            }
            
            // Auto-detect pulse waveform if any pulse_* parameters are present
            if (string.IsNullOrWhiteSpace(waveformType))
            {
                var hasPulseParam = definition.Parameters.Any(p => 
                    p.Key.StartsWith("pulse_", StringComparison.OrdinalIgnoreCase));
                
                if (hasPulseParam)
                {
                    waveformType = "pulse";
                }
            }
            
            // Auto-detect PWL waveform if pwl_t0 or pwl_v0 parameters are present
            if (string.IsNullOrWhiteSpace(waveformType))
            {
                var hasPwlT0 = definition.Parameters.Any(p => 
                    p.Key.Equals("pwl_t0", StringComparison.OrdinalIgnoreCase));
                var hasPwlV0 = definition.Parameters.Any(p => 
                    p.Key.Equals("pwl_v0", StringComparison.OrdinalIgnoreCase));
                
                if (hasPwlT0 || hasPwlV0)
                {
                    waveformType = "pwl";
                }
            }
            
            if (!string.IsNullOrWhiteSpace(waveformType))
            {
                try
                {
                    var waveform = CreateWaveform(waveformType, definition.Parameters);
                    if (waveform != null)
                    {
                        source.Parameters.Waveform = waveform;
                    }
                }
                catch (NotImplementedException)
                {
                    // Waveform type not supported by SpiceSharp for simulation (e.g., SFFM, AM)
                    // But parameters are stored in definition for netlist export
                    // Continue without setting waveform on SpiceSharp entity
                }
                catch (ArgumentException ex) when (ex.Message.Contains("constructor not found") || ex.Message.Contains("Could not create PWL waveform"))
                {
                    // PWL waveform creation may fail due to SpiceSharp constructor issues - log and continue
                    // Parameters are stored in definition for netlist export
                    // Continue without setting waveform on SpiceSharp entity
                    // Note: Validation errors (missing points, etc.) are NOT caught here - they propagate
                    System.Diagnostics.Debug.WriteLine($"PWL waveform creation failed: {ex.Message}");
                }
                catch (Exception ex) when (ex is System.Reflection.TargetInvocationException || ex is MissingMethodException)
                {
                    // Reflection-related exceptions for PWL - log and continue
                    System.Diagnostics.Debug.WriteLine($"PWL waveform creation failed (reflection): {ex.GetType().Name} - {ex.Message}");
                }
            }
            
            // Configure AC parameters (for AC analysis)
            foreach (var param in definition.Parameters)
            {
                // Skip waveform parameter as it's already handled
                if (param.Key.Equals("waveform", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                try
                {
                    // Support AC magnitude for AC analysis
                    if (param.Key.Equals("acmag", StringComparison.OrdinalIgnoreCase) || 
                        param.Key.Equals("ac", StringComparison.OrdinalIgnoreCase))
                    {
                        var acValue = Convert.ToDouble(param.Value);
                        source.SetParameter("acmag", acValue);
                    }
                    else if (param.Key.Equals("acphase", StringComparison.OrdinalIgnoreCase))
                    {
                        // Note: SpiceSharp may not support acphase directly
                        // Phase is typically handled through complex AC values
                        // Try to set it, but don't fail if it's not supported
                        try
                        {
                            var phaseValue = Convert.ToDouble(param.Value);
                            source.SetParameter("acphase", phaseValue);
                        }
                        catch
                        {
                            // acphase may not be supported - that's okay, phase defaults to 0
                        }
                    }
                    else if (!IsWaveformParameter(param.Key))
                    {
                        // Allow other parameters to be set (but skip waveform-specific parameters)
                        var paramValue = Convert.ToDouble(param.Value);
                        source.SetParameter(param.Key, paramValue);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Could not find parameter"))
                {
                    // Parameter not supported - skip it
                    System.Diagnostics.Debug.WriteLine($"Parameter '{param.Key}' not supported on voltage source, skipping.");
                }
            }
        }
        
        return source;
    }

    private IEntity CreateCurrentSource(ComponentDefinition definition)
    {
        ValidateValue(definition);
        var source = new CurrentSource(definition.Name, definition.Nodes[0], definition.Nodes[1], definition.Value!.Value);
        
        // Configure waveform and AC parameters if provided
        if (definition.Parameters != null && definition.Parameters.Count > 0)
        {
            // Check for waveform parameter first (for transient analysis)
            var waveformParam = definition.Parameters
                .FirstOrDefault(p => p.Key.Equals("waveform", StringComparison.OrdinalIgnoreCase));
            var waveformType = waveformParam.Key != null ? waveformParam.Value?.ToString()?.ToLower() : null;
            
            // Auto-detect sine waveform if sine_amplitude or sine_frequency parameters are present
            if (string.IsNullOrWhiteSpace(waveformType))
            {
                var hasSineAmplitude = definition.Parameters.Any(p => 
                    p.Key.Equals("sine_amplitude", StringComparison.OrdinalIgnoreCase) ||
                    p.Key.Equals("amplitude", StringComparison.OrdinalIgnoreCase));
                var hasSineFrequency = definition.Parameters.Any(p => 
                    p.Key.Equals("sine_frequency", StringComparison.OrdinalIgnoreCase) ||
                    p.Key.Equals("frequency", StringComparison.OrdinalIgnoreCase));
                
                if (hasSineAmplitude || hasSineFrequency)
                {
                    waveformType = "sine";
                }
            }
            
            // Auto-detect pulse waveform if any pulse_* parameters are present
            if (string.IsNullOrWhiteSpace(waveformType))
            {
                var hasPulseParam = definition.Parameters.Any(p => 
                    p.Key.StartsWith("pulse_", StringComparison.OrdinalIgnoreCase));
                
                if (hasPulseParam)
                {
                    waveformType = "pulse";
                }
            }
            
            // Auto-detect PWL waveform if pwl_t0 or pwl_v0 parameters are present
            if (string.IsNullOrWhiteSpace(waveformType))
            {
                var hasPwlT0 = definition.Parameters.Any(p => 
                    p.Key.Equals("pwl_t0", StringComparison.OrdinalIgnoreCase));
                var hasPwlV0 = definition.Parameters.Any(p => 
                    p.Key.Equals("pwl_v0", StringComparison.OrdinalIgnoreCase));
                
                if (hasPwlT0 || hasPwlV0)
                {
                    waveformType = "pwl";
                }
            }
            
            if (!string.IsNullOrWhiteSpace(waveformType))
            {
                try
                {
                    var waveform = CreateWaveform(waveformType, definition.Parameters);
                    if (waveform != null)
                    {
                        source.Parameters.Waveform = waveform;
                    }
                }
                catch (NotImplementedException)
                {
                    // Waveform type not supported by SpiceSharp for simulation (e.g., SFFM, AM)
                    // But parameters are stored in definition for netlist export
                    // Continue without setting waveform on SpiceSharp entity
                }
                catch (ArgumentException ex) when (ex.Message.Contains("constructor not found") || ex.Message.Contains("Could not create PWL waveform"))
                {
                    // PWL waveform creation may fail due to SpiceSharp constructor issues - log and continue
                    // Parameters are stored in definition for netlist export
                    // Continue without setting waveform on SpiceSharp entity
                    // Note: Validation errors (missing points, etc.) are NOT caught here - they propagate
                    System.Diagnostics.Debug.WriteLine($"PWL waveform creation failed: {ex.Message}");
                }
                catch (Exception ex) when (ex is System.Reflection.TargetInvocationException || ex is MissingMethodException)
                {
                    // Reflection-related exceptions for PWL - log and continue
                    System.Diagnostics.Debug.WriteLine($"PWL waveform creation failed (reflection): {ex.GetType().Name} - {ex.Message}");
                }
            }
            
            // Configure AC parameters (for AC analysis)
            foreach (var param in definition.Parameters)
            {
                // Skip waveform parameter as it's already handled
                if (param.Key.Equals("waveform", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                try
                {
                    // Support AC magnitude for AC analysis
                    if (param.Key.Equals("acmag", StringComparison.OrdinalIgnoreCase) || 
                        param.Key.Equals("ac", StringComparison.OrdinalIgnoreCase))
                    {
                        var acValue = Convert.ToDouble(param.Value);
                        source.SetParameter("acmag", acValue);
                    }
                    else if (param.Key.Equals("acphase", StringComparison.OrdinalIgnoreCase))
                    {
                        // Note: SpiceSharp may not support acphase directly
                        // Phase is typically handled through complex AC values
                        // Try to set it, but don't fail if it's not supported
                        try
                        {
                            var phaseValue = Convert.ToDouble(param.Value);
                            source.SetParameter("acphase", phaseValue);
                        }
                        catch
                        {
                            // acphase may not be supported - that's okay, phase defaults to 0
                        }
                    }
                    else if (!IsWaveformParameter(param.Key))
                    {
                        // Allow other parameters to be set (but skip waveform-specific parameters)
                        var paramValue = Convert.ToDouble(param.Value);
                        source.SetParameter(param.Key, paramValue);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Could not find parameter"))
                {
                    // Parameter not supported - skip it
                    System.Diagnostics.Debug.WriteLine($"Parameter '{param.Key}' not supported on current source, skipping.");
                }
            }
        }
        
        return source;
    }

    private IEntity CreateBJT(ComponentDefinition definition)
    {
        // BJT requires a model
        if (string.IsNullOrWhiteSpace(definition.Model))
            throw new ArgumentException("BJT components require a model to be specified.");

        // Nodes: collector, base, emitter[, substrate]
        if (definition.Nodes.Count < 3)
            throw new ArgumentException("BJT requires at least 3 nodes: collector, base, emitter.");

        // Collector, Base, Emitter are required
        var collector = definition.Nodes[0];
        var baseNode = definition.Nodes[1];
        var emitter = definition.Nodes[2];
        
        // Substrate is optional - default to ground if not provided
        var substrate = definition.Nodes.Count > 3 ? definition.Nodes[3] : "0";

        return new BipolarJunctionTransistor(
            definition.Name,
            collector,
            baseNode,
            emitter,
            substrate,
            definition.Model
        );
    }

    private IEntity CreateMOSFET(ComponentDefinition definition)
    {
        // MOSFET requires a model
        if (string.IsNullOrWhiteSpace(definition.Model))
            throw new ArgumentException("MOSFET components require a model to be specified.");

        if (definition.Nodes.Count < 4)
            throw new ArgumentException("MOSFET requires 4 nodes: drain, gate, source, bulk.");

        // Drain, Gate, Source, Bulk
        var drain = definition.Nodes[0];
        var gate = definition.Nodes[1];
        var source = definition.Nodes[2];
        var bulk = definition.Nodes[3];

        return new Mosfet1(
            definition.Name,
            drain,
            gate,
            source,
            bulk,
            definition.Model
        );
    }

    private IEntity CreateJFET(ComponentDefinition definition)
    {
        // JFET requires a model
        if (string.IsNullOrWhiteSpace(definition.Model))
            throw new ArgumentException("JFET components require a model to be specified.");

        if (definition.Nodes.Count < 3)
            throw new ArgumentException("JFET requires 3 nodes: drain, gate, source.");

        // Drain, Gate, Source
        var drain = definition.Nodes[0];
        var gate = definition.Nodes[1];
        var source = definition.Nodes[2];

        return new JFET(
            definition.Name,
            drain,
            gate,
            source,
            definition.Model
        );
    }

    private IEntity CreateVCVS(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 4)
            throw new ArgumentException("VCVS requires 4 nodes: output+, output-, input+, input-.");

        ValidateGain(definition);

        // Nodes: output+, output-, input+, input-
        var outputPos = definition.Nodes[0];
        var outputNeg = definition.Nodes[1];
        var inputPos = definition.Nodes[2];
        var inputNeg = definition.Nodes[3];

        var gain = GetGainParameter(definition.Parameters);

        return new VoltageControlledVoltageSource(
            definition.Name,
            outputPos,
            outputNeg,
            inputPos,
            inputNeg,
            gain
        );
    }

    private IEntity CreateVCCS(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 4)
            throw new ArgumentException("VCCS requires 4 nodes: output+, output-, input+, input-.");

        ValidateGain(definition);

        // Nodes: output+, output-, input+, input-
        var outputPos = definition.Nodes[0];
        var outputNeg = definition.Nodes[1];
        var inputPos = definition.Nodes[2];
        var inputNeg = definition.Nodes[3];

        var gain = GetGainParameter(definition.Parameters);

        return new VoltageControlledCurrentSource(
            definition.Name,
            outputPos,
            outputNeg,
            inputPos,
            inputNeg,
            gain
        );
    }

    private IEntity CreateCCVS(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 4)
            throw new ArgumentException("CCVS requires 4 nodes: output+, output-, control+, control-.");

        ValidateGain(definition);

        // Nodes: output+, output-, control+, control-
        // Note: In SPICE, CCVS references a voltage source name, but for API consistency we use nodes
        // We'll need to create a zero-voltage source for the control current and reference it
        var outputPos = definition.Nodes[0];
        var outputNeg = definition.Nodes[1];
        var controlPos = definition.Nodes[2];
        var controlNeg = definition.Nodes[3];

        var gain = GetGainParameter(definition.Parameters);

        // For current-controlled sources, SpiceSharp requires a voltage source name
        // We'll use a convention: create a zero-voltage source name from the control nodes
        // Format: V_CTRL_{controlPos}_{controlNeg} (sanitized)
        var controlSourceName = $"V_CTRL_{controlPos}_{controlNeg}".Replace("-", "_").Replace("+", "P");

        // Constructor: CurrentControlledVoltageSource(name, output+, output-, voltageSourceName, gain)
        return new CurrentControlledVoltageSource(
            definition.Name,
            outputPos,
            outputNeg,
            controlSourceName,
            gain
        );
    }

    private IEntity CreateCCCS(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 4)
            throw new ArgumentException("CCCS requires 4 nodes: output+, output-, control+, control-.");

        ValidateGain(definition);

        // Nodes: output+, output-, control+, control-
        var outputPos = definition.Nodes[0];
        var outputNeg = definition.Nodes[1];
        var controlPos = definition.Nodes[2];
        var controlNeg = definition.Nodes[3];

        var gain = GetGainParameter(definition.Parameters);

        // For current-controlled sources, SpiceSharp requires a voltage source name
        var controlSourceName = $"V_CTRL_{controlPos}_{controlNeg}".Replace("-", "_").Replace("+", "P");

        // Constructor: CurrentControlledCurrentSource(name, output+, output-, voltageSourceName, gain)
        return new CurrentControlledCurrentSource(
            definition.Name,
            outputPos,
            outputNeg,
            controlSourceName,
            gain
        );
    }

    private IEntity CreateMutualInductance(ComponentDefinition definition)
    {
        // Mutual inductance requires inductor1, inductor2, and coupling parameters
        if (definition.Parameters == null || definition.Parameters.Count == 0)
            throw new ArgumentException("Mutual inductance requires 'inductor1', 'inductor2', and 'coupling' parameters.");

        var inductor1Param = definition.Parameters.FirstOrDefault(p => p.Key.Equals("inductor1", StringComparison.OrdinalIgnoreCase));
        var inductor2Param = definition.Parameters.FirstOrDefault(p => p.Key.Equals("inductor2", StringComparison.OrdinalIgnoreCase));
        var couplingParam = definition.Parameters.FirstOrDefault(p => p.Key.Equals("coupling", StringComparison.OrdinalIgnoreCase));

        if (inductor1Param.Key == null)
            throw new ArgumentException("Mutual inductance requires 'inductor1' parameter specifying the first inductor name.");
        
        if (inductor2Param.Key == null)
            throw new ArgumentException("Mutual inductance requires 'inductor2' parameter specifying the second inductor name.");

        if (couplingParam.Key == null)
            throw new ArgumentException("Mutual inductance requires 'coupling' parameter (k factor, 0 < k <= 1).");

        var inductor1 = inductor1Param.Value?.ToString() ?? string.Empty;
        var inductor2 = inductor2Param.Value?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inductor1))
            throw new ArgumentException("'inductor1' parameter cannot be empty.");

        if (string.IsNullOrWhiteSpace(inductor2))
            throw new ArgumentException("'inductor2' parameter cannot be empty.");

        if (inductor1.Equals(inductor2, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("'inductor1' and 'inductor2' must be different inductor names.");

        // Parse coupling factor
        double coupling;
        try
        {
            coupling = Convert.ToDouble(couplingParam.Value);
        }
        catch
        {
            throw new ArgumentException($"Coupling parameter must be a numeric value between 0 and 1, but got: {couplingParam.Value}");
        }

        // Validate coupling factor: 0 < k <= 1
        if (coupling <= 0 || coupling > 1)
            throw new ArgumentException($"Coupling factor must be between 0 and 1 (0 < k <= 1), but got: {coupling}");

        // Create MutualInductance: MutualInductance(name, inductor1Name, inductor2Name, coupling)
        return new MutualInductance(definition.Name, inductor1, inductor2, coupling);
    }

    private IEntity CreateVoltageSwitch(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 2)
            throw new ArgumentException("Voltage switch requires 2 nodes: switch+, switch-.");

        // Validate model parameter
        if (definition.Parameters == null || definition.Parameters.Count == 0)
            throw new ArgumentException("Voltage switch requires a 'model' parameter to be specified.");

        var modelParam = definition.Parameters.FirstOrDefault(p => p.Key.Equals("model", StringComparison.OrdinalIgnoreCase));
        if (modelParam.Key == null)
            throw new ArgumentException("Voltage switch requires a 'model' parameter to be specified.");

        var modelName = modelParam.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model parameter cannot be empty.");

        // Get control nodes
        var controlNodesParam = definition.Parameters.FirstOrDefault(p => p.Key.Equals("controlNodes", StringComparison.OrdinalIgnoreCase));
        if (controlNodesParam.Key == null)
            throw new ArgumentException("Voltage switch requires 'controlNodes' parameter specifying [controlNode+, controlNode-].");

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
            throw new ArgumentException("'controlNodes' parameter must be an array of 2 node names: [controlNode+, controlNode-].");
        }

        if (controlNodes.Length != 2)
            throw new ArgumentException($"Voltage switch requires exactly 2 control nodes, but got {controlNodes.Length}.");

        if (string.IsNullOrWhiteSpace(controlNodes[0]) || string.IsNullOrWhiteSpace(controlNodes[1]))
            throw new ArgumentException("Control nodes cannot be empty.");

        // Nodes: switch+, switch-
        var switchPos = definition.Nodes[0];
        var switchNeg = definition.Nodes[1];
        var controlPos = controlNodes[0];
        var controlNeg = controlNodes[1];

        // Create VoltageSwitch: VoltageSwitch(name, switch+, switch-, control+, control-, modelName)
        return new VoltageSwitch(definition.Name, switchPos, switchNeg, controlPos, controlNeg, modelName);
    }

    private IEntity CreateCurrentSwitch(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 2)
            throw new ArgumentException("Current switch requires 2 nodes: switch+, switch-.");

        // Validate model parameter
        if (definition.Parameters == null || definition.Parameters.Count == 0)
            throw new ArgumentException("Current switch requires a 'model' parameter to be specified.");

        var modelParam = definition.Parameters.FirstOrDefault(p => p.Key.Equals("model", StringComparison.OrdinalIgnoreCase));
        if (modelParam.Key == null)
            throw new ArgumentException("Current switch requires a 'model' parameter to be specified.");

        var modelName = modelParam.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model parameter cannot be empty.");

        // Get control source name
        var controlSourceParam = definition.Parameters.FirstOrDefault(p => 
            p.Key.Equals("controlSource", StringComparison.OrdinalIgnoreCase) ||
            p.Key.Equals("control_source", StringComparison.OrdinalIgnoreCase));
        
        if (controlSourceParam.Key == null)
            throw new ArgumentException("Current switch requires 'controlSource' parameter specifying the voltage source name that controls the switch.");

        var controlSourceName = controlSourceParam.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(controlSourceName))
            throw new ArgumentException("Control source parameter cannot be empty.");

        // Nodes: switch+, switch-
        var switchPos = definition.Nodes[0];
        var switchNeg = definition.Nodes[1];

        // Create CurrentSwitch: CurrentSwitch(name, switch+, switch-, modelName)
        // Then set the control source via parameter
        var switchEntity = new CurrentSwitch(definition.Name, switchPos, switchNeg, modelName);
        
        // Set the control source parameter
        try
        {
            switchEntity.SetParameter("control", controlSourceName);
        }
        catch
        {
            // Try alternative parameter names
            try
            {
                switchEntity.SetParameter("controlSource", controlSourceName);
            }
            catch
            {
                // If parameter setting fails, the switch might use constructor parameter
                // Try constructor with control source as 4th parameter
                return new CurrentSwitch(definition.Name, switchPos, switchNeg, controlSourceName);
            }
        }
        
        return switchEntity;
    }

    private void ValidateGain(ComponentDefinition definition)
    {
        if (definition.Parameters == null || definition.Parameters.Count == 0)
            throw new ArgumentException($"Component type '{definition.ComponentType}' requires a 'gain' parameter to be specified.");

        if (!definition.Parameters.Any(p => p.Key.Equals("gain", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Component type '{definition.ComponentType}' requires a 'gain' parameter to be specified.");
    }

    private double GetGainParameter(Dictionary<string, object> parameters)
    {
        var gainParam = parameters.FirstOrDefault(p => p.Key.Equals("gain", StringComparison.OrdinalIgnoreCase));
        if (gainParam.Key == null)
            throw new ArgumentException("Gain parameter is required for dependent sources.");

        try
        {
            return Convert.ToDouble(gainParam.Value);
        }
        catch
        {
            throw new ArgumentException($"Gain parameter must be a numeric value, but got: {gainParam.Value}");
        }
    }

    private void ValidateValue(ComponentDefinition definition)
    {
        if (!definition.Value.HasValue)
            throw new ArgumentException($"Component type '{definition.ComponentType}' requires a value to be specified.");
    }

    /// <summary>
    /// Creates a waveform object based on the waveform type and parameters.
    /// </summary>
    /// <param name="waveformType">The type of waveform (e.g., "sine", "pulse", "pwl", "exponential")</param>
    /// <param name="parameters">The parameters dictionary containing waveform-specific parameters</param>
    /// <returns>The created waveform object, or null if waveform type is not supported</returns>
    /// <exception cref="ArgumentException">Thrown if required parameters are missing</exception>
    private IWaveformDescription? CreateWaveform(string waveformType, Dictionary<string, object> parameters)
    {
        return waveformType.ToLower() switch
        {
            "sine" => CreateSineWaveform(parameters),
            "pulse" => CreatePulseWaveform(parameters),
            "pwl" => CreatePwlWaveform(parameters),
            "sffm" => CreateSffmWaveform(parameters),
            "am" => CreateAmWaveform(parameters),
            _ => throw new ArgumentException(
                $"Unsupported waveform type: '{waveformType}'. " +
                $"Supported types: sine, pulse, pwl, sffm, am. " +
                $"Use GET /api/components/types to discover available waveform parameters and examples.")
        };
    }

    /// <summary>
    /// Creates a Sine waveform object from parameters.
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <returns>A Sine waveform object</returns>
    /// <exception cref="ArgumentException">Thrown if required parameters (amplitude, frequency) are missing</exception>
    private Sine CreateSineWaveform(Dictionary<string, object> parameters)
    {
        // Required parameters - support both "amplitude"/"frequency" and "sine_amplitude"/"sine_frequency"
        if (!TryGetDoubleParameter(parameters, "amplitude", out var amplitude) &&
            !TryGetDoubleParameter(parameters, "sine_amplitude", out amplitude))
        {
            throw new ArgumentException("Missing required parameter 'amplitude' or 'sine_amplitude' for waveform type 'sine'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "frequency", out var frequency) &&
            !TryGetDoubleParameter(parameters, "sine_frequency", out frequency))
        {
            throw new ArgumentException("Missing required parameter 'frequency' or 'sine_frequency' for waveform type 'sine'.");
        }

        // Optional parameters with defaults
        var offset = GetDoubleParameter(parameters, "offset", 0.0);
        var delay = GetDoubleParameter(parameters, "delay", 0.0);
        var theta = GetDoubleParameter(parameters, "damping", 0.0); // Map "damping" to "theta" (SpiceSharp uses theta)
        var phase = GetDoubleParameter(parameters, "phase", 0.0);

        // Create Sine object using the 6-parameter constructor to support all options
        // Constructor: Sine(offset, amplitude, frequency, delay, theta, phase)
        return new Sine(offset, amplitude, frequency, delay, theta, phase);
    }

    /// <summary>
    /// Creates a Pulse waveform object from parameters.
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <returns>A Pulse waveform object</returns>
    /// <exception cref="ArgumentException">Thrown if required parameters (v1, v2) are missing</exception>
    private Pulse CreatePulseWaveform(Dictionary<string, object> parameters)
    {
        // Check if any pulse parameters are present (to detect incomplete pulse configuration)
        var hasAnyPulseParam = parameters.Any(p => 
            p.Key.StartsWith("pulse_", StringComparison.OrdinalIgnoreCase) ||
            (p.Key.Equals("waveform", StringComparison.OrdinalIgnoreCase) && 
             p.Value?.ToString()?.Equals("pulse", StringComparison.OrdinalIgnoreCase) == true));
        
        // Required parameters: v1, v2 (support both flat "pulse_v1" and structured "v1" formats)
        bool hasV1 = TryGetDoubleParameter(parameters, "v1", out var v1) ||
                     TryGetDoubleParameter(parameters, "pulse_v1", out v1);
        bool hasV2 = TryGetDoubleParameter(parameters, "v2", out var v2) ||
                     TryGetDoubleParameter(parameters, "pulse_v2", out v2);
        
        if (!hasV1 && !hasV2)
        {
            // Neither parameter present - provide helpful error
            throw new ArgumentException(
                "Missing required parameters for PULSE waveform. " +
                "Required: 'pulse_v1' (initial value) and 'pulse_v2' (pulsed value). " +
                "Optional: 'pulse_td' (delay), 'pulse_tr' (rise time), 'pulse_tf' (fall time), " +
                "'pulse_pw' (pulse width), 'pulse_per' (period). " +
                "Example: { \"pulse_v1\": 0.0, \"pulse_v2\": 5.0, \"pulse_td\": 0.0, \"pulse_tr\": 1e-6, \"pulse_tf\": 1e-6, \"pulse_pw\": 1e-3, \"pulse_per\": 2e-3 }");
        }
        
        if (!hasV1)
        {
            throw new ArgumentException(
                "Missing required parameter 'pulse_v1' (or 'v1') for PULSE waveform. " +
                "This parameter specifies the initial value. " +
                "Example: { \"pulse_v1\": 0.0, \"pulse_v2\": 5.0 }");
        }
        
        if (!hasV2)
        {
            throw new ArgumentException(
                "Missing required parameter 'pulse_v2' (or 'v2') for PULSE waveform. " +
                "This parameter specifies the pulsed value. " +
                "Example: { \"pulse_v1\": 0.0, \"pulse_v2\": 5.0 }");
        }
        
        // Optional parameters with defaults (SPICE standard allows defaults)
        // Support both flat "pulse_td" and structured "td" formats
        var td = 0.0;
        if (!TryGetDoubleParameter(parameters, "td", out td))
            TryGetDoubleParameter(parameters, "pulse_td", out td);
        
        var tr = 0.0;
        if (!TryGetDoubleParameter(parameters, "tr", out tr))
            TryGetDoubleParameter(parameters, "pulse_tr", out tr);
        if (tr == 0.0)
            tr = 1e-9; // Default rise time
        
        var tf = 0.0;
        if (!TryGetDoubleParameter(parameters, "tf", out tf))
            TryGetDoubleParameter(parameters, "pulse_tf", out tf);
        if (tf == 0.0)
            tf = 1e-9; // Default fall time
        
        var pw = 0.0;
        if (!TryGetDoubleParameter(parameters, "pw", out pw))
            TryGetDoubleParameter(parameters, "pulse_pw", out pw);
        if (pw == 0.0)
            pw = 1e-3; // Default pulse width
        
        var per = 0.0;
        if (!TryGetDoubleParameter(parameters, "per", out per))
            TryGetDoubleParameter(parameters, "pulse_per", out per);
        if (per == 0.0)
            per = 2e-3; // Default period

        // Create Pulse object
        // Constructor: Pulse(initialValue, pulsedValue, riseDelay, riseTime, fallDelay, fallTime, pulseWidth, period)
        // Note: SpiceSharp uses: Pulse(v1, v2, td, tr, tf, pw, per)
        return new Pulse(v1, v2, td, tr, tf, pw, per);
    }

    /// <summary>
    /// Creates a PWL (Piecewise Linear) waveform object from parameters.
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <returns>A PWL waveform object</returns>
    /// <exception cref="ArgumentException">Thrown if required parameters are missing</exception>
    private IWaveformDescription CreatePwlWaveform(Dictionary<string, object> parameters)
    {
        var points = new List<(double time, double value)>();
        
        // Check for flat parameter format first (pwl_t0, pwl_v0, pwl_t1, pwl_v1, ...)
        var pwlTimeParams = parameters
            .Where(p => p.Key.StartsWith("pwl_t", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => ExtractIndex(p.Key))
            .ToList();
        var pwlVoltageParams = parameters
            .Where(p => p.Key.StartsWith("pwl_v", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => ExtractIndex(p.Key))
            .ToList();
        
        if (pwlTimeParams.Count > 0 || pwlVoltageParams.Count > 0)
        {
            // Validate that we have matching time and voltage parameters
            if (pwlTimeParams.Count != pwlVoltageParams.Count)
            {
                throw new ArgumentException(
                    $"PWL waveform requires matching time and voltage parameters. " +
                    $"Found {pwlTimeParams.Count} time parameter(s) (pwl_t*) and {pwlVoltageParams.Count} voltage parameter(s) (pwl_v*). " +
                    $"Each time parameter must have a corresponding voltage parameter. " +
                    $"Example: {{ \"pwl_t0\": 0.0, \"pwl_v0\": 0.0, \"pwl_t1\": 1e-3, \"pwl_v1\": 3.0 }}. " +
                    $"Use GET /api/components/types to see more examples.");
            }
            
            if (pwlTimeParams.Count < 2)
            {
                throw new ArgumentException(
                    $"PWL waveform requires at least 2 points (pairs of pwl_t*/pwl_v* parameters). " +
                    $"Found only {pwlTimeParams.Count} point(s). " +
                    $"Minimum example: {{ \"pwl_t0\": 0.0, \"pwl_v0\": 0.0, \"pwl_t1\": 1e-3, \"pwl_v1\": 3.0 }}. " +
                    $"Use GET /api/components/types to see more examples.");
            }
            
            // Extract points from flat parameters
            for (int i = 0; i < pwlTimeParams.Count; i++)
            {
                try
                {
                    var time = Convert.ToDouble(pwlTimeParams[i].Value);
                    var value = Convert.ToDouble(pwlVoltageParams[i].Value);
                    points.Add((time, value));
                }
                catch
                {
                    throw new ArgumentException(
                        $"Invalid PWL parameter values. pwl_t{i} and pwl_v{i} must be numeric values. " +
                        $"Got: pwl_t{i}={pwlTimeParams[i].Value}, pwl_v{i}={pwlVoltageParams[i].Value}");
                }
            }
        }
        else
        {
            // Fall back to structured format with "points" parameter
            var pointsParam = parameters.FirstOrDefault(p => p.Key.Equals("points", StringComparison.OrdinalIgnoreCase));
            if (pointsParam.Key == null)
            {
                throw new ArgumentException(
                    "Missing required parameters for waveform type 'pwl'. " +
                    "Either provide flat parameters (pwl_t0, pwl_v0, pwl_t1, pwl_v1, ...) " +
                    "or a 'points' parameter with an array of [time, voltage] pairs. " +
                    "Example (flat format): { \"pwl_t0\": 0.0, \"pwl_v0\": 0.0, \"pwl_t1\": 1e-3, \"pwl_v1\": 3.0 }. " +
                    "Use GET /api/components/types to see more examples.");
            }

            if (pointsParam.Value is not object[] pointsArray || pointsArray.Length == 0)
            {
                throw new ArgumentException("Parameter 'points' must be a non-empty array of [time, voltage] pairs for waveform type 'pwl'.");
            }

            foreach (var point in pointsArray)
            {
                if (point is not object[] pointPair || pointPair.Length != 2)
                {
                    throw new ArgumentException("Each point in 'points' array must be a [time, voltage] pair (array of 2 elements).");
                }

                try
                {
                    var time = Convert.ToDouble(pointPair[0]);
                    var value = Convert.ToDouble(pointPair[1]);
                    points.Add((time, value));
                }
                catch
                {
                    throw new ArgumentException($"Invalid point format. Each point must be [time, voltage] where both are numeric values. Got: [{pointPair[0]}, {pointPair[1]}]");
                }
            }
        }
        
        // Sort points by time (SpiceSharp may require monotonic time)
        points = points.OrderBy(p => p.time).ToList();
        
        // Create PWL waveform using SpiceSharp's Pwl class
        // Pwl constructor takes alternating time/value pairs: Pwl(t1, v1, t2, v2, ...)
        var flatPoints = new List<double>();
        foreach (var (time, value) in points)
        {
            flatPoints.Add(time);
            flatPoints.Add(value);
        }
        
        // SpiceSharp.Pwl has a parameterless constructor - create instance and set points via SetPoints method
        var pwl = new SpiceSharp.Components.Pwl();
        var doubleArray = flatPoints.ToArray();
        
        // Use SetPoints method which takes Double[]
        pwl.SetPoints(doubleArray);
        return pwl;
    }
    
    /// <summary>
    /// Extracts the numeric index from a PWL parameter name (e.g., "pwl_t0" -> 0, "pwl_v5" -> 5).
    /// </summary>
    private int ExtractIndex(string key)
    {
        // Extract number after "pwl_t" or "pwl_v"
        var match = System.Text.RegularExpressions.Regex.Match(key, @"pwl_[tv](\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
        {
            return index;
        }
        return int.MaxValue; // Put unmatched keys at the end
    }

    /// <summary>
    /// Creates an SFFM (Single-Frequency FM) waveform object from parameters.
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <returns>An SFFM waveform object</returns>
    /// <exception cref="ArgumentException">Thrown if required parameters are missing</exception>
    private IWaveformDescription CreateSffmWaveform(Dictionary<string, object> parameters)
    {
        // Required parameters: vo, va, fc, mdi, fs
        if (!TryGetDoubleParameter(parameters, "vo", out var vo))
        {
            throw new ArgumentException("Missing required parameter 'vo' for waveform type 'sffm'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "va", out var va))
        {
            throw new ArgumentException("Missing required parameter 'va' for waveform type 'sffm'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "fc", out var fc))
        {
            throw new ArgumentException("Missing required parameter 'fc' for waveform type 'sffm'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "mdi", out var mdi))
        {
            throw new ArgumentException("Missing required parameter 'mdi' for waveform type 'sffm'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "fs", out var fs))
        {
            throw new ArgumentException("Missing required parameter 'fs' for waveform type 'sffm'.");
        }

        // Note: SpiceSharp may not have a direct SFFM class
        // For now, we'll throw NotImplementedException if SpiceSharp doesn't support it
        // The netlist export will still work correctly
        throw new NotImplementedException("SFFM waveform creation is not yet supported by SpiceSharp. Netlist export is available.");
    }

    /// <summary>
    /// Creates an AM (Amplitude Modulation) waveform object from parameters.
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <returns>An AM waveform object</returns>
    /// <exception cref="ArgumentException">Thrown if required parameters are missing</exception>
    private IWaveformDescription CreateAmWaveform(Dictionary<string, object> parameters)
    {
        // Required parameters: vo, va, mf, fc
        if (!TryGetDoubleParameter(parameters, "vo", out var vo))
        {
            throw new ArgumentException("Missing required parameter 'vo' for waveform type 'am'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "va", out var va))
        {
            throw new ArgumentException("Missing required parameter 'va' for waveform type 'am'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "mf", out var mf))
        {
            throw new ArgumentException("Missing required parameter 'mf' for waveform type 'am'.");
        }
        
        if (!TryGetDoubleParameter(parameters, "fc", out var fc))
        {
            throw new ArgumentException("Missing required parameter 'fc' for waveform type 'am'.");
        }

        // Note: SpiceSharp may not have a direct AM class
        // For now, we'll throw NotImplementedException if SpiceSharp doesn't support it
        // The netlist export will still work correctly
        throw new NotImplementedException("AM waveform creation is not yet supported by SpiceSharp. Netlist export is available.");
    }

    /// <summary>
    /// Checks if a parameter key is a waveform-specific parameter (not a general component parameter).
    /// </summary>
    private bool IsWaveformParameter(string key)
    {
        // Check for PWL flat parameters (pwl_t*, pwl_v*)
        if (key.StartsWith("pwl_t", StringComparison.OrdinalIgnoreCase) || 
            key.StartsWith("pwl_v", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        var waveformParams = new[] { 
            "offset", "amplitude", "frequency", "delay", "damping", "theta", "phase",
            "sine_amplitude", "sine_frequency", "sine_offset", "sine_delay", "sine_damping", "sine_phase", // SINE flat parameters
            "v1", "v2", "td", "tr", "tf", "pw", "per", // Pulse structured parameters
            "pulse_v1", "pulse_v2", "pulse_td", "pulse_tr", "pulse_tf", "pulse_pw", "pulse_per", // Pulse flat parameters
            "points", // PWL structured parameters
            "vo", "va", "fc", "mdi", "fs", // SFFM parameters
            "mf" // AM parameters (fc shared with SFFM)
        };
        return waveformParams.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tries to get a double parameter from the parameters dictionary.
    /// </summary>
    private bool TryGetDoubleParameter(Dictionary<string, object> parameters, string key, out double value)
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
    private double GetDoubleParameter(Dictionary<string, object> parameters, string key, double defaultValue)
    {
        if (TryGetDoubleParameter(parameters, key, out var value))
            return value;
        return defaultValue;
    }

    /// <summary>
    /// Create a behavioral voltage source with expression support.
    /// Behavioral sources allow mathematical expressions like "V(input) * 2.5" or "V(a) - V(b)".
    /// </summary>
    /// <param name="definition">Component definition containing expression parameter</param>
    /// <returns>BehavioralVoltageSource entity</returns>
    private IEntity CreateBehavioralVoltageSource(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 2)
            throw new ArgumentException("Behavioral voltage source requires 2 nodes: output+, output-.");

        ValidateExpression(definition);

        var outputPos = definition.Nodes[0];
        var outputNeg = definition.Nodes[1];
        var expression = GetStringParameter(definition.Parameters, "expression");

        return new BehavioralVoltageSource(
            definition.Name,
            outputPos,
            outputNeg,
            expression
        );
    }

    /// <summary>
    /// Create a behavioral current source with expression support.
    /// Behavioral sources allow mathematical expressions like "V(ref) / 1000" or "I(Vsense) * 2".
    /// </summary>
    /// <param name="definition">Component definition containing expression parameter</param>
    /// <returns>BehavioralCurrentSource entity</returns>
    private IEntity CreateBehavioralCurrentSource(ComponentDefinition definition)
    {
        if (definition.Nodes.Count < 2)
            throw new ArgumentException("Behavioral current source requires 2 nodes: output+, output-.");

        ValidateExpression(definition);

        var outputPos = definition.Nodes[0];
        var outputNeg = definition.Nodes[1];
        var expression = GetStringParameter(definition.Parameters, "expression");

        // SpiceSharp BehavioralCurrentSource uses a sign convention where positive expression
        // values result in current flowing in the opposite direction than expected.
        // To match standard SPICE convention (positive current flows from first node to second),
        // we negate the expression.
        var negatedExpression = $"-({expression})";

        return new BehavioralCurrentSource(
            definition.Name,
            outputPos,
            outputNeg,
            negatedExpression
        );
    }

    /// <summary>
    /// Validate that an expression parameter is provided and not empty.
    /// </summary>
    /// <param name="definition">Component definition to validate</param>
    private void ValidateExpression(ComponentDefinition definition)
    {
        if (definition.Parameters == null || definition.Parameters.Count == 0)
            throw new ArgumentException($"Component type '{definition.ComponentType}' requires an 'expression' parameter to be specified.");

        if (!definition.Parameters.Any(p => p.Key.Equals("expression", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Component type '{definition.ComponentType}' requires an 'expression' parameter to be specified.");

        var expressionParam = definition.Parameters.FirstOrDefault(p => p.Key.Equals("expression", StringComparison.OrdinalIgnoreCase));
        
        if (expressionParam.Value == null)
            throw new ArgumentException("Expression parameter cannot be null.");

        var expressionValue = expressionParam.Value.ToString();
        
        if (string.IsNullOrWhiteSpace(expressionValue))
            throw new ArgumentException("Expression parameter cannot be empty or whitespace.");
    }

    /// <summary>
    /// Get a string parameter from the parameters dictionary.
    /// </summary>
    /// <param name="parameters">Parameters dictionary</param>
    /// <param name="key">Parameter key to find</param>
    /// <returns>String value of the parameter</returns>
    private string GetStringParameter(Dictionary<string, object> parameters, string key)
    {
        var param = parameters.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        
        if (param.Key == null)
            throw new ArgumentException($"Parameter '{key}' is required.");

        if (param.Value == null)
            throw new ArgumentException($"Parameter '{key}' cannot be null.");

        return param.Value.ToString() ?? throw new ArgumentException($"Parameter '{key}' must be a string.");
    }
}

