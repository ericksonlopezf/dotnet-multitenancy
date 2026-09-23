// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

/// <summary>
/// Provides OpenTelemetry metrics instruments and semantic tags for multi-tenant monitoring.
/// </summary>
public static class TenantMetrics
{
    /// <summary>
    /// Specifies the canonical Meter name for EricksonLopez.MultiTenancy instrumentation.
    /// </summary>
    /// <remarks>
    /// Register this meter with OpenTelemetry: <c>meterProviderBuilder.AddMeter(TenantMetrics.MeterName)</c>.
    /// </remarks>
    public const string MeterName = "EricksonLopez.MultiTenancy";

    /// <summary>
    /// Specifies the canonical version string of the <see cref="Meter"/> instance.
    /// </summary>
    public const string MeterVersion = "2.0.0";

    /// <summary>
    /// Gets the canonical <see cref="Meter"/> instance for multi-tenancy telemetry instrumentation.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, MeterVersion);

    /// <summary>
    /// Defines the counter instrument for total tenant resolution attempts.
    /// </summary>
    public static readonly Counter<long> ResolutionTotal = Meter.CreateCounter<long>(
        "tenant.resolution.total",
        unit: "{resolutions}",
        description: "Total number of tenant resolution attempts.");

    /// <summary>
    /// Defines the counter instrument for failed tenant resolution attempts.
    /// </summary>
    public static readonly Counter<long> ResolutionFailures = Meter.CreateCounter<long>(
        "tenant.resolution.failures",
        unit: "{failures}",
        description: "Total number of tenant resolution failures.");

    /// <summary>
    /// Defines the counter instrument for detected multi-tenant resolution conflicts.
    /// </summary>
    public static readonly Counter<long> ResolutionConflicts = Meter.CreateCounter<long>(
        "tenant.resolution.conflicts",
        unit: "{conflicts}",
        description: "Total number of resolution conflicts detected across strategies.");

    /// <summary>
    /// Defines the histogram instrument measuring the duration of tenant resolution operations in milliseconds.
    /// </summary>
    public static readonly Histogram<double> ResolutionDuration = Meter.CreateHistogram<double>(
        "tenant.resolution.duration",
        unit: "ms",
        description: "Duration of tenant resolution in milliseconds.");

    /// <summary>
    /// Records a successful tenant resolution metric.
    /// </summary>
    /// <param name="strategyName">The strategy that successfully resolved the tenant.</param>
    /// <param name="source">The resolution source enum value.</param>
    /// <param name="durationMs">The resolution duration in milliseconds.</param>
    public static void RecordResolutionSuccess(string strategyName, TenantResolutionSource source, double durationMs = 0)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("tenant.strategy", strategyName),
            new("tenant.source", source.ToString()),
            new("status", "success")
        };

        ResolutionTotal.Add(1, tags);
        if (durationMs > 0)
        {
            ResolutionDuration.Record(durationMs, tags);
        }
    }

    /// <summary>
    /// Records a failed tenant resolution metric.
    /// </summary>
    /// <param name="strategyName">The strategy that failed.</param>
    /// <param name="errorCode">The error code identifying the failure.</param>
    /// <param name="durationMs">The resolution duration in milliseconds.</param>
    public static void RecordResolutionFailure(string strategyName, string errorCode, double durationMs = 0)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("tenant.strategy", strategyName),
            new("error.code", errorCode),
            new("status", "failure")
        };

        ResolutionTotal.Add(1, tags);
        ResolutionFailures.Add(1, tags);
        if (durationMs > 0)
        {
            ResolutionDuration.Record(durationMs, tags);
        }
    }

    /// <summary>
    /// Records a detected resolution conflict between different strategies.
    /// </summary>
    /// <param name="firstStrategy">The name of the first strategy that resolved an ID.</param>
    /// <param name="secondStrategy">The name of the second strategy that resolved a conflicting ID.</param>
    public static void RecordResolutionConflict(string firstStrategy, string secondStrategy)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("tenant.strategy.first", firstStrategy),
            new("tenant.strategy.second", secondStrategy)
        };

        ResolutionConflicts.Add(1, tags);
    }
}
