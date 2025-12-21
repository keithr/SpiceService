using SpiceSharp;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Simulations;
using SpiceSharp.Behaviors;
using System.Diagnostics;
using System.Numerics;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing AC (frequency-domain) analysis.
/// </summary>
public class ACAnalysisService : IACAnalysisService
{
    /// <inheritdoc/>
    public ACAnalysisResult RunACAnalysis(
        CircuitModel circuit,
        double startFrequency,
        double stopFrequency,
        int numberOfPoints,
        IEnumerable<string>? signals = null)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (signals == null || !signals.Any())
            throw new ArgumentException("At least one signal is required.", nameof(signals));

        if (stopFrequency <= startFrequency)
            throw new ArgumentException("Stop frequency must be greater than start frequency.");

        if (numberOfPoints <= 0)
            throw new ArgumentException("Number of points must be greater than zero.");

        var sw = Stopwatch.StartNew();
        
        // Generate logarithmic frequency points
        // AC analysis uses logarithmic (decade) sweep
        var logStart = Math.Log10(Math.Max(startFrequency, 1e-30)); // Avoid log(0)
        var logStop = Math.Log10(stopFrequency);
        var logStep = (logStop - logStart) / (numberOfPoints - 1);
        
        var frequencyPoints = new List<double>();
        for (int i = 0; i < numberOfPoints; i++)
        {
            var logFreq = logStart + i * logStep;
            var freq = Math.Pow(10, logFreq);
            // Ensure frequency is within requested range and not zero
            if (freq >= startFrequency && freq <= stopFrequency && freq > 0)
            {
                frequencyPoints.Add(freq);
            }
        }
        
        // Ensure we have at least one frequency point
        if (frequencyPoints.Count == 0)
        {
            frequencyPoints.Add(startFrequency);
        }

        var magnitudeDb = new Dictionary<string, List<double>>();
        var phaseDegrees = new Dictionary<string, List<double>>();
        var frequencies = new List<double>();

        // Initialize signal storage
        foreach (var signal in signals)
        {
            magnitudeDb[signal] = new List<double>();
            phaseDegrees[signal] = new List<double>();
        }

        string status = "Success";
        try
        {
            // Validate circuit before running analysis
            var validator = new CircuitValidator();
            var validation = validator.Validate(circuit);
            if (!validation.IsValid)
            {
                var errorDetails = string.Join("; ", validation.Errors);
                status = $"Validation failed: {errorDetails}";
                throw new InvalidOperationException(status);
            }
            if (validation.HasWarnings)
            {
                var warnings = string.Join("; ", validation.Warnings);
                System.Diagnostics.Debug.WriteLine($"Circuit validation warnings: {warnings}");
                // Log warnings but don't fail - SpiceSharp will do the final validation
            }

            // SpiceSharp's AC constructor supports frequency sweep configuration using DecadeSweep
            // Format: AC(name, new DecadeSweep(startFreq, stopFreq, pointsPerDecade))
            // Calculate points per decade from total number of points
            var decades = Math.Log10(stopFrequency / Math.Max(startFrequency, 1e-30));
            var pointsPerDecade = Math.Max(1, (int)Math.Ceiling(numberOfPoints / decades));
            
            // Create AC simulation with proper frequency sweep configuration
            var ac = new AC("ac", new SpiceSharp.Simulations.DecadeSweep(startFrequency, stopFrequency, pointsPerDecade));

            // Create export objects for each signal (reusable across frequency points)
            var voltageExportTemplates = new Dictionary<string, string>(); // signal -> node name
            var currentExportTemplates = new Dictionary<string, string>(); // signal -> component name
            
            foreach (var signal in signals)
            {
                if (signal.StartsWith("v("))
                {
                    var node = signal.Substring(2, signal.Length - 3);
                    voltageExportTemplates[signal] = node;
                }
                else if (signal.StartsWith("i("))
                {
                    var component = signal.Substring(2, signal.Length - 3);
                    currentExportTemplates[signal] = component;
                }
            }

            // Run AC simulation with configured frequency sweep and collect all frequencies
            var collectedFrequencies = new List<double>();
            var collectedData = new Dictionary<double, Dictionary<string, (double magnitude, double phase)>>();
            var voltageExports = new Dictionary<string, ComplexVoltageExport>();
            var currentExports = new Dictionary<string, ComplexCurrentExport>();
            
            foreach (var (signal, node) in voltageExportTemplates)
            {
                voltageExports[signal] = new ComplexVoltageExport(ac, node);
            }
            
            foreach (var (signal, component) in currentExportTemplates)
            {
                try
                {
                    currentExports[signal] = new ComplexCurrentExport(ac, component);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"Could not create current export for {component}");
                }
            }
            
