using System.Numerics;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Service for extracting specific measurements from simulation results
/// </summary>
public class ResponseMeasurementService : IResponseMeasurementService
{
    private readonly CircuitResultsCache _resultsCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseMeasurementService"/> class
    /// </summary>
    /// <param name="resultsCache">Cache containing analysis results</param>
    public ResponseMeasurementService(CircuitResultsCache resultsCache)
    {
        _resultsCache = resultsCache ?? throw new ArgumentNullException(nameof(resultsCache));
    }

    /// <inheritdoc/>
    public MeasurementResult Measure(
        string circuitId,
        string measurement,
        string signal,
        string? reference,
        double? frequency,
        double? threshold,
        string? analysisId)
    {
        if (string.IsNullOrWhiteSpace(circuitId))
            throw new ArgumentException("circuitId is required.", nameof(circuitId));

        if (string.IsNullOrWhiteSpace(measurement))
            throw new ArgumentException("measurement is required.", nameof(measurement));

        if (string.IsNullOrWhiteSpace(signal))
            throw new ArgumentException("signal is required.", nameof(signal));

        // Get cached results
        var cachedResult = _resultsCache.Get(circuitId);
        if (cachedResult == null)
        {
            throw new ArgumentException($"No analysis results found for circuit '{circuitId}'. Run an analysis first.");
        }

        // Verify signal exists
        if (!cachedResult.Signals.ContainsKey(signal))
        {
            throw new ArgumentException($"Signal '{signal}' not found in cached results for circuit '{circuitId}'. " +
                $"Available signals: {string.Join(", ", cachedResult.Signals.Keys)}");
        }

        var signalData = cachedResult.Signals[signal];
        var xData = cachedResult.XData ?? Array.Empty<double>();

        if (signalData.Length == 0)
        {
            throw new InvalidOperationException($"Signal '{signal}' has no data points.");
        }

        if (xData.Length != signalData.Length)
        {
            throw new InvalidOperationException($"X-axis data length ({xData.Length}) does not match signal data length ({signalData.Length}).");
        }

        // For AC analysis, convert complex to magnitude dB if needed for frequency-domain measurements
        double[] magnitudeDb = signalData;
        if (cachedResult.AnalysisType == "ac" && cachedResult.ImaginarySignals.ContainsKey(signal))
        {
            // Calculate magnitude in dB from complex values
            var realData = signalData;
            var imagData = cachedResult.ImaginarySignals[signal];
            magnitudeDb = new double[realData.Length];
            for (int i = 0; i < realData.Length; i++)
            {
                var magnitude = Math.Sqrt(realData[i] * realData[i] + imagData[i] * imagData[i]);
                magnitudeDb[i] = magnitude > 0 ? 20.0 * Math.Log10(magnitude) : -1000.0; // Very negative dB for zero
            }
        }

        // Route to appropriate measurement method
        return measurement.ToLowerInvariant() switch
        {
            "bandwidth_3db" => MeasureBandwidth3dB(xData, magnitudeDb),
            "gain_at_freq" => MeasureGainAtFreq(xData, magnitudeDb, frequency ?? throw new ArgumentException("frequency parameter is required for 'gain_at_freq' measurement")),
            "freq_at_gain" => MeasureFreqAtGain(xData, magnitudeDb, threshold ?? throw new ArgumentException("threshold parameter is required for 'freq_at_gain' measurement")),
            "phase_at_freq" => MeasurePhaseAtFreq(cachedResult, signal, frequency ?? throw new ArgumentException("frequency parameter is required for 'phase_at_freq' measurement")),
            "peak_value" => MeasurePeakValue(signalData),
            "peak_frequency" => MeasurePeakFrequency(xData, magnitudeDb),
            "rise_time" => MeasureRiseTime(xData, signalData),
            "fall_time" => MeasureFallTime(xData, signalData),
            "overshoot" => MeasureOvershoot(xData, signalData),
            "settling_time" => MeasureSettlingTime(xData, signalData),
            "dc_gain" => MeasureDCGain(magnitudeDb),
            "unity_gain_freq" => MeasureUnityGainFreq(xData, magnitudeDb),
            "phase_margin" => MeasurePhaseMargin(cachedResult, signal),
            "gain_margin" => MeasureGainMargin(cachedResult, signal),
            _ => throw new ArgumentException($"Unknown measurement type: '{measurement}'. " +
                $"Supported measurements: bandwidth_3db, gain_at_freq, freq_at_gain, phase_at_freq, peak_value, peak_frequency, rise_time, fall_time, overshoot, settling_time, dc_gain, unity_gain_freq, phase_margin, gain_margin")
        };
    }

