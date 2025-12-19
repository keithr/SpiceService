using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Components;
using SpiceSharp.Api.Core.Models;
using System.Diagnostics;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing parameter sweep analyses
/// </summary>
public class ParameterSweepService : IParameterSweepService
{
    private readonly IOperatingPointService _operatingPointService;
    private readonly IDCAnalysisService _dcAnalysisService;
    private readonly IACAnalysisService _acAnalysisService;
    private readonly ITransientAnalysisService _transientAnalysisService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterSweepService"/> class
    /// </summary>
    public ParameterSweepService(
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
    public ParameterSweepResult RunParameterSweep(
        CircuitModel circuit,
        string parameterPath,
        double start,
        double stop,
        double step,
        string analysisType,
        object? analysisConfig,
        IEnumerable<string> exports)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (string.IsNullOrWhiteSpace(parameterPath))
            throw new ArgumentException("Parameter path is required.", nameof(parameterPath));

        if (step <= 0)
            throw new ArgumentException("Step must be greater than zero.", nameof(step));

        if (start > stop)
            throw new ArgumentException("Start value must be less than or equal to stop value.", nameof(start));

        var parsedPath = ParseParameterPath(parameterPath);
        var stopwatch = Stopwatch.StartNew();

        // Generate parameter values
        var parameterValues = GenerateSweepValues(start, stop, step);
        
        var result = new ParameterSweepResult
        {
            ParameterPath = parameterPath,
            ParameterValues = parameterValues,
            AnalysisType = analysisType,
            Status = "Success"
        };

        // Initialize results dictionary
        foreach (var export in exports)
        {
            result.Results[export] = new List<double>();
            result.Units[export] = GetUnitForExport(export);
            
            // Initialize AC and transient result dictionaries if needed
            if (analysisType.ToLower() == "ac")
            {
                result.ACResults[export] = new List<List<double>>();
            }
            else if (analysisType.ToLower() == "transient")
            {
                result.TransientResults[export] = new List<List<double>>();
            }
        }

        // Run sweep for each parameter value
        foreach (var paramValue in parameterValues)
        {
            // Clone circuit for this sweep point
            var clonedCircuit = CloneCircuit(circuit);

            // Modify parameter
            ModifyParameter(clonedCircuit, parsedPath, paramValue);

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
    /// Parses a parameter path into its components
    /// </summary>
    public static ParameterPath ParseParameterPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Parameter path cannot be empty.", nameof(path));

        var parts = path.Split('.');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid parameter path format: '{path}'. Expected format: 'ComponentName.property' or 'ModelName.parameter'", nameof(path));

        var firstPart = parts[0];
        var secondPart = parts[1];

        // Check if it's a model path (models are typically uppercase/underscore)
        // Component paths are typically like "R1.value" or "V1.parameters.acmag"
        // Model paths are typically like "LED_MODEL.IS"
        if (firstPart.Contains('_') || firstPart.All(char.IsUpper))
        {
            // Likely a model path
            return new ParameterPath
            {
                ModelName = firstPart,
                ParameterName = secondPart,
                IsModelPath = true
            };
        }
        else
        {
            // Likely a component path
            return new ParameterPath
            {
                ComponentName = firstPart,
                PropertyName = secondPart,
                IsComponentPath = true
            };
        }
    }

    /// <summary>
    /// Generates sweep values from start to stop with given step
    /// </summary>
    private List<double> GenerateSweepValues(double start, double stop, double step)
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
    /// Clones a circuit for parameter modification
    /// </summary>
    private CircuitModel CloneCircuit(CircuitModel original)
    {
        var cloned = new CircuitModel
        {
            Id = $"{original.Id}_clone_{Guid.NewGuid():N}",
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
    /// Modifies a parameter in the circuit
    /// </summary>
    private void ModifyParameter(CircuitModel circuit, ParameterPath path, double value)
    {
        if (path.IsComponentPath)
        {
            ModifyComponentParameter(circuit, path, value);
        }
        else if (path.IsModelPath)
        {
            ModifyModelParameter(circuit, path, value);
        }
        else
        {
            throw new ArgumentException($"Invalid parameter path: {path}");
        }
    }

    /// <summary>
    /// Modifies a component parameter
    /// </summary>
    private void ModifyComponentParameter(CircuitModel circuit, ParameterPath path, double value)
    {
        var component = circuit.InternalCircuit.TryGetEntity(path.ComponentName!, out var entity) ? entity : null;
        if (component == null)
            throw new ArgumentException($"Component '{path.ComponentName}' not found in circuit.");

        if (path.PropertyName == "value")
        {
            // For components with a Value property, we need to determine the component type
            // and use the appropriate parameter name
            var componentDef = circuit.ComponentDefinitions[path.ComponentName!];
            var componentType = componentDef.ComponentType.ToLower();

            // Try to set the parameter based on component type
            var parameterSet = false;
            if (componentType == "resistor")
            {
                try 
                { 
                    component.SetParameter("resistance", value); 
                    parameterSet = true;
                    // Also update ComponentDefinition to keep it in sync
                    if (circuit.ComponentDefinitions.TryGetValue(path.ComponentName!, out var updatedDef))
                    {
                        updatedDef.Value = value;
                    }
                } 
                catch { }
            }
            else if (componentType == "capacitor")
            {
                try { component.SetParameter("capacitance", value); parameterSet = true; } catch { }
            }
            else if (componentType == "inductor")
            {
                try { component.SetParameter("inductance", value); parameterSet = true; } catch { }
            }
            else if (componentType == "voltage_source" || componentType == "current_source")
            {
                try { component.SetParameter("dc", value); parameterSet = true; } catch { }
            }

            // If SetParameter didn't work, recreate the component with new value
            if (!parameterSet)
            {
                RecreateComponentWithNewValue(circuit, path.ComponentName!, value);
            }
        }
        else
        {
            // For other component parameters (e.g., V1.parameters.acmag)
            component.SetParameter(path.PropertyName!, value);
        }
    }

    /// <summary>
    /// Recreates a component with a new value
    /// </summary>
    private void RecreateComponentWithNewValue(CircuitModel circuit, string componentName, double newValue)
    {
        var componentDef = circuit.ComponentDefinitions[componentName];
        var oldComponent = circuit.InternalCircuit.TryGetEntity(componentName, out var entity) ? entity : null;
        
        if (oldComponent != null)
        {
            circuit.InternalCircuit.Remove(oldComponent);
            // Also remove from definitions temporarily to avoid duplicate check
            circuit.ComponentDefinitions.Remove(componentName);
        }

        // Update the definition to match the new value
        componentDef.Value = newValue;

        // Recreate the component
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, componentDef);
    }

    /// <summary>
    /// Modifies a model parameter
    /// </summary>
    private void ModifyModelParameter(CircuitModel circuit, ParameterPath path, double value)
    {
        var model = circuit.InternalCircuit.TryGetEntity(path.ModelName!, out var entity) ? entity : null;
        if (model == null)
            throw new ArgumentException($"Model '{path.ModelName}' not found in circuit.");

        model.SetParameter(path.ParameterName!, value);
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
        // Extract DC analysis config (source, start, stop, step)
        // For parameter sweep, we typically want a single point, so use operating point
        // Or extract from config if provided
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
            // For parameter sweep, we usually want operating point, but if DC is requested,
            // run a small sweep around the current value
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
    private void CollectResults(ParameterSweepResult sweepResult, object analysisResult, IEnumerable<string> exports)
    {
        // Handle AC analysis - store full frequency response
        if (analysisResult is ACAnalysisResult acResult)
        {
            // Store frequency points (same for all parameter values)
            if (sweepResult.ACFrequencies == null)
            {
                sweepResult.ACFrequencies = new List<double>(acResult.Frequencies);
            }

            // Store full frequency response for each export
            foreach (var export in exports)
            {
                if (!sweepResult.ACResults.ContainsKey(export))
                {
                    sweepResult.ACResults[export] = new List<List<double>>();
                }

                if (acResult.MagnitudeDb.ContainsKey(export))
                {
                    // Store the full magnitude frequency response
                    sweepResult.ACResults[export].Add(new List<double>(acResult.MagnitudeDb[export]));
                }
                else
                {
                    // No data for this export at this parameter point
                    sweepResult.ACResults[export].Add(new List<double>());
                }

                // Also store aggregated value (last frequency point) for backward compatibility
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
            return;
        }

        // Handle transient analysis - store full time series
        if (analysisResult is TransientAnalysisResult transResult)
        {
            // Store time points (same for all parameter values)
            if (sweepResult.TransientTime == null)
            {
                sweepResult.TransientTime = new List<double>(transResult.Time);
            }

            // Store full time series for each export
            foreach (var export in exports)
            {
                if (!sweepResult.TransientResults.ContainsKey(export))
                {
                    sweepResult.TransientResults[export] = new List<List<double>>();
                }

                if (transResult.Signals.ContainsKey(export))
                {
                    // Store the full time series
                    sweepResult.TransientResults[export].Add(new List<double>(transResult.Signals[export]));
                }
                else
                {
                    // No data for this export at this parameter point
                    sweepResult.TransientResults[export].Add(new List<double>());
                }

                // Also store aggregated value (last time point/steady-state) for backward compatibility
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
            return;
        }

        // Handle operating point and DC analysis - single values per parameter point
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
            // BranchCurrents uses component name as key, not the full "i(R1)" format
            if (result.BranchCurrents.ContainsKey(component))
                return result.BranchCurrents[component];
            // Also try the full export string in case it's stored that way
            if (result.BranchCurrents.ContainsKey(export))
                return result.BranchCurrents[export];
        }

        return null;
    }

    private double? ExtractFromDC(DCAnalysisResult result, string export)
    {
        if (result.Results.ContainsKey(export) && result.Results[export].Count > 0)
        {
            // Return the last value (or average, or first - depends on use case)
            // For parameter sweep, we typically want a single value, so return the last
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

/// <summary>
/// Represents a parsed parameter path
/// </summary>
public class ParameterPath
{
    /// <summary>
    /// Component name if this is a component path (e.g., "R1")
    /// </summary>
    public string? ComponentName { get; set; }

    /// <summary>
    /// Property name if this is a component path (e.g., "value")
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Model name if this is a model path (e.g., "LED_MODEL")
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Parameter name if this is a model path (e.g., "IS")
    /// </summary>
    public string? ParameterName { get; set; }

    /// <summary>
    /// Whether this is a component parameter path
    /// </summary>
    public bool IsComponentPath { get; set; }

    /// <summary>
    /// Whether this is a model parameter path
    /// </summary>
    public bool IsModelPath { get; set; }
}

