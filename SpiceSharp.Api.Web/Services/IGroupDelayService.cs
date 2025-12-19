using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Service for calculating group delay from phase data
/// </summary>
public interface IGroupDelayService
{
    /// <summary>
    /// Calculates group delay from AC analysis phase data
    /// </summary>
    /// <param name="circuitId">Circuit ID with cached AC analysis results</param>
    /// <param name="signal">Signal to analyze (e.g., "v(out)")</param>
    /// <param name="reference">Reference signal for ratio measurements (optional, typically input)</param>
    /// <returns>Group delay result with frequencies and delay values</returns>
    GroupDelayResult CalculateGroupDelay(string circuitId, string signal, string? reference);
}
