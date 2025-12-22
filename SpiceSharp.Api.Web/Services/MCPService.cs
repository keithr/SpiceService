using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Plot;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetlistSvg;
using NetlistSvg.Skins;
using System.Reflection;
using SpiceSharp.Entities;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Service for executing MCP tools
/// </summary>
public class MCPService
{
    private readonly ICircuitManager _circuitManager;
    private readonly IComponentService _componentService;
    private readonly IModelService _modelService;
    private readonly IOperatingPointService _operatingPointService;
    private readonly IDCAnalysisService _dcAnalysisService;
    private readonly ITransientAnalysisService _transientAnalysisService;
    private readonly IACAnalysisService _acAnalysisService;
    private readonly INetlistService _netlistService;
    private readonly IParameterSweepService _parameterSweepService;
    private readonly INoiseAnalysisService _noiseAnalysisService;
    private readonly ITemperatureSweepService _temperatureSweepService;
    private readonly IImpedanceAnalysisService _impedanceAnalysisService;
    private readonly IResponseMeasurementService _responseMeasurementService;
    private readonly IGroupDelayService _groupDelayService;
    private readonly INetlistParser _netlistParser;
    private readonly CircuitResultsCache _resultsCache;
    private readonly MCPServerConfig _config;
    private readonly ILibraryService? _libraryService;
    private readonly ISpeakerDatabaseService? _speakerDatabaseService;
    private readonly IEnclosureDesignService? _enclosureDesignService;
    private readonly ICrossoverCompatibilityService? _crossoverCompatibilityService;
    private readonly ILogger<MCPService>? _logger;

    public MCPService(
        ICircuitManager circuitManager,
        IComponentService componentService,
        IModelService modelService,
        IOperatingPointService operatingPointService,
        IDCAnalysisService dcAnalysisService,
        ITransientAnalysisService transientAnalysisService,
        IACAnalysisService acAnalysisService,
        INetlistService netlistService,
        IParameterSweepService parameterSweepService,
        INoiseAnalysisService noiseAnalysisService,
        ITemperatureSweepService temperatureSweepService,
        IImpedanceAnalysisService impedanceAnalysisService,
        IResponseMeasurementService responseMeasurementService,
        IGroupDelayService groupDelayService,
        INetlistParser netlistParser,
        CircuitResultsCache resultsCache,
        MCPServerConfig config,
        ILibraryService? libraryService = null,
        ISpeakerDatabaseService? speakerDatabaseService = null,
        IEnclosureDesignService? enclosureDesignService = null,
        ICrossoverCompatibilityService? crossoverCompatibilityService = null,
        ILogger<MCPService>? logger = null)
    {
        _circuitManager = circuitManager;
        _componentService = componentService;
        _modelService = modelService;
        _operatingPointService = operatingPointService;
        _dcAnalysisService = dcAnalysisService;
        _transientAnalysisService = transientAnalysisService;
        _acAnalysisService = acAnalysisService;
        _netlistService = netlistService;
        _parameterSweepService = parameterSweepService;
        _noiseAnalysisService = noiseAnalysisService;
        _temperatureSweepService = temperatureSweepService;
        _impedanceAnalysisService = impedanceAnalysisService;
        _responseMeasurementService = responseMeasurementService;
        _groupDelayService = groupDelayService;
        _netlistParser = netlistParser;
        _resultsCache = resultsCache;
        _config = config;
        _libraryService = libraryService;
        _speakerDatabaseService = speakerDatabaseService;
        _enclosureDesignService = enclosureDesignService;
        _crossoverCompatibilityService = crossoverCompatibilityService;
        _logger = logger;

        // Index libraries on startup if paths are configured
        if (_libraryService != null && _config.LibraryPaths != null)
        {
            _libraryService.IndexLibraries(_config.LibraryPaths);
        }
    }

