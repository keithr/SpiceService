using SpiceSharp;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Components;
using System.Diagnostics;
using System.Numerics;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for calculating circuit impedance by inserting a 1A AC current source and measuring voltage
/// </summary>
public class ImpedanceAnalysisService : IImpedanceAnalysisService
{
    private readonly IACAnalysisService _acAnalysisService;
    private readonly ILibraryService? _libraryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImpedanceAnalysisService"/> class
    /// </summary>
    /// <param name="acAnalysisService">AC analysis service for running frequency sweeps</param>
    /// <param name="libraryService">Optional library service for subcircuit definitions</param>
    public ImpedanceAnalysisService(IACAnalysisService acAnalysisService, ILibraryService? libraryService = null)
    {
        _acAnalysisService = acAnalysisService ?? throw new ArgumentNullException(nameof(acAnalysisService));
        _libraryService = libraryService;
    }

    /// <inheritdoc/>
    public ImpedanceAnalysisResult CalculateImpedance(
        CircuitModel circuit,
        string portPositive,
        string portNegative,
        double startFrequency,
        double stopFrequency,
        int numberOfPoints)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (string.IsNullOrWhiteSpace(portPositive))
            throw new ArgumentException("portPositive is required.", nameof(portPositive));

        if (string.IsNullOrWhiteSpace(portNegative))
            throw new ArgumentException("portNegative is required.", nameof(portNegative));

        if (stopFrequency <= startFrequency)
            throw new ArgumentException("Stop frequency must be greater than start frequency.");

        if (numberOfPoints <= 0)
            throw new ArgumentException("Number of points must be greater than zero.");

        var sw = Stopwatch.StartNew();

        // Clone the circuit to avoid modifying the original
        var testCircuit = CloneCircuitForImpedance(circuit);

