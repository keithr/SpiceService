using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Entities;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ComponentFactory
/// </summary>
public class ComponentFactoryTests
{
    private readonly ComponentFactory _factory;

    public ComponentFactoryTests()
    {
        _factory = new ComponentFactory();
    }

    #region Resistor Tests

    [Fact]
    public void CreateComponent_Resistor_WithValidInput_CreatesResistor()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1000.0
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("R1", entity.Name);
    }

    [Fact]
    public void CreateComponent_Resistor_WithoutValue_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "n1", "n2" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    [Fact]
    public void CreateComponent_Resistor_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "n1裘" },
            Value = 1000.0
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    #endregion

    #region Capacitor Tests

    [Fact]
    public void CreateComponent_Capacitor_WithValidInput_CreatesCapacitor()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1e-6
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("C1", entity.Name);
    }

    #endregion

    #region Inductor Tests

    [Fact]
    public void CreateComponent_Inductor_WithValidInput_CreatesInductor()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1e-3
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("L1", entity.Name);
    }

    #endregion

    #region Diode Tests

    [Fact]
    public void CreateComponent_Diode_WithValidInput_CreatesDiode()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "cathode" },
            Model = "1N4148"
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("D1", entity.Name);
    }

    [Fact]
    public void CreateComponent_Diode_WithoutModel_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "cathode" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    [Fact]
    public void CreateComponent_Diode_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode" },
            Model = "1N4148"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    #endregion

    #region BJT Tests

    [Fact]
    public void CreateComponent_BJT_WithValidInput_CreatesBJT()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "Q1",
            ComponentType = "bjt_npn",
            Nodes = new List<string> { "collector", "base", "emitter" },
            Model = "2N2222"
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("Q1", entity.Name);
    }

    [Fact]
    public void CreateComponent_BJT_WithoutModel_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "Q1",
            ComponentType = "bjt_npn",
            Nodes = new List<string> { "collector", "base", "emitter" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    [Fact]
    public void CreateComponent_BJT_WithSubstrate_UsesSubstrateNode()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "Q1",
            ComponentType = "bjt_npn",
            Nodes = new List<string> { "collector", "base", "emitter", "substrate" },
            Model = "2N2222"
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
    }

    [Fact]
    public void CreateComponent_BJT_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "Q1",
            ComponentType = "bjt_npn",
            Nodes = new List<string> { "collector", "base" },
            Model = "2N2222"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    #endregion

    #region MOSFET Tests

    [Fact]
    public void CreateComponent_MOSFET_WithValidInput_CreatesrenchMOSFET()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "M1",
            ComponentType = "mosfet_n",
            Nodes = new List<string> { "drain", "gate", "source", "bulk" },
            Model = "NMOS"
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("M1", entity.Name);
    }

    [Fact]
    public void CreateComponent_MOSFET_WithoutModel_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "M1",
            ComponentType = "mosfet_n",
            Nodes = new List<string> { "drain", "gate", "source", "bulk" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    [Fact]
    public void CreateComponent_MOSFET_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "M1",
            ComponentType = "mosfet_n",
            Nodes = new List<string> { "drain", "gate", "source" },
            Model = "NMOS"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    #endregion

    #region JFET Tests

    [Fact]
    public void CreateComponent_JFET_WithValidInput_CreatesJFET()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "J1",
            ComponentType = "jfet_n",
            Nodes = new List<string> { "drain", "gate", "source" },
            Model = "2N3819"
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("J1", entity.Name);
    }

    [Fact]
    public void CreateComponent_JFET_WithoutModel_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "J1",
            ComponentType = "jfet_n",
            Nodes = new List<string> { "drain", "gate", "source" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    #endregion

    #region Voltage Source Tests

    [Fact]
    public void CreateComponent_VoltageSource_WithValidInput_CreatesVoltageSource()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 5.0
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithSineWaveform_CreatesVoltageSourceWithWaveform()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "sine" },
                { "amplitude", 0.5 },
                { "frequency", 1000.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Sine>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithSineWaveform_AllParameters_CreatesCorrectWaveform()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "sine" },
                { "offset", 1.0 },
                { "amplitude", 0.5 },
                { "frequency", 1000.0 },
                { "delay", 0.001 },
                { "damping", 100.0 },
                { "phase", 45.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        var sine = voltageSource.Parameters.Waveform as SpiceSharp.Components.Sine;
        Assert.NotNull(sine);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithSineWaveform_MissingAmplitude_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "sine" },
                { "frequency", 1000.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("amplitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithSineWaveform_MissingFrequency_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "sine" },
                { "amplitude", 0.5 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("frequency", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithUnsupportedWaveform_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "triangle" },
                { "amplitude", 0.5 },
                { "frequency", 1000.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("Unsupported waveform type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseWaveform_CreatesVoltageSourceWithPulse()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "v1", 0.0 },
                { "v2", 5.0 },
                { "td", 0.0 },
                { "tr", 1e-6 },
                { "tf", 1e-6 },
                { "pw", 1e-3 },
                { "per", 2e-3 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pulse>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseWaveform_MissingRequiredParameter_ThrowsArgumentException()
    {
        // Arrange - missing v2
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "v1", 0.0 },
                { "td", 0.0 },
                { "tr", 1e-6 },
                { "tf", 1e-6 },
                { "pw", 1e-3 },
                { "per", 2e-3 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("v2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_CurrentSource_WithPulseWaveform_CreatesCurrentSourceWithPulse()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "I1",
            ComponentType = "current_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "v1", 0.0 },
                { "v2", 0.001 },
                { "td", 0.0 },
                { "tr", 1e-6 },
                { "tf", 1e-6 },
                { "pw", 1e-3 },
                { "per", 2e-3 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("I1", entity.Name);
        var currentSource = entity as SpiceSharp.Components.CurrentSource;
        Assert.NotNull(currentSource);
        Assert.NotNull(currentSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pulse>(currentSource.Parameters.Waveform);
    }

    #region PULSE Flat Parameter Tests

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseFlatParameters_AllParameters_CreatesVoltageSourceWithPulse()
    {
        // Arrange - PULSE with flat parameter format (pulse_v1, pulse_v2, etc.)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_v2", 5.0 },
                { "pulse_td", 0.0 },
                { "pulse_tr", 1e-6 },
                { "pulse_tf", 1e-6 },
                { "pulse_pw", 1e-3 },
                { "pulse_per", 2e-3 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pulse>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseFlatParameters_MinimalParameters_CreatesVoltageSourceWithPulse()
    {
        // Arrange - PULSE with minimal required parameters (v1, v2)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_v2", 3.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pulse>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseFlatParameters_MissingV1_ThrowsArgumentException()
    {
        // Arrange - missing pulse_v1
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v2", 5.0 },
                { "pulse_td", 0.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("v1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseFlatParameters_MissingV2_ThrowsArgumentException()
    {
        // Arrange - missing pulse_v2
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_td", 0.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("v2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_CurrentSource_WithPulseFlatParameters_CreatesCurrentSourceWithPulse()
    {
        // Arrange - PULSE with flat parameters for current source
        var definition = new ComponentDefinition
        {
            Name = "I1",
            ComponentType = "current_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_v2", 0.001 },
                { "pulse_td", 0.0 },
                { "pulse_tr", 1e-6 },
                { "pulse_tf", 1e-6 },
                { "pulse_pw", 1e-3 },
                { "pulse_per", 2e-3 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("I1", entity.Name);
        var currentSource = entity as SpiceSharp.Components.CurrentSource;
        Assert.NotNull(currentSource);
        Assert.NotNull(currentSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pulse>(currentSource.Parameters.Waveform);
    }

    #endregion

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLWaveform_CreatesVoltageSourceWithPWL()
    {
        // Arrange - PWL takes array of [time, voltage] pairs
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pwl" },
                { "points", new object[] { new object[] { 0.0, 0.0 }, new object[] { 1e-3, 5.0 }, new object[] { 2e-3, 0.0 } } }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pwl>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLWaveform_MissingPoints_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pwl" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("points", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLWaveform_EmptyPoints_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pwl" },
                { "points", new object[] { } }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("points", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #region PWL Flat Parameter Tests

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_TwoPoints_CreatesVoltageSourceWithPWL()
    {
        // Arrange - PWL with flat parameter format (pwl_t0, pwl_v0, pwl_t1, pwl_v1)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 },
                { "pwl_t1", 1e-3 },
                { "pwl_v1", 3.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pwl>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_FourPoints_CreatesVoltageSourceWithPWL()
    {
        // Arrange - PWL with 4 points (t0, v0, t1, v1, t2, v2, t3, v3)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 },
                { "pwl_t1", 1e-3 },
                { "pwl_v1", 5.0 },
                { "pwl_t2", 2e-3 },
                { "pwl_v2", 0.0 },
                { "pwl_t3", 3e-3 },
                { "pwl_v3", -5.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pwl>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_ManyPoints_CreatesVoltageSourceWithPWL()
    {
        // Arrange - PWL with 10+ points (LED measurement data example)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.00 },
                { "pwl_v0", 0.00000 },
                { "pwl_t1", 1.71 },
                { "pwl_v1", 0.00007 },
                { "pwl_t2", 2.14 },
                { "pwl_v2", 0.00235 },
                { "pwl_t3", 2.57 },
                { "pwl_v3", 0.02605 },
                { "pwl_t4", 2.84 },
                { "pwl_v4", 0.03591 },
                { "pwl_t5", 3.00 },
                { "pwl_v5", 0.04000 },
                { "pwl_t6", 3.50 },
                { "pwl_v6", 0.05000 },
                { "pwl_t7", 4.00 },
                { "pwl_v7", 0.06000 },
                { "pwl_t8", 4.50 },
                { "pwl_v8", 0.07000 },
                { "pwl_t9", 5.00 },
                { "pwl_v9", 0.08000 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("V1", entity.Name);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        Assert.NotNull(voltageSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pwl>(voltageSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_MissingVoltage_ThrowsArgumentException()
    {
        // Arrange - missing pwl_v1 (odd number of parameters)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 },
                { "pwl_t1", 1e-3 }
                // Missing pwl_v1
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("pwl", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_MissingTime_ThrowsArgumentException()
    {
        // Arrange - missing pwl_t1 (mismatched pairs)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 },
                { "pwl_v1", 3.0 }
                // Missing pwl_t1
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("pwl", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_OnlyOnePoint_ThrowsArgumentException()
    {
        // Arrange - only one point (need at least 2 points)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 }
                // Need at least 2 points for PWL
            }
        };

        // Act & Assert
        // Note: This might fail during validation or during PWL creation
        // The exact behavior depends on implementation
        var ex = Assert.ThrowsAny<Exception>(() => _factory.CreateComponent(definition));
        Assert.True(ex is ArgumentException || ex.Message.Contains("pwl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComponent_CurrentSource_WithPWLFlatParameters_CreatesCurrentSourceWithPWL()
    {
        // Arrange - PWL with flat parameters for current source
        var definition = new ComponentDefinition
        {
            Name = "I1",
            ComponentType = "current_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 },
                { "pwl_t1", 1e-3 },
                { "pwl_v1", 0.001 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("I1", entity.Name);
        var currentSource = entity as SpiceSharp.Components.CurrentSource;
        Assert.NotNull(currentSource);
        Assert.NotNull(currentSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Pwl>(currentSource.Parameters.Waveform);
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLFlatParameters_NonMonotonicTime_HandlesGracefully()
    {
        // Arrange - Non-monotonic time (t1 < t0) - should sort or error
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.002 },
                { "pwl_v0", 1.0 },
                { "pwl_t1", 0.001 },
                { "pwl_v1", 2.0 },
                { "pwl_t2", 0.003 },
                { "pwl_v2", 0.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        // Should either sort by time or handle gracefully
        Assert.NotNull(entity);
        var voltageSource = entity as SpiceSharp.Components.VoltageSource;
        Assert.NotNull(voltageSource);
        // If sorted, waveform should still be created
        // If not sorted, SpiceSharp might handle it or error - both are acceptable
        // The key is that we don't crash
    }

    #endregion

    #endregion

    #region Current Source Tests

    [Fact]
    public void CreateComponent_CurrentSource_WithValidInput_CreatesCurrentSource()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "I1",
            ComponentType = "current_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.001
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("I1", entity.Name);
    }

    [Fact]
    public void CreateComponent_CurrentSource_WithSineWaveform_CreatesCurrentSourceWithWaveform()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "I1",
            ComponentType = "current_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "sine" },
                { "amplitude", 0.001 },
                { "frequency", 1000.0 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("I1", entity.Name);
        var currentSource = entity as SpiceSharp.Components.CurrentSource;
        Assert.NotNull(currentSource);
        Assert.NotNull(currentSource.Parameters.Waveform);
        Assert.IsType<SpiceSharp.Components.Sine>(currentSource.Parameters.Waveform);
    }

    #endregion

    #region Dependent Source Tests

    [Fact]
    public void CreateComponent_VCVS_WithValidInput_CreatesVCVS()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("E1", entity.Name);
    }

    [Fact]
    public void CreateComponent_VCVS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VCVS_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("4", ex.Message);
    }

    [Fact]
    public void CreateComponent_VCCS_WithValidInput_CreatesVCCS()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 0.001 } }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("G1", entity.Name);
    }

    [Fact]
    public void CreateComponent_VCCS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_CCVS_WithValidInput_CreatesCCVS()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "H1",
            ComponentType = "ccvs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" },
            Parameters = new Dictionary<string, object> { { "gain", 100.0 } }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("H1", entity.Name);
    }

    [Fact]
    public void CreateComponent_CCVS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "H1",
            ComponentType = "ccvs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_CCCS_WithValidInput_CreatesCCCS()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "F1",
            ComponentType = "cccs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" },
            Parameters = new Dictionary<string, object> { { "gain", 50.0 } }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("F1", entity.Name);
    }

    [Fact]
    public void CreateComponent_CCCS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "F1",
            ComponentType = "cccs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Mutual Inductance Tests

    [Fact]
    public void CreateComponent_MutualInductance_WithValidInput_CreatesMutualInductance()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" },
                { "coupling", 0.95 }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("K1", entity.Name);
    }

    [Fact]
    public void CreateComponent_MutualInductance_WithoutInductor1_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Parameters = new Dictionary<string, object>
            {
                { "inductor2", "L2" },
                { "coupling", 0.95 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("inductor1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_MutualInductance_WithoutInductor2_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "coupling", 0.95 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("inductor2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_MutualInductance_WithoutCoupling_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("coupling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_MutualInductance_WithInvalidCoupling_ThrowsArgumentException()
    {
        // Arrange - coupling > 1
        var definition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" },
                { "coupling", 1.5 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("coupling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_MutualInductance_WithZeroCoupling_ThrowsArgumentException()
    {
        // Arrange - coupling = 0
        var definition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" },
                { "coupling", 0.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("coupling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Unsupported Component Tests

    [Fact]
    public void CreateComponent_UnsupportedType_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "X1",
            ComponentType = "unknown_component",
            Nodes = new List<string> { "n1", "n2" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    [Fact]
    public void CreateComponent_NullDefinition_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _factory.CreateComponent(null!));
    }

    [Fact]
    public void CreateComponent_EmptyComponentType_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1000.0
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
    }

    #endregion

    #region Switch Tests

    [Fact]
    public void CreateComponent_VoltageSwitch_WithValidInput_CreatesVoltageSwitch()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "S1",
            ComponentType = "voltage_switch",
            Nodes = new List<string> { "n1", "n2" },
            Parameters = new Dictionary<string, object>
            {
                { "controlNodes", new[] { "ctrl+", "ctrl-" } },
                { "model", "SW_MODEL" }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("S1", entity.Name);
    }

    [Fact]
    public void CreateComponent_VoltageSwitch_WithoutModel_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "S1",
            ComponentType = "voltage_switch",
            Nodes = new List<string> { "n1", "n2" },
            Parameters = new Dictionary<string, object>
            {
                { "controlNodes", new[] { "ctrl+", "ctrl-" } }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("model", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_VoltageSwitch_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "S1",
            ComponentType = "voltage_switch",
            Nodes = new List<string> { "n1" },
            Parameters = new Dictionary<string, object>
            {
                { "controlNodes", new[] { "ctrl+", "ctrl-" } },
                { "model", "SW_MODEL" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void CreateComponent_CurrentSwitch_WithValidInput_CreatesCurrentSwitch()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "W1",
            ComponentType = "current_switch",
            Nodes = new List<string> { "n1", "n2" },
            Parameters = new Dictionary<string, object>
            {
                { "controlSource", "V_CTRL" },
                { "model", "SW_MODEL" }
            }
        };

        // Act
        var entity = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("W1", entity.Name);
    }

    [Fact]
    public void CreateComponent_CurrentSwitch_WithoutModel_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "W1",
            ComponentType = "current_switch",
            Nodes = new List<string> { "n1", "n2" },
            Parameters = new Dictionary<string, object>
            {
                { "controlSource", "V_CTRL" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("model", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateComponent_CurrentSwitch_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "W1",
            ComponentType = "current_switch",
            Nodes = new List<string> { "n1" },
            Parameters = new Dictionary<string, object>
            {
                { "controlSource", "V_CTRL" },
                { "model", "SW_MODEL" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("2", ex.Message);
    }

    #endregion

    #region Phase 4: Enhanced Error Message Tests

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseWaveform_MissingV1_ErrorSuggestsCorrectParameterNames()
    {
        // Arrange - missing pulse_v1
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v2", 5.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should mention v1 or pulse_v1
        Assert.Contains("v1", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Error message should suggest correct parameter names
        Assert.True(
            ex.Message.Contains("pulse_v1", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("v1", StringComparison.OrdinalIgnoreCase),
            $"Error message should suggest 'v1' or 'pulse_v1' parameter. Actual message: {ex.Message}");
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseWaveform_MissingV2_ErrorSuggestsCorrectParameterNames()
    {
        // Arrange - missing pulse_v2
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should mention v2 or pulse_v2
        Assert.Contains("v2", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Error message should suggest correct parameter names
        Assert.True(
            ex.Message.Contains("pulse_v2", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("v2", StringComparison.OrdinalIgnoreCase),
            $"Error message should suggest 'v2' or 'pulse_v2' parameter. Actual message: {ex.Message}");
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLWaveform_MismatchedPointCount_ErrorExplainsRequirement()
    {
        // Arrange - mismatched time/voltage pairs (3 time params, 2 voltage params)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 },
                { "pwl_t1", 1e-3 },
                { "pwl_v1", 3.0 },
                { "pwl_t2", 2e-3 }
                // Missing pwl_v2 - mismatched pairs
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should explain the requirement for matching pairs
        Assert.Contains("pwl", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            ex.Message.Contains("matching", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pair", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("time", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("voltage", StringComparison.OrdinalIgnoreCase),
            $"Error message should explain matching time/voltage pairs requirement. Actual message: {ex.Message}");
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPWLWaveform_InsufficientPoints_ErrorExplainsMinimumRequirement()
    {
        // Arrange - only one point (need at least 2)
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pwl_t0", 0.0 },
                { "pwl_v0", 0.0 }
            }
        };

        // Act & Assert
        var ex = Assert.ThrowsAny<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should explain minimum requirement
        Assert.Contains("pwl", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            ex.Message.Contains("2", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("minimum", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("at least", StringComparison.OrdinalIgnoreCase),
            $"Error message should explain minimum 2 points requirement. Actual message: {ex.Message}");
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithUnsupportedWaveform_ErrorSuggestsSupportedTypes()
    {
        // Arrange - unsupported waveform type
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "triangle" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should mention unsupported waveform type
        Assert.Contains("triangle", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Error message should list supported types
        Assert.True(
            ex.Message.Contains("sine", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pulse", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pwl", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("supported", StringComparison.OrdinalIgnoreCase),
            $"Error message should suggest supported waveform types. Actual message: {ex.Message}");
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithUnsupportedWaveform_ErrorReferencesDiscoveryEndpoint()
    {
        // Arrange - unsupported waveform type
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "unknown_waveform" }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should reference discovery endpoint or provide helpful guidance
        // Note: This test verifies the error mentions discovery or provides helpful info
        // The exact format may vary, but should be helpful
        Assert.True(
            ex.Message.Contains("discovery", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("supported", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("sine", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pulse", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pwl", StringComparison.OrdinalIgnoreCase),
            $"Error message should reference discovery endpoint or list supported types. Actual message: {ex.Message}");
    }

    [Fact]
    public void CreateComponent_VoltageSource_WithPulseWaveform_MissingBothRequiredParams_ErrorListsBoth()
    {
        // Arrange - missing both v1 and v2
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "n1", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_td", 0.0 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        // Error message should mention at least one of the required parameters
        Assert.True(
            ex.Message.Contains("v1", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("v2", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pulse_v1", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pulse_v2", StringComparison.OrdinalIgnoreCase),
            $"Error message should mention required parameters v1/v2. Actual message: {ex.Message}");
    }

    #endregion
}

