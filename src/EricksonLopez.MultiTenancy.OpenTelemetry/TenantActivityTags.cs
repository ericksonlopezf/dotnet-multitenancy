// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

/// <summary>
/// Provides top-level canonical OpenTelemetry semantic attribute tag names and baggage keys for multi-tenancy telemetry.
/// </summary>
public static class TenantActivityTags
{
    /// <summary>
    /// Specifies the OpenTelemetry attribute key for the unique tenant identifier ("tenant.id").
    /// </summary>
    public const string TenantId = TenantActivitySource.Tags.TenantId;

    /// <summary>
    /// Specifies the OpenTelemetry attribute key for the human-readable display name of the tenant ("tenant.name").
    /// </summary>
    public const string TenantName = TenantActivitySource.Tags.TenantName;

    /// <summary>
    /// Specifies the OpenTelemetry attribute key for the resolution source mechanism ("tenant.source").
    /// </summary>
    public const string TenantSource = TenantActivitySource.Tags.TenantSource;

    /// <summary>
    /// Specifies the OpenTelemetry attribute key indicating whether the resolved tenant is marked active ("tenant.is_active").
    /// </summary>
    public const string TenantIsActive = TenantActivitySource.Tags.TenantIsActive;

    /// <summary>
    /// Specifies the OpenTelemetry attribute key for the strategy name used during resolution ("tenant.resolution_strategy").
    /// </summary>
    public const string ResolutionStrategy = TenantActivitySource.Tags.ResolutionStrategy;

    /// <summary>
    /// Specifies the standard W3C baggage key for tenant identifier propagation across service boundaries ("tenant.id").
    /// </summary>
    public const string BaggageTenantId = TenantActivitySource.Baggage.TenantId;
}
