using SpiceSharp;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Simulations;
using SpiceSharp.Behaviors;
using SpiceSharp.Components;
using System.Diagnostics;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing transient (time-domain) analysis.
/// </summary>
public class TransientAnalysisService : ITransientAnalysisService
{
    /// <inheritdoc/>
    public TransientAnalysisResult RunTransientAnalysis(
        CircuitModel circuit,
        double startTime,
        double stopTime,
        double maxStep,
        IEnumerable<string>? signals = null,
        bool useInitialConditions = false)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (signals == null || !signals.Any())
            throw new ArgumentException("At least one signal is required.", nameof(signals));

        if (stopTime <= startTime)
            throw new ArgumentException("Stop time must be greater than start time.");

        if (maxStep <= 0)
            throw new ArgumentException("Maximum step must be greater than zero.");

        var sw = Stopwatch.StartNew();
        
        // If useInitialConditions is true, set IC=0 on capacitors/inductors that don't have IC specified
        // This ensures capacitors start discharged for rise_time measurements
        // Note: In SpiceSharp, we set IC directly on components. If IC is not set, SpiceSharp will
        // calculate initial conditions from DC operating point. Setting IC=0 ensures components start at zero.
        if (useInitialConditions)
        {
            foreach (var entity in circuit.InternalCircuit)
            {
                if (entity is Capacitor capacitor)
                {
                    // Set IC=0 (discharged) if not already set
                    // We'll try to set it - if it fails (already set or not supported), that's okay
                    try
                    {
                        capacitor.SetParameter("ic", 0.0);
                    }
                    catch
                    {
                        // IC may already be set or parameter not accessible - that's fine
                        // Continue with other components
                    }
                }
                else if (entity is Inductor inductor)
                {
                    // Set IC=0 (no initial current) if not already set
                    try
                    {
                        inductor.SetParameter("ic", 0.0);
                    }
                    catch
                    {
                        // IC may already be set or parameter not accessible - that's fine
                        // Continue with other components
                    }
                }
            }
        }
        
        var tran = new Transient("tran", startTime, stopTime, maxStep);

        var signalData = new Dictionary<string, List<double>>();
        var timePoints = new List<double>();
        var units = new Dictionary<string, string>();

        // Initialize signal storage
        foreach (var signal in signals)
        {
            signalData[signal] = new List<double>();
            if (signal.StartsWith("v(")) units[signal] = "V";
            if (signal.StartsWith("i(")) units[signal] = "A";
        }

        // Create export objects for each signal
        // Note: For resistors, we'll calculate current using Ohm's law during the sweep
        // For capacitors, we'll calculate I = C * dV/dt (derivative of voltage)
        var exports = new List<(string name, IExport<double>? export, string? componentName, bool isResistor, bool isCapacitor)>();
        foreach (var signal in signals)
        {
            if (signal.StartsWith("v("))
            {
                var node = signal.Substring(2, signal.Length - 3);
                var voltageExport = new RealVoltageExport(tran, node);
                exports.Add((signal, voltageExport, null, false, false));
            }
            else if (signal.StartsWith("i("))
            {
                var component = signal.Substring(2, signal.Length - 3);
                
                // Check component type to determine how to get current
                var componentEntity = circuit.InternalCircuit.FirstOrDefault(e => e.Name == component);
                bool isResistor = componentEntity != null && componentEntity is Resistor;
                bool isCapacitor = componentEntity != null && componentEntity is Capacitor;
                bool isVoltageSource = componentEntity != null && componentEntity is VoltageSource;
                
                if (isResistor || isCapacitor)
                {
                    // For resistors and capacitors, we'll calculate current during the sweep
                    // Resistors: I = (V1 - V2) / R
                    // Capacitors: I = C * dV/dt (we'll calculate derivative from voltage time series)
                    exports.Add((signal, null, component, isResistor, isCapacitor));
                }
                else if (isVoltageSource)
                {
                    // For voltage sources, use GetCurrent during the sweep
                    exports.Add((signal, null, component, false, false));
                }
                else
                {
                    // For other components, try RealCurrentExport
                    try
                    {
                        var currentExport = new RealCurrentExport(tran, component);
                        exports.Add((signal, currentExport, component, false, false));
                    }
                    catch
                    {
                        // If we can't create a current export for the component,
                        // skip it - this could happen for certain component types
                        System.Diagnostics.Debug.WriteLine($"Could not create current export for {component}");
                    }
                }
            }
        }

