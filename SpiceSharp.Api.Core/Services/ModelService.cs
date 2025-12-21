using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Components;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing models within circuits.
/// </summary>
public class ModelService : IModelService
{
    /// <inheritdoc/>
    public IEntity DefineModel(CircuitModel circuit, ModelDefinition definition)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.ModelName))
            throw new ArgumentException("Model name is required.", nameof(definition));

        // Check for duplicate model name
        if (GetModel(circuit, definition.ModelName) != null)
            throw new ArgumentException($"Model with name '{definition.ModelName}' already exists in the circuit.");

        // Create the SpiceSharp model entity
        var modelEntity = CreateModel(definition);

        // Store the definition for export and metadata tracking
        circuit.ModelDefinitions[definition.ModelName] = definition;

        // Add to the circuit
        circuit.InternalCircuit.Add(modelEntity);

        // Update modified timestamp
        circuit.ModifiedAt = DateTime.UtcNow;

        return modelEntity;
    }

    /// <inheritdoc/>
    public IEntity? GetModel(CircuitModel circuit, string name)
    {
        if (circuit == null)
            return null;

        if (string.IsNullOrWhiteSpace(name))
            return null;

        return circuit.InternalCircuit.TryGetEntity(name, out var entity) ? entity : null;
    }

    /// <inheritdoc/>
    public IEnumerable<IEntity> ListModels(CircuitModel circuit)
    {
        if (circuit == null)
            return Enumerable.Empty<IEntity>();

        // Filter to only return model entities (not component entities)
        // Models are typically entities that are instances of specific model classes
        return circuit.InternalCircuit.Where(e => e is DiodeModel 
            || e is BipolarJunctionTransistorModel 
            || e is Mosfet1Model 
            || e is JFETModel
            || e is VoltageSwitchModel
            || e is CurrentSwitchModel);
    }

    /// <summary>
    /// Creates a SpiceSharp model entity from a model definition.
    /// </summary>
    internal IEntity CreateModel(ModelDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.ModelType))
            throw new ArgumentException("Model type is required.", nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.ModelName))
            throw new ArgumentException("Model name is required.", nameof(definition));

        // Create model based on type
        return definition.ModelType.ToLower() switch
        {
            "diode" => CreateDiodeModel(definition),
            "bjt_npn" or "bjt_pnp" => CreateBJTModel(definition),
            "mosfet_n" or "mosfet_p" => CreateMOSFETModel(definition),
            "jfet_n" or "jfet_p" => CreateJFETModel(definition),
            "voltage_switch" => CreateVoltageSwitchModel(definition),
            "current_switch" => CreateCurrentSwitchModel(definition),
            _ => throw new ArgumentException($"Unsupported model type: {definition.ModelType}")
        };
    }

    private IEntity CreateDiodeModel(ModelDefinition definition)
    {
        // Validate parameters before creating the model
        ValidateDiodeParameters(definition.Parameters);
        
        var model = new DiodeModel(definition.ModelName);
        
        // Apply parameters using SetParameter
        foreach (var param in definition.Parameters)
        {
            try
            {
                model.SetParameter(param.Key, param.Value);
            }
            catch (SpiceSharp.Diagnostics.ParameterNotFoundException ex)
            {
                // Provide helpful error message with workaround suggestions
                var errorMessage = GetDiodeParameterErrorMessage(param.Key);
                throw new ArgumentException(errorMessage, nameof(definition), ex);
            }
        }

        return model;
    }

    /// <summary>
    /// Validates diode parameters and throws ArgumentException for unsupported parameters with helpful messages.
    /// </summary>
    private void ValidateDiodeParameters(Dictionary<string, double> parameters)
    {
        // Supported parameters by SpiceSharp DiodeModel
        var supportedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Basic parameters
            "IS", "N", "EG", "RS",
            // AC/High-frequency parameters
            "CJO", "CJ0", "VJ", "PB", "M", "TT", "FC",
            // Breakdown parameters
            "BV", "IBV",
            // Temperature parameters
            "XTI", "TNOM"
        };

        // Unsupported parameters with workaround suggestions
        var unsupportedParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "RSH", "Shunt resistance (RSH) is not supported by SpiceSharp. Workaround: Use an external parallel resistor component connected across the diode to model shunt resistance for degraded devices." },
            { "IKF", "High-injection knee current (IKF) is not supported by SpiceSharp. This parameter is critical for accurate LED modeling at high injection levels. No direct workaround available." },
            { "KF", "Flicker noise coefficient (KF) is not supported by SpiceSharp. Noise analysis will not include flicker noise effects." },
            { "AF", "Flicker noise exponent (AF) is not supported by SpiceSharp. Noise analysis will not include flicker noise effects." },
            { "NBV", "Reverse breakdown emission coefficient (NBV) is not supported by SpiceSharp. Breakdown behavior may be less accurate." },
            { "TRS1", "First-order RS temperature coefficient (TRS1) is not supported by SpiceSharp. Temperature-dependent series resistance modeling is limited." },
            { "TRS2", "Second-order RS temperature coefficient (TRS2) is not supported by SpiceSharp. Temperature-dependent series resistance modeling is limited." },
            { "TBV1", "First-order BV temperature coefficient (TBV1) is not supported by SpiceSharp. Temperature-dependent breakdown voltage modeling is limited." },
            { "TBV2", "Second-order BV temperature coefficient (TBV2) is not supported by SpiceSharp. Temperature-dependent breakdown voltage modeling is limited." },
            { "AREA", "Area scaling factor (AREA) is not supported by SpiceSharp. Scale component values manually if needed." },
            { "PJ", "Perimeter scaling factor (PJ) is not supported by SpiceSharp. Scale component values manually if needed." }
        };

        foreach (var param in parameters.Keys)
        {
            // Check if parameter is supported (case-insensitive)
            if (!supportedParameters.Contains(param))
            {
                // Check if it's a known unsupported parameter
                if (unsupportedParameters.TryGetValue(param, out var workaround))
                {
                    throw new ArgumentException(
                        $"Diode parameter '{param}' is not supported by SpiceSharp. {workaround} " +
                        $"Supported parameters: IS, N, EG, RS, CJO, VJ, M, TT, FC, BV, IBV, XTI, TNOM. " +
                        $"See /api/models/types for complete parameter documentation.",
                        nameof(parameters));
                }
                else
                {
                    // Unknown parameter
                    throw new ArgumentException(
                        $"Unknown diode parameter '{param}'. " +
                        $"Supported parameters: IS, N, EG, RS, CJO, VJ, M, TT, FC, BV, IBV, XTI, TNOM. " +
                        $"See /api/models/types for complete parameter documentation.",
                        nameof(parameters));
                }
            }
        }
    }

    /// <summary>
    /// Gets a helpful error message for unsupported diode parameters.
    /// </summary>
    private string GetDiodeParameterErrorMessage(string parameterName)
    {
        var workarounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "RSH", "Shunt resistance (RSH) is not supported. Workaround: Use an external parallel resistor component." },
            { "IKF", "High-injection knee current (IKF) is not supported. No direct workaround available." },
            { "KF", "Flicker noise coefficient (KF) is not supported. Noise analysis will not include flicker noise." },
            { "AF", "Flicker noise exponent (AF) is not supported. Noise analysis will not include flicker noise." }
        };

        if (workarounds.TryGetValue(parameterName, out var message))
        {
            return $"Diode parameter '{parameterName}' is not supported by SpiceSharp. {message}";
        }

        return $"Diode parameter '{parameterName}' is not supported by SpiceSharp. See /api/models/types for supported parameters.";
    }

    private IEntity CreateBJTModel(ModelDefinition definition)
    {
        var model = new BipolarJunctionTransistorModel(definition.ModelName);

        // Apply parameters
        foreach (var param in definition.Parameters)
        {
            model.SetParameter(param.Key, param.Value);
        }

        return model;
    }

    private IEntity CreateMOSFETModel(ModelDefinition definition)
    {
        var model = new Mosfet1Model(definition.ModelName);

        // Apply parameters
        foreach (var param in definition.Parameters)
        {
            model.SetParameter(param.Key, param.Value);
        }

        return model;
    }

    private IEntity CreateJFETModel(ModelDefinition definition)
    {
        var model = new JFETModel(definition.ModelName);

        // Apply parameters
        foreach (var param in definition.Parameters)
        {
            model.SetParameter(param.Key, param.Value);
        }

        return model;
    }

    private IEntity CreateVoltageSwitchModel(ModelDefinition definition)
    {
        var model = new VoltageSwitchModel(definition.ModelName);

        // Apply parameters
        foreach (var param in definition.Parameters)
        {
            model.SetParameter(param.Key, param.Value);
        }

        return model;
    }

    private IEntity CreateCurrentSwitchModel(ModelDefinition definition)
    {
        var model = new CurrentSwitchModel(definition.ModelName);

        // Apply parameters
        foreach (var param in definition.Parameters)
        {
            model.SetParameter(param.Key, param.Value);
        }

        return model;
    }
}
