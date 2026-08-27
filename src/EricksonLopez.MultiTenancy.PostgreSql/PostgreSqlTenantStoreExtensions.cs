// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Provides extension methods for registering <see cref="PostgreSqlTenantStore{TTenant}"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class PostgreSqlTenantStoreExtensions
{
    /// <summary>
    /// Registers a PostgreSQL-backed tenant store using <see cref="TenantInfo"/>.
    /// </summary>
    /// <param name="services">The service collection to which the store is registered.</param>
    /// <param name="configure">The action to configure store options.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPostgreSqlTenantStore(
        this IServiceCollection services,
        Action<PostgreSqlTenantStoreOptions> configure)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner AddPostgreSqlTenantStore
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Guard clause defensively duplicated by inner AddPostgreSqlTenantStore
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddPostgreSqlTenantStore<TenantInfo>(configure);
    }

    /// <summary>
    /// Registers a PostgreSQL-backed tenant store using <see cref="TenantInfo"/> with a connection string.
    /// </summary>
    /// <param name="services">The service collection to which the store is registered.</param>
    /// <param name="connectionString">The PostgreSQL database connection string.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    public static IServiceCollection AddPostgreSqlTenantStore(
        this IServiceCollection services,
        string connectionString)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner AddPostgreSqlTenantStore
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        return services.AddPostgreSqlTenantStore<TenantInfo>(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Registers a strongly-typed PostgreSQL-backed tenant store for <typeparamref name="TTenant"/>.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the store is registered.</param>
    /// <param name="configure">The action to configure store options.</param>
    /// <param name="customMapper">The optional custom reader mapping delegate.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPostgreSqlTenantStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicConstructors)] TTenant>(
        this IServiceCollection services,
        Action<PostgreSqlTenantStoreOptions> configure,
        Func<DbDataReader, TTenant>? customMapper = null)
        where TTenant : class, ITenantInfo
    {
        // Stryker disable once statement : Guard clause duplicated by services.Configure
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Guard clause duplicated by services.Configure
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.TryAddScoped<PostgreSqlTenantStore<TTenant>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PostgreSqlTenantStoreOptions>>();
            var dataSource = sp.GetService<Npgsql.NpgsqlDataSource>();
            return new PostgreSqlTenantStore<TTenant>(options, dataSource, customMapper);
        });

        services.TryAddScoped<ITenantStore<TTenant>>(sp => sp.GetRequiredService<PostgreSqlTenantStore<TTenant>>());
        services.TryAddScoped<ITenantLookupStore<TTenant>>(sp => sp.GetRequiredService<PostgreSqlTenantStore<TTenant>>());
        services.TryAddScoped<ITenantStore>(sp => sp.GetRequiredService<PostgreSqlTenantStore<TTenant>>());
        services.TryAddScoped<ITenantLookupStore>(sp => sp.GetRequiredService<PostgreSqlTenantStore<TTenant>>());

        return services;
    }

    /// <summary>
    /// Registers a strongly-typed PostgreSQL-backed tenant store with a connection string for <typeparamref name="TTenant"/>.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the store is registered.</param>
    /// <param name="connectionString">The PostgreSQL database connection string.</param>
    /// <param name="customMapper">The optional custom reader mapping delegate.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    public static IServiceCollection AddPostgreSqlTenantStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicConstructors)] TTenant>(
        this IServiceCollection services,
        string connectionString,
        Func<DbDataReader, TTenant>? customMapper = null)
        where TTenant : class, ITenantInfo
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner AddPostgreSqlTenantStore
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        return services.AddPostgreSqlTenantStore<TTenant>(options =>
        {
            options.ConnectionString = connectionString;
        }, customMapper);
    }
}
