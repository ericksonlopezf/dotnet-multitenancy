// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics;

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

/// <summary>
/// Provides OpenTelemetry <see cref="ActivitySource"/> instrumentation and semantic attribute constants for multi-tenant tracing.
/// </summary>
public static class TenantActivitySource
{
    /// <summary>
    /// The canonical <see cref="ActivitySource"/> name for EricksonLopez.MultiTenancy tracing.
    /// </summary>
    /// <remarks>
    /// Register this source with OpenTelemetry: <c>tracerProviderBuilder.AddSource(TenantActivitySource.ActivitySourceName)</c>.
    /// </remarks>
    public const string ActivitySourceName = "EricksonLopez.MultiTenancy";

    /// <summary>
    /// The <see cref="ActivitySource"/> instance used to create multi-tenancy tracing spans.
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Semantic attribute names following OpenTelemetry conventions.
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// The unique tenant identifier.
        /// </summary>
        public const string TenantId = "tenant.id";

        /// <summary>
        /// The human-readable display name of the tenant.
        /// </summary>
        public const string TenantName = "tenant.name";

        /// <summary>
        /// The resolution source mechanism (e.g., JwtClaim, Host, Route, Header).
        /// </summary>
        public const string TenantSource = "tenant.source";

        /// <summary>
        /// Indicates whether the resolved tenant is marked active.
        /// </summary>
        public const string TenantIsActive = "tenant.is_active";

        /// <summary>
        /// The strategy name used during resolution.
        /// </summary>
        public const string ResolutionStrategy = "tenant.resolution_strategy";
    }

    /// <summary>
    /// W3C Baggage keys for cross-service tenant propagation.
    /// </summary>
    public static class Baggage
    {
        /// <summary>
        /// The standard W3C baggage key for tenant identifier propagation across HTTP/gRPC boundaries.
        /// </summary>
        public const string TenantId = "tenant.id";
    }
}
