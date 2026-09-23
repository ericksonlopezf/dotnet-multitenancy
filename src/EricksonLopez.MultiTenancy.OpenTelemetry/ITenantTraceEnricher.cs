// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics;

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

/// <summary>
/// Defines a contract for enriching telemetry activities with tenant identity.
/// </summary>
public interface ITenantTraceEnricher
{
    /// <summary>
    /// Enriches the ambient <see cref="Activity.Current"/> from the given tenant context.
    /// </summary>
    /// <param name="context">The resolved tenant context.</param>
    void Enrich(ITenantContext context);
}
