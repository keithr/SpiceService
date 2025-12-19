using SpiceSharp;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Simulations;
using SpiceSharp.Behaviors;
using SpiceSharp.Components;
using System.Diagnostics;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing DC sweep analysis.
/// </summary>
public class DCAnalysisService : IDCAnalysisService
{
    /// <inheritdoc/>
    public DCAnalysisResult RunDCAnalysis(
        CircuitModel circuit,
        string sourceName,
        double startValue,
        double stopValue,
        double stepValue,
        IEnumerable<string>? exports = null)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name is required.", nameof(sourceName));

        if (exports == null || !exports.Any())
            throw new ArgumentException("At least one export is required.", nameof(exports));

        var sw = Stopwatch.StartNew();
        var dc = new DC($"dc_{DateTime.Now.Ticks}", sourceName, startValue, stopValue, stepValue);

        var results = new Dictionary<string, List<double>>();
        var sweepValues = new List<double>();
        var units = new Dictionary<string, string>();

        // Initialize results structure
        foreach (var export in exports)
        {
            results[export] = new List<double>();
            if (export.StartsWith("v(")) units[export] = "V";
            if (export.StartsWith("i(")) units[export] = "A";
            if (export.StartsWith("p(")) units[export] = "W";
        }

        // Create export objects for each signal (similar to TransientAnalysisService)
        // This allows us to use RealCurrentExport for components like diodes
        // Note: export can be null for resistors/voltage sources which need special handling
        var exportObjects = new List<(string name, IExport<double>? export, bool isSourceFallback, string? componentName)>();
        foreach (var export in exports)
        {
            try
            {
                    if (export.StartsWith("v("))
                    {
                        var node = export.Substring(2, export.Length - 3);
                        var voltageExport = new RealVoltageExport(dc, node);
                        exportObjects.Add((export, voltageExport, false, null));
                    }
                else if (export.StartsWith("i("))
                {
                    var component = export.Substring(2, export.Length - 3);
                    
                    // Check component type to determine how to get current
                    var componentEntity = circuit.InternalCircuit.FirstOrDefault(e => e.Name == component);
                    bool isDiode = componentEntity != null && componentEntity is Diode;
                    bool isResistor = componentEntity != null && componentEntity is Resistor;
                    bool isVoltageSource = componentEntity != null && componentEntity is VoltageSource;
                    
                    if (isVoltageSource)
                    {
                        // For voltage sources, GetCurrent is reliable
                        // We'll use GetCurrent during the sweep loop, not RealCurrentExport
                        exportObjects.Add((export, null, false, component)); // Mark as voltage source - special handling
                    }
                    else if (isResistor)
                    {
                        // For resistors, calculate current using Ohm's law: I = (V1 - V2) / R
                        // We'll calculate this during the sweep loop using node voltages
                        exportObjects.Add((export, null, false, component)); // Mark as resistor - special handling
                    }
                    else if (isDiode)
                    {
                        // For diodes/LEDs, use source current (matching TestLED.cs approach)
                        // Note: Voltage source current is negative when current flows OUT of positive terminal
                        var currentExport = new RealCurrentExport(dc, sourceName);
                        exportObjects.Add((export, currentExport, true, component)); // Mark as fallback
                    }
                    else
                    {
                        try
                        {
                            // Try to create RealCurrentExport for other components
                            var currentExport = new RealCurrentExport(dc, component);
                            exportObjects.Add((export, currentExport, false, component));
                        }
                        catch
                        {
                            // If we can't create a current export for the component,
                            // fall back to getting current through the voltage source
                            // Note: Voltage source current is negative when current flows OUT of positive terminal
                            var currentExport = new RealCurrentExport(dc, sourceName);
                            exportObjects.Add((export, currentExport, true, component)); // Mark as fallback
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If we can't create an export, log but continue
                System.Diagnostics.Debug.WriteLine($"Could not create export for {export}: {ex.Message}");
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

            // Run DC sweep - at each sweep point, read the requested data
            foreach (int exportType in dc.Run(circuit.InternalCircuit))
            {
                // Collect all exports at this sweep point
                foreach (var (exportName, exportObj, isSourceFallback, componentName) in exportObjects)
                {
                    try
                    {
                        double value = 0.0;
                        bool gotValue = false;
                        
                        // Handle special cases: voltage sources and resistors
                        if (exportName.StartsWith("i(") && componentName != null)
                        {
                            var component = componentName;
                            var componentEntity = circuit.InternalCircuit.FirstOrDefault(e => e.Name == component);
                            
                            // For voltage sources, use GetCurrent
                            if (componentEntity is VoltageSource)
                            {
                                try
                                {
                                    value = dc.GetCurrent(component);
                                    gotValue = true;
                                    System.Diagnostics.Debug.WriteLine($"[DC] {component} (voltage source) current via GetCurrent: {value}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DC] GetCurrent failed for voltage source {component}: {ex.Message}");
                                }
                            }
                            // For resistors, calculate using Ohm's law: I = (V1 - V2) / R
                            else if (componentEntity is Resistor resistor)
                            {
                                try
                                {
                                    if (resistor.Nodes.Count >= 2)
                                    {
                                        var node1 = resistor.Nodes[0];
                                        var node2 = resistor.Nodes[1];
                                        
                                        // Get node voltages
                                        var v1 = dc.GetVoltage(node1);
                                        var v2 = dc.GetVoltage(node2);
                                        
                                        // Get resistance value from ComponentDefinition
                                        if (circuit.ComponentDefinitions.TryGetValue(component, out var componentDef) &&
                                            componentDef.Value.HasValue && componentDef.Value.Value > 0)
                                        {
                                            var resistance = componentDef.Value.Value;
                                            
                                            // Calculate current: I = (V1 - V2) / R
                                            // Current flows from node1 to node2 (positive when V1 > V2)
                                            var voltageDrop = v1 - v2;
                                            value = voltageDrop / resistance;
                                            gotValue = true;
                                            System.Diagnostics.Debug.WriteLine($"[DC] {component} (resistor) current via Ohm's law: {value} (V1={v1}V, V2={v2}V, Vdrop={voltageDrop}V, R={resistance}Ω)");
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[DC] Warning: ComponentDefinition for {component} missing or Value not set - cannot calculate current");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DC] Ohm's law calculation failed for resistor {component}: {ex.Message}");
                                }
                            }
                        }
                        
                        // If we didn't handle it as a special case, use the export object
                        if (!gotValue && exportObj != null)
                        {
                            value = exportObj.Value;
                            
                            // For current exports that fell back to source current, negate to get correct direction
                            // Voltage source current convention: negative when current flows OUT of positive terminal
                            // So for a component in series with the source, we need to negate
                            if (isSourceFallback)
                            {
                                value = -value;
                            }
                            gotValue = true;
                        }
                        else if (!gotValue && exportName.StartsWith("v("))
                        {
                            // Voltage exports should always have an export object
                            if (exportObj != null)
                            {
                                value = exportObj.Value;
                                gotValue = true;
                            }
                        }
                        
                        if (gotValue)
                        {
                            results[exportName].Add(value);
                        }
                        else
                        {
                            // If we couldn't get a value, add NaN or 0
                            System.Diagnostics.Debug.WriteLine($"[DC] Could not get value for {exportName}");
                            results[exportName].Add(double.NaN);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we can't get a value, log but continue
                        System.Diagnostics.Debug.WriteLine($"Could not get {exportName}: {ex.Message}");
                        results[exportName].Add(double.NaN);
                    }
                }
            }

            // Generate sweep values from our parameters to match export count
            sweepValues.Clear();
            for (double val = startValue; val <= stopValue; val += stepValue)
            {
                sweepValues.Add(val);
            }
            
            // Adjust if we didn't get the expected number of points (floating point issues)
            if (results.Values.Any() && results.Values.First().Count != sweepValues.Count)
            {
                // Trim sweepValues to match actual data points
                var actualPoints = results.Values.First().Count;
                sweepValues = sweepValues.Take(actualPoints).ToList();
            }
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

        return new DCAnalysisResult
        {
            SweepVariable = sourceName,
            SweepValues = sweepValues,
            Results = results,
            Units = units,
            AnalysisTimeMs = sw.ElapsedMilliseconds,
            Status = status
        };
    }
}