            // Run AC simulation and collect all frequencies in the configured range
            foreach (int exportType in ac.Run(circuit.InternalCircuit))
            {
                var currentFreq = ac.Frequency;
                
                // Only collect frequencies within our requested range and not zero
                if (currentFreq >= startFrequency && currentFreq <= stopFrequency && currentFreq > 0)
                {
                    if (!collectedData.ContainsKey(currentFreq))
                    {
                        collectedFrequencies.Add(currentFreq);
                        collectedData[currentFreq] = new Dictionary<string, (double, double)>();
                    }
                    
                    // Collect all signal values at this frequency point
                    foreach (var (signalName, export) in voltageExports)
                    {
                        try
                        {
                            var complexValue = export.Value;
                            var magnitude = Math.Sqrt(complexValue.Real * complexValue.Real + complexValue.Imaginary * complexValue.Imaginary);
                            var magnitudeDbValue = 20 * Math.Log10(magnitude + 1e-30);
                            var phaseRadians = Math.Atan2(complexValue.Imaginary, complexValue.Real);
                            var phaseDegreesValue = phaseRadians * 180.0 / Math.PI;

                            collectedData[currentFreq][signalName] = (magnitudeDbValue, phaseDegreesValue);
                        }
                        catch
                        {
                            collectedData[currentFreq][signalName] = (double.NaN, double.NaN);
                        }
                    }

                    foreach (var (signalName, export) in currentExports)
                    {
                        try
                        {
                            var complexValue = export.Value;
                            var magnitude = Math.Sqrt(complexValue.Real * complexValue.Real + complexValue.Imaginary * complexValue.Imaginary);
                            var magnitudeDbValue = 20 * Math.Log10(magnitude + 1e-30);
                            var phaseRadians = Math.Atan2(complexValue.Imaginary, complexValue.Real);
                            var phaseDegreesValue = phaseRadians * 180.0 / Math.PI;

                            collectedData[currentFreq][signalName] = (magnitudeDbValue, phaseDegreesValue);
                        }
                        catch
                        {
                            collectedData[currentFreq][signalName] = (double.NaN, double.NaN);
                        }
                    }
                }
            }
            
            // Now that we're using DecadeSweep, the AC simulation should generate frequencies
            // in the requested range. Collect all frequencies within range and match to requested points.
            var frequenciesInRange = collectedFrequencies.Where(f => f >= startFrequency && f <= stopFrequency && f > 0).OrderBy(f => f).ToList();
            
            // Match collected frequencies to requested frequency points
            // Use the generated logarithmic frequency points as targets
            foreach (var targetFreq in frequencyPoints)
            {
                if (frequenciesInRange.Count > 0)
                {
                    // Find the closest frequency in the collected data
                    var closestFreq = frequenciesInRange.OrderBy(f => Math.Abs(f - targetFreq)).First();
                    
                    // Use it if it's reasonably close (within 10% or 1 Hz, whichever is larger)
                    var tolerance = Math.Max(targetFreq * 0.1, 1.0);
                    if (Math.Abs(closestFreq - targetFreq) <= tolerance)
                    {
                        frequencies.Add(closestFreq);
                        
                        foreach (var signal in signals)
                        {
                            if (collectedData.ContainsKey(closestFreq) && collectedData[closestFreq].ContainsKey(signal))
                            {
                                var (mag, phase) = collectedData[closestFreq][signal];
                                magnitudeDb[signal].Add(mag);
                                phaseDegrees[signal].Add(phase);
                            }
                            else
                            {
                                magnitudeDb[signal].Add(double.NaN);
                                phaseDegrees[signal].Add(double.NaN);
                            }
                        }
                    }
                }
            }
            
            // If we didn't get enough points matching the requested frequencies,
            // use all collected frequencies in range (they should be close to what was requested)
            if (frequencies.Count < frequencyPoints.Count / 2 && frequenciesInRange.Count > 0)
            {
                frequencies.Clear();
                foreach (var signal in signals)
                {
                    magnitudeDb[signal].Clear();
                    phaseDegrees[signal].Clear();
                }
                
                // Use all collected frequencies in order
                foreach (var freq in frequenciesInRange)
                {
                    frequencies.Add(freq);
                    foreach (var signal in signals)
                    {
                        if (collectedData.ContainsKey(freq) && collectedData[freq].ContainsKey(signal))
                        {
                            var (mag, phase) = collectedData[freq][signal];
                            magnitudeDb[signal].Add(mag);
                            phaseDegrees[signal].Add(phase);
                        }
                        else
                        {
                            magnitudeDb[signal].Add(double.NaN);
                            phaseDegrees[signal].Add(double.NaN);
                        }
                    }
                }
            }
            
