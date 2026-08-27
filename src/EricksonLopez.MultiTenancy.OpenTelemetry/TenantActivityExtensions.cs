// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

/// <summary>
/// Provides extension methods for enriching OpenTelemetry activities and W3C baggage with tenant identity.
/// </summary>
public static class TenantActivityExtensions
{
    /// <summary>
    /// Enriches the specified activity with metadata from the provided tenant context.
    /// </summary>
    /// <param name="activity">The activity to enrich (may be <see langword="null"/>, in which case the method is a no-op).</param>
    /// <param name="tenantContext">The resolved tenant context.</param>
    /// <returns>The enriched activity for chaining.</returns>
    public static Activity? EnrichWithTenant(this Activity? activity, ITenantContext? tenantContext)
    {
        if (activity is null || tenantContext is null || !tenantContext.IsResolved)
        {
            return activity;
        }

        var tenant = tenantContext.Tenant;
        if (tenant is null)
        {
            return activity;
        }

        activity.SetTag(TenantActivitySource.Tags.TenantId, tenant.Id.ToString());
        activity.SetTag(TenantActivitySource.Tags.TenantName, tenant.Name);
        activity.SetTag(TenantActivitySource.Tags.TenantSource, tenantContext.Source.ToString());
        activity.SetTag(TenantActivitySource.Tags.TenantIsActive, tenant.IsActive);

        return activity;
    }

    /// <summary>
    /// Enriches the ambient <see cref="Activity.Current"/> with the provided tenant context.
    /// </summary>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identity to apply.</param>
    public static void EnrichCurrentActivity(ITenantContext? tenantContext)
    {
        Activity.Current.EnrichWithTenant(tenantContext);
    }

    /// <summary>
    /// Sets the W3C baggage item for the tenant identifier on the activity.
    /// </summary>
    /// <param name="activity">The activity to attach baggage to.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The updated activity.</returns>
    public static Activity? SetTenantBaggage(this Activity? activity, TenantId tenantId)
    {
        if (activity is null || tenantId == TenantId.Empty)
        {
            return activity;
        }

        activity.SetBaggage(TenantActivitySource.Baggage.TenantId, tenantId.ToString());
        return activity;
    }

    /// <summary>
    /// Records a tenant resolution failure event on the activity.
    /// </summary>
    /// <param name="activity">The activity on which to record the event.</param>
    /// <param name="error">The failure error details.</param>
    /// <param name="strategyName">The strategy that encountered the failure.</param>
    /// <returns>The updated activity.</returns>
    public static Activity? RecordTenantResolutionFailure(this Activity? activity, Error error, string? strategyName = null)
    {
        if (activity is null)
        {
            return null;
        }

        var tags = new ActivityTagsCollection
        {
            { "error.type", error.Code },
            { "error.message", error.Description }
        };

        if (!string.IsNullOrEmpty(strategyName))
        {
            tags.Add(TenantActivitySource.Tags.ResolutionStrategy, strategyName);
        }

        activity.AddEvent(new ActivityEvent("tenant.resolution_failed", tags: tags));
        return activity;
    }
}