    private MeasurementResult MeasureBandwidth3dB(double[] frequencies, double[] magnitudeDb)
    {
        // Find -3dB point (where magnitude is 3dB below peak/DC value)
        var maxMagnitude = magnitudeDb.Max();
        var targetMagnitude = maxMagnitude - 3.0;

        // Find first crossing of -3dB point
        for (int i = 1; i < magnitudeDb.Length; i++)
        {
            if (magnitudeDb[i - 1] >= targetMagnitude && magnitudeDb[i] < targetMagnitude)
            {
                // Interpolate to find exact frequency
                var freq = InterpolateLinear(
                    magnitudeDb[i - 1], frequencies[i - 1],
                    magnitudeDb[i], frequencies[i],
                    targetMagnitude);
                
                return new MeasurementResult
                {
                    Value = freq,
                    Unit = "Hz",
                    Description = "-3dB bandwidth frequency"
                };
            }
        }

        throw new InvalidOperationException("Could not find -3dB point in the frequency response.");
    }

    private MeasurementResult MeasureGainAtFreq(double[] frequencies, double[] magnitudeDb, double targetFreq)
    {
        // Find gain at specific frequency using interpolation
        if (targetFreq < frequencies[0] || targetFreq > frequencies[frequencies.Length - 1])
        {
            throw new ArgumentException($"Target frequency {targetFreq} Hz is outside the analysis range [{frequencies[0]}, {frequencies[frequencies.Length - 1]}] Hz.");
        }

        // Find surrounding points
        for (int i = 1; i < frequencies.Length; i++)
        {
            if (frequencies[i - 1] <= targetFreq && frequencies[i] >= targetFreq)
            {
                // Interpolate gain
                var gain = InterpolateLinear(
                    frequencies[i - 1], magnitudeDb[i - 1],
                    frequencies[i], magnitudeDb[i],
                    targetFreq);

                return new MeasurementResult
                {
                    Value = gain,
                    Unit = "dB",
                    Description = $"Gain at {targetFreq} Hz"
                };
            }
        }

        // Exact match
        var index = Array.IndexOf(frequencies, targetFreq);
        if (index >= 0)
        {
            return new MeasurementResult
            {
                Value = magnitudeDb[index],
                Unit = "dB",
                Description = $"Gain at {targetFreq} Hz"
            };
        }

        throw new InvalidOperationException($"Could not find gain at frequency {targetFreq} Hz.");
    }

    private MeasurementResult MeasureFreqAtGain(double[] frequencies, double[] magnitudeDb, double targetGain)
    {
        // Find frequency at specific gain level
        for (int i = 1; i < magnitudeDb.Length; i++)
        {
            if ((magnitudeDb[i - 1] >= targetGain && magnitudeDb[i] <= targetGain) ||
                (magnitudeDb[i - 1] <= targetGain && magnitudeDb[i] >= targetGain))
            {
                // Interpolate to find exact frequency
                var freq = InterpolateLinear(
                    magnitudeDb[i - 1], frequencies[i - 1],
                    magnitudeDb[i], frequencies[i],
                    targetGain);

                return new MeasurementResult
                {
                    Value = freq,
                    Unit = "Hz",
                    Description = $"Frequency at {targetGain} dB gain"
                };
            }
        }

        throw new InvalidOperationException($"Could not find frequency at gain {targetGain} dB.");
    }

    private MeasurementResult MeasurePhaseAtFreq(CachedAnalysisResult cachedResult, string signal, double targetFreq)
    {
        if (cachedResult.AnalysisType != "ac")
        {
            throw new InvalidOperationException("phase_at_freq requires AC analysis results.");
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
            throw new InvalidOperationException("Frequency, real, and imaginary data arrays have mismatched lengths.");
        }

        // Find phase at specific frequency
        for (int i = 1; i < frequencies.Length; i++)
        {
            if (frequencies[i - 1] <= targetFreq && frequencies[i] >= targetFreq)
            {
                // Calculate phase from real and imaginary parts
                var phase1 = Math.Atan2(imagData[i - 1], realData[i - 1]) * 180.0 / Math.PI;
                var phase2 = Math.Atan2(imagData[i], realData[i]) * 180.0 / Math.PI;

                // Interpolate phase
                var phase = InterpolateLinear(
                    frequencies[i - 1], phase1,
                    frequencies[i], phase2,
                    targetFreq);

                return new MeasurementResult
                {
                    Value = phase,
                    Unit = "°",
                    Description = $"Phase at {targetFreq} Hz"
                };
            }
        }

        throw new InvalidOperationException($"Could not find phase at frequency {targetFreq} Hz.");
    }