            // Verify we got frequencies in the requested range
            if (frequencies.Count == 0)
            {
                status = $"Warning: No frequencies collected in requested range ({startFrequency} Hz to {stopFrequency} Hz). " +
                        $"Collected {collectedFrequencies.Count} frequencies, max: {collectedFrequencies.DefaultIfEmpty(0).Max():F2} Hz.";
            }
            else if (frequencies.Max() < stopFrequency * 0.9)
            {
                status = $"Warning: AC analysis only reached {frequencies.Max():F2} Hz, " +
                        $"but requested range goes up to {stopFrequency} Hz. " +
                        $"Got {frequencies.Count} frequency points.";
            }
        }
        catch (Exception ex)
        {
            // Capture detailed error information including inner exceptions
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"Error: {ex.GetType().Name}");
            errorDetails.AppendLine($"Message: {ex.Message}");
            
            // Try to extract more details from SpiceSharp's ValidationFailedException
            if (ex.GetType().Name.Contains("ValidationFailedException", StringComparison.OrdinalIgnoreCase))
            {
                errorDetails.AppendLine();
                errorDetails.AppendLine("SpiceSharp Validation Failed - Common causes:");
                errorDetails.AppendLine("1. Floating nodes (nodes not connected to any component)");
                errorDetails.AppendLine("2. Nodes without DC paths to ground (required for AC analysis)");
                errorDetails.AppendLine("3. Inductor loops without resistance");
                errorDetails.AppendLine("4. Voltage source loops");
                errorDetails.AppendLine();
                errorDetails.AppendLine("Suggested fixes:");
                errorDetails.AppendLine("- Add large resistors (1e9 to 1e12 ohms) from floating nodes to ground");
                errorDetails.AppendLine("- Add small series resistors (1e-6 ohms) to inductors in loops");
                errorDetails.AppendLine("- Ensure all subcircuit internal nodes have DC paths to external pins");
                errorDetails.AppendLine();
                
                try
                {
                    // Use reflection to try to get rule violation details
                    var exType = ex.GetType();
                    var violationsProp = exType.GetProperty("Violations", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                    if (violationsProp != null)
                    {
                        var violations = violationsProp.GetValue(ex);
                        if (violations != null)
                        {
                            errorDetails.AppendLine("Rule Violations:");
                            // Try to enumerate violations if it's a collection
                            if (violations is System.Collections.IEnumerable violationsEnum && 
                                violations is not string)
                            {
                                foreach (var violation in violationsEnum)
                                {
                                    errorDetails.AppendLine($"  - {violation}");
                                }
                            }
                            else
                            {
                                errorDetails.AppendLine($"  {violations}");
                            }
                        }
                    }
                    
                    // Try to get Rules property
                    var rulesProp = exType.GetProperty("Rules", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                    if (rulesProp != null)
                    {
                        var rules = rulesProp.GetValue(ex);
                        if (rules != null)
                        {
                            errorDetails.AppendLine($"Rules: {rules}");
                        }
                    }
                }
                catch
                {
                    // If reflection fails, continue with standard error reporting
                    errorDetails.AppendLine("(Could not extract detailed violation information)");
                }
            }
            
            if (ex.InnerException != null)
            {
                errorDetails.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                errorDetails.AppendLine($"Inner Message: {ex.InnerException.Message}");
            }
            
            // Check for SpiceSharp-specific error information
            if (ex.Data != null && ex.Data.Count > 0)
            {
                errorDetails.AppendLine("Additional Data:");
                foreach (var key in ex.Data.Keys)
                {
                    errorDetails.AppendLine($"  {key}: {ex.Data[key]}");
                }
            }
            
            // Include stack trace for debugging (first few lines)
            var stackTrace = ex.StackTrace?.Split('\n').Take(5);
            if (stackTrace != null && stackTrace.Any())
            {
                errorDetails.AppendLine("Stack Trace (first 5 lines):");
                foreach (var line in stackTrace)
                {
                    errorDetails.AppendLine($"  {line.Trim()}");
                }
            }
            
            status = $"Failed: {errorDetails}";
        }

        sw.Stop();

        return new ACAnalysisResult
        {
            Frequencies = frequencies,
            MagnitudeDb = magnitudeDb,
            PhaseDegrees = phaseDegrees,
            AnalysisTimeMs = sw.ElapsedMilliseconds,
            Status = status
        };
    }
}
