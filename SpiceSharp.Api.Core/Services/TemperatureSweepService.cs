using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Components;
using SpiceSharp.Api.Core.Models;
using System.Diagnostics;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing temperature sweep analyses
/// Uses workaround approach: modifies model parameters based on temperature coefficients
/// </summary>
public class TemperatureSweepService : ITemperatureSweepService
{
    private readonly IOperatingPointService _operatingPointService;
    private readonly IDCAnalysisService _dcAnalysisService;
    private readonly IACAnalysisService _acAnalysisService;
    private readonly ITransientAnalysisService _transientAnalysisService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemperatureSweepService"/> class
    /// </summary>
    public TemperatureSweepService(
        IOperatingPointService operatingPointService,
        IDCAnalysisService dcAnalysisService,
        IACAnalysisService acAnalysisService,
        ITransientAnalysisService transientAnalysisService)
    {
        _operatingPointService = operatingPointService;
        _dcAnalysisService = dcAnalysisService;
        _acAnalysisService = acAnalysisService;
        _transientAnalysisService = transientAnalysisService;
    }

    /// <inheritdoc/>
    public TemperatureSweepResult RunTemperatureSweep(
        CircuitModel circuit,
        double startTemp,
        double stopTemp,
        double stepTemp,
        string analysisType,
        object? analysisConfig,
        IEnumerable<string> exports)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (stepTemp <= 0)
            throw new ArgumentException("Temperature step must be greater than zero.", nameof(stepTemp));

        if (startTemp > stopTemp)
            throw new ArgumentException("Start temperature must be less than or equal to stop temperature.", nameof(startTemp));

        var stopwatch = Stopwatch.StartNew();

        // Generate temperature values
        var temperatureValues = GenerateTemperatureValues(startTemp, stopTemp, stepTemp);
        
        var result = new TemperatureSweepResult
        {
            TemperatureValues = temperatureValues,
            AnalysisType = analysisType,
            Status = "Success"
        };

        // Initialize results dictionary
        foreach (var export in exports)
        {
            result.Results[export] = new List<double>();
            result.Units[export] = GetUnitForExport(export);
        }

        // Run sweep for each temperature point
        foreach (var temp in temperatureValues)
        {
            // Clone circuit for this temperature point
            var clonedCircuit = CloneCircuit(circuit);

            // Adjust model parameters based on temperature
            AdjustModelParametersForTemperature(clonedCircuit, temp);

            // Run analysis
            var analysisResult = RunAnalysis(clonedCircuit, analysisType, analysisConfig, exports);

            // Collect results
            CollectResults(result, analysisResult, exports);
        }

        stopwatch.Stop();
        result.AnalysisTimeMs = stopwatch.Elapsed.TotalMilliseconds;

