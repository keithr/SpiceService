using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

public class NetlistServiceTests
{
    private readonly NetlistService _netlistService;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public NetlistServiceTests()
    {
        _netlistService = new NetlistService();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
    }

    [Fact]
    public void ExportNetlist_WithSimpleCircuit_IncludesTitle()
    {
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Simple test circuit");
        var netlist = _netlistService.ExportNetlist(circuit);

        Assert.Contains(".TITLE test_circuit", netlist);
    }

    [Fact]
    public void ExportNetlist_WithComponents_IncludesComponentLines()
    {
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit with components");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1000
        });

        var netlist = _netlistService.ExportNetlist(circuit);

        Assert.Contains("R1 n1 n2", netlist);
        Assert.Contains("1000", netlist);
    }

    [Fact]
    public void ExportNetlist_WithResistors_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("resistor_test", "Resistor test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "a", "b" },
            Value = 4700
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Contains(lines, line => line.Trim() == "R1 a b 4700");
    }

    [Fact]
    public void ExportNetlist_WithCapacitors_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("cap_test", "Capacitor test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "in", "out" },
            Value = 1e-6
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Contains(lines, line => line.Contains("C1 in out"));
    }

    [Fact]
    public void ExportNetlist_WithDiode_UsesModelReference()
    {
        var circuit = _circuitManager.CreateCircuit("diode_test", "Diode test");
        
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "1N4148",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>()
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "cathode" },
            Model = "1N4148"
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Contains(lines, line => line.Trim() == "D1 anode cathode 1N4148");
        Assert.Contains(lines, line => line.Contains(".MODEL 1N4148 D"));
    }

    [Fact]
    public void ExportNetlist_WithVoltageSource_IncludesDC()
    {
        var circuit = _circuitManager.CreateCircuit("source_test", "Voltage source test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "vcc" },
            Value = 5
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Contains(lines, line => line.Trim() == "V1 0 vcc DC 5");
    }

    [Fact]
    public void ExportNetlist_IncludesEndStatement()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Test");
        var netlist = _netlistService.ExportNetlist(circuit);

        Assert.Contains(".END", netlist);
    }

    [Fact]
    public void ExportNetlist_WithComments_IncludesDescriptiveComments()
    {
        var circuit = _circuitManager.CreateCircuit("test_id", "Test description");
        var netlist = _netlistService.ExportNetlist(circuit, includeComments: true);

        Assert.Contains("* SPICE Netlist", netlist);
        Assert.Contains("* Circuit: test_id", netlist);
        Assert.Contains("* Description: Test description", netlist);
    }

    [Fact]
    public void ExportNetlist_WithoutComments_SkipsComments()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Description");
        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);

        Assert.DoesNotContain("*", netlist);
    }

    [Fact]
    public void ExportNetlist_WithVCVS_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("vcvs_test", "VCVS test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: E1 out+ out- in+ in- 10
        Assert.Contains(lines, line => line.Trim().StartsWith("E1 out+ out- in+ in-"));
        Assert.Contains(lines, line => line.Contains("10"));
    }

    [Fact]
    public void ExportNetlist_WithVCCS_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("vccs_test", "VCCS test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 0.001 } }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: G1 out+ out- in+ in- 0.001
        Assert.Contains(lines, line => line.Trim().StartsWith("G1 out+ out- in+ in-"));
        Assert.Contains(lines, line => line.Contains("0.001"));
    }

    [Fact]
    public void ExportNetlist_WithCCVS_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("ccvs_test", "CCVS test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "H1",
            ComponentType = "ccvs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" },
            Parameters = new Dictionary<string, object> { { "gain", 100.0 } }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: H1 out+ out- V_CTRL_ctrlP_ctrl_ 100
        // Note: CCVS references a voltage source name, not nodes directly
        Assert.Contains(lines, line => line.Trim().StartsWith("H1 out+ out-"));
        Assert.Contains(lines, line => line.Contains("100"));
    }

    [Fact]
    public void ExportNetlist_WithCCCS_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("cccs_test", "CCCS test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "F1",
            ComponentType = "cccs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" },
            Parameters = new Dictionary<string, object> { { "gain", 50.0 } }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: F1 out+ out- V_CTRL_ctrlP_ctrl_ 50
        // Note: CCCS references a voltage source name, not nodes directly
        Assert.Contains(lines, line => line.Trim().StartsWith("F1 out+ out-"));
        Assert.Contains(lines, line => line.Contains("50"));
    }

    [Fact]
    public void ExportNetlist_WithPulseWaveform_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("pulse_test", "Pulse waveform test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
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
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: V1 in 0 PULSE(0 5 0 1e-6 1e-6 1e-3 2e-3)
        Assert.Contains(lines, line => line.Trim().StartsWith("V1 in 0"));
        Assert.Contains(lines, line => line.Contains("PULSE"));
        Assert.Contains(lines, line => line.Contains("0") && line.Contains("5"));
    }

    [Fact]
    public void ExportNetlist_WithPWLWaveform_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("pwl_test", "PWL waveform test");
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pwl" },
                { "points", new object[] { new object[] { 0.0, 0.0 }, new object[] { 1e-3, 5.0 }, new object[] { 2e-3, 0.0 } } }
            }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: V1 in 0 PWL(0 0 0.001 5 0.002 0)
        Assert.Contains(lines, line => line.Trim().StartsWith("V1 in 0"));
        Assert.Contains(lines, line => line.Contains("PWL"));
        Assert.Contains(lines, line => line.Contains("0") && line.Contains("5"));
    }

    [Fact]
    public void ExportNetlist_WithSFFMWaveform_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("sffm_test", "SFFM waveform test");
        // Note: SFFM waveform creation throws NotImplementedException, but netlist export works
        // We'll add the component definition directly to test netlist export
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "sffm" },
                { "vo", 1.0 },
                { "va", 0.5 },
                { "fc", 1e6 },
                { "mdi", 0.1 },
                { "fs", 1e3 }
            }
        };
        
        // Add component definition directly (bypassing ComponentFactory which doesn't support SFFM yet)
        // Use reflection to access internal ComponentDefinitions property for testing netlist export
        var circuitType = typeof(CircuitModel);
        var componentDefsProperty = circuitType.GetProperty("ComponentDefinitions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (componentDefsProperty != null)
        {
            var componentDefs = componentDefsProperty.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
            if (componentDefs != null)
            {
                componentDefs[definition.Name] = definition;
            }
        }

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: V1 in 0 SFFM(1 0.5 1e6 0.1 1e3)
        Assert.Contains(lines, line => line.Trim().StartsWith("V1 in 0"));
        Assert.Contains(lines, line => line.Contains("SFFM"));
        Assert.Contains(lines, line => line.Contains("1") && line.Contains("0.5"));
    }

    [Fact]
    public void ExportNetlist_WithAMWaveform_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("am_test", "AM waveform test");
        // Note: AM waveform creation throws NotImplementedException, but netlist export works
        // We'll add the component definition directly to test netlist export
        var definition = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "am" },
                { "vo", 1.0 },
                { "va", 0.5 },
                { "mf", 1e3 },
                { "fc", 1e6 }
            }
        };
        
        // Add component definition directly (bypassing ComponentFactory which doesn't support AM yet)
        // Use reflection to access internal ComponentDefinitions property for testing netlist export
        var circuitType = typeof(CircuitModel);
        var componentDefsProperty = circuitType.GetProperty("ComponentDefinitions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (componentDefsProperty != null)
        {
            var componentDefs = componentDefsProperty.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
            if (componentDefs != null)
            {
                componentDefs[definition.Name] = definition;
            }
        }

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: V1 in 0 AM(1 0.5 1e3 1e6)
        Assert.Contains(lines, line => line.Trim().StartsWith("V1 in 0"));
        Assert.Contains(lines, line => line.Contains("AM"));
        Assert.Contains(lines, line => line.Contains("1") && line.Contains("0.5"));
    }

    #region Mutual Inductance Netlist Export Tests

    [Fact]
    public void ExportNetlist_WithMutualInductance_FormatsCorrectly()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("mutual_test", "Mutual inductance test");
        
        // Add two inductors first
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1e-3
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L2",
            ComponentType = "inductor",
            Nodes = new List<string> { "n3", "n4" },
            Value = 1e-3
        });

        // Add mutual inductance
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Nodes = new List<string>(),
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" },
                { "coupling", 0.95 }
            }
        });

        // Act
        var netlist = _netlistService.ExportNetlist(circuit);

        // Assert
        Assert.Contains("L1 n1 n2 0.001", netlist);
        Assert.Contains("L2 n3 n4 0.001", netlist);
        Assert.Contains("K1 L1 L2 0.95", netlist);
    }

    [Fact]
    public void ExportNetlist_MutualInductance_WithoutInductor1_ShowsError()
    {
        // Arrange - Create a circuit and manually add invalid mutual inductance definition
        // Note: We bypass ComponentService validation to test netlist export error handling
        var circuit = _circuitManager.CreateCircuit("mutual_error_test", "Mutual inductance error test");
        
        // Use reflection to access internal ComponentDefinitions for testing error formatting
        var circuitType = typeof(CircuitModel);
        var componentDefsProperty = circuitType.GetProperty("ComponentDefinitions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (componentDefsProperty != null)
        {
            var componentDefs = componentDefsProperty.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
            if (componentDefs != null)
            {
                componentDefs["K1"] = new ComponentDefinition
                {
                    Name = "K1",
                    ComponentType = "mutual_inductance",
                    Nodes = new List<string>(),
                    Parameters = new Dictionary<string, object>
                    {
                        { "inductor2", "L2" },
                        { "coupling", 0.95 }
                    }
                };
            }
        }

        // Act
        var netlist = _netlistService.ExportNetlist(circuit);

        // Assert
        Assert.Contains("* Invalid mutual inductance K1", netlist);
        Assert.Contains("missing required parameters", netlist);
    }

    #endregion

    #region Switch Export Tests

    [Fact]
    public void ExportNetlist_WithVoltageSwitch_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("vswitch_test", "Voltage switch test");
        
        // Create switch model
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "voltage_switch",
            ModelName = "SW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "VT", 1.0 },
                { "VH", 0.5 },
                { "RON", 1.0 },
                { "ROFF", 1e6 }
            }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "S1",
            ComponentType = "voltage_switch",
            Nodes = new List<string> { "out+", "out-" },
            Parameters = new Dictionary<string, object>
            {
                { "controlNodes", new[] { "ctrl+", "ctrl-" } },
                { "model", "SW_MODEL" }
            }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: S1 out+ out- ctrl+ ctrl- SW_MODEL
        Assert.Contains(lines, line => line.Trim().StartsWith("S1 out+ out- ctrl+ ctrl- SW_MODEL"));
    }

    [Fact]
    public void ExportNetlist_WithCurrentSwitch_FormatsCorrectly()
    {
        var circuit = _circuitManager.CreateCircuit("cswitch_test", "Current switch test");
        
        // Create switch model
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "current_switch",
            ModelName = "CSW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "IT", 0.001 },
                { "IH", 0.0005 },
                { "RON", 1.0 },
                { "ROFF", 1e6 }
            }
        });

        // Create control voltage source
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V_CTRL",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "ctrl+", "ctrl-" },
            Value = 0.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "W1",
            ComponentType = "current_switch",
            Nodes = new List<string> { "out+", "out-" },
            Parameters = new Dictionary<string, object>
            {
                { "controlSource", "V_CTRL" },
                { "model", "CSW_MODEL" }
            }
        });

        var netlist = _netlistService.ExportNetlist(circuit, includeComments: false);
        var lines = netlist.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Format: W1 out+ out- V_CTRL CSW_MODEL
        Assert.Contains(lines, line => line.Trim().StartsWith("W1 out+ out- V_CTRL CSW_MODEL"));
    }

    /// <summary>
    /// Test: Behavioral voltage source exports with correct SPICE syntax.
    /// Expected: Test FAILS until netlist export is implemented.
    /// SPICE syntax: Bname n+ n- V={expression}
    /// </summary>
    [Fact]
    public void NetlistService_BehavioralVoltageSource_CorrectSyntax()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_bvs", "Test behavioral voltage source");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "B1",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(input) * 2.5" }
            }
        });

        // Act
        var netlist = _netlistService.ExportNetlist(circuit);

        // Assert
        Assert.Contains("B1 output 0 V={V(input) * 2.5}", netlist);
    }

    /// <summary>
    /// Test: Behavioral current source exports with correct SPICE syntax.
    /// Expected: Test FAILS until netlist export is implemented.
    /// SPICE syntax: Bname n+ n- I={expression}
    /// </summary>
    [Fact]
    public void NetlistService_BehavioralCurrentSource_CorrectSyntax()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_bcs", "Test behavioral current source");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "B2",
            ComponentType = "behavioral_current_source",
            Nodes = new List<string> { "load", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(ref) / 1000" }
            }
        });

        // Act
        var netlist = _netlistService.ExportNetlist(circuit);

        // Assert
        Assert.Contains("B2 load 0 I={V(ref) / 1000}", netlist);
    }

    /// <summary>
    /// Test: Complex behavioral expression exports correctly.
    /// Expected: Test FAILS until netlist export is implemented.
    /// </summary>
    [Fact]
    public void NetlistService_BehavioralSource_ComplexExpression_CorrectSyntax()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_complex", "Test complex expression");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "B3",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "out", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(a) - V(b) * 2.0 + 1.5" }
            }
        });

        // Act
        var netlist = _netlistService.ExportNetlist(circuit);

        // Assert
        Assert.Contains("B3 out 0 V={V(a) - V(b) * 2.0 + 1.5}", netlist);
    }

    #endregion
}