    private MeasurementResult MeasurePeakValue(double[] signalData)
    {
        var peak = signalData.Max();
        return new MeasurementResult
        {
            Value = peak,
            Unit = "V", // Assume voltage, could be enhanced to detect unit from signal name
            Description = "Peak value"
        };
    }

    private MeasurementResult MeasurePeakFrequency(double[] frequencies, double[] magnitudeDb)
    {
        var maxIndex = 0;
        var maxValue = magnitudeDb[0];
        for (int i = 1; i < magnitudeDb.Length; i++)
        {
            if (magnitudeDb[i] > maxValue)
            {
                maxValue = magnitudeDb[i];
                maxIndex = i;
            }
        }

        return new MeasurementResult
        {
            Value = frequencies[maxIndex],
            Unit = "Hz",
            Description = "Frequency at peak magnitude"
        };
    }

    private MeasurementResult MeasureRiseTime(double[] time, double[] signalData)
    {
        var min = signalData.Min();
        var max = signalData.Max();
        var range = max - min;
        var threshold10 = min + 0.1 * range;
        var threshold90 = min + 0.9 * range;

        double? time10 = null;
        double? time90 = null;

        // Find 10% point
        for (int i = 1; i < signalData.Length; i++)
        {
            if (signalData[i - 1] < threshold10 && signalData[i] >= threshold10)
            {
                time10 = InterpolateLinear(
                    signalData[i - 1], time[i - 1],
                    signalData[i], time[i],
                    threshold10);
                break;
            }
        }

        // Find 90% point
        for (int i = 1; i < signalData.Length; i++)
        {
            if (signalData[i - 1] < threshold90 && signalData[i] >= threshold90)
            {
                time90 = InterpolateLinear(
                    signalData[i - 1], time[i - 1],
                    signalData[i], time[i],
                    threshold90);
                break;
            }
        }

        if (!time10.HasValue || !time90.HasValue)
        {
            throw new InvalidOperationException("Could not find 10% and 90% points for rise time calculation.");
        }

        return new MeasurementResult
        {
            Value = time90.Value - time10.Value,
            Unit = "s",
            Description = "Rise time (10% to 90%)"
        };
    }

    private MeasurementResult MeasureFallTime(double[] time, double[] signalData)
    {
        var min = signalData.Min();
        var max = signalData.Max();
        var range = max - min;
        var threshold90 = min + 0.9 * range;
        var threshold10 = min + 0.1 * range;

        double? time90 = null;
        double? time10 = null;

        // Find 90% point (falling)
        for (int i = 1; i < signalData.Length; i++)
        {
            if (signalData[i - 1] > threshold90 && signalData[i] <= threshold90)
            {
                time90 = InterpolateLinear(
                    signalData[i - 1], time[i - 1],
                    signalData[i], time[i],
                    threshold90);
                break;
            }
        }

        // Find 10% point (falling)
        for (int i = 1; i < signalData.Length; i++)
        {
            if (signalData[i - 1] > threshold10 && signalData[i] <= threshold10)
            {
                time10 = InterpolateLinear(
                    signalData[i - 1], time[i - 1],
                    signalData[i], time[i],
                    threshold10);
                break;
            }
        }

        if (!time90.HasValue || !time10.HasValue)
        {
            throw new InvalidOperationException("Could not find 90% and 10% points for fall time calculation.");
        }

        return new MeasurementResult
        {
            Value = time10.Value - time90.Value,
            Unit = "s",
            Description = "Fall time (90% to 10%)"
        };
    }

    private MeasurementResult MeasureOvershoot(double[] time, double[] signalData)
    {
        var finalValue = signalData[signalData.Length - 1];
        var peak = signalData.Max();
        var overshoot = ((peak - finalValue) / Math.Abs(finalValue)) * 100.0;

        return new MeasurementResult
        {
            Value = overshoot,
            Unit = "%",
            Description = "Overshoot percentage"
        };
    }

    private MeasurementResult MeasureSettlingTime(double[] time, double[] signalData, double tolerancePercent = 2.0)
    {
        var finalValue = signalData[signalData.Length - 1];
        var tolerance = Math.Abs(finalValue) * (tolerancePercent / 100.0);

        // Find last point that goes outside tolerance
        for (int i = signalData.Length - 1; i >= 0; i--)
        {
            if (Math.Abs(signalData[i] - finalValue) > tolerance)
            {
                if (i < signalData.Length - 1)
                {
                    return new MeasurementResult
                    {
                        Value = time[i + 1],
                        Unit = "s",
                        Description = $"Settling time ({tolerancePercent}% tolerance)"
                    };
                }
            }
        }

        // Signal was always within tolerance
        return new MeasurementResult
        {
            Value = 0.0,
            Unit = "s",
            Description = $"Settling time ({tolerancePercent}% tolerance)"
        };
    }

