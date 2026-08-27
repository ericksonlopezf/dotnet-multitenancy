// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides extension methods for configuring multi-tenancy core services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core multi-tenancy infrastructure services in the service collection.
    /// </summary>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="ITenantContextAccessor"/> as Scoped (<see cref="ScopedTenantContextAccessor"/>)</item>
    ///   <item><see cref="ITenantContext"/> as Scoped (resolved from the accessor)</item>
    ///   <item><see cref="ITenantScopeFactory"/> as Singleton (<see cref="DefaultTenantScopeFactory"/>)</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The service collection to which the services are registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped — one accessor per request or DI scope. Write-once semantics.
        services.TryAddScoped<ITenantContextAccessor, ScopedTenantContextAccessor>();

        services.TryAddScoped<ITenantContext>(sp =>
        {
            var accessor = sp.GetRequiredService<ITenantContextAccessor>();
            return accessor.TenantContext ?? TenantContext.Empty;
        });

        // Singleton — stateless factory, safe as singleton
        services.TryAddSingleton<ITenantScopeFactory, DefaultTenantScopeFactory>();

        return services;
    }

    /// <summary>
    /// Registers core multi-tenancy infrastructure services with a strongly-typed tenant model in the service collection.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the services are registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddMultiTenancy<TTenant>(this IServiceCollection services)
        where TTenant : class, ITenantInfo
    {
        // Argument validation is performed by the non-generic AddMultiTenancy overload.
        services.AddMultiTenancy();
        services.TryAddScoped<ITenantContext<TTenant>>(sp =>
        {
            var accessor = sp.GetRequiredService<ITenantContextAccessor>();
            if (accessor.TenantContext is ITenantContext<TTenant> typedContext)
            {
                return typedContext;
            }

            if (accessor.TenantContext?.Tenant is TTenant typedTenant)
            {
                return new TenantContext<TTenant>(typedTenant, accessor.TenantContext.Source);
            }

            return TenantContext<TTenant>.Empty;
        });

        return services;
    }

    /// <summary>
    /// Registers an in-memory tenant store in the service collection.
    /// </summary>
    /// <remarks>
    /// Intended for testing, development, and static tenant catalogs.
    /// </remarks>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the services are registered.</param>
    /// <param name="configureStore">An optional action to configure or seed the in-memory store.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddInMemoryTenantStore<TTenant>(
        this IServiceCollection services,
        Action<InMemoryTenantStore<TTenant>>? configureStore = null)
        where TTenant : class, ITenantInfo
    {
        ArgumentNullException.ThrowIfNull(services);

        var store = new InMemoryTenantStore<TTenant>();
        configureStore?.Invoke(store);

        services.TryAddSingleton<ITenantStore<TTenant>>(store);
        services.TryAddSingleton<ITenantStore>(store);

        return services;
    }

    /// <summary>
    /// Registers a remote HTTP tenant store querying an upstream tenant service in the service collection.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the services are registered.</param>
    /// <param name="configure">An optional action to configure remote tenant store options.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddHttpRemoteTenantStore<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TTenant>(
        this IServiceCollection services,
        Action<Stores.HttpRemoteTenantStoreOptions>? configure = null)
        where TTenant : class, ITenantInfo
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddScoped<Stores.HttpRemoteTenantStore<TTenant>>(sp =>
        {
            var options = sp.GetService<Microsoft.Extensions.Options.IOptions<Stores.HttpRemoteTenantStoreOptions>>()
                ?? Microsoft.Extensions.Options.Options.Create(new Stores.HttpRemoteTenantStoreOptions());
            var httpClient = sp.GetService<System.Net.Http.HttpClient>() ?? new System.Net.Http.HttpClient();
            return new Stores.HttpRemoteTenantStore<TTenant>(httpClient, options);
        });

        services.TryAddScoped<ITenantStore<TTenant>>(sp => sp.GetRequiredService<Stores.HttpRemoteTenantStore<TTenant>>());
        services.TryAddScoped<ITenantStore>(sp => sp.GetRequiredService<Stores.HttpRemoteTenantStore<TTenant>>());
        services.TryAddScoped<ITenantLookupStore<TTenant>>(sp => sp.GetRequiredService<Stores.HttpRemoteTenantStore<TTenant>>());
        services.TryAddScoped<ITenantLookupStore>(sp => sp.GetRequiredService<Stores.HttpRemoteTenantStore<TTenant>>());

        return services;
    }
}
