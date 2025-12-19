using SpiceSharp.Api.Core.Models;
using System.Diagnostics;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Service for calculating group delay from phase data
/// </summary>
public class GroupDelayService : IGroupDelayService
{
    private readonly CircuitResultsCache _resultsCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupDelayService"/> class
    /// </summary>
    /// <param name="resultsCache">Cache containing analysis results</param>
    public GroupDelayService(CircuitResultsCache resultsCache)
    {
        _resultsCache = resultsCache ?? throw new ArgumentNullException(nameof(resultsCache));
    }

    /// <inheritdoc/>
    public GroupDelayResult CalculateGroupDelay(string circuitId, string signal, string? reference)
    {
        if (string.IsNullOrWhiteSpace(circuitId))
            throw new ArgumentException("circuitId is required.", nameof(circuitId));

        if (string.IsNullOrWhiteSpace(signal))
            throw new ArgumentException("signal is required.", nameof(signal));

        var sw = Stopwatch.StartNew();

        // Get cached results
        var cachedResult = _resultsCache.Get(circuitId);
        if (cachedResult == null)
        {
            throw new ArgumentException($"No analysis results found for circuit '{circuitId}'. Run an AC analysis first.");
        }

        if (cachedResult.AnalysisType != "ac")
        {
            throw new InvalidOperationException($"Group delay calculation requires AC analysis results. Found analysis type: {cachedResult.AnalysisType}");
        }

        // Verify signal exists
        if (!cachedResult.Signals.ContainsKey(signal))
        {
            throw new ArgumentException($"Signal '{signal}' not found in cached results for circuit '{circuitId}'. " +
                $"Available signals: {string.Join(", ", cachedResult.Signals.Keys)}");
        }

        if (!cachedResult.ImaginarySignals.ContainsKey(signal))
        {
            throw new ArgumentException($"Signal '{signal}' does not have phase data (imaginary component) in AC analysis.");
        }

        var frequencies = cachedResult.XData ?? Array.Empty<double>();
        var realData = cachedResult.Signals[signal];
        var imagData = cachedResult.ImaginarySignals[signal];

        if (frequencies.Length != realData.Length || frequencies.Length != imagData.Length)
        {
            throw new InvalidOperationException($"Frequency, real, and imaginary data arrays have mismatched lengths.");
        }

        if (frequencies.Length < 2)
        {
            throw new InvalidOperationException("At least 2 frequency points are required for group delay calculation.");
        }

        // Calculate phase from complex values and unwrap if needed
        var phases = new double[frequencies.Length];
        for (int i = 0; i < frequencies.Length; i++)
        {
            phases[i] = Math.Atan2(imagData[i], realData[i]); // Phase in radians
        }

        // Unwrap phase (handle jumps from -π to +π)
        UnwrapPhase(phases);

        // Calculate group delay: τ_g = -dφ/dω = -dφ/df * 1/(2π)
        // where φ is phase in radians, ω is angular frequency (2πf), f is frequency in Hz
        var groupDelays = new List<double>();
        
        // First point: use forward difference
        if (frequencies.Length > 0)
        {
            var dPhase = phases[1] - phases[0];
            var dFreq = frequencies[1] - frequencies[0];
            if (Math.Abs(dFreq) > 1e-10)
            {
                var dPhaseDf = dPhase / dFreq;
                var groupDelay = -dPhaseDf / (2.0 * Math.PI); // Convert to seconds
                groupDelays.Add(groupDelay);
            }
            else
            {
                groupDelays.Add(0.0);
            }
        }

        // Middle points: use central difference for better accuracy
        for (int i = 1; i < frequencies.Length - 1; i++)
        {
            var dPhase = phases[i + 1] - phases[i - 1];
            var dFreq = frequencies[i + 1] - frequencies[i - 1];
            if (Math.Abs(dFreq) > 1e-10)
            {
                var dPhaseDf = dPhase / dFreq;
                var groupDelay = -dPhaseDf / (2.0 * Math.PI); // Convert to seconds
                groupDelays.Add(groupDelay);
            }
            else
            {
                groupDelays.Add(0.0);
            }
        }

        // Last point: use backward difference
        if (frequencies.Length > 1)
        {
            var dPhase = phases[frequencies.Length - 1] - phases[frequencies.Length - 2];
            var dFreq = frequencies[frequencies.Length - 1] - frequencies[frequencies.Length - 2];
            if (Math.Abs(dFreq) > 1e-10)
            {
                var dPhaseDf = dPhase / dFreq;
                var groupDelay = -dPhaseDf / (2.0 * Math.PI); // Convert to seconds
                groupDelays.Add(groupDelay);
            }
            else
            {
                groupDelays.Add(0.0);
            }
        }

        sw.Stop();

        return new GroupDelayResult
        {
            Frequencies = frequencies.ToList(),
            GroupDelay = groupDelays,
            Signal = signal,
            Reference = reference,
            AnalysisTimeMs = sw.ElapsedMilliseconds,
            Status = "Success"
        };
    }

    /// <summary>
    /// Unwraps phase to remove discontinuities (handles jumps from -π to +π)
    /// </summary>
    private static void UnwrapPhase(double[] phases)
    {
        if (phases.Length < 2)
            return;

        for (int i = 1; i < phases.Length; i++)
        {
            var diff = phases[i] - phases[i - 1];
            
            // If phase jump is greater than π, assume it wrapped
            if (diff > Math.PI)
            {
                // Phase wrapped from negative to positive (e.g., -179° to +179°)
                phases[i] -= 2.0 * Math.PI;
            }
            else if (diff < -Math.PI)
            {
                // Phase wrapped from positive to negative (e.g., +179° to -179°)
                phases[i] += 2.0 * Math.PI;
            }
        }
    }
}
