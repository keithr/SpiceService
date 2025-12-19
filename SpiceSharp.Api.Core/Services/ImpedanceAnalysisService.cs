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

    /// <summary>
    /// Initializes a new instance of the <see cref="ImpedanceAnalysisService"/> class
    /// </summary>
    /// <param name="acAnalysisService">AC analysis service for running frequency sweeps</param>
    public ImpedanceAnalysisService(IACAnalysisService acAnalysisService)
    {
        _acAnalysisService = acAnalysisService ?? throw new ArgumentNullException(nameof(acAnalysisService));
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

        // Check for existing voltage sources at the measurement port
        // This will cause a conflict when we try to add our test source
        var existingVoltageSources = testCircuit.ComponentDefinitions.Values
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
        var result = _acAnalysisService.RunACAnalysis(
            testCircuit,
            startFrequency,
            stopFrequency,
            numberOfPoints,
            new[] { voltageSignal, currentSignal }
        );

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
        var componentService = new ComponentService();
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