        return result;
    }

    /// <summary>
    /// Generates temperature values from start to stop with given step
    /// </summary>
    private List<double> GenerateTemperatureValues(double start, double stop, double step)
    {
        var values = new List<double>();
        var current = start;

        while (current <= stop + step * 0.0001) // Small tolerance for floating point
        {
            values.Add(current);
            current += step;
        }

        return values;
    }

    /// <summary>
    /// Clones a circuit for temperature modification
    /// </summary>
    private CircuitModel CloneCircuit(CircuitModel original)
    {
        var cloned = new CircuitModel
        {
            Id = $"{original.Id}_temp_{Guid.NewGuid():N}",
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

    /// <summary>
    /// Adjusts model parameters based on temperature using SPICE temperature equations
    /// </summary>
    private void AdjustModelParametersForTemperature(CircuitModel circuit, double temperatureCelsius)
    {
        // Convert Celsius to Kelvin
        double tempK = temperatureCelsius + 273.15;

        // Adjust all diode models in the circuit
        foreach (var modelDef in circuit.ModelDefinitions.Values)
        {
            if (modelDef.ModelType.ToLower() != "diode")
                continue;

            var model = circuit.InternalCircuit.TryGetEntity(modelDef.ModelName, out var entity) ? entity : null;
            if (model == null)
                continue;

            // Get original parameters from model definition
            var originalParams = modelDef.Parameters;
            
            // Get nominal temperature (default to 27°C = 300.15K if not specified)
            double tnomK = originalParams.ContainsKey("TNOM") 
                ? originalParams["TNOM"] + 273.15 
                : 300.15;

            // Temperature ratio
            double tempRatio = tempK / tnomK;

            // Adjust IS (saturation current) based on XTI (temperature exponent)
            if (originalParams.ContainsKey("IS") && originalParams.ContainsKey("XTI"))
            {
                double isOriginal = originalParams["IS"];
                double xti = originalParams["XTI"];
                
                // IS(T) = IS(TNOM) * (T/TNOM)^XTI * exp(EG * (T - TNOM) / (k * T * TNOM))
                // Simplified: IS(T) = IS(TNOM) * (T/TNOM)^XTI * exp(EG * (T - TNOM) / (0.00008617 * T * TNOM))
                // Where EG is in eV, k = 8.617e-5 eV/K
                double eg = originalParams.ContainsKey("EG") ? originalParams["EG"] : 1.0;
                double k = 8.617e-5; // Boltzmann constant in eV/K
                
                double isAdjusted = isOriginal * Math.Pow(tempRatio, xti) * 
                    Math.Exp(eg * (tempK - tnomK) / (k * tempK * tnomK));
                
                model.SetParameter("IS", isAdjusted);
            }

            // Adjust RS (series resistance) based on TRS1 and TRS2 (if available)
            // RS(T) = RS(TNOM) * (1 + TRS1*(T-TNOM) + TRS2*(T-TNOM)^2)
            if (originalParams.ContainsKey("RS"))
            {
                double rsOriginal = originalParams["RS"];
                double rsAdjusted = rsOriginal;
                
                double deltaT = temperatureCelsius - (tnomK - 273.15);
                
                if (originalParams.ContainsKey("TRS1"))
                {
                    double trs1 = originalParams["TRS1"];
                    rsAdjusted = rsAdjusted * (1 + trs1 * deltaT);
                }
                
                if (originalParams.ContainsKey("TRS2"))
                {
                    double trs2 = originalParams["TRS2"];
                    rsAdjusted = rsAdjusted * (1 + trs2 * deltaT * deltaT);
                }
                
                // Only adjust if we have temperature coefficients
                if (originalParams.ContainsKey("TRS1") || originalParams.ContainsKey("TRS2"))
                {
                    model.SetParameter("RS", rsAdjusted);
                }
            }

            // Adjust BV (breakdown voltage) based on TBV1 and TBV2 (if available)
            // BV(T) = BV(TNOM) * (1 + TBV1*(T-TNOM) + TBV2*(T-TNOM)^2)
            if (originalParams.ContainsKey("BV"))
            {
                double bvOriginal = originalParams["BV"];
                double bvAdjusted = bvOriginal;
                
                double deltaT = temperatureCelsius - (tnomK - 273.15);
                
                if (originalParams.ContainsKey("TBV1"))
                {
                    double tbv1 = originalParams["TBV1"];
                    bvAdjusted = bvAdjusted * (1 + tbv1 * deltaT);
                }
                
                if (originalParams.ContainsKey("TBV2"))
                {
                    double tbv2 = originalParams["TBV2"];
                    bvAdjusted = bvAdjusted * (1 + tbv2 * deltaT * deltaT);
                }
                
                // Only adjust if we have temperature coefficients
                if (originalParams.ContainsKey("TBV1") || originalParams.ContainsKey("TBV2"))
                {
                    model.SetParameter("BV", bvAdjusted);
                }
            }
        }
    }

    /// <summary>
    /// Runs the specified analysis type
    /// </summary>
    private object RunAnalysis(CircuitModel circuit, string analysisType, object? analysisConfig, IEnumerable<string> exports)
    {
        return analysisType.ToLower() switch
        {
            "operating-point" => _operatingPointService.RunOperatingPointAnalysis(circuit, true),
            "dc" => RunDCAnalysisWithConfig(circuit, analysisConfig, exports),
            "ac" => RunACAnalysisWithConfig(circuit, analysisConfig, exports),
            "transient" => RunTransientAnalysisWithConfig(circuit, analysisConfig, exports),
            _ => throw new ArgumentException($"Unsupported analysis type: {analysisType}")
        };
    }

    private DCAnalysisResult RunDCAnalysisWithConfig(CircuitModel circuit, object? config, IEnumerable<string> exports)
    {
        if (config is Dictionary<string, object> dcConfig)
        {
            var source = dcConfig.ContainsKey("source") ? dcConfig["source"].ToString()! : "V1";
            var start = dcConfig.ContainsKey("start") ? Convert.ToDouble(dcConfig["start"]) : 0.0;
            var stop = dcConfig.ContainsKey("stop") ? Convert.ToDouble(dcConfig["stop"]) : 0.0;
            var step = dcConfig.ContainsKey("step") ? Convert.ToDouble(dcConfig["step"]) : 0.1;
            return _dcAnalysisService.RunDCAnalysis(circuit, source, start, stop, step, exports);
        }
        else
        {
            // Default: single point DC analysis (operating point)
            return _dcAnalysisService.RunDCAnalysis(circuit, "V1", 0.0, 0.0, 0.1, exports);
        }
    }

    private ACAnalysisResult RunACAnalysisWithConfig(CircuitModel circuit, object? config, IEnumerable<string> exports)
    {
        if (config is Dictionary<string, object> acConfig)
        {
            var startFreq = acConfig.ContainsKey("startFrequency") ? Convert.ToDouble(acConfig["startFrequency"]) : 1000.0;
            var stopFreq = acConfig.ContainsKey("stopFrequency") ? Convert.ToDouble(acConfig["stopFrequency"]) : 1000000.0;
            var numPoints = acConfig.ContainsKey("numberOfPoints") ? Convert.ToInt32(acConfig["numberOfPoints"]) : 100;
            return _acAnalysisService.RunACAnalysis(circuit, startFreq, stopFreq, numPoints, exports);
        }
        else
        {
            return _acAnalysisService.RunACAnalysis(circuit, 1000.0, 1000000.0, 100, exports);
        }
    }

    private TransientAnalysisResult RunTransientAnalysisWithConfig(CircuitModel circuit, object? config, IEnumerable<string> exports)
    {
        if (config is Dictionary<string, object> transConfig)
        {
            var startTime = transConfig.ContainsKey("startTime") ? Convert.ToDouble(transConfig["startTime"]) : 0.0;
            var stopTime = transConfig.ContainsKey("stopTime") ? Convert.ToDouble(transConfig["stopTime"]) : 1e-3;
            var timeStep = transConfig.ContainsKey("timeStep") ? Convert.ToDouble(transConfig["timeStep"]) : 1e-6;
            return _transientAnalysisService.RunTransientAnalysis(circuit, startTime, stopTime, timeStep, exports, false);
        }
        else
        {
            return _transientAnalysisService.RunTransientAnalysis(circuit, 0.0, 1e-3, 1e-6, exports, false);
        }
    }

    /// <summary>
    /// Collects results from analysis into the sweep result
    /// </summary>
    private void CollectResults(TemperatureSweepResult sweepResult, object analysisResult, IEnumerable<string> exports)
    {
        foreach (var export in exports)
        {
            var value = ExtractExportValue(analysisResult, export);
            if (value.HasValue)
            {
                sweepResult.Results[export].Add(value.Value);
            }
            else
            {
                sweepResult.Results[export].Add(double.NaN);
            }
        }
    }

    /// <summary>
    /// Extracts a single export value from analysis result
    /// </summary>
    private double? ExtractExportValue(object analysisResult, string export)
    {
        return analysisResult switch
        {
            OperatingPointResult opResult => ExtractFromOperatingPoint(opResult, export),
            DCAnalysisResult dcResult => ExtractFromDC(dcResult, export),
            ACAnalysisResult acResult => ExtractFromAC(acResult, export),
            TransientAnalysisResult transResult => ExtractFromTransient(transResult, export),
            _ => null
        };
    }

    private double? ExtractFromOperatingPoint(OperatingPointResult result, string export)
    {
        if (export.StartsWith("v(") && export.EndsWith(")"))
        {
            var node = export.Substring(2, export.Length - 3);
            if (result.NodeVoltages.ContainsKey(node))
                return result.NodeVoltages[node];
        }
        else if (export.StartsWith("i(") && export.EndsWith(")"))
        {
            var component = export.Substring(2, export.Length - 3);
            // Try both the export format "i(D1)" and the component name "D1"
            if (result.BranchCurrents.ContainsKey(export))
                return result.BranchCurrents[export];
            else if (result.BranchCurrents.ContainsKey(component))
                return result.BranchCurrents[component];
        }

        return null;
    }

    private double? ExtractFromDC(DCAnalysisResult result, string export)
    {
        if (result.Results.ContainsKey(export) && result.Results[export].Count > 0)
        {
            // Return the last value (or average, or first - depends on use case)
            return result.Results[export].Last();
        }
        return null;
    }

    private double? ExtractFromAC(ACAnalysisResult result, string export)
    {
        // For AC, we might want magnitude or phase - default to magnitude
        if (result.MagnitudeDb.ContainsKey(export) && result.MagnitudeDb[export].Count > 0)
        {
            return result.MagnitudeDb[export].Last();
        }
        return null;
    }

    private double? ExtractFromTransient(TransientAnalysisResult result, string export)
    {
        if (result.Signals.ContainsKey(export) && result.Signals[export].Count > 0)
        {
            return result.Signals[export].Last();
        }
        return null;
    }

    /// <summary>
    /// Gets the unit for an export signal
    /// </summary>
    private string GetUnitForExport(string export)
    {
        if (export.StartsWith("v("))
            return "V";
        else if (export.StartsWith("i("))
            return "A";
        else if (export.StartsWith("p("))
            return "W";
        else
            return "";
    }
}

