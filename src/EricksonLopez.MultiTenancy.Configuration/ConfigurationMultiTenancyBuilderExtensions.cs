// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.MultiTenancy.Configuration;

/// <summary>
/// Provides extension methods for configuring <see cref="ConfigurationTenantStore{TTenant}"/>.
/// </summary>
public static class ConfigurationMultiTenancyBuilderExtensions
{
    /// <summary>
    /// Registers a tenant store that retrieves tenant metadata from configuration.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the store is registered.</param>
    /// <param name="configureOptions">The action to configure the multi-tenancy options.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/></exception>
    public static IServiceCollection AddConfigurationTenantStore<TTenant>(this IServiceCollection services, Action<MultiTenancyOptions<TTenant>> configureOptions)
        where TTenant : class, ITenantInfo, new()
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner Configure
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Guard clause defensively duplicated by inner Configure
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<ITenantStore<TTenant>, ConfigurationTenantStore<TTenant>>();
        services.TryAddSingleton<ITenantLookupStore<TTenant>, ConfigurationTenantStore<TTenant>>();

        return services;
    }
}
