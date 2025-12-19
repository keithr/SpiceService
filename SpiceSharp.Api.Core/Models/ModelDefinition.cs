namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Defines a component model for semiconductors.
/// </summary>
public class ModelDefinition
{
    /// <summary>
    /// Type of model (e.g., "diode", "bjt_npn", "mosfet_n").
    /// </summary>
    public string ModelType { get; set; } = string.Empty;

    /// <summary>
    /// Unique name for the model (e.g., "1N4148", "2N2222").
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Model parameters specific to the model type.
    /// </summary>
    public Dictionary<string, double> Parameters { get; set; } = new();
}