    private MeasurementResult MeasureDCGain(double[] signalData)
    {
        // DC gain is typically the first value (or average of first few values)
        var dcGain = signalData[0];
        return new MeasurementResult
        {
            Value = dcGain,
            Unit = "dB",
            Description = "DC gain"
        };
    }

    private MeasurementResult MeasureUnityGainFreq(double[] frequencies, double[] magnitudeDb)
    {
        // Find frequency where gain crosses 0 dB
        return MeasureFreqAtGain(frequencies, magnitudeDb, 0.0);
    }

    private MeasurementResult MeasurePhaseMargin(CachedAnalysisResult cachedResult, string signal)
    {
        if (cachedResult.AnalysisType != "ac")
        {
            throw new InvalidOperationException("phase_margin requires AC analysis results.");
        }

        // Calculate magnitude dB from complex if needed
        var xData = cachedResult.XData ?? Array.Empty<double>();
        var realData = cachedResult.Signals[signal];
        double[] magnitudeDb;
        if (cachedResult.ImaginarySignals.ContainsKey(signal))
        {
            var imagData = cachedResult.ImaginarySignals[signal];
            magnitudeDb = new double[realData.Length];
            for (int i = 0; i < realData.Length; i++)
            {
                var magnitude = Math.Sqrt(realData[i] * realData[i] + imagData[i] * imagData[i]);
                magnitudeDb[i] = magnitude > 0 ? 20.0 * Math.Log10(magnitude) : -1000.0;
            }
        }
        else
        {
            magnitudeDb = realData; // Already in dB
        }

        // Phase margin = 180° - phase at unity gain frequency
        var unityGainFreq = MeasureUnityGainFreq(xData, magnitudeDb);

        var phaseAtUnity = MeasurePhaseAtFreq(cachedResult, signal, unityGainFreq.Value);
        var phaseMargin = 180.0 - phaseAtUnity.Value;

        return new MeasurementResult
        {
            Value = phaseMargin,
            Unit = "°",
            Description = "Phase margin"
        };
    }

    private MeasurementResult MeasureGainMargin(CachedAnalysisResult cachedResult, string signal)
    {
        if (cachedResult.AnalysisType != "ac")
        {
            throw new InvalidOperationException("gain_margin requires AC analysis results.");
        }

        if (!cachedResult.ImaginarySignals.ContainsKey(signal))
        {
            throw new ArgumentException($"Signal '{signal}' does not have phase data (imaginary component) in AC analysis.");
        }

        // Gain margin = -gain at frequency where phase = -180°
        var frequencies = cachedResult.XData ?? Array.Empty<double>();
        var realData = cachedResult.Signals[signal];
        var imagData = cachedResult.ImaginarySignals[signal];
        
        // Calculate magnitude dB from complex
        var magnitudeDb = new double[realData.Length];
        for (int i = 0; i < realData.Length; i++)
        {
            var magnitude = Math.Sqrt(realData[i] * realData[i] + imagData[i] * imagData[i]);
            magnitudeDb[i] = magnitude > 0 ? 20.0 * Math.Log10(magnitude) : -1000.0;
        }

        // Find frequency where phase = -180°
        for (int i = 1; i < frequencies.Length; i++)
        {
            var phase1 = Math.Atan2(imagData[i - 1], realData[i - 1]) * 180.0 / Math.PI;
            var phase2 = Math.Atan2(imagData[i], realData[i]) * 180.0 / Math.PI;

            // Normalize phases to -180 to +180 range
            while (phase1 > 180) phase1 -= 360;
            while (phase1 < -180) phase1 += 360;
            while (phase2 > 180) phase2 -= 360;
            while (phase2 < -180) phase2 += 360;

            if ((phase1 >= -180 && phase2 <= -180) || (phase1 <= -180 && phase2 >= -180))
            {
                // Interpolate to find exact frequency
                var freq = InterpolateLinear(phase1, frequencies[i - 1], phase2, frequencies[i], -180.0);
                
                // Find gain at this frequency
                var gainResult = MeasureGainAtFreq(frequencies, magnitudeDb, freq);
                
                return new MeasurementResult
                {
                    Value = -gainResult.Value,
                    Unit = "dB",
                    Description = "Gain margin"
                };
            }
        }

        throw new InvalidOperationException("Could not find -180° phase point for gain margin calculation.");
    }

    /// <summary>
    /// Linear interpolation helper
    /// </summary>
    private static double InterpolateLinear(double x1, double y1, double x2, double y2, double x)
    {
        if (Math.Abs(x2 - x1) < 1e-10)
            return y1; // Avoid division by zero

        return y1 + (y2 - y1) * (x - x1) / (x2 - x1);
    }
}