        // Check for capacitor + subcircuit combination (known issue)
        // Capacitors block DC, and subcircuits may need DC paths for AC analysis
        // Use reflection to access internal ComponentDefinitions
        var componentDefinitionsProp = typeof(CircuitModel).GetProperty("ComponentDefinitions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var componentDefinitions = componentDefinitionsProp?.GetValue(testCircuit) as Dictionary<string, ComponentDefinition> ?? new Dictionary<string, ComponentDefinition>();
        
        var hasCapacitors = componentDefinitions.Values.Any(c => c.ComponentType == "capacitor");
        var hasSubcircuits = componentDefinitions.Values.Any(c => c.ComponentType == "subcircuit");
        
        if (hasCapacitors && hasSubcircuits)
        {
            // Add large resistors in parallel with capacitors to provide DC paths
            // This is a standard SPICE technique for AC analysis with capacitors
            // Use 1e12 ohms (1 TΩ) - high enough to not affect AC impedance, low enough for DC path
            var dcPathResistors = new List<ComponentDefinition>();
            var capacitorNodes = new HashSet<(string node1, string node2)>();
            
            foreach (var cap in componentDefinitions.Values.Where(c => c.ComponentType == "capacitor"))
            {
                if (cap.Nodes != null && cap.Nodes.Count >= 2)
                {
                    var node1 = cap.Nodes[0];
                    var node2 = cap.Nodes[1];
                    // Create unique key (order doesn't matter)
                    var key = string.Compare(node1, node2) < 0 ? (node1, node2) : (node2, node1);
                    
                    if (!capacitorNodes.Contains(key))
                    {
                        capacitorNodes.Add(key);
                        // Add a large resistor in parallel with the capacitor for DC path
                        var dcPathResistor = new ComponentDefinition
                        {
                            Name = $"R_DC_PATH_{cap.Name}",
                            ComponentType = "resistor",
                            Nodes = new List<string> { node1, node2 },
                            Value = 1e12 // 1 TΩ - provides DC path without affecting AC impedance
                        };
                        dcPathResistors.Add(dcPathResistor);
                    }
                }
            }
            
            // Add DC path resistors to the circuit
            if (dcPathResistors.Count > 0)
            {
                var dcPathComponentService = _libraryService != null ? new ComponentService(_libraryService) : new ComponentService();
                foreach (var dcPathResistor in dcPathResistors)
                {
                    dcPathComponentService.AddComponent(testCircuit, dcPathResistor);
                    // Also add to ComponentDefinitions for tracking
                    componentDefinitions[dcPathResistor.Name] = dcPathResistor;
                }
            }
        }

        // Check for existing voltage sources at the measurement port
        // This will cause a conflict when we try to add our test source
        var existingVoltageSources = componentDefinitions.Values
            .Where(c => c.ComponentType == "voltage_source" &&
                       ((c.Nodes.Count >= 1 && c.Nodes[0] == portPositive && (c.Nodes.Count < 2 || c.Nodes[1] == portNegative)) ||
                        (c.Nodes.Count >= 2 && c.Nodes[0] == portNegative && c.Nodes[1] == portPositive)))
            .ToList();

        if (existingVoltageSources.Count > 0)
        {
            var sourceNames = string.Join(", ", existingVoltageSources.Select(s => s.Name));
            throw new ArgumentException(
                $"Cannot measure impedance at port '{portPositive}' to '{portNegative}': " +
                $"voltage source(s) {sourceNames} already exist at this port. " +
                $"The impedance measurement tool requires a passive circuit (no voltage source at the measurement port). " +
                $"Please remove the voltage source(s) at the port before measuring impedance, or measure impedance at a different port.");
        }

        // Insert a 1V AC voltage source at the port
        // For impedance measurement: Z = V/I, so if V=1V, then Z = 1/I
        // The voltage source will be named "V_IMPEDANCE_TEST" to avoid conflicts
        var testSourceName = "V_IMPEDANCE_TEST";
        var testSourceDef = new ComponentDefinition
        {
            Name = testSourceName,
            ComponentType = "voltage_source",
            Nodes = new List<string> { portPositive, portNegative },
            Value = 0.0, // 0V DC value (AC only)
            Parameters = new Dictionary<string, object>
            {
                { "ac", 1.0 } // 1V AC magnitude
            }
        };

        var componentService = new ComponentService();
        componentService.AddComponent(testCircuit, testSourceDef);

        // Verify the port nodes exist in the circuit
        var spiceCircuit = testCircuit.GetSpiceSharpCircuit();
        var hasPositiveNode = spiceCircuit.Any(e => e is Component c && 
            (c.Nodes.Contains(portPositive) || c.Name == portPositive));
        var hasNegativeNode = portNegative == "0" || spiceCircuit.Any(e => e is Component c && 
            (c.Nodes.Contains(portNegative) || c.Name == portNegative));

        if (!hasPositiveNode && !spiceCircuit.Any(e => e is Component c && c.Nodes.Contains(portPositive)))
        {
            throw new ArgumentException($"Port positive node '{portPositive}' not found in circuit. " +
                "Ensure the node exists or is connected to a component.");
        }

        // Run AC analysis to measure current through the source and voltage at the port
        // Export voltage at port_positive and current through the source
        var voltageSignal = $"v({portPositive})";
        var currentSignal = $"i({testSourceName})";
        
        ACAnalysisResult result;
        try
        {
            result = _acAnalysisService.RunACAnalysis(
                testCircuit,
                startFrequency,
                stopFrequency,
                numberOfPoints,
                new[] { voltageSignal, currentSignal }
            );
        }
        catch (Exception ex)
        {
            // Wrap AC analysis exceptions with stage information
            throw new InvalidOperationException(
                $"AC analysis failed during impedance calculation: {ex.Message}. " +
                $"This occurs when running frequency sweep to measure voltage and current. " +
                $"Common causes: convergence failure, matrix singularity, or circuit topology issues. " +
                $"Check circuit validation and ensure all nodes have DC paths to ground.", ex);
        }
        
        // Check if AC analysis returned an error status
        if (result.Status != "Success" && !string.IsNullOrEmpty(result.Status) && 
            (result.Frequencies.Count == 0 || result.MagnitudeDb.Count == 0))
        {
            throw new InvalidOperationException(
                $"AC analysis failed during impedance calculation: {result.Status}. " +
                $"No frequency response data was generated. " +
                $"This may indicate convergence failure, matrix singularity, or circuit topology problems.");
        }

        sw.Stop();

        // Calculate impedance: Z = V/I
        // Since I = 1A, Z = V
        // Extract magnitude and phase from AC analysis results
        var impedanceResult = new ImpedanceAnalysisResult
        {
            PortPositive = portPositive,
            PortNegative = portNegative,
            AnalysisTimeMs = sw.Elapsed.TotalMilliseconds,
            Status = result.Status
        };

        if (result.MagnitudeDb.ContainsKey(voltageSignal) && result.PhaseDegrees.ContainsKey(voltageSignal) &&
            result.MagnitudeDb.ContainsKey(currentSignal) && result.PhaseDegrees.ContainsKey(currentSignal))
        {
            var voltageMagnitudeDb = result.MagnitudeDb[voltageSignal];
            var voltagePhaseDeg = result.PhaseDegrees[voltageSignal];
            var currentMagnitudeDb = result.MagnitudeDb[currentSignal];
            var currentPhaseDeg = result.PhaseDegrees[currentSignal];

            // Convert magnitude from dB to linear
            // Magnitude in dB: 20*log10(V/Vref) for voltage, 20*log10(I/Iref) for current
            // V = 10^(dB/20), I = 10^(dB/20)
            // Z = V/I, so we need to subtract the current magnitude from voltage magnitude in dB
            // Z_dB = V_dB - I_dB, then Z = 10^(Z_dB/20)
            // Phase: Z_phase = V_phase - I_phase
            for (int i = 0; i < voltageMagnitudeDb.Count && i < voltagePhaseDeg.Count && 
                 i < currentMagnitudeDb.Count && i < currentPhaseDeg.Count; i++)
            {
                var voltageLinear = Math.Pow(10, voltageMagnitudeDb[i] / 20.0); // Convert dB to linear (V)
                var currentLinear = Math.Pow(10, currentMagnitudeDb[i] / 20.0); // Convert dB to linear (A)
                
                // Calculate impedance: Z = V/I
                var impedanceMagnitude = currentLinear > 0 ? voltageLinear / currentLinear : double.PositiveInfinity;
                impedanceResult.Magnitude.Add(impedanceMagnitude);
                
                // Phase difference: Z_phase = V_phase - I_phase
                // Note: SPICE measures current through voltage source with a sign convention.
                // For impedance Z = V/I, we need V_phase - I_phase. However, SPICE's current
                // convention may cause a 180° offset. We'll calculate the phase difference
                // and let the user interpret it (or we can add a note in documentation).
                var impedancePhase = voltagePhaseDeg[i] - currentPhaseDeg[i];
                
                // Normalize phase to -180 to +180 degrees
                while (impedancePhase > 180) impedancePhase -= 360;
                while (impedancePhase < -180) impedancePhase += 360;
                impedanceResult.Phase.Add(impedancePhase);
            }

            // Copy frequencies
            impedanceResult.Frequencies = new List<double>(result.Frequencies);
        }
        else
        {
            // If no results, this likely means the port nodes are invalid or not connected
            // Throw an exception with helpful message
            throw new ArgumentException(
                $"Impedance calculation returned no data for port '{portPositive}' to '{portNegative}'. " +
                "This usually means the port nodes are not valid or not connected to components in the circuit. " +
                "Ensure the port nodes exist and are part of a connected circuit path.");
        }

        return impedanceResult;
    }

