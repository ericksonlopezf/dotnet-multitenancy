// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.AspNetCore.Options;

/// <summary>
/// Provides extension methods for registering per-tenant options caching in an <see cref="IServiceCollection"/>.
/// </summary>
public static class MultiTenancyOptionsExtensions
{
    /// <summary>
    /// Configures the specified options type to be isolated and cached per tenant.
    /// </summary>
    /// <remarks>
    /// Required to support per-tenant settings such as authentication options or connection strings.
    /// </remarks>
    /// <typeparam name="TOptions">The type of options being configured.</typeparam>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the options cache is registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPerTenantOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions, TTenant>(this IServiceCollection services)
        where TOptions : class
        where TTenant : class, ITenantInfo
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replace the default IOptionsMonitorCache<TOptions> with our tenant-aware version
        services.Replace(ServiceDescriptor.Singleton<IOptionsMonitorCache<TOptions>, TenantOptionsCache<TOptions, TTenant>>());

        return services;
    }

    /// <summary>
    /// Configures the specified options type to be isolated and cached per tenant, and applies a configuration action with the active tenant.
    /// </summary>
    /// <typeparam name="TOptions">The type of options being configured.</typeparam>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the options cache is registered.</param>
    /// <param name="configureOptions">The action to configure the options using the active tenant metadata.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPerTenantOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions, TTenant>(
        this IServiceCollection services,
        Action<TOptions, TTenant> configureOptions)
        where TOptions : class
        where TTenant : class, ITenantInfo
    {
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddPerTenantOptions<TOptions, TTenant>();

        services.AddTransient<IConfigureOptions<TOptions>>(sp =>
        {
            var accessor = sp.GetService<ITenantContextAccessor>();
            return new ConfigureNamedOptions<TOptions>(null, options =>
            {
                if (accessor?.TenantContext?.Tenant is TTenant tenant)
                {
                    configureOptions(options, tenant);
                }
            });
        });

        return services;
    }


    /// <summary>
    /// Configures the specified options type to be isolated and cached per tenant, and configures cache options.
    /// </summary>
    /// <typeparam name="TOptions">The type of options being configured.</typeparam>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the options cache is registered.</param>
    /// <param name="configureCache">The action to configure the tenant options cache.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureCache"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPerTenantOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions, TTenant>(
        this IServiceCollection services,
        Action<TenantOptionsCacheOptions> configureCache)
        where TOptions : class
        where TTenant : class, ITenantInfo
    {
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(configureCache);

        services.Configure(configureCache);
        return services.AddPerTenantOptions<TOptions, TTenant>();
    }

    /// <summary>
    /// Configures the specified options type to be isolated and cached per tenant, applies cache options, and applies a configuration action with the active tenant.
    /// </summary>
    /// <typeparam name="TOptions">The type of options being configured.</typeparam>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the options cache is registered.</param>
    /// <param name="configureCache">The action to configure the tenant options cache.</param>
    /// <param name="configureOptions">The action to configure the options using the active tenant metadata.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="configureCache"/>, or <paramref name="configureOptions"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPerTenantOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions, TTenant>(
        this IServiceCollection services,
        Action<TenantOptionsCacheOptions> configureCache,
        Action<TOptions, TTenant> configureOptions)
        where TOptions : class
        where TTenant : class, ITenantInfo
    {
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(configureCache);
        // Stryker disable once statement : Argument null guards
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureCache);
        return services.AddPerTenantOptions<TOptions, TTenant>(configureOptions);
    }
}

