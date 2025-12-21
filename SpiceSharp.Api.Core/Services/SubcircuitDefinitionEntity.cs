using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Components;
using SpiceSharp.Simulations;
using SpiceSharp.ParameterSets;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Wrapper entity that allows SubcircuitDefinition to be stored in a Circuit.
/// Since SubcircuitDefinition doesn't implement IEntity, this wrapper makes it
/// possible to register definitions in the circuit so they can be retrieved by name.
/// This class also implements ISubcircuitDefinition to allow casting in tests.
/// </summary>
internal class SubcircuitDefinitionEntity : IEntity, ISubcircuitDefinition
{
    private readonly string _name;
    private readonly ISubcircuitDefinition _definition;

    /// <summary>
    /// Gets the name of the entity (the subcircuit definition name).
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets whether the entity has link parameters.
    /// Definitions don't have link parameters.
    /// </summary>
    public bool LinkParameters => false;

    /// <summary>
    /// Gets the wrapped subcircuit definition.
    /// </summary>
    public ISubcircuitDefinition Definition => _definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubcircuitDefinitionEntity"/> class.
    /// </summary>
    /// <param name="name">The name of the subcircuit definition.</param>
    /// <param name="definition">The subcircuit definition to wrap.</param>
    public SubcircuitDefinitionEntity(string name, ISubcircuitDefinition definition)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    /// Creates behaviors for the entity.
    /// Definitions don't create behaviors themselves - instances do.
    /// </summary>
    /// <param name="simulation">The simulation.</param>
    public void CreateBehaviors(ISimulation simulation)
    {
        // Definitions don't create behaviors - only instances do
        // Do nothing
    }

    /// <summary>
    /// Gets a parameter set of the specified type.
    /// Definitions don't have parameter sets.
    /// </summary>
    public P GetParameterSet<P>() where P : IParameterSet, ICloneable<P>
    {
        throw new NotSupportedException("SubcircuitDefinitionEntity does not support parameter sets.");
    }

    /// <summary>
    /// Tries to get a parameter set of the specified type.
    /// Definitions don't have parameter sets.
    /// </summary>
    public bool TryGetParameterSet<P>(out P value) where P : IParameterSet, ICloneable<P>
    {
        value = default(P)!;
        return false;
    }

    /// <summary>
    /// Gets all parameter sets.
    /// Definitions don't have parameter sets.
    /// </summary>
    public IEnumerable<IParameterSet> ParameterSets => Enumerable.Empty<IParameterSet>();

    // IParameterSet implementation (definitions don't have parameters)
    public void SetParameter<P>(string name, P value)
    {
        throw new NotSupportedException("SubcircuitDefinitionEntity does not support parameter sets.");
    }
    
    public bool TrySetParameter<P>(string name, P value)
    {
        return false;
    }
    
    public P GetProperty<P>(string name)
    {
        throw new NotSupportedException("SubcircuitDefinitionEntity does not support parameter sets.");
    }
    
    public bool TryGetProperty<P>(string name, out P value)
    {
        value = default(P)!;
        return false;
    }
    
    public Action<P> CreateParameterSetter<P>(string name)
    {
        throw new NotSupportedException("SubcircuitDefinitionEntity does not support parameter sets.");
    }
    
    public Func<P> CreatePropertyGetter<P>(string name)
    {
        throw new NotSupportedException("SubcircuitDefinitionEntity does not support parameter sets.");
    }

    // ISubcircuitDefinition implementation (delegated to definition)
    // Only implement what's actually in the interface
    public IEntityCollection Entities => _definition.Entities;
    public IReadOnlyList<string> Pins => _definition.Pins;

    // ICloneable<IEntity> implementation
    IEntity ICloneable<IEntity>.Clone() => new SubcircuitDefinitionEntity(_name, _definition);
    
    // ICloneable<ISubcircuitDefinition> implementation
    ISubcircuitDefinition ICloneable<ISubcircuitDefinition>.Clone() => _definition.Clone();
}

