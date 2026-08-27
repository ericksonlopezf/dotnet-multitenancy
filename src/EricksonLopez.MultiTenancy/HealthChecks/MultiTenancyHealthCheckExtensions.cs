// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.MultiTenancy.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides extension methods for registering <see cref="MultiTenancyHealthCheck"/> in health check services.
/// </summary>
public static class MultiTenancyHealthCheckExtensions
{
    /// <summary>
    /// Registers a <see cref="MultiTenancyHealthCheck"/> service in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the health check is added.</param>
    /// <param name="configure">An optional action to configure health check options.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddMultiTenancyHealthCheck(
        this IServiceCollection services,
        Action<MultiTenancyHealthCheckOptions>? configure = null)
    {
        // Stryker disable once statement : Guard clause mutation is redundant with chained extension methods
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddTransient<IHealthCheck, MultiTenancyHealthCheck>();
        return services;
    }
}
