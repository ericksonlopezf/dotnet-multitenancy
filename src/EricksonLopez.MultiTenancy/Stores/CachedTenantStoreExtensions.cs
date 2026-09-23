// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.MultiTenancy.Stores;

/// <summary>
/// Provides extension methods for registering <see cref="CachedTenantStore{TTenant}"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class CachedTenantStoreExtensions
{
    /// <summary>
    /// Decorates the registered <see cref="ITenantStore{TTenant}"/> and <see cref="ITenantLookupStore{TTenant}"/> with a memory cache.
    /// </summary>
    /// <remarks>
    /// Requires memory caching to be registered in the service collection.
    /// </remarks>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection containing the store to decorate.</param>
    /// <param name="configureOptions">An optional configuration action for cache options.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException">No <see cref="ITenantStore{TTenant}"/> is registered in <paramref name="services"/></exception>
    public static IServiceCollection AddCachedTenantStore<TTenant>(this IServiceCollection services, Action<CachedTenantStoreOptions>? configureOptions = null)
        where TTenant : class, ITenantInfo
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITenantStore<TTenant>));
        if (descriptor is null)
        {
            throw new InvalidOperationException($"No {typeof(ITenantStore<TTenant>).Name} is registered to decorate with a cache.");
        }

        var objectFactory = ActivatorUtilities.CreateFactory(typeof(CachedTenantStore<TTenant>), [typeof(ITenantStore<TTenant>)]);

        services.Replace(ServiceDescriptor.Describe(
            typeof(ITenantStore<TTenant>),
            provider =>
            {
                var innerInstance = descriptor.ImplementationInstance ??
                                    (descriptor.ImplementationFactory != null
                                        ? descriptor.ImplementationFactory(provider)
                                        : ActivatorUtilities.GetServiceOrCreateInstance(provider, descriptor.ImplementationType!));

                return (ITenantStore<TTenant>)objectFactory(provider, [innerInstance]);
            },
            descriptor.Lifetime));

        var lookupDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITenantLookupStore<TTenant>));
        if (lookupDescriptor is not null)
        {
            services.Replace(ServiceDescriptor.Describe(
                typeof(ITenantLookupStore<TTenant>),
                provider => (ITenantLookupStore<TTenant>)provider.GetRequiredService<ITenantStore<TTenant>>(),
                descriptor.Lifetime));
        }

        var untypedLookupDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITenantLookupStore));
        if (untypedLookupDescriptor is not null)
        {
            services.Replace(ServiceDescriptor.Describe(
                typeof(ITenantLookupStore),
                provider => (ITenantLookupStore)provider.GetRequiredService<ITenantStore<TTenant>>(),
                descriptor.Lifetime));
        }

        return services;
    }
}