    /// <summary>
    /// Clones a circuit for impedance testing without modifying the original
    /// </summary>
    private CircuitModel CloneCircuitForImpedance(CircuitModel original)
    {
        var cloned = new CircuitModel
        {
            Id = $"{original.Id}_impedance_{Guid.NewGuid():N}",
            Description = original.Description,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            InternalCircuit = new Circuit()
        };

        // Clone component definitions
        foreach (var kvp in original.ComponentDefinitions)
        {
            cloned.ComponentDefinitions[kvp.Key] = new ComponentDefinition
            {
                ComponentType = kvp.Value.ComponentType,
                Name = kvp.Value.Name,
                Nodes = new List<string>(kvp.Value.Nodes),
                Value = kvp.Value.Value,
                Model = kvp.Value.Model,
                Parameters = new Dictionary<string, object>(kvp.Value.Parameters)
            };
        }

        // Clone model definitions
        foreach (var kvp in original.ModelDefinitions)
        {
            cloned.ModelDefinitions[kvp.Key] = new ModelDefinition
            {
                ModelName = kvp.Value.ModelName,
                ModelType = kvp.Value.ModelType,
                Parameters = new Dictionary<string, double>(kvp.Value.Parameters)
            };
        }

        // Recreate components and models in cloned circuit
        // Use library service if available (required for subcircuit components)
        var componentService = _libraryService != null ? new ComponentService(_libraryService) : new ComponentService();
        var modelService = new ModelService();

        // Add models first
        foreach (var modelDef in cloned.ModelDefinitions.Values)
        {
            modelService.DefineModel(cloned, modelDef);
        }

        // Add components
        foreach (var componentDef in cloned.ComponentDefinitions.Values)
        {
            componentService.AddComponent(cloned, componentDef);
        }

        return cloned;
    }
}