    /// <summary>
    /// Get list of available tools
    /// </summary>
    public List<MCPToolDefinition> GetTools()
    {
        var tools = new List<MCPToolDefinition>
        {
            new MCPToolDefinition
            {
                Name = "get_service_status",
                Description = "Get current service status and capabilities",
                InputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new MCPToolDefinition
            {
                Name = "create_circuit",
                Description = "Create a new circuit or switch to existing circuit",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Unique circuit identifier" },
                        description = new { type = "string", description = "Optional description" },
                        make_active = new { type = "boolean", description = "Set as active circuit", @default = true }
                    },
                    required = new[] { "circuit_id" }
                }
            },
            new MCPToolDefinition
            {
                Name = "add_component",
                Description = "Add a component to the active circuit. WORKFLOW: For standard components (LED, diode, transistor, MOSFET, BJT, JFET, etc.), use library_search FIRST to find available models and their parameters before adding components. For subcircuits: Use component_type='subcircuit' with the 'model' parameter set to the subcircuit name (e.g., from library_search results). Example: {\"name\": \"Xtweeter\", \"component_type\": \"subcircuit\", \"nodes\": [\"tw_out\", \"0\"], \"model\": \"275_030\"}. The subcircuit definition will be automatically loaded from the library if available. IMPORTANT: For AC analysis, voltage sources MUST include AC specification via parameters. Use parameters: {\"ac\": 1} or {\"acmag\": 1} to specify AC magnitude (typically 1V for AC analysis). Example: {\"name\": \"V1\", \"component_type\": \"voltage_source\", \"nodes\": [\"input\", \"0\"], \"value\": 1, \"parameters\": {\"ac\": 1}}. POWER SUPPLY NAMING: For power supplies, use standard naming conventions to make schematics clearer: VCC (positive supply, e.g., 5V, 12V), VDD (positive MOSFET supply), VSS (negative/ground), VEE (negative supply). The component name appears on the schematic, so use descriptive names like \"VCC_5V\" or \"VDD_12V\" to show both the label and voltage value. Example: {\"name\": \"VCC_5V\", \"component_type\": \"voltage_source\", \"nodes\": [\"VCC\", \"0\"], \"value\": 5} creates a 5V power supply labeled \"VCC_5V\" on the schematic. For behavioral sources (behavioral_voltage_source, behavioral_current_source): CRITICAL LIMITATION - expressions CANNOT use parameter substitution or variable names. You MUST use literal numeric values only. WRONG: 'V(input)*{gain}' or 'V(vel)*BL/Sd'. CORRECT: 'V(input)*5.1' or 'V(vel)*6.5/0.0046'. WORKAROUND: Pre-calculate parameter relationships (e.g., BL/Sd = 6.5/0.0046 = 1413.04) and either use the literal formula 'V(vel)*6.5/0.0046' (more readable/maintainable) or pre-calculated constant 'V(vel)*1413.04' (slightly faster). Document original parameters in circuit description.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID (optional, uses active if omitted)" },
                        name = new { type = "string", description = "Component name (e.g., R1, C1, V1). For power supplies, use standard conventions: VCC_5V, VDD_12V, VSS, VEE for clarity. The name appears on the schematic, so include voltage value in the name (e.g., \"VCC_5V\") to make it clear what the power supply voltage is." },
                        component_type = new { type = "string", @enum = new[] { "resistor", "capacitor", "inductor", "diode", "voltage_source", "current_source", "bjt_npn", "bjt_pnp", "mosfet_n", "mosfet_p", "jfet_n", "jfet_p", "vcvs", "vccs", "ccvs", "cccs", "behavioral_voltage_source", "behavioral_current_source", "mutual_inductance", "voltage_switch", "current_switch", "subcircuit" }, description = "Component type (must be lowercase with underscores). For subcircuit, use the 'model' parameter to specify the subcircuit name (e.g., from library_search)." },
                        nodes = new { type = "array", items = new { type = "string" }, description = "Connection nodes" },
                        value = new { type = "number", description = "Component value (DC value for sources)" },
                        model = new { type = "string", description = "Model name (for semiconductors) or subcircuit name (for subcircuit component_type)" },
                        parameters = new { type = "object", description = "Additional parameters. For voltage/current sources: Use \"ac\" or \"acmag\" (number) to specify AC magnitude for AC analysis (e.g., {\"ac\": 1}). Use \"acphase\" (number, degrees) for AC phase. For transient analysis, use waveform parameters. For PULSE waveform, both formats are supported: use \"pulse_v1\", \"pulse_v2\", \"pulse_td\", \"pulse_tr\", \"pulse_tf\", \"pulse_pw\", \"pulse_per\" OR the shorter format \"v1\", \"v2\", \"td\", \"tr\", \"tf\", \"pw\", \"per\". Example: {\"waveform\": \"pulse\", \"pulse_v1\": 0.0, \"pulse_v2\": 5.0, \"pulse_td\": 0.0, \"pulse_tr\": 1e-6, \"pulse_tf\": 1e-6, \"pulse_pw\": 1e-3, \"pulse_per\": 2e-3} OR {\"waveform\": \"pulse\", \"v1\": 0.0, \"v2\": 5.0, \"td\": 0.0, \"tr\": 1e-6, \"tf\": 1e-6, \"pw\": 1e-3, \"per\": 2e-3}. For behavioral sources: Use \"expression\" (string) with LITERAL NUMERIC VALUES ONLY - parameter substitution NOT supported. Expression must contain actual numbers, not variable names. Valid: \"V(input)*5.1\" or \"V(velocity)*6.5/0.0046\". Invalid: \"V(input)*{gain}\" or \"V(vel)*BL/Sd\". Pre-calculate any parameter relationships before creating the component." }
                    },
                    required = new[] { "name", "component_type", "nodes" }
                }
            },
            new MCPToolDefinition
            {
                Name = "define_model",
                Description = "Define a semiconductor model",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID where the model will be defined" },
                        model_name = new { type = "string", description = "Unique model name (e.g., 'RED_LED', '2N2222')" },
                        model_type = new { type = "string", @enum = new[] { "diode", "bjt_npn", "bjt_pnp", "mosfet_n", "mosfet_p", "jfet_n", "jfet_p" }, description = "Type of semiconductor model" },
                        parameters = new { type = "object", description = "Model parameters (e.g., {'IS': 1e-15, 'N': 3.5} for diode)" }
                    },
                    required = new[] { "circuit_id", "model_name", "model_type", "parameters" }
                }
            },
            new MCPToolDefinition
            {
                Name = "run_dc_analysis",
                Description = "Run DC sweep analysis. WORKFLOW: (1) After defining circuit, use validate_circuit before analysis to check for topology issues. (2) If analysis fails with 'rule violations' or other errors, use validate_circuit to diagnose circuit topology issues (missing ground, floating nodes, etc.) before retrying. (3) After running analysis, use plot_results to visualize DC sweep results. Use matplotlib only for custom visualizations not supported by plot_results.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to analyze" },
                        source = new { type = "string", description = "Source name to sweep (e.g., 'V1')" },
                        start = new { type = "number", description = "Start value for sweep" },
                        stop = new { type = "number", description = "Stop value for sweep" },
                        step = new { type = "number", description = "Step size for sweep" },
                        exports = new { type = "array", items = new { type = "string" }, description = "Signals to export (e.g., ['v(out)', 'i(R1)'])" }
                    },
                    required = new[] { "circuit_id", "source", "start", "stop", "step" }
                }
            },
            new MCPToolDefinition
            {
                Name = "run_transient_analysis",
                Description = "Run transient (time-domain) analysis. WORKFLOW: (1) After defining circuit, use validate_circuit before analysis to check for topology issues. (2) If analysis fails with 'rule violations' or other errors, use validate_circuit to diagnose circuit topology issues (missing ground, floating nodes, etc.) before retrying. (3) After running analysis, use plot_results to visualize transient results. Use matplotlib only for custom visualizations not supported by plot_results. Use 'use_initial_conditions' (UIC) to ensure capacitors/inductors start at specified initial conditions (IC=0 if not specified) - useful for rise_time measurements.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to analyze" },
                        start_time = new { type = "number", @default = 0.0, description = "Start time in seconds" },
                        stop_time = new { type = "number", description = "Stop time in seconds" },
                        time_step = new { type = "number", description = "Time step in seconds" },
                        signals = new { type = "array", items = new { type = "string" }, description = "Signals to export (e.g., ['v(out)', 'i(R1)'])" },
                        use_initial_conditions = new { type = "boolean", @default = false, description = "If true, use initial conditions (UIC mode). Sets IC=0 on capacitors/inductors that don't have IC specified, ensuring they start discharged/with no current. Required for accurate rise_time measurements." }
                    },
                    required = new[] { "circuit_id", "stop_time", "time_step", "signals" }
                }
            },
            new MCPToolDefinition
            {
                Name = "run_ac_analysis",
                Description = "Run AC (frequency-domain) analysis. WORKFLOW: (1) After defining circuit, use validate_circuit before analysis to check for topology issues. (2) If analysis fails with 'rule violations' or other errors, use validate_circuit to diagnose circuit topology issues (missing ground, floating nodes, etc.) before retrying. (3) After running analysis, use plot_results to visualize AC results (Bode plots). Use matplotlib only for custom visualizations not supported by plot_results. AC analysis requires a voltage source with AC specification (use parameters: {\"ac\": 1} or {\"acmag\": 1} in add_component).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to analyze" },
                        start_frequency = new { type = "number", description = "Start frequency in Hz" },
                        stop_frequency = new { type = "number", description = "Stop frequency in Hz" },
                        number_of_points = new { type = "integer", description = "Number of frequency points" },
                        signals = new { type = "array", items = new { type = "string" }, description = "Signals to export (e.g., ['v(out)'])" }
                    },
                    required = new[] { "circuit_id", "start_frequency", "stop_frequency", "number_of_points", "signals" }
                }
            },
            new MCPToolDefinition
            {
                Name = "run_operating_point",
                Description = "Calculate DC operating point. WORKFLOW: (1) After defining circuit, use validate_circuit before analysis to check for topology issues. (2) If analysis fails with 'rule violations' or other errors, use validate_circuit to diagnose circuit topology issues (missing ground, floating nodes, etc.) before retrying. (3) After running analysis, use plot_results to visualize operating point results (bar charts for comparisons). Use matplotlib only for custom visualizations not supported by plot_results.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to analyze" },
                        include_power = new { type = "boolean", @default = false, description = "Include power calculations for components" }
                    },
                    required = new[] { "circuit_id" }
                }
            },
            new MCPToolDefinition
            {
                Name = "export_netlist",
                Description = "Export circuit as SPICE netlist",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to export" }
                    },
                    required = new[] { "circuit_id" }
                }
            },
            new MCPToolDefinition
            {
                Name = "list_circuits",
                Description = "List all available circuits",
                InputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new MCPToolDefinition
            {
                Name = "delete_circuit",
                Description = "Delete a circuit by ID. If the deleted circuit was active, another circuit will be activated automatically if available.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to delete" }
                    },
                    required = new[] { "circuit_id" }
                }
            },
            new MCPToolDefinition
            {
                Name = "get_component_info",
                Description = "Get detailed information about a component in a circuit, including its type, parameters, nodes, and associated model (if any).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID (optional, uses active circuit if omitted)" },
                        component = new { type = "string", description = "Component name (e.g., R1, C1, D1)" }
                    },
                    required = new[] { "component" }
                }
            },
            new MCPToolDefinition
            {
                Name = "modify_component",
                Description = "Modify a component's parameters in a circuit. Use 'value' to update the main value (resistance, capacitance, inductance, or DC voltage/current). Use other parameter names (e.g., 'ac', 'acphase') to update specific component parameters. The component will be updated in place when possible, or recreated if necessary.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID (optional, uses active circuit if omitted)" },
                        component = new { type = "string", description = "Component name (e.g., R1, C1, V1)" },
                        parameters = new { type = "object", description = "Dictionary of parameter names and values to update. Use 'value' for main component value, or specific parameter names like 'ac', 'acphase', etc." }
                    },
                    required = new[] { "component", "parameters" }
                }
            },
            new MCPToolDefinition
            {
                Name = "library_search",
                Description = "REQUIRED: Search SPICE component libraries for models and subcircuits. Returns matching model definitions with their parameters, and subcircuit definitions with their external nodes. USE THIS TOOL FIRST when creating circuits with common component types (LED, diode, transistor, MOSFET, BJT, JFET, etc.) or when looking for subcircuit definitions. Use this to discover available components and their parameters before adding them to circuits with add_component. This tool helps you find the correct model names and understand what parameters are available for each component type, as well as find subcircuit definitions for use with component_type='subcircuit'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Search query (searches model/subcircuit names, and for subcircuits: PRODUCT_NAME, PART_NUMBER, MANUFACTURER metadata fields, case-insensitive). Empty string returns all models/subcircuits.", @default = "" },
                        type = new { type = "string", description = "Optional type filter. For models: 'diode', 'bjt_npn', 'bjt_pnp', 'mosfet_n', 'mosfet_p', 'jfet_n', 'jfet_p'. For subcircuits: 'woofers', 'tweeters', 'midrange', etc. (filters by metadata TYPE field).", @enum = new[] { "diode", "bjt_npn", "bjt_pnp", "mosfet_n", "mosfet_p", "jfet_n", "jfet_p", "woofers", "tweeters", "midrange" } },
                        limit = new { type = "integer", description = "Maximum number of results to return (maximum: 100)", @default = 20, minimum = 1, maximum = 100 },
                        include_parameters = new { type = "boolean", description = "Include full parameter details for each model. Set to false for summary-only results (faster, smaller response).", @default = true },
                        count_only = new { type = "boolean", description = "Return only the count of matching models, without the model list. Useful for checking if models exist before fetching.", @default = false }
                    },
                    required = Array.Empty<string>()
                }
            },
            new MCPToolDefinition
            {
                Name = "validate_circuit",
                Description = "CRITICAL: Validate circuit topology and check for common issues before simulation. REQUIRED WORKFLOW STEP: Use this tool (1) After creating or modifying a circuit with add_component, (2) Before running any analysis (run_dc_analysis, run_transient_analysis, run_ac_analysis, run_operating_point), (3) When analysis fails with 'rule violations' or vague errors, (4) When troubleshooting circuit problems. This tool checks for missing ground nodes, floating nodes, and other topology issues that cause SPICE simulation failures. Returns detailed errors and warnings that help diagnose why analyses fail. Always run this before retrying a failed analysis.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to validate" }
                    },
                    required = new[] { "circuit_id" }
                }
            },
            new MCPToolDefinition
            {
                Name = "search_speakers_by_parameters",
                Description = "Search for speakers by Thiele-Small parameters and specifications. Returns matching speakers with their T/S parameters, metadata, and pricing information. Use this tool to find speakers that match specific design requirements (e.g., FS range, QTS range, diameter, impedance, price).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        driver_type = new { type = "array", items = new { type = "string" }, description = "Driver type filter (e.g., ['woofers', 'tweeters', 'midrange'])" },
                        diameter_min = new { type = "number", description = "Minimum diameter in inches" },
                        diameter_max = new { type = "number", description = "Maximum diameter in inches" },
                        impedance = new { type = "integer", description = "Impedance in ohms" },
                        fs_min = new { type = "number", description = "Minimum FS (free air resonance) in Hz" },
                        fs_max = new { type = "number", description = "Maximum FS in Hz" },
                        qts_min = new { type = "number", description = "Minimum QTS (total Q factor)" },
                        qts_max = new { type = "number", description = "Maximum QTS" },
                        qes_min = new { type = "number", description = "Minimum QES (electrical Q factor)" },
                        qes_max = new { type = "number", description = "Maximum QES" },
                        qms_min = new { type = "number", description = "Minimum QMS (mechanical Q factor)" },
                        qms_max = new { type = "number", description = "Maximum QMS" },
                        vas_min = new { type = "number", description = "Minimum VAS (equivalent air compliance) in liters" },
                        vas_max = new { type = "number", description = "Maximum VAS in liters" },
                        sensitivity_min = new { type = "number", description = "Minimum sensitivity in dB" },
                        sensitivity_max = new { type = "number", description = "Maximum sensitivity in dB" },
                        power_min = new { type = "integer", description = "Minimum power handling (RMS) in watts" },
                        power_max = new { type = "integer", description = "Maximum power handling (RMS) in watts" },
                        xmax_min = new { type = "number", description = "Minimum XMAX (maximum linear excursion) in mm" },
                        xmax_max = new { type = "number", description = "Maximum XMAX in mm" },
                        manufacturer = new { type = "string", description = "Manufacturer name filter (partial match)" },
                        price_max = new { type = "number", description = "Maximum price in USD" },
                        sort_by = new { type = "string", @enum = new[] { "sensitivity", "price", "fs", "qts", "vas" }, description = "Sort field" },
                        sort_direction = new { type = "string", @enum = new[] { "asc", "desc" }, description = "Sort direction", @default = "asc" },
                        limit = new { type = "integer", description = "Maximum number of results to return", @default = 50, minimum = 1, maximum = 1000 }
                    },
                    required = Array.Empty<string>()
                }
            },
            new MCPToolDefinition
            {
                Name = "calculate_enclosure_design",
                Description = "Calculate speaker enclosure design parameters (sealed or vented box). Looks up speaker by model name from the database and calculates optimal box volume, tuning frequency, port dimensions, and predicted frequency response. Supports multiple alignment types for vented boxes (QB3, B4, SBB4, C4) and target Qtc values for sealed boxes (Butterworth 0.707, Bessel 0.577, etc.).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        model = new { type = "string", description = "Speaker model/subcircuit name (e.g., '264_1148')" },
                        enclosure_type = new { type = "string", @enum = new[] { "sealed", "vented" }, description = "Enclosure type: 'sealed' or 'vented'" },
                        alignment = new { type = "string", @enum = new[] { "QB3", "B4", "SBB4", "C4" }, description = "Vented box alignment type (required for vented boxes): QB3 (extended bass), B4 (balanced), SBB4 (small box), C4 (compact)" },
                        target_qtc = new { type = "number", description = "Target Qtc for sealed boxes (e.g., 0.707 for Butterworth, 0.577 for Bessel, 1.0 for Critically Damped). Required for sealed boxes." }
                    },
                    required = new[] { "model", "enclosure_type" }
                }
            },
            new MCPToolDefinition
            {
                Name = "check_crossover_compatibility",
                Description = "Check compatibility between a woofer and tweeter for a given crossover configuration. Validates woofer beaming limits, tweeter Fs requirements, sensitivity matching, and impedance compatibility. Returns a compatibility score (0-100) with detailed recommendations and warnings.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        woofer_model = new { type = "string", description = "Woofer model/subcircuit name (e.g., '264_1148')" },
                        tweeter_model = new { type = "string", description = "Tweeter model/subcircuit name" },
                        crossover_frequency = new { type = "number", description = "Crossover frequency in Hz" },
                        crossover_order = new { type = "integer", description = "Crossover order (1, 2, 3, or 4)", minimum = 1, maximum = 4 }
                    },
                    required = new[] { "woofer_model", "tweeter_model", "crossover_frequency", "crossover_order" }
                }
            },
            new MCPToolDefinition
            {
                Name = "render_schematic",
                Description = "Render a circuit as an SVG schematic diagram. Usage: Provide 'circuit_id' to render a specific circuit, or omit it to render the active circuit. Use 'output_format' to specify how to return the SVG: 'image' (base64-encoded for display), 'text' (raw SVG string), or 'file' (save to disk). You can specify multiple formats as an array (e.g., ['image', 'text']). If 'file' is specified, provide 'file_path' to specify where to save the SVG file. Options: 'skin_type' ('Analog' for resistors/capacitors/transistors, 'Digital' for logic gates), 'show_values' (display component values), 'external_ports' (see details below). For large circuits, optionally increase 'max_memory' (bytes, default 4GB) or 'timeout_seconds' (default 600 seconds) if you encounter memory or timeout errors. VISUAL APPEARANCE - external_ports: By default (or with []), the schematic renders as a clean closed circuit WITHOUT antenna symbols - this is what most users expect. If you specify node names in external_ports, those nodes will display antenna symbols indicating they are external connection points. Only use external_ports when designing modular/subcircuit blocks that will connect to other circuits. For standalone circuits, omit external_ports or use [] to avoid confusing antenna symbols. POWER SUPPLY LABELING: Power supplies (voltage sources) are labeled with their component name on the schematic. Use descriptive names like \"VCC_5V\" or \"VDD_12V\" when creating power supplies to make the voltage value clear. With 'show_values' enabled (default), the voltage value also appears, but the component name is the primary identifier. Standard conventions: VCC (positive supply), VDD (MOSFET positive), VSS (negative/ground), VEE (negative supply).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to render (optional, uses active circuit if omitted)" },
                        skin_type = new { type = "string", @enum = new[] { "Analog", "Digital" }, @default = "Analog", description = "Schematic skin type. Use 'Analog' for resistors, capacitors, transistors. Use 'Digital' for logic gates." },
                        show_values = new { type = "boolean", @default = true, description = "Display component values on schematic" },
                        external_ports = new { type = "array", items = new { type = "string" }, @default = Array.Empty<string>(), description = "VISUAL: Controls whether antenna symbols appear on the schematic. Default [] (or omit) = NO antenna symbols (clean closed circuit - recommended for most users). If you specify node names like ['input', 'output'], those nodes will show antenna symbols. Only use this when designing subcircuit modules that connect to other circuits. For standalone circuits, always use [] or omit this parameter to avoid confusing antenna symbols in the diagram." },
                        output_format = new { type = "array", items = new { type = "string", @enum = new[] { "image", "text", "file" } }, @default = new[] { "image" }, description = "Output format(s): 'image' (base64-encoded for display), 'text' (raw SVG string), 'file' (save to disk). Can specify multiple formats. Default: ['image']." },
                        file_path = new { type = "string", description = "File path where SVG should be saved (required if 'file' is in output_format, optional otherwise). If not provided and 'file' is requested, a default path will be generated." },
                        max_memory = new { type = "integer", description = "Maximum memory in bytes (optional, default 4GB = 4,000,000,000). Increase for very large circuits if you encounter MemoryLimitExceededException." },
                        timeout_seconds = new { type = "integer", description = "Timeout in seconds (optional, default 600 = 10 minutes). Increase for complex circuits if rendering times out." }
                    },
                    required = Array.Empty<string>()
                }
            },
            new MCPToolDefinition
            {
                Name = "plot_results",
                Description = "DEFAULT plotting tool for all circuit analysis results. Plot results from circuit analysis as an SVG or PNG image. Supports line plots for DC sweep/transient analysis, Bode plots (magnitude and phase) for AC analysis, and bar charts for operating point comparisons. MULTIPLE CURVES: You can plot multiple signals on the same graph by specifying an array of signals (e.g., ['v(out)', 'i(R1)', 'v(in)']). All specified signals appear as separate curves on a single plot with different colors and a legend. If no signals are specified, all exported signals are plotted together. Use this tool after running any analysis (run_dc_analysis, run_transient_analysis, run_ac_analysis, run_operating_point) to visualize the results. Use matplotlib only for custom visualizations not supported by this tool. Features: DC sweep visualization, transient time-domain plots, AC frequency response (Bode plots), operating point comparisons, multiple curves on same graph, SVG output for embedding in HTML/text artifacts, and invert_signals support for SPICE current convention issues. I-V CHARACTERISTIC CURVES: For I-V curves from DC analysis (e.g., diode, transistor characteristics), use plot_type='scatter' with x_signal='v(device)' and signals=['i(device)']. Example: plot_type='scatter', x_signal='v(anode)', signals=['i(D1)'] creates a current vs voltage characteristic curve. FORMAT RECOMMENDATIONS: For embedding in HTML/text artifacts (most common use case, especially when generating HTML documents), use image_format='svg' with output_format=['text'] to get raw SVG string that can be directly embedded in HTML. For direct image display in MCP clients, try image_format='png' with output_format=['image'], but if that fails, fall back to SVG+text format. SVG+text is the most reliable option for programmatic use and HTML document generation.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit ID to plot results from (optional, uses active circuit if omitted)" },
                        signals = new { type = "array", items = new { type = "string" }, description = "Signals to plot on the same graph (e.g., ['v(out)', 'i(R1)', 'v(in)']). Multiple signals create multiple curves on a single plot with different colors. Optional, defaults to all exported signals (all plotted together)." },
                        invert_signals = new { type = "array", items = new { type = "string" }, description = "Signals to invert (multiply by -1). Useful for SPICE current convention where voltage source currents are negative. Example: ['i(V1)'] to invert current through V1." },
                        plot_type = new { type = "string", @enum = new[] { "auto", "line", "bode", "bar", "scatter" }, @default = "auto", description = "Plot type: 'auto' (selects based on analysis type), 'line', 'bode', 'bar', 'scatter'" },
                        x_signal = new { type = "string", description = "Signal for X-axis. For scatter plots or custom X-Y relationships. Optional, uses default based on analysis type." },
                        output_format = new { type = "array", items = new { type = "string", @enum = new[] { "image", "text", "file" } }, @default = new[] { "image" }, description = "Output format(s): 'text' (raw SVG string - RECOMMENDED for embedding in HTML/text artifacts, only works with SVG format), 'image' (base64-encoded image for display), 'file' (save to disk, not recommended due to filesystem isolation). BEST PRACTICE: Use image_format='svg' with output_format=['text'] for reliable embedding in artifacts. PNG+image may fail in some MCP clients with 'unsupported format' errors." },
                        image_format = new { type = "string", @enum = new[] { "svg", "png" }, @default = "png", description = "Image format: 'svg' (RECOMMENDED - use with output_format=['text'] for embedding in HTML/text artifacts and HTML document generation, most reliable), 'png' (raster image, may fail in some MCP clients). For programmatic use and HTML documents, prefer SVG+text format." },
                        file_path = new { type = "string", description = "File path when 'file' is in output_format. Optional, auto-generated if not provided." },
                        options = new
                        {
                            type = "object",
                            description = "Plot customization options",
                            properties = new
                            {
                                title = new { type = "string", description = "Plot title (auto-generated if not provided)" },
                                x_label = new { type = "string", description = "X-axis label (auto-generated if not provided)" },
                                y_label = new { type = "string", description = "Y-axis label (auto-generated if not provided). NOTE: Labels are cosmetic only - they do not convert units. If you specify 'mA' but values are in Amps, the plot will still show Ampere values. Use invert_signals or manual data conversion if unit conversion is needed." },
                                x_scale = new { type = "string", @enum = new[] { "linear", "log" }, @default = "linear", description = "X-axis scale: 'linear' or 'log'" },
                                y_scale = new { type = "string", @enum = new[] { "linear", "log" }, @default = "linear", description = "Y-axis scale: 'linear' or 'log'" },
                                grid = new { type = "boolean", @default = true, description = "Show grid lines" },
                                legend = new { type = "boolean", @default = true, description = "Show legend (when multiple signals)" },
                                colors = new { type = "array", items = new { type = "string" }, description = "Colors for each signal (hex or named colors)" },
                                width = new { type = "integer", @default = 800, description = "Image width in pixels" },
                                height = new { type = "integer", @default = 600, description = "Image height in pixels" }
                            },
                            required = Array.Empty<string>()
                        }
                    },
                    required = Array.Empty<string>()
                }
            },
            new MCPToolDefinition
            {
                Name = "run_parameter_sweep",
                Description = "Sweep a component parameter across a range and observe circuit behavior. Use for sensitivity analysis, finding optimal values, or exploring design space.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit to analyze (uses active circuit if omitted)" },
                        component = new { type = "string", description = "Component name to sweep (e.g., 'C1', 'R2', 'L1')" },
                        parameter = new { type = "string", description = "Parameter to vary (e.g., 'resistance', 'capacitance', 'inductance', or 'value' for basic components)", @default = "value" },
                        start = new { type = "number", description = "Starting value" },
                        stop = new { type = "number", description = "Ending value" },
                        points = new { type = "integer", description = "Number of sweep points", @default = 20 },
                        scale = new { type = "string", @enum = new[] { "linear", "log", "decade" }, @default = "linear", description = "Sweep scale: 'linear' (evenly spaced), 'log' (logarithmic), 'decade' (points per decade for log scale)" },
                        analysis_type = new { type = "string", @enum = new[] { "ac", "dc", "transient", "operating_point" }, description = "Analysis to run at each sweep point" },
                        analysis_params = new { type = "object", description = "Parameters for the analysis (frequency range for AC, time range for transient, etc.)" },
                        outputs = new { type = "array", items = new { type = "string" }, description = "Node voltages or currents to capture (e.g., ['v(out)', 'i(R1)'])" }
                    },
                    required = new[] { "component", "start", "stop", "analysis_type", "outputs" }
                }
            },
            // NOTE: run_noise_analysis is implemented but not exposed because SpiceSharp doesn't support noise analysis yet.
            // When SpiceSharp adds noise analysis support, uncomment this tool definition and the case statement in ExecuteTool.
            // new MCPToolDefinition
            // {
            //     Name = "run_noise_analysis",
            //     Description = "Calculate noise contribution of each component and total output noise. Critical for low-noise audio design.",
            //     InputSchema = new
            //     {
            //         type = "object",
            //         properties = new
            //         {
            //             circuit_id = new { type = "string", description = "Circuit to analyze (uses active circuit if omitted)" },
            //             output_node = new { type = "string", description = "Node to measure noise at" },
            //             reference_node = new { type = "string", description = "Reference node (usually '0' for ground)", @default = "0" },
            //             input_source = new { type = "string", description = "Input source for input-referred noise calculation" },
            //             start_freq = new { type = "number", description = "Start frequency in Hz", @default = 20.0 },
            //             stop_freq = new { type = "number", description = "Stop frequency in Hz", @default = 20000.0 },
            //             points_per_decade = new { type = "integer", description = "Number of points per decade", @default = 10 }
            //         },
            //         required = new[] { "output_node" }
            //     }
            // },
            new MCPToolDefinition
            {
                Name = "run_temperature_sweep",
                Description = "Analyze circuit behavior across a temperature range. Essential for validating designs will work in real-world conditions.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit to analyze (uses active circuit if omitted)" },
                        start_temp = new { type = "number", description = "Starting temperature in Celsius", @default = -40.0 },
                        stop_temp = new { type = "number", description = "Ending temperature in Celsius", @default = 85.0 },
                        points = new { type = "integer", description = "Number of temperature points", @default = 10 },
                        analysis_type = new { type = "string", @enum = new[] { "ac", "dc", "transient", "operating_point" }, description = "Analysis to run at each temperature point" },
                        analysis_params = new { type = "object", description = "Parameters for the analysis (frequency range for AC, time range for transient, etc.)" },
                        outputs = new { type = "array", items = new { type = "string" }, description = "Node voltages or currents to capture (e.g., ['v(out)', 'i(R1)'])" }
                    },
                    required = new[] { "analysis_type", "outputs" }
                }
            },
            new MCPToolDefinition
            {
                Name = "plot_impedance",
                Description = "Plot impedance magnitude and phase versus frequency. Essential for speaker and filter design. IMPORTANT: The circuit must NOT have a voltage source at the measurement port (port_positive to port_negative). The tool injects its own test signal to measure Z = V/I. If a voltage source exists at the port, remove it before calling this tool, or measure impedance at a different port. For HTML document generation, use format='svg' with output_format=['text'] to get raw SVG output that can be embedded directly.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit to analyze (uses active circuit if omitted)" },
                        port_positive = new { type = "string", description = "Positive terminal node of the port" },
                        port_negative = new { type = "string", description = "Negative terminal node (usually '0' for ground)", @default = "0" },
                        start_freq = new { type = "number", description = "Start frequency in Hz", @default = 20.0 },
                        stop_freq = new { type = "number", description = "Stop frequency in Hz", @default = 20000.0 },
                        points_per_decade = new { type = "integer", description = "Number of points per decade", @default = 20 },
                        format = new { type = "string", @enum = new[] { "svg", "png" }, @default = "png", description = "Image format: 'svg' (RECOMMENDED - use with output_format=['text'] for embedding in HTML/text artifacts, most reliable), 'png' (raster image, may fail in some MCP clients). For programmatic use and HTML documents, prefer SVG+text format." },
                        output_format = new { type = "array", items = new { type = "string", @enum = new[] { "image", "text", "file" } }, @default = new[] { "image" }, description = "Output format(s): 'text' (raw SVG string - RECOMMENDED for embedding in HTML/text artifacts, only works with SVG format), 'image' (base64-encoded image for display), 'file' (save to disk, not recommended due to filesystem isolation). BEST PRACTICE: Use format='svg' with output_format=['text'] for reliable embedding in artifacts. PNG+image may fail in some MCP clients with 'unsupported format' errors." },
                        file_path = new { type = "string", description = "File path when 'file' is in output_format. Optional, auto-generated if not provided." }
                    },
                    required = new[] { "port_positive" }
                }
            },
            new MCPToolDefinition
            {
                Name = "measure_response",
                Description = "Extract specific measurements from simulation results. Like SPICE .MEAS directive. Requires cached analysis results from run_ac_analysis, run_transient_analysis, or run_dc_analysis.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit to measure (uses active circuit if omitted)" },
                        measurement = new { 
                            type = "string", 
                            @enum = new[] { 
                                "bandwidth_3db", "gain_at_freq", "freq_at_gain", "phase_at_freq",
                                "peak_value", "peak_frequency", "rise_time", "fall_time",
                                "overshoot", "settling_time", "dc_gain", "unity_gain_freq",
                                "phase_margin", "gain_margin"
                            },
                            description = "Type of measurement to perform"
                        },
                        signal = new { type = "string", description = "Signal to measure (e.g., 'v(out)')" },
                        reference = new { type = "string", description = "Reference signal for ratio measurements (optional)" },
                        frequency = new { type = "number", description = "Frequency for point measurements in Hz (required for gain_at_freq, phase_at_freq)" },
                        threshold = new { type = "number", description = "Threshold value for crossing measurements (required for freq_at_gain)" },
                        analysis_id = new { type = "string", description = "Which cached analysis to use (optional, uses most recent if omitted)" }
                    },
                    required = new[] { "measurement", "signal" }
                }
            },
            new MCPToolDefinition
            {
                Name = "calculate_group_delay",
                Description = "Calculate and plot group delay (negative derivative of phase vs frequency). Critical for evaluating transient response quality in filters and crossovers. Requires cached AC analysis results.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        circuit_id = new { type = "string", description = "Circuit to analyze (uses active circuit if omitted)" },
                        signal = new { type = "string", description = "Signal to analyze (e.g., 'v(out)')" },
                        reference = new { type = "string", description = "Reference signal for ratio measurements (optional, typically input)" },
                        format = new { type = "string", @enum = new[] { "svg", "png" }, @default = "png", description = "Image format. PNG is recommended for MCP client display compatibility." }
                    },
                    required = new[] { "signal" }
                }
            },
            new MCPToolDefinition
            {
                Name = "import_netlist",
                Description = "Import a SPICE netlist to create a circuit. Supports standard SPICE format with components (R, C, L, V, I, D, Q, M, J, X) and .MODEL statements. Subcircuit instantiation (X lines) is supported - subcircuit definitions will be automatically loaded from the library if available. Example: Xtweeter tw_out 0 275_030",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        netlist = new { type = "string", description = "SPICE netlist text" },
                        circuit_name = new { type = "string", description = "Name for the imported circuit" },
                        set_active = new { type = "boolean", @default = true, description = "Set the imported circuit as active" }
                    },
                    required = new[] { "netlist" }
                }
            },
            new MCPToolDefinition
            {
                Name = "reindex_libraries",
                Description = "Re-index all SPICE library files from configured library paths. This will re-parse all .lib files and update the speaker database with corrected metadata (e.g., impedance values). Use this after fixing metadata parsing issues or when library files are updated.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            }
        };
        
        // Validate all tools have valid schemas
        ValidateToolsSchema(tools);
        
        return tools;
    }
    
    /// <summary>
    /// Validate that all tools have valid JSON Schema definitions
    /// </summary>
    private void ValidateToolsSchema(List<MCPToolDefinition> tools)
    {
        var errors = new List<string>();
        
        foreach (var tool in tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                errors.Add($"Tool has empty or null name");
                continue;
            }
            
            if (string.IsNullOrWhiteSpace(tool.Description))
            {
                errors.Add($"Tool '{tool.Name}' has empty or null description");
            }
            
            if (tool.InputSchema == null)
            {
                errors.Add($"Tool '{tool.Name}' has null InputSchema");
                continue;
            }
            
            // Try to serialize the schema to validate it's valid JSON
            try
            {
                var schemaJson = JsonSerializer.Serialize(tool.InputSchema);
                var schemaDoc = JsonDocument.Parse(schemaJson);
                
                // Validate basic JSON Schema structure
                if (!schemaDoc.RootElement.TryGetProperty("type", out var typeElement))
                {
                    errors.Add($"Tool '{tool.Name}' schema missing 'type' property");
                }
                else if (typeElement.GetString() != "object")
                {
                    errors.Add($"Tool '{tool.Name}' schema type must be 'object', got '{typeElement.GetString()}'");
                }
                
                // Validate properties exist if type is object
                if (schemaDoc.RootElement.TryGetProperty("properties", out var propertiesElement))
                {
                    if (propertiesElement.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"Tool '{tool.Name}' schema 'properties' must be an object");
                    }
                }
                
                // Validate required array if present
                if (schemaDoc.RootElement.TryGetProperty("required", out var requiredElement))
                {
                    if (requiredElement.ValueKind != JsonValueKind.Array)
                    {
                        errors.Add($"Tool '{tool.Name}' schema 'required' must be an array");
                    }
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"Tool '{tool.Name}' has invalid JSON schema: {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"Tool '{tool.Name}' schema validation error: {ex.Message}");
            }
        }
        
        if (errors.Count > 0)
        {
            var errorMessage = $"Tools schema validation failed:\n{string.Join("\n", errors)}";
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Execute an MCP tool
    /// </summary>
    public async Task<MCPToolResult> ExecuteTool(string toolName, JsonElement arguments)
    {
        _logger?.LogDebug("ExecuteTool called: {ToolName}", toolName);
        
        try
        {
            var result = toolName switch
            {
                "get_service_status" => await GetServiceStatus(),
                "create_circuit" => await CreateCircuit(arguments),
                "list_circuits" => await ListCircuits(),
                "delete_circuit" => await DeleteCircuit(arguments),
                "get_component_info" => await GetComponentInfo(arguments),
                "modify_component" => await ModifyComponent(arguments),
                "library_search" => await LibrarySearch(arguments),
                "add_component" => await AddComponent(arguments),
                "define_model" => await DefineModel(arguments),
                "run_dc_analysis" => await RunDCAnalysis(arguments),
                "run_transient_analysis" => await RunTransientAnalysis(arguments),
                "run_ac_analysis" => await RunACAnalysis(arguments),
                "run_operating_point" => await RunOperatingPoint(arguments),
                "export_netlist" => await ExportNetlist(arguments),
                "validate_circuit" => await ValidateCircuit(arguments),
                "render_schematic" => await RenderSchematic(arguments),
                "plot_results" => await PlotResults(arguments),
                "run_parameter_sweep" => await RunParameterSweep(arguments),
                // "run_noise_analysis" => await RunNoiseAnalysis(arguments), // Not exposed - SpiceSharp doesn't support noise analysis yet
                "run_temperature_sweep" => await RunTemperatureSweep(arguments),
                "plot_impedance" => await PlotImpedance(arguments),
                "measure_response" => await MeasureResponse(arguments),
                "calculate_group_delay" => await CalculateGroupDelay(arguments),
                "import_netlist" => await ImportNetlist(arguments),
                "reindex_libraries" => await ReindexLibraries(arguments),
                "search_speakers_by_parameters" => await SearchSpeakersByParameters(arguments),
                "calculate_enclosure_design" => await CalculateEnclosureDesign(arguments),
                "check_crossover_compatibility" => await CheckCrossoverCompatibility(arguments),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };
            
            _logger?.LogDebug("Tool {ToolName} executed successfully", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool {ToolName} failed: {ErrorMessage}", toolName, ex.Message);
            throw;
        }
    }

    private async Task<MCPToolResult> GetServiceStatus()
    {
        var activeCircuit = _circuitManager.GetActiveCircuit();
        var status = new
        {
            status = "ready",
            version = _config.Version,
            capabilities = new[] { "dc_analysis", "ac_analysis", "transient_analysis", "operating_point", "parameter_sweep", "temperature_sweep" },
            active_circuit = activeCircuit?.Id
        };

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> CreateCircuit(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var description = arguments.TryGetProperty("description", out var descElement) ? descElement.GetString() : string.Empty;
        var makeActive = !arguments.TryGetProperty("make_active", out var activeElement) || activeElement.GetBoolean();

        var circuit = _circuitManager.CreateCircuit(circuitId, description ?? string.Empty);
        if (makeActive)
        {
            _circuitManager.SetActiveCircuit(circuitId);
        }

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new { circuit_id = circuit.Id, description = circuit.Description, is_active = circuit.IsActive }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> AddComponent(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("name", out var nameElement))
            throw new ArgumentException("name is required");
        if (!arguments.TryGetProperty("component_type", out var typeElement))
            throw new ArgumentException("component_type is required");
        if (!arguments.TryGetProperty("nodes", out var nodesElement))
            throw new ArgumentException("nodes is required");

        var name = nameElement.GetString() ?? throw new ArgumentException("name must be a string");
        var componentType = typeElement.GetString() ?? throw new ArgumentException("component_type must be a string");
        
        var nodes = new List<string>();
        foreach (var node in nodesElement.EnumerateArray())
        {
            nodes.Add(node.GetString() ?? throw new ArgumentException("nodes must be strings"));
        }

        var circuitId = arguments.TryGetProperty("circuit_id", out var circuitIdElement) 
            ? circuitIdElement.GetString() 
            : _circuitManager.GetActiveCircuit()?.Id;

        if (string.IsNullOrEmpty(circuitId))
            throw new ArgumentException("circuit_id is required (no active circuit)");

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var definition = new ComponentDefinition
        {
            Name = name,
            ComponentType = componentType,
            Nodes = nodes,
            Value = arguments.TryGetProperty("value", out var valueElement) ? valueElement.GetDouble() : null,
            Model = arguments.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : null
        };

        // Parse parameters if present
        if (arguments.TryGetProperty("parameters", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in paramsElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    definition.Parameters[prop.Name] = prop.Value.GetDouble();
                else if (prop.Value.ValueKind == JsonValueKind.String)
                    definition.Parameters[prop.Name] = prop.Value.GetString()!;
                else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                    definition.Parameters[prop.Name] = prop.Value.GetBoolean();
            }
        }

        try
        {
            _componentService.AddComponent(circuit, definition);

            // Clear cached results when circuit is modified
            _resultsCache.Clear(circuitId);

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new { component = name, circuit_id = circuitId, status = "added" }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (ArgumentException ex) when (componentType.Equals("subcircuit", StringComparison.OrdinalIgnoreCase) && 
                                          ex.Message.Contains("not found") && 
                                          _speakerDatabaseService != null)
        {
            // Check if this subcircuit exists in the database but not in library
            var subcircuitName = definition.Model;
            if (!string.IsNullOrWhiteSpace(subcircuitName))
            {
                // Try to find it in the database by searching for the subcircuit name
                var dbResults = _speakerDatabaseService.SearchSpeakersByParameters(new SpeakerSearchParameters
                {
                    Name = subcircuitName,
                    Limit = 1
                });
                
                var inDatabase = dbResults.Any(r => r.SubcircuitName == subcircuitName);
                
                if (inDatabase && _libraryService != null)
                {
                    // Subcircuit is in database but not in library - suggest reindexing
                    throw new ArgumentException(
                        $"{ex.Message} " +
                        $"This subcircuit was found in the speaker database but is not available in the library index. " +
                        $"This indicates a disconnect between the database and library. " +
                        $"Run 'reindex_libraries' to update the library index, or ensure the library files containing this subcircuit are in the configured library paths. " +
                        $"Library paths: {(_config.LibraryPaths != null && _config.LibraryPaths.Any() ? string.Join(", ", _config.LibraryPaths) : "not configured")}.",
                        ex);
                }
                else if (inDatabase && _libraryService == null)
                {
                    // Subcircuit is in database but library service is not configured
                    throw new ArgumentException(
                        $"{ex.Message} " +
                        $"This subcircuit was found in the speaker database, but LibraryService is not configured. " +
                        $"Configure LibraryService in dependency injection to enable subcircuit support.",
                        ex);
                }
            }
            
            // Re-throw the original exception if we can't provide additional context
            throw;
        }
    }

    private async Task<MCPToolResult> DefineModel(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");
        if (!arguments.TryGetProperty("model_name", out var modelNameElement))
            throw new ArgumentException("model_name is required");
        if (!arguments.TryGetProperty("model_type", out var modelTypeElement))
            throw new ArgumentException("model_type is required");
        if (!arguments.TryGetProperty("parameters", out var paramsElement))
            throw new ArgumentException("parameters is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var modelName = modelNameElement.GetString() ?? throw new ArgumentException("model_name must be a string");
        var modelType = modelTypeElement.GetString() ?? throw new ArgumentException("model_type must be a string");

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var parameters = new Dictionary<string, double>();
        if (paramsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in paramsElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    parameters[prop.Name] = prop.Value.GetDouble();
            }
        }

        var definition = new ModelDefinition
        {
            ModelName = modelName,
            ModelType = modelType,
            Parameters = parameters
        };

        _modelService.DefineModel(circuit, definition);

        // Clear cached results when circuit is modified
        _resultsCache.Clear(circuitId);

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new { model_name = modelName, circuit_id = circuitId, status = "defined" }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> RunDCAnalysis(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");
        if (!arguments.TryGetProperty("source", out var sourceElement))
            throw new ArgumentException("source is required");
        if (!arguments.TryGetProperty("start", out var startElement))
            throw new ArgumentException("start is required");
        if (!arguments.TryGetProperty("stop", out var stopElement))
            throw new ArgumentException("stop is required");
        if (!arguments.TryGetProperty("step", out var stepElement))
            throw new ArgumentException("step is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var source = sourceElement.GetString() ?? throw new ArgumentException("source must be a string");
        var start = startElement.GetDouble();
        var stop = stopElement.GetDouble();
        var step = stepElement.GetDouble();

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var exports = new List<string>();
        if (arguments.TryGetProperty("exports", out var exportsElement) && exportsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var export in exportsElement.EnumerateArray())
            {
                exports.Add(export.GetString() ?? throw new ArgumentException("exports must be strings"));
            }
        }

        var result = _dcAnalysisService.RunDCAnalysis(circuit, source, start, stop, step, exports);

        // Cache results for plotting
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "dc_sweep",
            XData = result.SweepValues.ToArray(),
            XLabel = $"{source} (V)",
            Signals = result.Results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
        };
        _resultsCache.Store(circuitId, cachedResult);

        // Generate suggestion for I-V curves if voltage and current signals are present
        string? suggestion = null;
        var hasVoltage = result.Results.Keys.Any(k => k.StartsWith("v(", StringComparison.OrdinalIgnoreCase));
        var hasCurrent = result.Results.Keys.Any(k => k.StartsWith("i(", StringComparison.OrdinalIgnoreCase));
        if (hasVoltage && hasCurrent)
        {
            // Find a voltage and current signal pair that might represent an I-V curve
            var voltageSignals = result.Results.Keys.Where(k => k.StartsWith("v(", StringComparison.OrdinalIgnoreCase)).ToList();
            var currentSignals = result.Results.Keys.Where(k => k.StartsWith("i(", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (voltageSignals.Count > 0 && currentSignals.Count > 0)
            {
                // Suggest I-V curve plotting for the first voltage-current pair
                var voltageSignal = voltageSignals[0];
                var currentSignal = currentSignals[0];
                suggestion = $"For I-V characteristic curve, use plot_results with plot_type='scatter', x_signal='{voltageSignal}', signals=['{currentSignal}']";
            }
        }

        // Create response with optional suggestion
        var response = new
        {
            Results = result,
            Suggestion = suggestion
        };

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> RunTransientAnalysis(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");
        if (!arguments.TryGetProperty("stop_time", out var stopTimeElement))
            throw new ArgumentException("stop_time is required");
        if (!arguments.TryGetProperty("time_step", out var timeStepElement))
            throw new ArgumentException("time_step is required");
        if (!arguments.TryGetProperty("signals", out var signalsElement))
            throw new ArgumentException("signals is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var startTime = arguments.TryGetProperty("start_time", out var startTimeElement) ? startTimeElement.GetDouble() : 0.0;
        var stopTime = stopTimeElement.GetDouble();
        var timeStep = timeStepElement.GetDouble();
        var useInitialConditions = arguments.TryGetProperty("use_initial_conditions", out var uicElement) && uicElement.GetBoolean();

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var signals = new List<string>();
        foreach (var signal in signalsElement.EnumerateArray())
        {
            signals.Add(signal.GetString() ?? throw new ArgumentException("signals must be strings"));
        }

        var result = _transientAnalysisService.RunTransientAnalysis(circuit, startTime, stopTime, timeStep, signals, useInitialConditions);

        // Cache results for plotting
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "transient",
            XData = result.Time.ToArray(),
            XLabel = "Time (s)",
            Signals = result.Signals.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
        };
        _resultsCache.Store(circuitId, cachedResult);

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> RunACAnalysis(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");
        if (!arguments.TryGetProperty("start_frequency", out var startFreqElement))
            throw new ArgumentException("start_frequency is required");
        if (!arguments.TryGetProperty("stop_frequency", out var stopFreqElement))
            throw new ArgumentException("stop_frequency is required");
        if (!arguments.TryGetProperty("number_of_points", out var pointsElement))
            throw new ArgumentException("number_of_points is required");
        if (!arguments.TryGetProperty("signals", out var signalsElement))
            throw new ArgumentException("signals is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var startFreq = startFreqElement.GetDouble();
        var stopFreq = stopFreqElement.GetDouble();
        var numPoints = pointsElement.GetInt32();

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var signals = new List<string>();
        foreach (var signal in signalsElement.EnumerateArray())
        {
            signals.Add(signal.GetString() ?? throw new ArgumentException("signals must be strings"));
        }

        var result = _acAnalysisService.RunACAnalysis(circuit, startFreq, stopFreq, numPoints, signals);

        // Cache results for plotting (convert magnitude/phase to complex)
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "ac",
            XData = result.Frequencies.ToArray(),
            XLabel = "Frequency (Hz)",
            Signals = new Dictionary<string, double[]>(),
            ImaginarySignals = new Dictionary<string, double[]>()
        };

        // Convert magnitude/phase to complex (real/imaginary)
        foreach (var signal in signals)
        {
            if (result.MagnitudeDb.TryGetValue(signal, out var magnitudeDb) &&
                result.PhaseDegrees.TryGetValue(signal, out var phaseDeg))
            {
                var real = new List<double>();
                var imag = new List<double>();
                
                for (int i = 0; i < magnitudeDb.Count && i < phaseDeg.Count; i++)
                {
                    // Convert dB to linear magnitude
                    var magnitude = Math.Pow(10, magnitudeDb[i] / 20.0);
                    // Convert phase degrees to radians
                    var phaseRad = phaseDeg[i] * Math.PI / 180.0;
                    // Convert to complex: real = mag * cos(phase), imag = mag * sin(phase)
                    real.Add(magnitude * Math.Cos(phaseRad));
                    imag.Add(magnitude * Math.Sin(phaseRad));
                }
                
                cachedResult.Signals[signal] = real.ToArray();
                cachedResult.ImaginarySignals[signal] = imag.ToArray();
            }
        }
        
        _resultsCache.Store(circuitId, cachedResult);

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> RunOperatingPoint(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var includePower = arguments.TryGetProperty("include_power", out var powerElement) && powerElement.GetBoolean();

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var result = _operatingPointService.RunOperatingPointAnalysis(circuit, includePower);

        // Cache results for plotting
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "operating_point",
            OperatingPointData = new Dictionary<string, double>()
        };
        
        // Add node voltages
        foreach (var kvp in result.NodeVoltages)
        {
            cachedResult.OperatingPointData[$"v({kvp.Key})"] = kvp.Value;
        }
        
        // Add branch currents
        foreach (var kvp in result.BranchCurrents)
        {
            cachedResult.OperatingPointData[$"i({kvp.Key})"] = kvp.Value;
        }
        
        // Add power dissipation if requested
        if (includePower)
        {
            foreach (var kvp in result.PowerDissipation)
            {
                cachedResult.OperatingPointData[$"p({kvp.Key})"] = kvp.Value;
            }
        }
        
        _resultsCache.Store(circuitId, cachedResult);

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> ListCircuits()
    {
        var circuits = _circuitManager.ListCircuits().ToList();
        var activeCircuit = _circuitManager.GetActiveCircuit();
        
        var circuitList = circuits.Select(c => new
        {
            id = c.Id,
            description = c.Description,
            is_active = c.Id == activeCircuit?.Id
        }).ToList();

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(circuitList, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> DeleteCircuit(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");

        var circuitId = circuitIdElement.GetString();
        if (string.IsNullOrWhiteSpace(circuitId))
        {
            throw new ArgumentException("circuit_id parameter is required and cannot be empty or whitespace. Provide a valid circuit identifier.");
        }

        // Check if circuit exists before attempting to delete
        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
        {
            // Provide helpful error with available circuits
            var availableCircuits = _circuitManager.ListCircuits().Select(c => c.Id).ToList();
            var circuitList = availableCircuits.Count > 0 
                ? $" Available circuits: {string.Join(", ", availableCircuits)}"
                : " No circuits exist.";
            
            throw new ArgumentException($"Circuit '{circuitId}' not found.{circuitList}");
        }

        // Clear cached results for this circuit
        _resultsCache.Clear(circuitId);

        // Delete the circuit
        var deleted = _circuitManager.ClearCircuit(circuitId);
        
        if (!deleted)
        {
            throw new InvalidOperationException($"Failed to delete circuit '{circuitId}'. Circuit may have been deleted by another operation.");
        }

        // Get new active circuit if one exists
        var newActiveCircuit = _circuitManager.GetActiveCircuit();

        var result = new
        {
            circuit_id = circuitId,
            deleted = true,
            new_active_circuit = newActiveCircuit?.Id,
            message = $"Circuit '{circuitId}' deleted successfully."
        };

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> GetComponentInfo(JsonElement arguments)
    {
        // Get circuit (use active if circuit_id not provided)
        CircuitModel? circuit = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            var circuitId = circuitIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(circuitId))
            {
                circuit = _circuitManager.GetCircuit(circuitId);
                if (circuit == null)
                {
                    var availableCircuits = _circuitManager.ListCircuits().Select(c => c.Id).ToList();
                    var circuitList = availableCircuits.Count > 0 
                        ? $" Available circuits: {string.Join(", ", availableCircuits)}"
                        : " No circuits exist.";
                    throw new ArgumentException($"Circuit '{circuitId}' not found.{circuitList}");
                }
            }
        }
        
        // If no circuit_id provided, use active circuit
        if (circuit == null)
        {
            circuit = _circuitManager.GetActiveCircuit();
            if (circuit == null)
            {
                throw new InvalidOperationException("No active circuit. Create a circuit first or specify circuit_id.");
            }
        }

        // Get component name
        if (!arguments.TryGetProperty("component", out var componentElement))
        {
            throw new ArgumentException("component parameter is required");
        }

        var componentName = componentElement.GetString();
        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new ArgumentException("component parameter is required and cannot be empty or whitespace");
        }

        // Get component definition using reflection (ComponentDefinitions is internal)
        var componentDefsProperty = typeof(CircuitModel).GetProperty("ComponentDefinitions", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (componentDefsProperty == null)
        {
            throw new InvalidOperationException("Cannot access ComponentDefinitions property");
        }
        
        var componentDefs = componentDefsProperty.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
        if (componentDefs == null || !componentDefs.TryGetValue(componentName, out var componentDef))
        {
            // Provide helpful error with available components
            var availableComponents = componentDefs?.Keys.ToList() ?? new List<string>();
            var componentList = availableComponents.Count > 0 
                ? $" Available components: {string.Join(", ", availableComponents)}"
                : " No components exist in this circuit.";
            
            throw new ArgumentException($"Component '{componentName}' not found in circuit '{circuit.Id}'.{componentList}");
        }

        // Build response with component information
        var response = new Dictionary<string, object>
        {
            { "component_name", componentDef.Name },
            { "component_type", componentDef.ComponentType },
            { "nodes", componentDef.Nodes ?? new List<string>() },
            { "value", componentDef.Value.HasValue ? (object)componentDef.Value.Value : null! },
            { "parameters", componentDef.Parameters ?? new Dictionary<string, object>() }
        };

        // If component has a model, include model information
        if (!string.IsNullOrWhiteSpace(componentDef.Model))
        {
            // For subcircuits, check InternalCircuit for SubcircuitDefinitionEntity using reflection
            if (componentDef.ComponentType.Equals("subcircuit", StringComparison.OrdinalIgnoreCase))
            {
                // Access InternalCircuit using reflection (it's internal)
                var internalCircuitProperty = typeof(CircuitModel).GetProperty("InternalCircuit", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (internalCircuitProperty != null)
                {
                    var internalCircuit = internalCircuitProperty.GetValue(circuit);
                    if (internalCircuit != null)
                    {
                        // Try to get entity by name using TryGetEntity method
                        var tryGetEntityMethod = internalCircuit.GetType().GetMethod("TryGetEntity", 
                            new[] { typeof(string), typeof(IEntity).MakeByRefType() });
                        if (tryGetEntityMethod != null)
                        {
                            var parameters = new object[] { componentDef.Model, null! };
                            var found = (bool)tryGetEntityMethod.Invoke(internalCircuit, parameters)!;
                            var definitionEntity = parameters[1] as IEntity;
                            
                            if (found && definitionEntity != null)
                            {
                                // Check if it's a SubcircuitDefinitionEntity wrapper (using reflection since it's internal)
                                var definitionEntityType = definitionEntity.GetType();
                                var definitionProperty = definitionEntityType.GetProperty("Definition", 
                                    BindingFlags.Public | BindingFlags.Instance);
                                
                                if (definitionProperty != null)
                                {
                                    // It's a SubcircuitDefinitionEntity wrapper
                                    var subcircuitDef = definitionProperty.GetValue(definitionEntity);
                                    if (subcircuitDef != null)
                                    {
                                        // Get Pins property using reflection
                                        var pinsProperty = subcircuitDef.GetType().GetProperty("Pins");
                                        var pins = pinsProperty?.GetValue(subcircuitDef) as System.Collections.Generic.IReadOnlyList<string>;
                                        
                                        response["model_name"] = componentDef.Model;
                                        response["model_type"] = "subcircuit";
                                        response["model_parameters"] = new Dictionary<string, double>(); // Subcircuits don't have parameters
                                        if (pins != null && pins.Count > 0)
                                        {
                                            response["model_pins"] = pins.ToList();
                                        }
                                    }
                                    else
                                    {
                                        response["model_name"] = componentDef.Model;
                                        response["model_type"] = "subcircuit";
                                        response["model_parameters"] = (Dictionary<string, double>?)null!;
                                        response["model_error"] = "Subcircuit definition found but could not be accessed";
                                    }
                                }
                                else
                                {
                                    // Entity might directly implement ISubcircuitDefinition
                                    // Check for Pins property
                                    var pinsProperty = definitionEntityType.GetProperty("Pins");
                                    if (pinsProperty != null)
                                    {
                                        var pins = pinsProperty.GetValue(definitionEntity) as System.Collections.Generic.IReadOnlyList<string>;
                                        response["model_name"] = componentDef.Model;
                                        response["model_type"] = "subcircuit";
                                        response["model_parameters"] = new Dictionary<string, double>();
                                        if (pins != null && pins.Count > 0)
                                        {
                                            response["model_pins"] = pins.ToList();
                                        }
                                    }
                                    else
                                    {
                                        response["model_name"] = componentDef.Model;
                                        response["model_type"] = "subcircuit";
                                        response["model_parameters"] = (Dictionary<string, double>?)null!;
                                        response["model_error"] = "Subcircuit definition found but structure is unexpected";
                                    }
                                }
                            }
                            else
                            {
                                // Subcircuit definition not found in circuit
                                response["model_name"] = componentDef.Model;
                                response["model_type"] = "subcircuit";
                                response["model_parameters"] = (Dictionary<string, double>?)null!;
                                response["model_error"] = "Subcircuit definition not found in circuit";
                            }
                        }
                        else
                        {
                            response["model_name"] = componentDef.Model;
                            response["model_type"] = "subcircuit";
                            response["model_parameters"] = (Dictionary<string, double>?)null!;
                            response["model_error"] = "Cannot access circuit entities (reflection failed)";
                        }
                    }
                    else
                    {
                        response["model_name"] = componentDef.Model;
                        response["model_type"] = "subcircuit";
                        response["model_parameters"] = (Dictionary<string, double>?)null!;
                        response["model_error"] = "Cannot access internal circuit (reflection failed)";
                    }
                }
                else
                {
                    response["model_name"] = componentDef.Model;
                    response["model_type"] = "subcircuit";
                    response["model_parameters"] = (Dictionary<string, double>?)null!;
                    response["model_error"] = "Cannot access internal circuit property (reflection failed)";
                }
            }
            else
            {
                // For non-subcircuit components, check ModelDefinitions
                var modelDefsProperty = typeof(CircuitModel).GetProperty("ModelDefinitions", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (modelDefsProperty != null)
                {
                    var modelDefs = modelDefsProperty.GetValue(circuit) as Dictionary<string, ModelDefinition>;
                    if (modelDefs != null && modelDefs.TryGetValue(componentDef.Model, out var modelDef))
                    {
                        response["model_name"] = modelDef.ModelName;
                        response["model_type"] = modelDef.ModelType;
                        response["model_parameters"] = modelDef.Parameters ?? new Dictionary<string, double>();
                    }
                    else
                    {
                        // Model referenced but not found
                        response["model_name"] = componentDef.Model;
                        response["model_type"] = (string?)null!;
                        response["model_parameters"] = (Dictionary<string, double>?)null!;
                        response["model_error"] = "Model definition not found";
                    }
                }
            }
        }

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> ModifyComponent(JsonElement arguments)
    {
        // Get circuit (use active if circuit_id not provided)
        CircuitModel? circuit = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            var circuitId = circuitIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(circuitId))
            {
                circuit = _circuitManager.GetCircuit(circuitId);
                if (circuit == null)
                {
                    var availableCircuits = _circuitManager.ListCircuits().Select(c => c.Id).ToList();
                    var circuitList = availableCircuits.Count > 0 
                        ? $" Available circuits: {string.Join(", ", availableCircuits)}"
                        : " No circuits exist.";
                    throw new ArgumentException($"Circuit '{circuitId}' not found.{circuitList}");
                }
            }
        }
        
        // If no circuit_id provided, use active circuit
        if (circuit == null)
        {
            circuit = _circuitManager.GetActiveCircuit();
            if (circuit == null)
            {
                throw new InvalidOperationException("No active circuit. Create a circuit first or specify circuit_id.");
            }
        }

        // Get component name
        if (!arguments.TryGetProperty("component", out var componentElement))
        {
            throw new ArgumentException("component parameter is required");
        }

        var componentName = componentElement.GetString();
        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new ArgumentException("component parameter is required and cannot be empty or whitespace");
        }

        // Get parameters
        if (!arguments.TryGetProperty("parameters", out var parametersElement))
        {
            throw new ArgumentException("parameters parameter is required");
        }

        // Parse parameters dictionary
        var parameters = new Dictionary<string, object>();
        if (parametersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in parametersElement.EnumerateObject())
            {
                var key = prop.Name;
                var value = prop.Value;

                // Convert JSON value to appropriate C# type
                object? paramValue = null;
                if (value.ValueKind == JsonValueKind.Number)
                {
                    if (value.TryGetDouble(out var doubleVal))
                    {
                        paramValue = doubleVal;
                    }
                    else if (value.TryGetInt64(out var longVal))
                    {
                        paramValue = (double)longVal;
                    }
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    paramValue = value.GetString();
                }
                else if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    paramValue = value.GetBoolean();
                }
                else if (value.ValueKind == JsonValueKind.Null)
                {
                    paramValue = null;
                }

                if (paramValue != null)
                {
                    parameters[key] = paramValue;
                }
            }
        }

        if (parameters.Count == 0)
        {
            throw new ArgumentException("At least one parameter must be specified in the parameters dictionary");
        }

        // Call ComponentService to modify the component
        _componentService.ModifyComponent(circuit, componentName, parameters);

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new 
                    { 
                        message = $"Component '{componentName}' modified successfully",
                        circuit_id = circuit.Id,
                        component = componentName,
                        parameters_updated = parameters.Keys.ToList()
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> LibrarySearch(JsonElement arguments)
    {
        // Get query (optional, defaults to empty string)
        var query = string.Empty;
        if (arguments.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        // Get type filter (optional)
        string? typeFilter = null;
        if (arguments.TryGetProperty("type", out var typeElement))
        {
            typeFilter = typeElement.GetString();
        }

        // Get limit (optional, defaults to 20 for better performance)
        var limit = 20;
        var requestedLimit = limit;
        if (arguments.TryGetProperty("limit", out var limitElement))
        {
            if (limitElement.ValueKind == JsonValueKind.Number && limitElement.TryGetInt32(out var limitValue))
            {
                requestedLimit = limitValue;
                limit = Math.Max(1, Math.Min(100, limitValue)); // Clamp between 1 and 100 (reduced from 1000)
            }
        }

        // Get include_parameters flag (optional, defaults to true)
        var includeParameters = true;
        if (arguments.TryGetProperty("include_parameters", out var includeParamsElement))
        {
            if (includeParamsElement.ValueKind == JsonValueKind.True || includeParamsElement.ValueKind == JsonValueKind.False)
            {
                includeParameters = includeParamsElement.GetBoolean();
            }
        }

        // Get count_only flag (optional, defaults to false)
        var countOnly = false;
        if (arguments.TryGetProperty("count_only", out var countOnlyElement))
        {
            if (countOnlyElement.ValueKind == JsonValueKind.True || countOnlyElement.ValueKind == JsonValueKind.False)
            {
                countOnly = countOnlyElement.GetBoolean();
            }
        }

        // If library service is not configured, return helpful error message
        if (_libraryService == null)
        {
            var configInfo = _config.LibraryPaths == null || !_config.LibraryPaths.Any()
                ? "No library paths are configured in MCPServerConfig.LibraryPaths."
                : $"Library paths configured: {string.Join(", ", _config.LibraryPaths)}";
            
            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            error = "Library service is not configured",
                            message = $"Library service is not available. {configInfo} " +
                                      "To enable library search, configure LibraryPaths in MCPServerConfig with directories containing .lib files. " +
                                      "The library service will automatically index all .lib files in those directories on startup (including models and subcircuits). " +
                                      "See sample_libraries/sample_components.lib for an example library file format.",
                            query = query,
                            type_filter = typeFilter,
                            limit = limit,
                            count = 0,
                            model_count = 0,
                            subcircuit_count = 0,
                            models = new List<object>(),
                            subcircuits = new List<object>()
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }

        // Search models - use a higher limit for count_only to get accurate total
        var searchLimit = countOnly ? int.MaxValue : limit;
        var models = _libraryService.SearchModels(query, typeFilter, searchLimit);
        var totalModelCount = models.Count;

        // Search subcircuits - use typeFilter for subcircuit type filtering (e.g., "woofers", "tweeters")
        // Note: typeFilter applies to both models and subcircuits, but model types and subcircuit types are different
        // For subcircuits, typeFilter searches the metadata TYPE field
        var subcircuits = _libraryService.SearchSubcircuits(query, typeFilter, searchLimit);
        var totalSubcircuitCount = subcircuits.Count;

        // Calculate total count
        var totalCount = totalModelCount + totalSubcircuitCount;

        // If count_only, return just the count
        if (countOnly)
        {
            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            query = query,
                            type_filter = typeFilter,
                            count = totalCount,
                            model_count = totalModelCount,
                            subcircuit_count = totalSubcircuitCount,
                            message = totalCount > 0 
                                ? $"Found {totalModelCount} matching model(s) and {totalSubcircuitCount} matching subcircuit(s). Use library_search with include_parameters=true to get details."
                                : "No matching models or subcircuits found."
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }

        // Format results - limit to requested count
        var limitedModels = models.Take(limit).ToList();
        var modelResults = limitedModels.Select(m => new
        {
            model_name = m.ModelName,
            model_type = m.ModelType,
            type = "model",
            parameters = includeParameters ? (m.Parameters ?? new Dictionary<string, double>()) : null,
            parameter_count = includeParameters ? (m.Parameters?.Count ?? 0) : (m.Parameters?.Count ?? 0)
        }).ToList();

        // Format subcircuit results - limit to requested count
        var limitedSubcircuits = subcircuits.Take(limit).ToList();
        var subcircuitResults = limitedSubcircuits.Select(s => new
        {
            name = s.Name,
            type = "subcircuit",
            nodes = s.Nodes ?? new List<string>(),
            node_count = s.Nodes?.Count ?? 0,
            metadata = includeParameters ? (s.Metadata ?? new Dictionary<string, string>()) : null,
            ts_parameters = includeParameters ? (s.TsParameters ?? new Dictionary<string, double>()) : null
        }).ToList();

        // Build response message
        var limitWarning = requestedLimit > 100 
            ? $" Note: Requested limit ({requestedLimit}) exceeds maximum (100), capped at 100."
            : "";
        
        var totalReturned = modelResults.Count + subcircuitResults.Count;
        var message = totalCount > limit
            ? $"Found {totalModelCount} matching model(s) and {totalSubcircuitCount} matching subcircuit(s), showing first {limit}. Use 'limit' parameter to see more (max 100), or 'count_only=true' to get total count.{limitWarning}"
            : totalCount > 0
                ? $"Found {totalModelCount} matching model(s) and {totalSubcircuitCount} matching subcircuit(s).{limitWarning}"
                : "No matching models or subcircuits found.";

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new
                    {
                        query = query,
                        type_filter = typeFilter,
                        limit = limit,
                        count = totalCount,
                        model_count = totalModelCount,
                        subcircuit_count = totalSubcircuitCount,
                        returned = totalReturned,
                        models_returned = modelResults.Count,
                        subcircuits_returned = subcircuitResults.Count,
                        include_parameters = includeParameters,
                        message = message,
                        models = modelResults,
                        subcircuits = subcircuitResults
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> ExportNetlist(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var netlist = _netlistService.ExportNetlist(circuit);

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = netlist
                }
            }
        };
    }

    private async Task<MCPToolResult> ValidateCircuit(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("circuit_id", out var circuitIdElement))
            throw new ArgumentException("circuit_id is required");

        var circuitId = circuitIdElement.GetString() ?? throw new ArgumentException("circuit_id must be a string");
        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
            throw new ArgumentException($"Circuit '{circuitId}' not found");

        var validator = new CircuitValidator();
        var validation = validator.Validate(circuit);

        var result = new
        {
            circuit_id = circuitId,
            is_valid = validation.IsValid,
            errors = validation.Errors,
            warnings = validation.Warnings,
            component_count = circuit.ComponentCount,
            has_ground = circuit.HasGround
        };

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<MCPToolResult> RenderSchematic(JsonElement arguments)
    {
        // Get circuit
        CircuitModel? circuit;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            var circuitId = circuitIdElement.GetString();
            if (string.IsNullOrEmpty(circuitId))
            {
                circuit = _circuitManager.GetActiveCircuit();
            }
            else
            {
                circuit = _circuitManager.GetCircuit(circuitId);
            }
        }
        else
        {
            circuit = _circuitManager.GetActiveCircuit();
        }

        if (circuit == null)
            throw new ArgumentException("Circuit not found. Provide circuit_id or ensure an active circuit exists.");

        // Get options
        var skinTypeStr = arguments.TryGetProperty("skin_type", out var skinProp)
            ? skinProp.GetString() ?? "Analog"
            : "Analog";
        
        var skinType = skinTypeStr.Equals("Digital", StringComparison.OrdinalIgnoreCase)
            ? SkinType.Digital
            : SkinType.Analog;

        var showValues = arguments.TryGetProperty("show_values", out var showProp)
            ? showProp.GetBoolean()
            : true;

        IList<string>? externalPorts = null;
        if (arguments.TryGetProperty("external_ports", out var portsProp))
        {
            if (portsProp.ValueKind == JsonValueKind.Array)
            {
                externalPorts = portsProp.EnumerateArray()
                    .Select(p => p.GetString()!)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
        }

        // Get memory and timeout options (with defaults)
        var maxMemory = arguments.TryGetProperty("max_memory", out var memoryProp) && memoryProp.ValueKind == JsonValueKind.Number
            ? memoryProp.GetInt64()
            : 4_000_000_000L;  // Default 4GB

        var timeoutSeconds = arguments.TryGetProperty("timeout_seconds", out var timeoutProp) && timeoutProp.ValueKind == JsonValueKind.Number
            ? timeoutProp.GetInt32()
            : 600;  // Default 10 minutes

        _logger?.LogInformation("Rendering schematic for circuit {CircuitId} with memory limit {MemoryGB}GB and timeout {TimeoutSeconds}s", 
            circuit.Id, maxMemory / 1_000_000_000.0, timeoutSeconds);

        // Render SVG using the SpiceSharp circuit
        // Configure renderer with user-specified or default memory limit and timeout
        var renderOptions = new RenderOptions
        {
            MaxMemory = maxMemory,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        
        string svg;
        try
        {
            using var renderer = new SchematicRenderer(renderOptions);
            svg = renderer.Render(circuit.GetSpiceSharpCircuit(), new SpiceRenderOptions
            {
                Skin = skinType,
                ShowValues = showValues,
                ExternalPorts = externalPorts ?? Array.Empty<string>()
            });
        }
        catch (Exception ex) when (ex.Message.Contains("allocated") || ex.Message.Contains("limited") || ex.Message.Contains("Memory"))
        {
            _logger?.LogError(ex, "Memory limit exceeded during rendering. Configured limit: {MemoryGB}GB. Try increasing max_memory parameter.", maxMemory / 1_000_000_000.0);
            throw new ArgumentException($"Memory limit exceeded during rendering. Configured limit: {maxMemory / 1_000_000_000.0:F1}GB ({maxMemory:N0} bytes). " +
                $"Try increasing the 'max_memory' parameter (e.g., 8GB = 8,000,000,000). Original error: {ex.Message}");
        }

        // Parse output format(s)
        var outputFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (arguments.TryGetProperty("output_format", out var formatProp))
        {
            if (formatProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var format in formatProp.EnumerateArray())
                {
                    var formatStr = format.GetString();
                    if (!string.IsNullOrEmpty(formatStr))
                    {
                        outputFormats.Add(formatStr);
                    }
                }
            }
            else if (formatProp.ValueKind == JsonValueKind.String)
            {
                var formatStr = formatProp.GetString();
                if (!string.IsNullOrEmpty(formatStr))
                {
                    outputFormats.Add(formatStr);
                }
            }
        }
        
        // Default to "image" if no format specified
        if (outputFormats.Count == 0)
        {
            outputFormats.Add("image");
        }

        // Get file path if file output is requested
        string? filePath = null;
        string? fileSaveError = null;
        if (outputFormats.Contains("file", StringComparer.OrdinalIgnoreCase))
        {
            if (arguments.TryGetProperty("file_path", out var filePathProp))
            {
                filePath = filePathProp.GetString();
            }
            
            // Generate default path if not provided
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safeCircuitId = string.Join("_", circuit.Id.Split(Path.GetInvalidFileNameChars()));
                filePath = Path.Combine(Path.GetTempPath(), $"schematic_{safeCircuitId}_{timestamp}.svg");
            }
            
            // Try to save file with error handling
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save SVG to file
                await File.WriteAllTextAsync(filePath, svg);
                
                // Verify file was written
                if (!File.Exists(filePath))
                {
                    fileSaveError = "File write completed but file not found at expected path. This may indicate filesystem isolation between MCP server and client.";
                }
                else
                {
                    _logger?.LogInformation("SVG saved to file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                fileSaveError = $"File save failed: {ex.Message}. This may be due to filesystem isolation between MCP server and client. Use 'text' output format instead.";
                filePath = null; // Clear path since save failed
                _logger?.LogWarning(ex, "Failed to save SVG to file");
            }
        }

        // Build content list based on requested formats
        var contentList = new List<MCPContent>();

        // Add image content (base64-encoded)
        if (outputFormats.Contains("image", StringComparer.OrdinalIgnoreCase))
        {
            var svgBytes = System.Text.Encoding.UTF8.GetBytes(svg);
            var base64Svg = Convert.ToBase64String(svgBytes);
            
            contentList.Add(new MCPContent
            {
                Type = "image",
                Data = base64Svg,
                MimeType = "image/svg+xml"
            });
        }

        // Add text content (raw SVG string)
        if (outputFormats.Contains("text", StringComparer.OrdinalIgnoreCase))
        {
            contentList.Add(new MCPContent
            {
                Type = "text",
                Text = svg,
                MimeType = "image/svg+xml"
            });
        }

        // Add file path info if file was saved (or attempted)
        if (outputFormats.Contains("file", StringComparer.OrdinalIgnoreCase) && (!string.IsNullOrEmpty(filePath) || !string.IsNullOrEmpty(fileSaveError)))
        {
            // Always include warning about filesystem isolation
            string isolationWarning = "IMPORTANT: Due to filesystem isolation, files saved by the MCP server may not be accessible from the client filesystem. " +
                "The file may exist in the server's filesystem but not be visible to the client. " +
                "For reliable access, use 'text' output format and save the raw SVG string manually.";
            
            var fileInfo = new
            {
                file_path = filePath,
                file_size_bytes = filePath != null && File.Exists(filePath) ? new FileInfo(filePath).Length : (long?)null,
                status = string.IsNullOrEmpty(fileSaveError) ? "saved" : "failed",
                error = fileSaveError,
                warning = string.IsNullOrEmpty(fileSaveError) ? isolationWarning : null,
                note = string.IsNullOrEmpty(fileSaveError) ? isolationWarning : "Due to filesystem isolation, file saves may not be accessible from the client. Use 'text' output format and save manually if needed."
            };
            
            contentList.Add(new MCPContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(fileInfo, new JsonSerializerOptions { WriteIndented = true })
            });
        }

        return new MCPToolResult
        {
            Content = contentList
        };
    }

    private async Task<MCPToolResult> PlotResults(JsonElement arguments)
    {
        // Get circuit ID (optional, uses active if omitted)
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("No circuit_id provided and no active circuit available");
        }

        // Get cached results
        var cachedResults = _resultsCache.Get(circuitId);
        if (cachedResults == null)
        {
            throw new InvalidOperationException($"No analysis results cached for circuit '{circuitId}'. Run an analysis first (run_dc_analysis, run_transient_analysis, run_ac_analysis, or run_operating_point).");
        }

        // Parse plot type
        var plotTypeStr = "auto";
        if (arguments.TryGetProperty("plot_type", out var plotTypeElement))
        {
            plotTypeStr = plotTypeElement.GetString() ?? "auto";
        }
        var plotType = plotTypeStr.ToLowerInvariant() switch
        {
            "line" => PlotType.Line,
            "bode" => PlotType.Bode,
            "bar" => PlotType.Bar,
            "scatter" => PlotType.Scatter,
            _ => PlotType.Auto
        };

        // Parse image format (default to PNG for better client compatibility)
        var imageFormatStr = "png";
        if (arguments.TryGetProperty("image_format", out var imageFormatElement))
        {
            imageFormatStr = imageFormatElement.GetString() ?? "png";
        }
        var imageFormat = imageFormatStr.ToLowerInvariant() == "svg" ? ImageFormat.Svg : ImageFormat.Png;

        // Parse signals to plot
        var signalsToPlot = new List<string>();
        if (arguments.TryGetProperty("signals", out var signalsElement) && signalsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var signal in signalsElement.EnumerateArray())
            {
                var signalName = signal.GetString();
                if (!string.IsNullOrEmpty(signalName))
                {
                    signalsToPlot.Add(signalName);
                }
            }
        }

        // If no signals specified, use all available signals
        if (signalsToPlot.Count == 0)
        {
            signalsToPlot.AddRange(cachedResults.Signals.Keys);
            if (signalsToPlot.Count == 0 && cachedResults.OperatingPointData.Count > 0)
            {
                signalsToPlot.AddRange(cachedResults.OperatingPointData.Keys);
            }
        }

        // Parse signals to invert
        var signalsToInvert = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (arguments.TryGetProperty("invert_signals", out var invertSignalsElement) && invertSignalsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var signal in invertSignalsElement.EnumerateArray())
            {
                var signalName = signal.GetString();
                if (!string.IsNullOrEmpty(signalName))
                {
                    signalsToInvert.Add(signalName);
                }
            }
        }

        // Validate signals exist
        foreach (var signal in signalsToPlot)
        {
            if (!cachedResults.Signals.ContainsKey(signal) && !cachedResults.OperatingPointData.ContainsKey(signal))
            {
                throw new ArgumentException($"Signal '{signal}' not found in cached results. Available signals: {string.Join(", ", cachedResults.Signals.Keys.Concat(cachedResults.OperatingPointData.Keys))}");
            }
        }

        // Parse options and X label
        var options = new PlotOptions();
        string? xLabel = null;
        
        // Populate InvertedSignals in options if any signals are being inverted
        if (signalsToInvert.Count > 0)
        {
            options.InvertedSignals = signalsToInvert;
        }
        if (arguments.TryGetProperty("options", out var optionsElement))
        {
            if (optionsElement.TryGetProperty("title", out var titleElement))
            {
                var title = titleElement.GetString();
                if (!string.IsNullOrWhiteSpace(title))
                    options.Title = title;
            }
            if (optionsElement.TryGetProperty("x_label", out var xLabelElement))
            {
                var label = xLabelElement.GetString();
                if (!string.IsNullOrWhiteSpace(label))
                    xLabel = label;
            }
            if (optionsElement.TryGetProperty("y_label", out var yLabelElement))
            {
                var label = yLabelElement.GetString();
                if (!string.IsNullOrWhiteSpace(label))
                    options.YLabel = label;
            }
            if (optionsElement.TryGetProperty("x_scale", out var xScaleElement))
                options.XScale = xScaleElement.GetString()?.ToLowerInvariant() == "log" ? ScaleType.Log : ScaleType.Linear;
            if (optionsElement.TryGetProperty("y_scale", out var yScaleElement))
                options.YScale = yScaleElement.GetString()?.ToLowerInvariant() == "log" ? ScaleType.Log : ScaleType.Linear;
            if (optionsElement.TryGetProperty("grid", out var gridElement))
                options.ShowGrid = gridElement.GetBoolean();
            if (optionsElement.TryGetProperty("legend", out var legendElement))
                options.ShowLegend = legendElement.GetBoolean();
            if (optionsElement.TryGetProperty("width", out var widthElement))
                options.Width = widthElement.GetInt32();
            if (optionsElement.TryGetProperty("height", out var heightElement))
                options.Height = heightElement.GetInt32();
            if (optionsElement.TryGetProperty("colors", out var colorsElement) && colorsElement.ValueKind == JsonValueKind.Array)
            {
                var colors = new List<string>();
                foreach (var color in colorsElement.EnumerateArray())
                {
                    var colorStr = color.GetString();
                    if (!string.IsNullOrEmpty(colorStr))
                    {
                        colors.Add(colorStr);
                    }
                }
                if (colors.Count > 0)
                {
                    options.ColorPalette = colors.ToArray();
                }
            }
        }

        // Determine analysis type
        var analysisType = cachedResults.AnalysisType.ToLowerInvariant() switch
        {
            "dc_sweep" => AnalysisType.DcSweep,
            "transient" => AnalysisType.Transient,
            "ac" => AnalysisType.Ac,
            "operating_point" => AnalysisType.OperatingPoint,
            _ => AnalysisType.Transient
        };

        // Auto-determine plot type if needed
        if (plotType == PlotType.Auto)
        {
            plotType = analysisType switch
            {
                AnalysisType.Ac => PlotType.Bode,
                AnalysisType.OperatingPoint => PlotType.Bar,
                _ => PlotType.Line
            };
        }

        // Build plot request
        var plotRequest = new PlotRequest
        {
            AnalysisType = analysisType,
            PlotType = plotType,
            ImageFormat = imageFormat,
            XData = cachedResults.XData,
            XLabel = xLabel ?? cachedResults.XLabel,
            Options = options
        };

        // Add series
        if (cachedResults.OperatingPointData.Count > 0 && plotType == PlotType.Bar)
        {
            // For bar charts with operating point data
            // If signalsToPlot is empty or doesn't match any operating point data, use all operating point data
            var opData = signalsToPlot.Count > 0 
                ? cachedResults.OperatingPointData.Where(kvp => signalsToPlot.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase)).ToList()
                : cachedResults.OperatingPointData.ToList();
            
            // If still no data after filtering, use all operating point data as fallback
            if (opData.Count == 0)
            {
                opData = cachedResults.OperatingPointData.ToList();
            }
            
            if (opData.Count == 0)
            {
                throw new InvalidOperationException($"No operating point data found. Available signals: {string.Join(", ", cachedResults.OperatingPointData.Keys)}");
            }
            
            // For bar charts, XData represents bar positions (one per signal)
            // Each series must have values matching XData length (validation requirement)
            // For operating point, we want one bar per signal, so we create one series with all values
            plotRequest.XData = Enumerable.Range(0, opData.Count).Select(i => (double)i).ToArray();
            
            // Collect all values and names
            var allValues = new List<double>();
            var seriesNames = new List<string>();
            
            foreach (var kvp in opData)
            {
                var value = kvp.Value;
                // Invert value if this signal is in the invert list
                if (signalsToInvert.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    value = -value;
                }
                allValues.Add(value);
                seriesNames.Add(kvp.Key);
            }
            
            // Create one series with all values (matching XData length)
            plotRequest.Series = new List<DataSeries>
            {
                new DataSeries
                {
                    Name = string.Join(", ", seriesNames),
                    Values = allValues.ToArray()
                }
            };
        }
        else
        {
            // For line/scatter/Bode plots
            plotRequest.Series = signalsToPlot.Select(signal =>
            {
                var values = cachedResults.Signals.TryGetValue(signal, out var signalValues) ? signalValues : Array.Empty<double>();
                var imagValues = cachedResults.ImaginarySignals.TryGetValue(signal, out var imagSignalValues) ? imagSignalValues : null;
                
                // Invert values if this signal is in the invert list
                if (signalsToInvert.Contains(signal, StringComparer.OrdinalIgnoreCase))
                {
                    values = values.Select(v => -v).ToArray();
                    if (imagValues != null)
                    {
                        imagValues = imagValues.Select(v => -v).ToArray();
                    }
                }
                
                return new DataSeries
                {
                    Name = signal,
                    Values = values,
                    ImagValues = imagValues
                };
            }).ToList();
        }

        // Generate plot
        var plotGenerator = PlotGeneratorFactory.Create();
        var plotResult = plotGenerator.GeneratePlot(plotRequest);

        if (!plotResult.Success)
        {
            throw new InvalidOperationException($"Plot generation failed: {plotResult.ErrorMessage}");
        }

        // Parse output formats
        var outputFormats = new List<string> { "image" };
        if (arguments.TryGetProperty("output_format", out var outputFormatElement) && outputFormatElement.ValueKind == JsonValueKind.Array)
        {
            outputFormats.Clear();
            foreach (var format in outputFormatElement.EnumerateArray())
            {
                var formatStr = format.GetString();
                if (!string.IsNullOrEmpty(formatStr))
                {
                    outputFormats.Add(formatStr.ToLowerInvariant());
                }
            }
        }

        var contentList = new List<MCPContent>();

        // Add text content (for SVG when text format is requested, or always for SVG when image format is requested)
        if (plotResult.ImageData != null && imageFormat == ImageFormat.Svg)
        {
            bool shouldAddText = outputFormats.Contains("text", StringComparer.OrdinalIgnoreCase) ||
                                 outputFormats.Contains("image", StringComparer.OrdinalIgnoreCase);
            
            if (shouldAddText)
            {
                var svgText = System.Text.Encoding.UTF8.GetString(plotResult.ImageData);
                contentList.Add(new MCPContent
                {
                    Type = "text",
                    Text = svgText,
                    MimeType = "image/svg+xml"
                });
            }
        }

        // Add image content (base64)
        if (outputFormats.Contains("image", StringComparer.OrdinalIgnoreCase) && plotResult.ImageData != null)
        {
            if (imageFormat == ImageFormat.Svg)
            {
                // SVG image format (base64) - note: may not display in all clients
                var base64Image = Convert.ToBase64String(plotResult.ImageData);
                contentList.Add(new MCPContent
                {
                    Type = "image",
                    Data = base64Image,
                    MimeType = "image/svg+xml"
                });
            }
            else
            {
                // PNG format - standard image output
                var base64Image = Convert.ToBase64String(plotResult.ImageData);
                contentList.Add(new MCPContent
                {
                    Type = "image",
                    Data = base64Image,
                    MimeType = "image/png"
                });
            }
        }

        // Save to file if requested
        string? filePath = null;
        string? fileSaveError = null;
        if (outputFormats.Contains("file", StringComparer.OrdinalIgnoreCase) && plotResult.ImageData != null)
        {
            // Get file path
            if (arguments.TryGetProperty("file_path", out var filePathElement))
            {
                filePath = filePathElement.GetString();
            }

            if (string.IsNullOrEmpty(filePath))
            {
                // Generate default file path
                var extension = imageFormat == ImageFormat.Svg ? ".svg" : ".png";
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                filePath = Path.Combine(Path.GetTempPath(), $"plot_{circuitId}_{timestamp}{extension}");
            }

            // Try to save file with error handling
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write file
                await File.WriteAllBytesAsync(filePath, plotResult.ImageData);
                
                // Verify file was written
                if (!File.Exists(filePath))
                {
                    fileSaveError = "File write completed but file not found at expected path. This may indicate filesystem isolation between MCP server and client.";
                }
            }
            catch (Exception ex)
            {
                fileSaveError = $"File save failed: {ex.Message}. This may be due to filesystem isolation between MCP server and client. Use 'image' output format instead.";
                filePath = null; // Clear path since save failed
            }
        }

        // Add file path info if file was saved (or attempted)
        if (!string.IsNullOrEmpty(filePath) || !string.IsNullOrEmpty(fileSaveError))
        {
            // Always include warning about filesystem isolation, even if file appears to save successfully
            // The file may exist in the server's filesystem but not be accessible from the client
            string isolationWarning = "IMPORTANT: Due to filesystem isolation, files saved by the MCP server may not be accessible from the client filesystem. " +
                "The file may exist in the server's filesystem but not be visible to the client. " +
                "For reliable access, use 'image' output format and save the base64-encoded image manually, " +
                "or use 'text' format for SVG and save the raw SVG string manually.";
            
            var fileInfo = new
            {
                file_path = filePath,
                file_size_bytes = filePath != null && File.Exists(filePath) ? new FileInfo(filePath).Length : (long?)null,
                status = string.IsNullOrEmpty(fileSaveError) ? "saved" : "failed",
                error = fileSaveError,
                warning = string.IsNullOrEmpty(fileSaveError) ? isolationWarning : null,
                note = string.IsNullOrEmpty(fileSaveError) ? isolationWarning : "Due to filesystem isolation, file saves may not be accessible from the client. Use 'image' output format and save manually if needed."
            };
            
            contentList.Add(new MCPContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(fileInfo, new JsonSerializerOptions { WriteIndented = true })
            });
        }

        // Check for potential I-V curve plotting issue (warning, not error)
        string? warning = null;
        if (analysisType == AnalysisType.DcSweep && plotType == PlotType.Line)
        {
            var hasVoltage = signalsToPlot.Any(s => s.StartsWith("v(", StringComparison.OrdinalIgnoreCase));
            var hasCurrent = signalsToPlot.Any(s => s.StartsWith("i(", StringComparison.OrdinalIgnoreCase));
            var hasXSignal = arguments.TryGetProperty("x_signal", out var xSignalElement) && !string.IsNullOrWhiteSpace(xSignalElement.GetString());
            
            if (hasVoltage && hasCurrent && !hasXSignal)
            {
                // User is plotting voltage and current separately - suggest I-V curve
                var voltageSignal = signalsToPlot.FirstOrDefault(s => s.StartsWith("v(", StringComparison.OrdinalIgnoreCase));
                var currentSignal = signalsToPlot.FirstOrDefault(s => s.StartsWith("i(", StringComparison.OrdinalIgnoreCase));
                if (voltageSignal != null && currentSignal != null)
                {
                    warning = $"Plotting voltage and current separately. For I-V characteristic curve, consider using plot_type='scatter' with x_signal='{voltageSignal}' and signals=['{currentSignal}']";
                }
            }
        }

        // Build response
        var response = new
        {
            success = true,
            analysis_type = cachedResults.AnalysisType,
            plot_type = plotTypeStr,
            signals_plotted = signalsToPlot,
            data_points = plotRequest.XData?.Length ?? 0,
            image_format = imageFormatStr,
            warning = warning
        };

        contentList.Add(new MCPContent
        {
            Type = "text",
            Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
        });

        return new MCPToolResult
        {
            Content = contentList
        };
    }

    private async Task<MCPToolResult> RunParameterSweep(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("component", out var componentElement))
            throw new ArgumentException("component is required");
        if (!arguments.TryGetProperty("start", out var startElement))
            throw new ArgumentException("start is required");
        if (!arguments.TryGetProperty("stop", out var stopElement))
            throw new ArgumentException("stop is required");
        if (!arguments.TryGetProperty("analysis_type", out var analysisTypeElement))
            throw new ArgumentException("analysis_type is required");
        if (!arguments.TryGetProperty("outputs", out var outputsElement))
            throw new ArgumentException("outputs is required");

        var component = componentElement.GetString() ?? throw new ArgumentException("component must be a string");
        var parameter = arguments.TryGetProperty("parameter", out var paramElement) 
            ? paramElement.GetString() ?? "value" 
            : "value";
        var start = startElement.GetDouble();
        var stop = stopElement.GetDouble();
        var points = arguments.TryGetProperty("points", out var pointsElement) 
            ? pointsElement.GetInt32() 
            : 20;
        var scale = arguments.TryGetProperty("scale", out var scaleElement) 
            ? scaleElement.GetString() ?? "linear" 
            : "linear";
        var analysisType = analysisTypeElement.GetString() ?? throw new ArgumentException("analysis_type must be a string");
        
        // Normalize analysis type (convert underscore to hyphen for ParameterSweepService)
        var normalizedAnalysisType = analysisType.Replace("_", "-").ToLower();
        
        // Validate analysis type is supported
        var supportedTypes = new[] { "operating-point", "ac", "dc", "transient" };
        if (!supportedTypes.Contains(normalizedAnalysisType))
        {
            throw new ArgumentException($"Unsupported analysis type '{analysisType}'. Supported types: operating_point (or operating-point), ac, dc, transient.");
        }

        // Get circuit ID (optional, uses active if omitted)
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("circuit_id is required (no active circuit)");
        }

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
        {
            throw new ArgumentException($"Circuit '{circuitId}' not found");
        }

        // Validate component/model exists
        // Check if this is a model parameter (model names typically contain underscores or are all caps)
        var isModelParameter = component.Contains("_") || component.All(c => char.IsUpper(c) || c == '_');
        
        if (isModelParameter)
        {
            // For model parameters, validate the model exists in the circuit
            var modelExists = circuit.GetSpiceSharpCircuit().TryGetEntity(component, out var modelEntity);
            if (!modelExists)
            {
                throw new ArgumentException($"Model '{component}' not found in circuit '{circuitId}'. Verify the model name is correct. Model parameter sweeps require the model to be defined in the circuit.");
            }
        }
        else
        {
            // For component parameters, validate the component exists
            var existingComponent = _componentService.GetComponent(circuit, component);
            if (existingComponent == null)
            {
                // Try to provide helpful error message with available components
                var availableComponents = new List<string>();
                try
                {
                    // Try to get component names from the circuit's internal entities
                    var entities = circuit.GetSpiceSharpCircuit();
                    foreach (var entity in entities)
                    {
                        if (entity is SpiceSharp.Components.Component)
                        {
                            availableComponents.Add(entity.Name);
                        }
                    }
                }
                catch
                {
                    // If we can't enumerate, just provide generic message
                }
                
                var componentList = availableComponents.Count > 0 
                    ? $" Available components: {string.Join(", ", availableComponents)}"
                    : "";
                
                throw new ArgumentException($"Component '{component}' not found in circuit '{circuitId}'.{componentList} Verify the component name is correct.");
            }
        }

        // Parse outputs
        var outputs = new List<string>();
        if (outputsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var output in outputsElement.EnumerateArray())
            {
                outputs.Add(output.GetString() ?? throw new ArgumentException("outputs must be strings"));
            }
        }
        
        if (outputs.Count == 0)
        {
            throw new ArgumentException("At least one output must be specified in the 'outputs' parameter. The outputs array cannot be empty. Example: outputs=[\"v(out)\"] or outputs=[\"v(out)\", \"i(R1)\"].");
        }

        // Validate points
        if (points <= 1)
        {
            throw new ArgumentException($"Parameter sweep requires at least 2 points for meaningful analysis. You specified {points} point(s). Minimum: 2 points.");
        }

        // Validate start and stop relationship
        if (start > stop)
        {
            throw new ArgumentException($"Invalid parameter range: start value ({start}) must be less than or equal to stop value ({stop}). For parameter sweep, ensure start ≤ stop.");
        }
        if (start == stop && points > 2)
        {
            throw new ArgumentException($"Invalid parameter range: start value ({start}) equals stop value ({stop}), but you requested {points} points. For a single value, use points=2.");
        }

        // Generate parameter values based on scale
        List<double> parameterValues;
        if (scale == "linear")
        {
            parameterValues = GenerateLinearSweepValues(start, stop, points);
        }
        else if (scale == "log" || scale == "decade")
        {
            if (start <= 0 || stop <= 0)
            {
                throw new ArgumentException($"Logarithmic scale requires positive values for both start and stop. Received: start={start}, stop={stop}. Logarithm is undefined for zero or negative numbers.");
            }
            if (start >= stop)
            {
                throw new ArgumentException($"For logarithmic scale, start value ({start}) must be less than stop value ({stop}).");
            }
            parameterValues = GenerateLogSweepValues(start, stop, points);
        }
        else
        {
            // Unknown scale, default to linear
            parameterValues = GenerateLinearSweepValues(start, stop, points);
        }

        // Build parameter path (e.g., "R1.value" or "C1.capacitance")
        var parameterPath = $"{component}.{parameter}";

        // Parse analysis_params if provided
        object? analysisConfig = null;
        if (arguments.TryGetProperty("analysis_params", out var analysisParamsElement) && 
            analysisParamsElement.ValueKind == JsonValueKind.Object)
        {
            var configDict = new Dictionary<string, object>();
            foreach (var prop in analysisParamsElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    configDict[prop.Name] = prop.Value.GetDouble();
                }
                else if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    configDict[prop.Name] = prop.Value.GetString()!;
                }
                else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                {
                    configDict[prop.Name] = prop.Value.GetBoolean();
                }
            }
            analysisConfig = configDict;
        }

        // For log scale, we need to generate log-spaced values and run sweep manually
        // For linear scale, use ParameterSweepService directly
        ParameterSweepResult sweepResult;
        try
        {
            if (scale == "log" || scale == "decade")
            {
                // Generate log-spaced values and run sweep manually
                _logger?.LogDebug("Running parameter sweep with log scale: circuit={CircuitId}, path={ParameterPath}, start={Start}, stop={Stop}, points={Points}, analysis={AnalysisType}", 
                    circuitId, parameterPath, start, stop, points, normalizedAnalysisType);
                
                sweepResult = RunParameterSweepWithLogScale(
                    circuit,
                    parameterPath,
                    parameterValues,
                    normalizedAnalysisType,
                    analysisConfig,
                    outputs);
            }
            else
            {
                // Use linear step for ParameterSweepService
                var step = (stop - start) / (points - 1);
                _logger?.LogDebug("Running parameter sweep (linear): circuit={CircuitId}, path={ParameterPath}, start={Start}, stop={Stop}, step={Step}, analysis={AnalysisType}", 
                    circuitId, parameterPath, start, stop, step, normalizedAnalysisType);
                
                sweepResult = _parameterSweepService.RunParameterSweep(
                    circuit,
                    parameterPath,
                    start,
                    stop,
                    step,
                    normalizedAnalysisType,
                    analysisConfig,
                    outputs);
            }
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Parameter sweep failed with argument error: {Message}", ex.Message);
            throw new ArgumentException($"Parameter sweep failed: {ex.Message}. Parameter path: '{parameterPath}', Component: '{component}', Parameter: '{parameter}'.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Parameter sweep failed with unexpected error: {Message}", ex.Message);
            throw new InvalidOperationException($"Parameter sweep failed: {ex.Message}. Parameter path: '{parameterPath}', Component: '{component}', Parameter: '{parameter}'. Inner exception: {ex.GetType().Name}.", ex);
        }

        // Store results in cache for plotting
        // For AC/transient, we'll flatten the data for plotting (parameter × frequency/time)
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "parameter_sweep",
            XData = sweepResult.ParameterValues.ToArray(),
            XLabel = $"{parameterPath}",
            Signals = sweepResult.Results.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToArray())
        };

        // For AC analysis, we could also cache the full frequency data
        // For now, the aggregated values in Results are cached for backward compatibility
        // The full AC/transient data is available in the response but not cached separately
        
        _resultsCache.Store(circuitId, cachedResult);

        // Build response with full AC/transient data if available
        var response = new
        {
            parameter_path = sweepResult.ParameterPath,
            parameter_values = sweepResult.ParameterValues,
            analysis_type = sweepResult.AnalysisType,
            results = sweepResult.Results,
            units = sweepResult.Units,
            analysis_time_ms = sweepResult.AnalysisTimeMs,
            status = sweepResult.Status,
            // Include full AC data if available
            ac_data = sweepResult.ACResults.Count > 0 ? new
            {
                frequencies = sweepResult.ACFrequencies,
                results = sweepResult.ACResults
            } : null,
            // Include full transient data if available
            transient_data = sweepResult.TransientResults.Count > 0 ? new
            {
                time = sweepResult.TransientTime,
                results = sweepResult.TransientResults
            } : null
        };

        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    /// <summary>
    /// Generate linear-spaced sweep values
    /// </summary>
    private List<double> GenerateLinearSweepValues(double start, double stop, int points)
    {
        var values = new List<double>();
        var step = (stop - start) / (points - 1);
        for (int i = 0; i < points; i++)
        {
            values.Add(start + i * step);
        }
        return values;
    }

    /// <summary>
    /// Generate logarithmically-spaced sweep values
    /// </summary>
    private List<double> GenerateLogSweepValues(double start, double stop, int points)
    {
        var values = new List<double>();
        var logStart = Math.Log10(start);
        var logStop = Math.Log10(stop);
        var logStep = (logStop - logStart) / (points - 1);
        
        for (int i = 0; i < points; i++)
        {
            var logValue = logStart + i * logStep;
            values.Add(Math.Pow(10, logValue));
        }
        return values;
    }

    /// <summary>
    /// Run parameter sweep with pre-generated log-spaced values
    /// For log scale, we call ParameterSweepService for each value individually
    /// </summary>
    private ParameterSweepResult RunParameterSweepWithLogScale(
        CircuitModel circuit,
        string parameterPath,
        List<double> parameterValues,
        string analysisType,
        object? analysisConfig,
        List<string> outputs)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = new ParameterSweepResult
        {
            ParameterPath = parameterPath,
            ParameterValues = parameterValues,
            AnalysisType = analysisType,
            Status = "Success"
        };

        // Initialize results dictionary
        foreach (var output in outputs)
        {
            result.Results[output] = new List<double>();
            result.Units[output] = GetUnitForExport(output);
        }

        // For each log-spaced value, run a single-point sweep
        // This is less efficient but ensures correct log spacing
        foreach (var paramValue in parameterValues)
        {
            // Use a tiny step to get exactly one point
            var tinyStep = Math.Abs(paramValue) * 1e-10; // Very small relative step
            
            try
            {
                var singlePointResult = _parameterSweepService.RunParameterSweep(
                    circuit,
                    parameterPath,
                    paramValue,
                    paramValue + tinyStep, // Stop just slightly above start
                    tinyStep,
                    analysisType,
                    analysisConfig,
                    outputs);
                
                // Extract the first (and only) result for each output
                foreach (var output in outputs)
                {
                    if (singlePointResult.Results.TryGetValue(output, out var values) && values.Count > 0)
                    {
                        result.Results[output].Add(values[0]);
                    }
                    else
                    {
                        result.Results[output].Add(double.NaN);
                    }
                    
                    // Also store full AC/transient data if available
                    if (singlePointResult.ACResults.TryGetValue(output, out var acData) && acData.Count > 0)
                    {
                        if (!result.ACResults.ContainsKey(output))
                        {
                            result.ACResults[output] = new List<List<double>>();
                        }
                        result.ACResults[output].Add(new List<double>(acData[0]));
                        
                        if (result.ACFrequencies == null && singlePointResult.ACFrequencies != null)
                        {
                            result.ACFrequencies = new List<double>(singlePointResult.ACFrequencies);
                        }
                    }
                    
                    if (singlePointResult.TransientResults.TryGetValue(output, out var transData) && transData.Count > 0)
                    {
                        if (!result.TransientResults.ContainsKey(output))
                        {
                            result.TransientResults[output] = new List<List<double>>();
                        }
                        result.TransientResults[output].Add(new List<double>(transData[0]));
                        
                        if (result.TransientTime == null && singlePointResult.TransientTime != null)
                        {
                            result.TransientTime = new List<double>(singlePointResult.TransientTime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to run sweep point at value {Value}: {Message}", paramValue, ex.Message);
                // Add NaN for failed points
                foreach (var output in outputs)
                {
                    result.Results[output].Add(double.NaN);
                }
            }
        }

        stopwatch.Stop();
        result.AnalysisTimeMs = stopwatch.Elapsed.TotalMilliseconds;

        return result;
    }

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

    private async Task<MCPToolResult> RunNoiseAnalysis(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("output_node", out var outputNodeElement))
            throw new ArgumentException("output_node is required");

        var outputNode = outputNodeElement.GetString() ?? throw new ArgumentException("output_node must be a string");
        var referenceNode = arguments.TryGetProperty("reference_node", out var refNodeElement) 
            ? refNodeElement.GetString() ?? "0" 
            : "0";
        var inputSource = arguments.TryGetProperty("input_source", out var inputSourceElement) 
            ? inputSourceElement.GetString() 
            : null;
        var startFreq = arguments.TryGetProperty("start_freq", out var startFreqElement) 
            ? startFreqElement.GetDouble() 
            : 20.0;
        var stopFreq = arguments.TryGetProperty("stop_freq", out var stopFreqElement) 
            ? stopFreqElement.GetDouble() 
            : 20000.0;
        var pointsPerDecade = arguments.TryGetProperty("points_per_decade", out var pointsElement) 
            ? pointsElement.GetInt32() 
            : 10;

        // Calculate number of points from points_per_decade
        var decades = Math.Log10(stopFreq / startFreq);
        var numberOfPoints = (int)Math.Ceiling(decades * pointsPerDecade) + 1;

        // Get circuit ID (optional, uses active if omitted)
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("circuit_id is required (no active circuit)");
        }

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
        {
            throw new ArgumentException($"Circuit '{circuitId}' not found");
        }

        if (string.IsNullOrWhiteSpace(inputSource))
        {
            throw new ArgumentException("input_source is required for noise analysis");
        }

        try
        {
            _logger?.LogDebug("Running noise analysis: circuit={CircuitId}, output={OutputNode}, input={InputSource}, freq={StartFreq}-{StopFreq}Hz, points={Points}", 
                circuitId, outputNode, inputSource, startFreq, stopFreq, numberOfPoints);

            var result = _noiseAnalysisService.RunNoiseAnalysis(
                circuit,
                startFreq,
                stopFreq,
                numberOfPoints,
                outputNode,
                inputSource);

            // Build response
            var response = new
            {
                frequencies = result.Frequencies,
                output_noise_density = result.OutputNoiseDensity,
                input_referred_noise_density = result.InputReferredNoiseDensity,
                total_output_noise = result.TotalOutputNoise,
                total_input_referred_noise = result.TotalInputReferredNoise,
                output_node = result.OutputNode,
                input_source = result.InputSource,
                analysis_time_ms = result.AnalysisTimeMs,
                status = result.Status
            };

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (NotImplementedException ex)
        {
            // Noise analysis is not yet supported by SpiceSharp
            // Return a clear error message with helpful information
            _logger?.LogWarning("Noise analysis requested but not yet supported: {Message}", ex.Message);
            var helpfulMessage = 
                "Noise analysis is not yet supported by SpiceSharp. " +
                "The API structure is in place and will be implemented when SpiceSharp adds noise analysis support. " +
                "There is currently no scheduled release date for this feature. " +
                "For now, you can use AC analysis to analyze frequency response. " +
                "Monitor https://github.com/SpiceSharp/SpiceSharp for updates.";
            throw new NotImplementedException(helpfulMessage, ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Noise analysis failed: {Message}", ex.Message);
            throw new InvalidOperationException($"Noise analysis failed: {ex.Message}", ex);
        }
    }

    private async Task<MCPToolResult> RunTemperatureSweep(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("analysis_type", out var analysisTypeElement))
            throw new ArgumentException("analysis_type is required");
        if (!arguments.TryGetProperty("outputs", out var outputsElement))
            throw new ArgumentException("outputs is required");

        var analysisType = analysisTypeElement.GetString() ?? throw new ArgumentException("analysis_type must be a string");
        var startTemp = arguments.TryGetProperty("start_temp", out var startTempElement) 
            ? startTempElement.GetDouble() 
            : -40.0;
        var stopTemp = arguments.TryGetProperty("stop_temp", out var stopTempElement) 
            ? stopTempElement.GetDouble() 
            : 85.0;
        var points = arguments.TryGetProperty("points", out var pointsElement) 
            ? pointsElement.GetInt32() 
            : 10;

        // Calculate step from points
        var stepTemp = (stopTemp - startTemp) / (points - 1);

        // Parse outputs
        var outputs = new List<string>();
        foreach (var output in outputsElement.EnumerateArray())
        {
            outputs.Add(output.GetString() ?? throw new ArgumentException("outputs must be strings"));
        }

        // Parse analysis_params if provided
        object? analysisConfig = null;
        if (arguments.TryGetProperty("analysis_params", out var analysisParamsElement) && analysisParamsElement.ValueKind == JsonValueKind.Object)
        {
            analysisConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(analysisParamsElement.GetRawText());
        }

        // Get circuit ID (optional, uses active if omitted)
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("circuit_id is required (no active circuit)");
        }

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
        {
            throw new ArgumentException($"Circuit '{circuitId}' not found");
        }

        // Normalize analysis type (convert underscore to hyphen for TemperatureSweepService)
        var normalizedAnalysisType = analysisType.Replace("_", "-").ToLower();

        // Validate analysis type is supported
        var supportedTypes = new[] { "operating-point", "ac", "dc", "transient" };
        if (!supportedTypes.Contains(normalizedAnalysisType))
        {
            throw new ArgumentException($"Unsupported analysis type '{analysisType}'. Supported types: operating_point (or operating-point), ac, dc, transient.");
        }

        try
        {
            _logger?.LogDebug("Running temperature sweep: circuit={CircuitId}, temp={StartTemp}-{StopTemp}°C, step={StepTemp}°C, analysis={AnalysisType}", 
                circuitId, startTemp, stopTemp, stepTemp, normalizedAnalysisType);

            var result = _temperatureSweepService.RunTemperatureSweep(
                circuit,
                startTemp,
                stopTemp,
                stepTemp,
                normalizedAnalysisType,
                analysisConfig,
                outputs);

            // Store results in cache for plotting
            var cachedResult = new CachedAnalysisResult
            {
                AnalysisType = "temperature_sweep",
                XData = result.TemperatureValues.ToArray(),
                XLabel = "Temperature (°C)",
                Signals = result.Results.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToArray())
            };

            _resultsCache.Store(circuitId, cachedResult);

            // Build response
            var response = new
            {
                temperature_values = result.TemperatureValues,
                analysis_type = result.AnalysisType,
                results = result.Results,
                units = result.Units,
                analysis_time_ms = result.AnalysisTimeMs,
                status = result.Status
            };

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Temperature sweep failed with argument error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Temperature sweep failed with unexpected error: {Message}", ex.Message);
            throw new InvalidOperationException($"Temperature sweep failed: {ex.Message}", ex);
        }
    }

    private async Task<MCPToolResult> PlotImpedance(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("port_positive", out var portPositiveElement))
            throw new ArgumentException("port_positive is required");

        var portPositive = portPositiveElement.GetString() ?? throw new ArgumentException("port_positive must be a string");
        var portNegative = arguments.TryGetProperty("port_negative", out var portNegativeElement) 
            ? portNegativeElement.GetString() ?? "0" 
            : "0";
        var startFreq = arguments.TryGetProperty("start_freq", out var startFreqElement) 
            ? startFreqElement.GetDouble() 
            : 20.0;
        var stopFreq = arguments.TryGetProperty("stop_freq", out var stopFreqElement) 
            ? stopFreqElement.GetDouble() 
            : 20000.0;
        var pointsPerDecade = arguments.TryGetProperty("points_per_decade", out var pointsElement) 
            ? pointsElement.GetInt32() 
            : 20;
        var format = arguments.TryGetProperty("format", out var formatElement) 
            ? formatElement.GetString()?.ToLowerInvariant() ?? "png" 
            : "png";

        // Parse output formats
        var outputFormats = new List<string> { "image" };
        if (arguments.TryGetProperty("output_format", out var outputFormatElement) && outputFormatElement.ValueKind == JsonValueKind.Array)
        {
            outputFormats.Clear();
            foreach (var fmt in outputFormatElement.EnumerateArray())
            {
                var fmtStr = fmt.GetString();
                if (!string.IsNullOrEmpty(fmtStr))
                {
                    outputFormats.Add(fmtStr.ToLowerInvariant());
                }
            }
        }

        // Calculate number of points from points_per_decade
        var decades = Math.Log10(stopFreq / Math.Max(startFreq, 1e-30));
        var numberOfPoints = Math.Max(2, (int)Math.Ceiling(decades * pointsPerDecade) + 1);

        // Get circuit ID (optional, uses active if omitted)
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("circuit_id is required (no active circuit)");
        }

        var circuit = _circuitManager.GetCircuit(circuitId);
        if (circuit == null)
        {
            throw new ArgumentException($"Circuit '{circuitId}' not found");
        }

        // Calculate circuit complexity stats for error reporting
        // Use reflection to access internal ComponentDefinitions property
        var componentDefinitionsProp = typeof(CircuitModel).GetProperty("ComponentDefinitions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var componentDefinitions = componentDefinitionsProp?.GetValue(circuit) as Dictionary<string, ComponentDefinition> ?? new Dictionary<string, ComponentDefinition>();
        
        var componentCount = componentDefinitions.Count;
        var subcircuitCount = componentDefinitions.Values.Count(c => c.ComponentType == "subcircuit");
        var internalNodes = new HashSet<string>();
        foreach (var comp in componentDefinitions.Values)
        {
            if (comp.Nodes != null)
            {
                foreach (var node in comp.Nodes)
                {
                    if (node != "0" && node != portPositive && node != portNegative)
                    {
                        internalNodes.Add(node);
                    }
                }
            }
        }
        var circuitStats = new
        {
            total_components = componentCount,
            subcircuits = subcircuitCount,
            internal_nodes = internalNodes.Count,
            estimated_complexity = componentCount > 10 || subcircuitCount > 1 ? "high" : componentCount > 5 ? "medium" : "low"
        };

        try
        {
            _logger?.LogDebug("Plotting impedance: circuit={CircuitId}, port={PortPos}-{PortNeg}, freq={StartFreq}-{StopFreq}Hz, points={Points}, complexity={Complexity}", 
                circuitId, portPositive, portNegative, startFreq, stopFreq, numberOfPoints, circuitStats.estimated_complexity);

            // Calculate impedance - wrap in try-catch to identify stage
            ImpedanceAnalysisResult impedanceResult;
            try
            {
                impedanceResult = _impedanceAnalysisService.CalculateImpedance(
                    circuit,
                    portPositive,
                    portNegative,
                    startFreq,
                    stopFreq,
                    numberOfPoints);
            }
            catch (Exception ex) when (ex.Message.Contains("AC analysis") || ex.Message.Contains("ACAnalysis") || ex.Message.Contains("converge"))
            {
                _logger?.LogError(ex, "AC analysis stage failed for impedance calculation");
                throw new InvalidOperationException(
                    $"Impedance calculation failed at AC analysis stage for port '{portPositive}' to '{portNegative}'. " +
                    $"Circuit complexity: {circuitStats.total_components} components ({circuitStats.subcircuits} subcircuits), {circuitStats.internal_nodes} internal nodes. " +
                    $"Error: {ex.Message}. " +
                    $"This may indicate convergence issues, matrix singularity, or circuit topology problems. " +
                    $"Try: (1) Validate circuit with validate_circuit, (2) Check for parallel inductors or reactive loops, (3) Simplify circuit topology.", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("DC") || ex.Message.Contains("operating point"))
            {
                _logger?.LogError(ex, "DC operating point stage failed for impedance calculation");
                throw new InvalidOperationException(
                    $"Impedance calculation failed at DC operating point stage for port '{portPositive}' to '{portNegative}'. " +
                    $"Circuit complexity: {circuitStats.total_components} components ({circuitStats.subcircuits} subcircuits), {circuitStats.internal_nodes} internal nodes. " +
                    $"Error: {ex.Message}. " +
                    $"AC analysis requires a valid DC operating point. " +
                    $"Try: (1) Ensure all nodes have DC paths to ground, (2) Check for floating nodes, (3) Verify subcircuit definitions are complete.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Impedance calculation stage failed (unknown stage)");
                throw new InvalidOperationException(
                    $"Impedance calculation failed for port '{portPositive}' to '{portNegative}'. " +
                    $"Circuit complexity: {circuitStats.total_components} components ({circuitStats.subcircuits} subcircuits), {circuitStats.internal_nodes} internal nodes. " +
                    $"Error: {ex.Message}. " +
                    $"Exception type: {ex.GetType().Name}. " +
                    $"This may indicate a circuit topology issue or analysis convergence problem.", ex);
            }

            if (impedanceResult.Frequencies.Count == 0 || impedanceResult.Magnitude.Count == 0)
            {
                throw new ArgumentException($"Impedance calculation returned no data. Port nodes '{portPositive}' and '{portNegative}' may not be valid or connected in the circuit. " +
                    "Ensure the port nodes exist and are connected to components.");
            }

            // Create plot using Bode plot type (magnitude and phase)
            var imageFormat = format == "png" ? ImageFormat.Png : ImageFormat.Svg;
            
            // Convert impedance magnitude and phase to complex form (real + j*imag)
            // Z = |Z| * e^(j*phase) = |Z| * (cos(phase) + j*sin(phase))
            // real = |Z| * cos(phase), imag = |Z| * sin(phase)
            var realValues = new List<double>();
            var imagValues = new List<double>();
            
            for (int i = 0; i < impedanceResult.Magnitude.Count && i < impedanceResult.Phase.Count; i++)
            {
                var magnitude = impedanceResult.Magnitude[i];
                var phaseRad = impedanceResult.Phase[i] * Math.PI / 180.0; // Convert degrees to radians
                realValues.Add(magnitude * Math.Cos(phaseRad));
                imagValues.Add(magnitude * Math.Sin(phaseRad));
            }

            // Create data series with complex data (Bode plot will calculate magnitude and phase)
            var impedanceSeries = new DataSeries
            {
                Name = $"Z ({portPositive}-{portNegative})",
                Values = realValues.ToArray(),
                ImagValues = imagValues.ToArray()
            };

            // Build plot request
            var plotRequest = new PlotRequest
            {
                AnalysisType = AnalysisType.Ac,
                PlotType = PlotType.Bode, // Two-panel plot with magnitude and phase
                ImageFormat = imageFormat,
                XData = impedanceResult.Frequencies.ToArray(),
                XLabel = "Frequency (Hz)",
                Series = new List<DataSeries> { impedanceSeries },
                Options = new PlotOptions
                {
                    Title = $"Impedance: {portPositive}-{portNegative}",
                    YLabel = "Magnitude (dBΩ) / Phase (°)", // Bode plot will show both
                    XScale = ScaleType.Log,
                    YScale = ScaleType.Linear,
                    ShowGrid = true,
                    ShowLegend = true
                }
            };

            // Generate plot
            var plotGenerator = PlotGeneratorFactory.Create();
            var plotResult = plotGenerator.GeneratePlot(plotRequest);

            if (!plotResult.Success)
            {
                throw new InvalidOperationException($"Plot generation failed: {plotResult.ErrorMessage}");
            }

            // Build response
            var contentList = new List<MCPContent>();

            // Add text content (for SVG when text format is requested, or always for SVG when image format is requested)
            if (plotResult.ImageData != null && imageFormat == ImageFormat.Svg)
            {
                bool shouldAddText = outputFormats.Contains("text", StringComparer.OrdinalIgnoreCase) ||
                                     outputFormats.Contains("image", StringComparer.OrdinalIgnoreCase);
                
                if (shouldAddText)
                {
                    var svgText = System.Text.Encoding.UTF8.GetString(plotResult.ImageData);
                    contentList.Add(new MCPContent
                    {
                        Type = "text",
                        Text = svgText,
                        MimeType = "image/svg+xml"
                    });
                }
            }

            // Add image content (base64)
            if (outputFormats.Contains("image", StringComparer.OrdinalIgnoreCase) && plotResult.ImageData != null)
            {
                if (imageFormat == ImageFormat.Svg)
                {
                    // SVG image format (base64) - note: may not display in all clients
                    var base64Image = Convert.ToBase64String(plotResult.ImageData);
                    contentList.Add(new MCPContent
                    {
                        Type = "image",
                        Data = base64Image,
                        MimeType = "image/svg+xml"
                    });
                }
                else
                {
                    // PNG format - standard image output
                    var base64Image = Convert.ToBase64String(plotResult.ImageData);
                    contentList.Add(new MCPContent
                    {
                        Type = "image",
                        Data = base64Image,
                        MimeType = "image/png"
                    });
                }
            }

            // Save to file if requested
            string? filePath = null;
            string? fileSaveError = null;
            if (outputFormats.Contains("file", StringComparer.OrdinalIgnoreCase) && plotResult.ImageData != null)
            {
                // Get file path
                if (arguments.TryGetProperty("file_path", out var filePathElement))
                {
                    filePath = filePathElement.GetString();
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    // Generate default file path
                    var extension = imageFormat == ImageFormat.Svg ? ".svg" : ".png";
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    filePath = Path.Combine(Path.GetTempPath(), $"impedance_{circuitId}_{portPositive}_{timestamp}{extension}");
                }

                // Try to save file with error handling
                try
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write file
                    await File.WriteAllBytesAsync(filePath, plotResult.ImageData);
                    
                    // Verify file was written
                    if (!File.Exists(filePath))
                    {
                        fileSaveError = "File write completed but file not found at expected path. This may indicate filesystem isolation between MCP server and client.";
                    }
                }
                catch (Exception ex)
                {
                    fileSaveError = $"Failed to save file: {ex.Message}";
                }
            }

            // Build response with file path info if file format was requested
            var responseText = new System.Text.StringBuilder();
            if (outputFormats.Contains("file", StringComparer.OrdinalIgnoreCase))
            {
                responseText.Append(JsonSerializer.Serialize(new
                {
                    file_path = filePath,
                    file_saved = !string.IsNullOrEmpty(filePath) && string.IsNullOrEmpty(fileSaveError),
                    file_error = fileSaveError
                }, new JsonSerializerOptions { WriteIndented = true }));
            }

            // If we have response text, add it as a separate content item
            if (responseText.Length > 0)
            {
                contentList.Add(new MCPContent
                {
                    Type = "text",
                    Text = responseText.ToString(),
                    MimeType = "application/json"
                });
            }

            return new MCPToolResult
            {
                Content = contentList
            };
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Impedance plot failed with argument error: {Message}", ex.Message);
            // Preserve ArgumentException with detailed message including circuit stats
            throw new ArgumentException(
                $"Impedance calculation failed for port '{portPositive}' to '{portNegative}': {ex.Message}. " +
                $"Circuit complexity: {circuitStats.total_components} components ({circuitStats.subcircuits} subcircuits), {circuitStats.internal_nodes} internal nodes. " +
                "Common causes: (1) Port nodes not connected to circuit, (2) Circuit lacks DC path to ground (required for AC analysis), " +
                "(3) Voltage source already exists at measurement port. " +
                "Suggestion: Validate circuit topology and ensure all nodes have DC paths to ground.", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Impedance plot failed with operation error: {Message}", ex.Message);
            // If the exception already contains stage information, preserve it; otherwise add circuit stats
            if (ex.Message.Contains("stage") || ex.Message.Contains("Circuit complexity"))
            {
                // Already has detailed info, just re-throw
                throw;
            }
            else
            {
                // Add circuit stats to existing message
                throw new InvalidOperationException(
                    $"Impedance calculation failed for port '{portPositive}' to '{portNegative}': {ex.Message}. " +
                    $"Circuit complexity: {circuitStats.total_components} components ({circuitStats.subcircuits} subcircuits), {circuitStats.internal_nodes} internal nodes. " +
                    "This often indicates a circuit topology issue (e.g., missing DC path, floating nodes, or validation failure). " +
                    "Try: (1) Validate circuit with validate_circuit tool, (2) Ensure all nodes have DC paths to ground, " +
                    "(3) Check for floating nodes or disconnected components.", ex);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Impedance plot failed with unexpected error: {Message}", ex.Message);
            throw new InvalidOperationException(
                $"Impedance calculation failed for port '{portPositive}' to '{portNegative}': {ex.Message}. " +
                $"Circuit complexity: {circuitStats.total_components} components ({circuitStats.subcircuits} subcircuits), {circuitStats.internal_nodes} internal nodes. " +
                $"Error type: {ex.GetType().Name}. " +
                "This may indicate a circuit topology issue, missing DC path, or internal error. " +
                "Try validating the circuit and checking for DC path requirements.", ex);
        }
    }

    private async Task<MCPToolResult> MeasureResponse(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("measurement", out var measurementElement))
            throw new ArgumentException("measurement is required");

        if (!arguments.TryGetProperty("signal", out var signalElement))
            throw new ArgumentException("signal is required");

        var measurement = measurementElement.GetString() ?? throw new ArgumentException("measurement must be a string");
        var signal = signalElement.GetString() ?? throw new ArgumentException("signal must be a string");

        // Get optional parameters
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("circuit_id is required (no active circuit)");
        }

        string? reference = null;
        if (arguments.TryGetProperty("reference", out var referenceElement))
        {
            reference = referenceElement.GetString();
        }

        double? frequency = null;
        if (arguments.TryGetProperty("frequency", out var frequencyElement))
        {
            frequency = frequencyElement.GetDouble();
        }

        double? threshold = null;
        if (arguments.TryGetProperty("threshold", out var thresholdElement))
        {
            threshold = thresholdElement.GetDouble();
        }

        string? analysisId = null;
        if (arguments.TryGetProperty("analysis_id", out var analysisIdElement))
        {
            analysisId = analysisIdElement.GetString();
        }

        try
        {
            _logger?.LogDebug("Measuring response: circuit={CircuitId}, measurement={Measurement}, signal={Signal}", 
                circuitId, measurement, signal);

            // Perform measurement
            var result = _responseMeasurementService.Measure(
                circuitId,
                measurement,
                signal,
                reference,
                frequency,
                threshold,
                analysisId);

            // Return measurement result
            var response = new
            {
                circuit_id = circuitId,
                measurement = measurement,
                signal = signal,
                value = result.Value,
                unit = result.Unit,
                description = result.Description
            };

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Measurement failed with argument error: {Message}", ex.Message);
            // Preserve ArgumentException with clear message - it's already user-friendly
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Measurement failed with operation error: {Message}", ex.Message);
            // Preserve InvalidOperationException - it often contains helpful context
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Measurement failed with unexpected error: {Message}", ex.Message);
            // Wrap unexpected exceptions but preserve the original message
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private async Task<MCPToolResult> CalculateGroupDelay(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("signal", out var signalElement))
            throw new ArgumentException("signal is required");

        var signal = signalElement.GetString() ?? throw new ArgumentException("signal must be a string");
        var reference = arguments.TryGetProperty("reference", out var referenceElement) 
            ? referenceElement.GetString() 
            : null;
        var format = arguments.TryGetProperty("format", out var formatElement) 
            ? formatElement.GetString()?.ToLowerInvariant() ?? "png" 
            : "png";

        // Get circuit ID (optional, uses active if omitted)
        string? circuitId = null;
        if (arguments.TryGetProperty("circuit_id", out var circuitIdElement))
        {
            circuitId = circuitIdElement.GetString();
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            var activeCircuit = _circuitManager.GetActiveCircuit();
            if (activeCircuit != null)
            {
                circuitId = activeCircuit.Id;
            }
        }

        if (string.IsNullOrEmpty(circuitId))
        {
            throw new ArgumentException("circuit_id is required (no active circuit)");
        }

        try
        {
            _logger?.LogDebug("Calculating group delay: circuit={CircuitId}, signal={Signal}", 
                circuitId, signal);

            // Calculate group delay
            var result = _groupDelayService.CalculateGroupDelay(circuitId, signal, reference);

            if (result.Frequencies.Count == 0 || result.GroupDelay.Count == 0)
            {
                throw new InvalidOperationException($"Group delay calculation returned no data. Ensure AC analysis has been run for circuit '{circuitId}' with signal '{signal}'.");
            }

            // Create plot using line plot type
            var imageFormat = format == "png" ? ImageFormat.Png : ImageFormat.Svg;
            
            // Create data series for group delay (convert seconds to milliseconds for display)
            var groupDelayMs = result.GroupDelay.Select(gd => gd * 1000.0).ToArray(); // Convert to ms
            
            var groupDelaySeries = new DataSeries
            {
                Name = $"Group Delay ({signal})",
                Values = groupDelayMs
            };

            // Build plot request
            var plotRequest = new PlotRequest
            {
                AnalysisType = AnalysisType.Ac,
                PlotType = PlotType.Line, // Line plot for group delay vs frequency
                ImageFormat = imageFormat,
                XData = result.Frequencies.ToArray(),
                XLabel = "Frequency (Hz)",
                Series = new List<DataSeries> { groupDelaySeries },
                Options = new PlotOptions
                {
                    Title = $"Group Delay: {signal}",
                    YLabel = "Group Delay (ms)",
                    XScale = ScaleType.Log,
                    YScale = ScaleType.Linear,
                    ShowGrid = true,
                    ShowLegend = true
                }
            };

            // Generate plot
            var plotGenerator = PlotGeneratorFactory.Create();
            var plotResult = plotGenerator.GeneratePlot(plotRequest);

            if (!plotResult.Success)
            {
                throw new InvalidOperationException($"Plot generation failed: {plotResult.ErrorMessage}");
            }

            // Build response
            var contentList = new List<MCPContent>();

            if (plotResult.ImageData != null)
            {
                var base64Image = Convert.ToBase64String(plotResult.ImageData);
                var mimeType = imageFormat == ImageFormat.Svg ? "image/svg+xml" : "image/png";
                
                contentList.Add(new MCPContent
                {
                    Type = "image",
                    Data = base64Image,
                    MimeType = mimeType
                });
            }

            // Also include text summary
            var summary = new
            {
                circuit_id = circuitId,
                signal = signal,
                reference = reference,
                frequency_range_hz = new { min = result.Frequencies.Min(), max = result.Frequencies.Max() },
                group_delay_range_ms = new { min = result.GroupDelay.Min() * 1000.0, max = result.GroupDelay.Max() * 1000.0 },
                analysis_time_ms = result.AnalysisTimeMs,
                status = result.Status
            };

            contentList.Add(new MCPContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true })
            });

            return new MCPToolResult
            {
                Content = contentList
            };
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Group delay calculation failed with argument error: {Message}", ex.Message);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Group delay calculation failed with operation error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Group delay calculation failed with unexpected error: {Message}", ex.Message);
            throw new InvalidOperationException($"Group delay calculation failed: {ex.Message}", ex);
        }
    }

    private async Task<MCPToolResult> ReindexLibraries(JsonElement arguments)
    {
        try
        {
            // Check if library service is configured
            if (_libraryService == null)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Library service is not configured",
                                message = "Library service is not available. Configure LibraryPaths in MCPServerConfig to enable library indexing."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            if (_config.LibraryPaths == null || !_config.LibraryPaths.Any())
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "No library paths configured",
                                message = "No library paths are configured. Set MCPServerConfig.LibraryPaths to specify directories containing .lib files."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            // Re-index libraries
            _libraryService.IndexLibraries(_config.LibraryPaths);

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            success = true,
                            message = "Libraries re-indexed successfully",
                            library_paths = _config.LibraryPaths,
                            note = "All .lib files have been re-parsed and the speaker database has been updated with corrected metadata."
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Re-index libraries failed: {Message}", ex.Message);
            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            error = "Re-index failed",
                            message = $"Failed to re-index libraries: {ex.Message}"
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
    }

    private async Task<MCPToolResult> ImportNetlist(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("netlist", out var netlistElement))
            throw new ArgumentException("netlist is required");

        var netlist = netlistElement.GetString() ?? throw new ArgumentException("netlist must be a string");
        var circuitName = arguments.TryGetProperty("circuit_name", out var circuitNameElement) 
            ? circuitNameElement.GetString() 
            : "Imported Circuit";
        var setActive = arguments.TryGetProperty("set_active", out var setActiveElement) 
            ? setActiveElement.GetBoolean() 
            : true;

        try
        {
            _logger?.LogDebug("Importing netlist: circuit_name={CircuitName}, set_active={SetActive}", 
                circuitName, setActive);

            // Parse the netlist
            var parsedNetlist = _netlistParser.ParseNetlist(netlist);

            // Create circuit
            var circuit = _circuitManager.CreateCircuit(circuitName ?? "Imported Circuit", $"Imported from netlist");

            // Track failures for error reporting
            var failedComponents = new List<object>();
            var failedModels = new List<object>();
            var errors = new List<string>();
            var warnings = new List<string>();

            // Add models first (components may reference them)
            var modelsAdded = 0;
            foreach (var model in parsedNetlist.Models)
            {
                try
                {
                    _modelService.DefineModel(circuit, model);
                    modelsAdded++;
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Model '{model.ModelName}': {ex.Message}";
                    failedModels.Add(new
                    {
                        name = model.ModelName,
                        type = model.ModelType,
                        error = ex.Message
                    });
                    errors.Add(errorMessage);
                    _logger?.LogWarning(ex, "Failed to add model {ModelName}: {Message}", model.ModelName, ex.Message);
                    // Continue with other models
                }
            }

            // Add components
            var componentsAdded = 0;
            foreach (var component in parsedNetlist.Components)
            {
                try
                {
                    _componentService.AddComponent(circuit, component);
                    componentsAdded++;
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Component '{component.Name}': {ex.Message}";
                    failedComponents.Add(new
                    {
                        name = component.Name,
                        type = component.ComponentType,
                        error = ex.Message
                    });
                    errors.Add(errorMessage);
                    _logger?.LogWarning(ex, "Failed to add component {ComponentName}: {Message}", component.Name, ex.Message);
                    // Continue with other components
                }
            }

            // Set as active if requested
            if (setActive)
            {
                _circuitManager.SetActiveCircuit(circuit.Id);
            }

            // Determine status based on failures
            string status;
            if (failedComponents.Count == 0 && failedModels.Count == 0)
            {
                status = "Success";
            }
            else if (componentsAdded > 0 || modelsAdded > 0)
            {
                status = "Partial Success";
            }
            else
            {
                status = "Failed";
            }

            // Build response
            var summary = new
            {
                circuit_id = circuit.Id,
                circuit_name = circuitName,
                components_added = componentsAdded,
                models_added = modelsAdded,
                total_components = parsedNetlist.Components.Count,
                total_models = parsedNetlist.Models.Count,
                failed_components = failedComponents.Count > 0 ? failedComponents : null,
                failed_models = failedModels.Count > 0 ? failedModels : null,
                errors = errors.Count > 0 ? errors : null,
                warnings = warnings.Count > 0 ? warnings : null,
                is_active = setActive && _circuitManager.GetActiveCircuit()?.Id == circuit.Id,
                status = status
            };

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Netlist import failed with argument error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Netlist import failed with unexpected error: {Message}", ex.Message);
            throw new InvalidOperationException($"Netlist import failed: {ex.Message}", ex);
        }
    }

    private async Task<MCPToolResult> SearchSpeakersByParameters(JsonElement arguments)
    {
        if (_speakerDatabaseService == null)
        {
            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            error = "Speaker database service is not configured",
                            message = "Speaker database service is not available. To enable speaker search, configure ISpeakerDatabaseService in dependency injection."
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }

        try
        {
            // Parse search parameters
            var parameters = new SpeakerSearchParameters();

            if (arguments.TryGetProperty("driver_type", out var driverTypeElement) && driverTypeElement.ValueKind == JsonValueKind.Array)
            {
                parameters.DriverType = driverTypeElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList()!;
            }

            if (arguments.TryGetProperty("diameter_min", out var diameterMinElement) && diameterMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.DiameterMin = diameterMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("diameter_max", out var diameterMaxElement) && diameterMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.DiameterMax = diameterMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("impedance", out var impedanceElement) && impedanceElement.ValueKind == JsonValueKind.Number)
            {
                parameters.Impedance = impedanceElement.GetInt32();
            }

            if (arguments.TryGetProperty("fs_min", out var fsMinElement) && fsMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.FsMin = fsMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("fs_max", out var fsMaxElement) && fsMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.FsMax = fsMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("qts_min", out var qtsMinElement) && qtsMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.QtsMin = qtsMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("qts_max", out var qtsMaxElement) && qtsMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.QtsMax = qtsMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("qes_min", out var qesMinElement) && qesMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.QesMin = qesMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("qes_max", out var qesMaxElement) && qesMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.QesMax = qesMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("qms_min", out var qmsMinElement) && qmsMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.QmsMin = qmsMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("qms_max", out var qmsMaxElement) && qmsMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.QmsMax = qmsMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("vas_min", out var vasMinElement) && vasMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.VasMin = vasMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("vas_max", out var vasMaxElement) && vasMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.VasMax = vasMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("sensitivity_min", out var sensitivityMinElement) && sensitivityMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.SensitivityMin = sensitivityMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("sensitivity_max", out var sensitivityMaxElement) && sensitivityMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.SensitivityMax = sensitivityMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("power_min", out var powerMinElement) && powerMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.PowerMin = powerMinElement.GetInt32();
            }

            if (arguments.TryGetProperty("power_max", out var powerMaxElement) && powerMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.PowerMax = powerMaxElement.GetInt32();
            }

            if (arguments.TryGetProperty("xmax_min", out var xmaxMinElement) && xmaxMinElement.ValueKind == JsonValueKind.Number)
            {
                parameters.XmaxMin = xmaxMinElement.GetDouble();
            }

            if (arguments.TryGetProperty("xmax_max", out var xmaxMaxElement) && xmaxMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.XmaxMax = xmaxMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("manufacturer", out var manufacturerElement) && manufacturerElement.ValueKind == JsonValueKind.String)
            {
                parameters.Manufacturer = manufacturerElement.GetString();
            }

            if (arguments.TryGetProperty("price_max", out var priceMaxElement) && priceMaxElement.ValueKind == JsonValueKind.Number)
            {
                parameters.PriceMax = priceMaxElement.GetDouble();
            }

            if (arguments.TryGetProperty("sort_by", out var sortByElement) && sortByElement.ValueKind == JsonValueKind.String)
            {
                parameters.SortBy = sortByElement.GetString();
            }

            if (arguments.TryGetProperty("sort_direction", out var sortDirElement) && sortDirElement.ValueKind == JsonValueKind.String)
            {
                parameters.SortDirection = sortDirElement.GetString();
            }

            if (arguments.TryGetProperty("limit", out var limitElement) && limitElement.ValueKind == JsonValueKind.Number)
            {
                parameters.Limit = Math.Max(1, Math.Min(1000, limitElement.GetInt32()));
            }

            // Perform search
            var results = _speakerDatabaseService.SearchSpeakersByParameters(parameters);

            // Validate that subcircuits found in database also exist in library index
            var missingSubcircuits = new List<string>();
            var warnings = new List<string>();
            
            if (_libraryService != null)
            {
                foreach (var result in results)
                {
                    if (!string.IsNullOrWhiteSpace(result.SubcircuitName))
                    {
                        var subcircuit = _libraryService.GetSubcircuitByName(result.SubcircuitName);
                        if (subcircuit == null)
                        {
                            missingSubcircuits.Add(result.SubcircuitName);
                        }
                    }
                }
                
                if (missingSubcircuits.Any())
                {
                    warnings.Add($"Warning: {missingSubcircuits.Count} subcircuit(s) found in database but not in library index: {string.Join(", ", missingSubcircuits)}. " +
                                "These subcircuits cannot be used in simulation. Run 'reindex_libraries' to update the library index, or ensure the library files containing these subcircuits are in the configured library paths.");
                }
            }
            else if (results.Any(r => !string.IsNullOrWhiteSpace(r.SubcircuitName)))
            {
                warnings.Add("Warning: Library service is not configured. Subcircuits found in database cannot be verified or used. " +
                              "Configure LibraryService in dependency injection to enable subcircuit support.");
            }

            // Format response
            var response = new
            {
                count = results.Count,
                results = results.Select(r => new
                {
                    subcircuit_name = r.SubcircuitName,
                    manufacturer = r.Manufacturer,
                    part_number = r.PartNumber,
                    type = r.Type,
                    diameter = r.Diameter,
                    impedance = r.Impedance,
                    ts_parameters = r.TsParameters,
                    sensitivity = r.Sensitivity,
                    power_rms = r.PowerRms,
                    price = r.Price,
                    // Add availability flag if library service is configured
                    available_in_library = _libraryService != null && !string.IsNullOrWhiteSpace(r.SubcircuitName) 
                        ? _libraryService.GetSubcircuitByName(r.SubcircuitName) != null 
                        : (bool?)null
                }).ToList(),
                warnings = warnings.Any() ? warnings : null,
                missing_subcircuits = missingSubcircuits.Any() ? missingSubcircuits : null
            };

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Speaker search failed: {Message}", ex.Message);
            throw new InvalidOperationException($"Speaker search failed: {ex.Message}", ex);
        }
    }

    private async Task<MCPToolResult> CalculateEnclosureDesign(JsonElement arguments)
    {
        if (_speakerDatabaseService == null || _enclosureDesignService == null)
        {
            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            error = "Enclosure design service is not configured",
                            message = "Speaker database service or enclosure design service is not available. To enable enclosure design, configure ISpeakerDatabaseService and IEnclosureDesignService in dependency injection."
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }

        try
        {
            // Parse arguments
            if (!arguments.TryGetProperty("model", out var modelElement) || modelElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("'model' parameter is required and must be a string");
            }

            var modelName = modelElement.GetString();
            if (string.IsNullOrWhiteSpace(modelName))
            {
                throw new ArgumentException("'model' parameter cannot be empty");
            }

            if (!arguments.TryGetProperty("enclosure_type", out var enclosureTypeElement) || enclosureTypeElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("'enclosure_type' parameter is required and must be 'sealed' or 'vented'");
            }

            var enclosureType = enclosureTypeElement.GetString()?.ToLowerInvariant();
            if (enclosureType != "sealed" && enclosureType != "vented")
            {
                throw new ArgumentException("'enclosure_type' must be 'sealed' or 'vented'");
            }

            // Look up speaker from database
            var speaker = _speakerDatabaseService.GetSpeakerByName(modelName);
            if (speaker == null)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Speaker not found",
                                message = $"Speaker model '{modelName}' not found in database. Use search_speakers_by_parameters to find available speakers."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            // Convert SpeakerSearchResult to SpeakerTsParameters
            var tsParams = new SpeakerTsParameters
            {
                Fs = speaker.TsParameters.TryGetValue("FS", out var fs) ? fs : 0,
                Qts = speaker.TsParameters.TryGetValue("QTS", out var qts) ? qts : 0,
                Qes = speaker.TsParameters.TryGetValue("QES", out var qes) ? qes : 0,
                Qms = speaker.TsParameters.TryGetValue("QMS", out var qms) ? qms : 0,
                Vas = speaker.TsParameters.TryGetValue("VAS", out var vas) ? vas : 0,
                Re = speaker.TsParameters.TryGetValue("RE", out var re) ? re : 0,
                Le = speaker.TsParameters.TryGetValue("LE", out var le) ? le : 0,
                Bl = speaker.TsParameters.TryGetValue("BL", out var bl) ? bl : 0,
                Xmax = speaker.TsParameters.TryGetValue("XMAX", out var xmax) ? xmax : 0,
                Mms = speaker.TsParameters.TryGetValue("MMS", out var mms) ? mms : 0,
                Cms = speaker.TsParameters.TryGetValue("CMS", out var cms) ? cms : 0,
                Sd = speaker.TsParameters.TryGetValue("SD", out var sd) ? sd : 0
            };

            // Validate required T/S parameters
            if (tsParams.Fs <= 0 || tsParams.Qts <= 0 || tsParams.Vas <= 0)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Incomplete T/S parameters",
                                message = $"Speaker '{modelName}' is missing required T/S parameters (FS, QTS, VAS). Found: FS={tsParams.Fs}, QTS={tsParams.Qts}, VAS={tsParams.Vas}"
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            object designResult;

            if (enclosureType == "sealed")
            {
                // Get target Qtc (default to Butterworth 0.707)
                double targetQtc = 0.707;
                if (arguments.TryGetProperty("target_qtc", out var qtcElement) && qtcElement.ValueKind == JsonValueKind.Number)
                {
                    targetQtc = qtcElement.GetDouble();
                }

                var sealedDesign = _enclosureDesignService.CalculateSealedBox(tsParams, targetQtc);
                designResult = new
                {
                    model = modelName,
                    manufacturer = speaker.Manufacturer,
                    enclosure_type = "sealed",
                    target_qtc = targetQtc,
                    volume_liters = Math.Round(sealedDesign.VolumeLiters, 2),
                    volume_cubic_feet = Math.Round(sealedDesign.VolumeCubicFeet, 3),
                    qtc = Math.Round(sealedDesign.Qtc, 3),
                    f3_hz = Math.Round(sealedDesign.F3, 2),
                    fc_hz = Math.Round(sealedDesign.Fc, 2)
                };
            }
            else // vented
            {
                // Get alignment (default to QB3)
                string alignment = "QB3";
                if (arguments.TryGetProperty("alignment", out var alignmentElement) && alignmentElement.ValueKind == JsonValueKind.String)
                {
                    alignment = alignmentElement.GetString() ?? "QB3";
                }

                var ventedDesign = _enclosureDesignService.CalculateVentedBox(tsParams, alignment);
                designResult = new
                {
                    model = modelName,
                    manufacturer = speaker.Manufacturer,
                    enclosure_type = "vented",
                    alignment = alignment,
                    volume_liters = Math.Round(ventedDesign.VolumeLiters, 2),
                    volume_cubic_feet = Math.Round(ventedDesign.VolumeCubicFeet, 3),
                    tuning_frequency_hz = Math.Round(ventedDesign.Fb, 2),
                    f3_hz = Math.Round(ventedDesign.F3, 2),
                    port = new
                    {
                        diameter_inches = Math.Round(ventedDesign.PortDiameterInches, 2),
                        diameter_cm = Math.Round(ventedDesign.PortDiameterCm, 2),
                        length_inches = Math.Round(ventedDesign.PortLengthInches, 2),
                        length_cm = Math.Round(ventedDesign.PortLengthCm, 2)
                    },
                    warnings = !string.IsNullOrWhiteSpace(ventedDesign.PortVelocityWarning) 
                        ? new[] { ventedDesign.PortVelocityWarning } 
                        : Array.Empty<string>()
                };
            }

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(designResult, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Enclosure design calculation failed: {Message}", ex.Message);
            throw new InvalidOperationException($"Enclosure design calculation failed: {ex.Message}", ex);
        }
    }

    private async Task<MCPToolResult> CheckCrossoverCompatibility(JsonElement arguments)
    {
        if (_speakerDatabaseService == null || _crossoverCompatibilityService == null)
        {
            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            error = "Crossover compatibility service is not configured",
                            message = "Speaker database service or crossover compatibility service is not available. To enable crossover compatibility checking, configure ISpeakerDatabaseService and ICrossoverCompatibilityService in dependency injection."
                        }, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }

        try
        {
            // Parse arguments
            if (!arguments.TryGetProperty("woofer_model", out var wooferModelElement) || wooferModelElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("'woofer_model' parameter is required and must be a string");
            }

            if (!arguments.TryGetProperty("tweeter_model", out var tweeterModelElement) || tweeterModelElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("'tweeter_model' parameter is required and must be a string");
            }

            if (!arguments.TryGetProperty("crossover_frequency", out var freqElement) || freqElement.ValueKind != JsonValueKind.Number)
            {
                throw new ArgumentException("'crossover_frequency' parameter is required and must be a number");
            }

            if (!arguments.TryGetProperty("crossover_order", out var orderElement) || orderElement.ValueKind != JsonValueKind.Number)
            {
                throw new ArgumentException("'crossover_order' parameter is required and must be an integer (1-4)");
            }

            var wooferModel = wooferModelElement.GetString();
            var tweeterModel = tweeterModelElement.GetString();
            var crossoverFreq = freqElement.GetDouble();
            var crossoverOrder = orderElement.GetInt32();

            if (string.IsNullOrWhiteSpace(wooferModel) || string.IsNullOrWhiteSpace(tweeterModel))
            {
                throw new ArgumentException("Woofer and tweeter model names cannot be empty");
            }

            if (crossoverFreq <= 0)
            {
                throw new ArgumentException("Crossover frequency must be positive");
            }

            if (crossoverOrder < 1 || crossoverOrder > 4)
            {
                throw new ArgumentException("Crossover order must be between 1 and 4");
            }

            // Look up speakers from database
            var woofer = _speakerDatabaseService.GetSpeakerByName(wooferModel);
            if (woofer == null)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Woofer not found",
                                message = $"Woofer model '{wooferModel}' not found in database. Use search_speakers_by_parameters to find available speakers."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            var tweeter = _speakerDatabaseService.GetSpeakerByName(tweeterModel);
            if (tweeter == null)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Tweeter not found",
                                message = $"Tweeter model '{tweeterModel}' not found in database. Use search_speakers_by_parameters to find available speakers."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            // Convert SpeakerSearchResult to SpeakerTsParameters
            var wooferTs = ConvertToTsParameters(woofer);
            var tweeterTs = ConvertToTsParameters(tweeter);

            // Validate required T/S parameters
            if (wooferTs.Fs <= 0 || wooferTs.Sd <= 0)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Incomplete woofer T/S parameters",
                                message = $"Woofer '{wooferModel}' is missing required T/S parameters (FS, SD)."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            if (tweeterTs.Fs <= 0)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent>
                    {
                        new MCPContent
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(new
                            {
                                error = "Incomplete tweeter T/S parameters",
                                message = $"Tweeter '{tweeterModel}' is missing required T/S parameter (FS)."
                            }, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }

            // Check compatibility
            // Pass sensitivity and impedance from metadata (not estimated from T/S parameters)
            var result = _crossoverCompatibilityService.CheckCompatibility(
                wooferTs, 
                tweeterTs, 
                crossoverFreq, 
                crossoverOrder,
                wooferSensitivity: woofer.Sensitivity,
                tweeterSensitivity: tweeter.Sensitivity,
                wooferImpedance: woofer.Impedance.HasValue ? (double?)woofer.Impedance.Value : null,
                tweeterImpedance: tweeter.Impedance.HasValue ? (double?)tweeter.Impedance.Value : null);

            // Format response
            var response = new
            {
                woofer_model = wooferModel,
                woofer_manufacturer = woofer.Manufacturer,
                tweeter_model = tweeterModel,
                tweeter_manufacturer = tweeter.Manufacturer,
                crossover_frequency_hz = crossoverFreq,
                crossover_order = crossoverOrder,
                compatibility_score = Math.Round(result.CompatibilityScore, 1),
                woofer_beaming_ok = result.WooferBeamingOk,
                max_crossover_frequency_hz = Math.Round(result.MaxCrossoverFrequency, 1),
                tweeter_fs_ok = result.TweeterFsOk,
                min_crossover_frequency_hz = Math.Round(result.MinCrossoverFrequency, 1),
                sensitivity_difference_db = Math.Round(result.SensitivityDifference, 1),
                sensitivity_match_ok = result.SensitivityMatchOk,
                impedance_match_ok = result.ImpedanceMatchOk,
                woofer_impedance_ohms = result.WooferImpedance.HasValue ? Math.Round(result.WooferImpedance.Value, 1) : (double?)null,
                tweeter_impedance_ohms = result.TweeterImpedance.HasValue ? Math.Round(result.TweeterImpedance.Value, 1) : (double?)null,
                recommendations = result.Recommendations,
                warnings = result.Warnings
            };

            return new MCPToolResult
            {
                Content = new List<MCPContent>
                {
                    new MCPContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Crossover compatibility check failed: {Message}", ex.Message);
            throw new InvalidOperationException($"Crossover compatibility check failed: {ex.Message}", ex);
        }
    }

    private static SpeakerTsParameters ConvertToTsParameters(SpeakerSearchResult speaker)
    {
        return new SpeakerTsParameters
        {
            Fs = speaker.TsParameters.TryGetValue("FS", out var fs) ? fs : 0,
            Qts = speaker.TsParameters.TryGetValue("QTS", out var qts) ? qts : 0,
            Qes = speaker.TsParameters.TryGetValue("QES", out var qes) ? qes : 0,
            Qms = speaker.TsParameters.TryGetValue("QMS", out var qms) ? qms : 0,
            Vas = speaker.TsParameters.TryGetValue("VAS", out var vas) ? vas : 0,
            Re = speaker.TsParameters.TryGetValue("RE", out var re) ? re : 0,
            Le = speaker.TsParameters.TryGetValue("LE", out var le) ? le : 0,
            Bl = speaker.TsParameters.TryGetValue("BL", out var bl) ? bl : 0,
            Xmax = speaker.TsParameters.TryGetValue("XMAX", out var xmax) ? xmax : 0,
            Mms = speaker.TsParameters.TryGetValue("MMS", out var mms) ? mms : 0,
            Cms = speaker.TsParameters.TryGetValue("CMS", out var cms) ? cms : 0,
            Sd = speaker.TsParameters.TryGetValue("SD", out var sd) ? sd : 0
        };
    }
}

/// <summary>
/// MCP tool definition
/// </summary>
public class MCPToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object InputSchema { get; set; } = new { };
}

/// <summary>
/// MCP tool result
/// </summary>
public class MCPToolResult
{
    public List<MCPContent> Content { get; set; } = new();
}

/// <summary>
/// MCP content item
/// </summary>
public class MCPContent
{
    public string Type { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
    public string? Data { get; set; }
    public string? MimeType { get; set; }
}

