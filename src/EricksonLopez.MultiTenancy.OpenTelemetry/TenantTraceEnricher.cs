// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

/// <summary>
/// Provides the default implementation of <see cref="ITenantTraceEnricher"/> that writes tenant tags to the ambient <see cref="System.Diagnostics.Activity"/>.
/// </summary>
public sealed class TenantTraceEnricher : ITenantTraceEnricher
{
    /// <inheritdoc />
    public void Enrich(ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Activity.Current.EnrichWithTenant(context);
    }
}
