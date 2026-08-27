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
}