        string status = "Success";
        try
        {
            // Validate circuit before running analysis
            var validator = new CircuitValidator();
            var validation = validator.Validate(circuit);
            if (!validation.IsValid)
            {
                var errorDetails = string.Join("; ", validation.Errors);
                status = $"Validation failed: {errorDetails}";
                throw new InvalidOperationException(status);
            }
            if (validation.HasWarnings)
            {
                System.Diagnostics.Debug.WriteLine($"Circuit validation warnings: {string.Join("; ", validation.Warnings)}");
            }

            // Track previous voltages for capacitor current calculation (I = C * dV/dt)
            var previousVoltages = new Dictionary<string, double>();
            var previousTime = startTime;
            
            // Run transient simulation
            foreach (int exportType in tran.Run(circuit.InternalCircuit))
            {
                // Get current time
                var currentTime = tran.Time;
                timePoints.Add(currentTime);
                var dt = currentTime - previousTime;
                if (dt <= 0) dt = maxStep; // Avoid division by zero

                // Collect all signal values at this time point
                foreach (var (signalName, export, componentName, isResistor, isCapacitor) in exports)
                {
                    try
                    {
                        double value = 0.0;
                        bool gotValue = false;
                        
                        // Handle special cases: resistors, capacitors, voltage sources
                        if (signalName.StartsWith("i(") && componentName != null)
                        {
                            var componentEntity = circuit.InternalCircuit.FirstOrDefault(e => e.Name == componentName);
                            
                            // For voltage sources, use GetCurrent
                            if (componentEntity is VoltageSource)
                            {
                                try
                                {
                                    value = tran.GetCurrent(componentName);
                                    gotValue = true;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TRAN] GetCurrent failed for voltage source {componentName}: {ex.Message}");
                                }
                            }
                            // For resistors, calculate using Ohm's law: I = (V1 - V2) / R
                            else if (isResistor && componentEntity is Resistor resistor)
                            {
                                try
                                {
                                    if (resistor.Nodes.Count >= 2)
                                    {
                                        var node1 = resistor.Nodes[0];
                                        var node2 = resistor.Nodes[1];
                                        
                                        // Get node voltages
                                        var v1 = tran.GetVoltage(node1);
                                        var v2 = tran.GetVoltage(node2);
                                        
                                        // Get resistance value from ComponentDefinition
                                        if (circuit.ComponentDefinitions.TryGetValue(componentName, out var componentDef) &&
                                            componentDef.Value.HasValue && componentDef.Value.Value > 0)
                                        {
                                            var resistance = componentDef.Value.Value;
                                            
                                            // Calculate current: I = (V1 - V2) / R
                                            var voltageDrop = v1 - v2;
                                            value = voltageDrop / resistance;
                                            gotValue = true;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TRAN] Ohm's law calculation failed for resistor {componentName}: {ex.Message}");
                                }
                            }
                            // For capacitors, calculate I = C * dV/dt
                            else if (isCapacitor && componentEntity is Capacitor capacitor)
                            {
                                try
                                {
                                    if (capacitor.Nodes.Count >= 2)
                                    {
                                        var node1 = capacitor.Nodes[0];
                                        var node2 = capacitor.Nodes[1];
                                        
                                        // Get current node voltages
                                        var v1 = tran.GetVoltage(node1);
                                        var v2 = tran.GetVoltage(node2);
                                        var vCurrent = v1 - v2; // Voltage across capacitor
                                        
                                        // Get capacitance value from ComponentDefinition
                                        if (circuit.ComponentDefinitions.TryGetValue(componentName, out var componentDef) &&
                                            componentDef.Value.HasValue && componentDef.Value.Value > 0)
                                        {
                                            var capacitance = componentDef.Value.Value;
                                            
                                            // Calculate current: I = C * dV/dt
                                            // dV/dt = (V_current - V_previous) / dt
                                            var key = $"{componentName}_voltage";
                                            if (previousVoltages.TryGetValue(key, out var vPrevious))
                                            {
                                                var dV = vCurrent - vPrevious;
                                                var dVdt = dV / dt;
                                                value = capacitance * dVdt;
                                                gotValue = true;
                                            }
                                            
                                            // Store current voltage for next iteration
                                            previousVoltages[key] = vCurrent;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TRAN] Capacitor current calculation failed for {componentName}: {ex.Message}");
                                }
                            }
                        }
                        
                        // If we didn't handle it as a special case, use the export object
                        if (!gotValue && export != null)
                        {
                            value = export.Value;
                            gotValue = true;
                        }
                        else if (!gotValue && signalName.StartsWith("v("))
                        {
                            // Voltage exports should always have an export object
                            if (export != null)
                            {
                                value = export.Value;
                                gotValue = true;
                            }
                        }
                        
                        if (gotValue)
                        {
                            signalData[signalName].Add(value);
                        }
                        else
                        {
                            // If we couldn't get a value, add NaN
                            signalData[signalName].Add(double.NaN);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If export fails, add NaN
                        System.Diagnostics.Debug.WriteLine($"[TRAN] Error getting {signalName}: {ex.Message}");
                        signalData[signalName].Add(double.NaN);
                    }
                }
                
                previousTime = currentTime;
            }

            // Generate time array from our stored points
            // Note: tran.Time gives us the actual time points from the simulation
            // which may differ from the requested parameters due to adaptive stepping
        }
        catch (Exception ex)
        {
            // Capture detailed error information including inner exceptions
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"Error: {ex.GetType().Name}");
            errorDetails.AppendLine($"Message: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                errorDetails.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                errorDetails.AppendLine($"Inner Message: {ex.InnerException.Message}");
            }
            
            // Check for SpiceSharp-specific error information
            if (ex.Data != null && ex.Data.Count > 0)
            {
                errorDetails.AppendLine("Additional Data:");
                foreach (var key in ex.Data.Keys)
                {
                    errorDetails.AppendLine($"  {key}: {ex.Data[key]}");
                }
            }
            
            // Include stack trace for debugging (first few lines)
            var stackTrace = ex.StackTrace?.Split('\n').Take(5);
            if (stackTrace != null && stackTrace.Any())
            {
                errorDetails.AppendLine("Stack Trace (first 5 lines):");
                foreach (var line in stackTrace)
                {
                    errorDetails.AppendLine($"  {line.Trim()}");
                }
            }
            
            status = $"Failed: {errorDetails}";
        }

        sw.Stop();

        return new TransientAnalysisResult
        {
            Time = timePoints,
            Signals = signalData,
            Units = units,
            AnalysisTimeMs = sw.ElapsedMilliseconds,
            Status = status
        };
    }
}

