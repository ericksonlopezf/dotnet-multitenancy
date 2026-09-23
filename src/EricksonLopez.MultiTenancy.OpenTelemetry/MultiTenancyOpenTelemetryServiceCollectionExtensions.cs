// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.OpenTelemetry;

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
