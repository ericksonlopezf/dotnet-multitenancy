// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

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

/// <summary>
/// Provides extension methods for registering OpenTelemetry multi-tenancy instrumentation in an <see cref="IServiceCollection"/>.
/// </summary>
public static class MultiTenancyOpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers multi-tenancy OpenTelemetry tracing enrichers into the service container.
    /// </summary>
    /// <param name="services">The service collection to which the enricher is registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddMultiTenancyOpenTelemetry(this IServiceCollection services)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner AddSingleton
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITenantTraceEnricher, TenantTraceEnricher>();
        return services;
    }
}
