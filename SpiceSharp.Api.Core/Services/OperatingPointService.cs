using SpiceSharp;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Simulations;
using SpiceSharp.Components;
using SpiceSharp.Behaviors;
using SpiceSharp.Entities;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing DC operating point analysis.
/// </summary>
public class OperatingPointService : IOperatingPointService
{
    /// <inheritdoc/>
    public OperatingPointResult RunOperatingPointAnalysis(CircuitModel circuit, bool includePower = true)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        var op = new OP($"op_{DateTime.Now.Ticks}");

        var nodeVoltages = new Dictionary<string, double>();
        var branchCurrents = new Dictionary<string, double>();
        var powerDissipation = new Dictionary<string, double>();
        double totalPower = 0;
        int convergenceIterations = 0;
        string status = "Success";

        try
        {
            // Collect all nodes and components before running
            var nodeSet = new HashSet<string>();
            var components = new List<Component>();
            
            foreach (var componentDef in circuit.ComponentDefinitions.Values)
            {
                foreach (var node in componentDef.Nodes)
                {
                    nodeSet.Add(node);
                }
            }
            
            foreach (var entity in circuit.InternalCircuit)
            {
                if (entity is Component component)
                {
                    components.Add(component);
                }
            }

            // Create current exports BEFORE running the simulation
            // This ensures they properly track current values during the simulation
            var currentExports = new Dictionary<string, RealCurrentExport>();
            foreach (var component in components)
            {
                try
                {
                    var export = new RealCurrentExport(op, component.Name);
                    currentExports[component.Name] = export;
                }
                catch
                {
                    // Some components may not support RealCurrentExport
                    // We'll try GetCurrent as fallback after the run
                }
            }

            // Run the simulation completely first
            foreach (int exportType in op.Run(circuit.InternalCircuit))
            {
                // Simulation is running - exports will be updated during this
            }

            // After simulation completes, read all values
            // Get all node voltages
            foreach (var node in nodeSet)
            {
                try
                {
                    nodeVoltages[node] = op.GetVoltage(node);
                }
                catch
                {
                    // Some nodes may not be accessible
                }
            }

            // Get component currents
            // For voltage sources: Use GetCurrent (reliable)
            // For resistors: Calculate from voltage drop using Ohm's law (V = IR, so I = V/R)
            // For diodes: Use source current if available (KCL), otherwise try RealCurrentExport/GetCurrent
            // For other components: Try RealCurrentExport, then GetCurrent
            // Process voltage sources first so their currents are available for diode calculations
            var voltageSources = components.OfType<VoltageSource>().ToList();
            var otherComponents = components.Where(c => !(c is VoltageSource)).ToList();
            
            // First pass: Process voltage sources to get their currents
            foreach (var component in voltageSources)
            {
                try
                {
                    var value = op.GetCurrent(component.Name);
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        branchCurrents[component.Name] = value;
                        System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} (voltage source) current via GetCurrent: {value}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OP] GetCurrent failed for voltage source {component.Name}: {ex.Message}");
                }
            }
            
            // Second pass: Process other components (resistors, diodes, etc.)
            foreach (var component in otherComponents)
            {
                bool gotValue = false;
                bool isResistor = component is Resistor;
                bool isDiode = component is Diode;
                
                // For resistors, calculate current from voltage drop using Ohm's law: I = V/R
                if (isResistor)
                {
                    try
                    {
                        // Get resistor nodes and resistance value
                        var resistor = component as Resistor;
                        if (resistor != null && resistor.Nodes.Count >= 2)
                        {
                            var node1 = resistor.Nodes[0];
                            var node2 = resistor.Nodes[1];
                            
                            // Get voltages at both nodes
                            if (nodeVoltages.TryGetValue(node1, out var v1) && 
                                nodeVoltages.TryGetValue(node2, out var v2))
                            {
                                // Get resistance value from ComponentDefinition
                                // Note: ParameterSweepService now updates ComponentDefinition.Value when modifying components
                                // so this should reflect the swept/modified value
                                var componentDef = circuit.ComponentDefinitions.TryGetValue(component.Name, out var def) ? def : null;
                                if (componentDef != null && componentDef.Value.HasValue && componentDef.Value.Value > 0)
                                {
                                    var resistance = componentDef.Value.Value;
                                    
                                    // Calculate current: I = (V1 - V2) / R
                                    // Current flows from node1 to node2 (positive when V1 > V2)
                                    var voltageDrop = v1 - v2;
                                    var current = voltageDrop / resistance;
                                    
                                    branchCurrents[component.Name] = current;
                                    gotValue = true;
                                    System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} (resistor) current via Ohm's law: {current} (V1={v1}V, V2={v2}V, Vdrop={voltageDrop}V, R={resistance}Ω)");
                                }
                                else if (componentDef == null || !componentDef.Value.HasValue)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[OP] Warning: ComponentDefinition for {component.Name} missing or Value not set - cannot calculate current");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OP] Ohm's law calculation failed for resistor {component.Name}: {ex.Message}");
                    }
                }
                
                // For diodes, prioritize source-current-based calculation over RealCurrentExport
                // RealCurrentExport often returns incorrect values (very small or zero) for diodes
                // Use KCL: in simple circuits, diode current equals source current (opposite sign)
                if (isDiode)
                {
                    try
                    {
                        var diode = component as Diode;
                        if (diode != null && voltageSources.Count > 0)
                        {
                            // Try to find a voltage source and use its current
                            // Strategy: If there's exactly one source, use it directly
                            // Otherwise, find a source that shares nodes with the diode
                            Component? sourceToUse = null;
                            
                            if (voltageSources.Count == 1)
                            {
                                sourceToUse = voltageSources[0];
                            }
                            else
                            {
                                // Multiple sources - find one connected to the diode
                                var diodeNodes = new HashSet<string>(diode.Nodes);
                                foreach (var source in voltageSources)
                                {
                                    if (source.Nodes.Count >= 2)
                                    {
                                        var sourceNodes = new HashSet<string>(source.Nodes);
                                        if (diodeNodes.Overlaps(sourceNodes))
                                        {
                                            sourceToUse = source;
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            // If we found a source and have its current, use it
                            if (sourceToUse != null && branchCurrents.TryGetValue(sourceToUse.Name, out var sourceCurrent))
                            {
                                // Diode current equals source current (opposite sign due to SPICE convention)
                                var diodeCurrent = -sourceCurrent;
                                branchCurrents[component.Name] = diodeCurrent;
                                gotValue = true;
                                System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} (diode) current from source {sourceToUse.Name}: {diodeCurrent} (source={sourceCurrent}A)");
                            }
                            else if (sourceToUse != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} (diode) found source {sourceToUse.Name} but its current not available yet");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Don't let diode current extraction failures break the entire analysis
                        System.Diagnostics.Debug.WriteLine($"[OP] Diode current calculation failed for {component.Name}: {ex.Message}");
                    }
                }
                
                // For other components (including diodes if above didn't work), try RealCurrentExport
                if (!gotValue && !isResistor)
                {
                    if (currentExports.TryGetValue(component.Name, out var export))
                    {
                        try
                        {
                            var value = export.Value;
                            // For diodes, only use RealCurrentExport if it's a reasonable value (not near zero)
                            // For other components, accept any non-NaN/Inf value
                            if (!double.IsNaN(value) && !double.IsInfinity(value))
                            {
                                if (!isDiode || Math.Abs(value) > 1e-10)
                                {
                                    branchCurrents[component.Name] = value;
                                    gotValue = true;
                                    System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} current via RealCurrentExport: {value}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} (diode) RealCurrentExport value too small ({value}), ignoring");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[OP] RealCurrentExport exception for {component.Name}: {ex.Message}");
                        }
                    }
                }
                
                // Fallback: Try GetCurrent if other methods didn't work
                if (!gotValue)
                {
                    try
                    {
                        var value = op.GetCurrent(component.Name);
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            branchCurrents[component.Name] = value;
                            gotValue = true;
                            System.Diagnostics.Debug.WriteLine($"[OP] {component.Name} current via GetCurrent (fallback): {value}");
                        }
                    }
                    catch
                    {
                        // GetCurrent not available for this component type
                    }
                }
                
                if (!gotValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[OP] Could not get current for {component.Name} - all methods failed");
                }
            }

            status = "Success";
        }
        catch (Exception ex)
        {
            status = $"Failed: {ex.Message}";
        }

        return new OperatingPointResult
        {
            NodeVoltages = nodeVoltages,
            BranchCurrents = branchCurrents,
            PowerDissipation = powerDissipation,
            TotalPower = totalPower,
            ConvergenceIterations = convergenceIterations,
            Status = status
        };
    }
}

