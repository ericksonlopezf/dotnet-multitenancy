// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides extension methods for ASP.NET Core multi-tenancy registration and pipeline configuration.
/// </summary>
public static class AspNetCoreMultiTenancyExtensions
{
    /// <summary>
    /// Registers ASP.NET Core tenant resolution strategies and context accessor in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the services are registered.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="TenantResolutionMiddlewareOptions"/>.</param>
    /// <returns>The specified service collection for chaining.</returns>
    public static IServiceCollection AddAspNetCoreMultiTenancy(
        this IServiceCollection services,
        Action<TenantResolutionMiddlewareOptions>? configureOptions = null)
    {
        // Null check delegated to inner extensions
        services.AddHttpContextAccessor();
        services.AddMultiTenancy();

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy, ClaimTenantResolutionStrategy>());

        return services;
    }

    /// <summary>
    /// Registers the <see cref="EricksonLopez.MultiTenancy.AspNetCore.Routing.TenantRouteConstraint"/> for use in ASP.NET Core routing.
    /// </summary>
    /// <param name="services">The service collection to which the constraint is registered.</param>
    /// <param name="constraintName">The constraint name to register in the route options (default: <c>tenant</c>).</param>
    /// <returns>The specified service collection for chaining.</returns>
    public static IServiceCollection AddTenantRouteConstraint(this IServiceCollection services, string constraintName = "tenant")
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(constraintName))
        {
            throw new ArgumentException("Constraint name cannot be null or whitespace.", nameof(constraintName));
        }

        services.Configure<RouteOptions>(options =>
        {
#pragma warning disable IL2026 // Parameter policies are safe for trimming as long as the constraint type is preserved
            options.SetParameterPolicy<EricksonLopez.MultiTenancy.AspNetCore.Routing.TenantRouteConstraint>(constraintName);
#pragma warning restore IL2026
        });

        return services;
    }

    /// <summary>
    /// Registers a header-based tenant resolution strategy for internal service-to-service communication.
    /// </summary>
    /// <remarks>
    /// CAUTION: This strategy resolves tenant identifiers from untrusted client headers without secret validation.
    /// It should strictly be used in internal architectures protected by mTLS or reverse proxies that strip client headers.
    /// Use <see cref="AddInternalHeaderTenantResolution(IServiceCollection, string, string, string)"/> to enforce shared gateway secret verification.
    /// </remarks>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    public static IServiceCollection AddInternalHeaderTenantResolution(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>());
        return services;
    }

    /// <summary>
    /// Registers a header-based tenant resolution strategy authenticated by a shared gateway secret.
    /// </summary>
    /// <remarks>
    /// This strategy requires the incoming request to supply the expected shared secret header, preventing external spoofing.
    /// </remarks>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <param name="expectedSharedSecret">The expected shared secret from the trusted reverse proxy / API gateway.</param>
    /// <param name="headerName">The header name used to extract the tenant identifier (default: <c>X-Tenant-ID</c>).</param>
    /// <param name="secretHeaderName">The header name containing the shared gateway secret (default: <c>X-Gateway-Secret</c>).</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="expectedSharedSecret"/> is <see langword="null"/> or whitespace</exception>
    public static IServiceCollection AddInternalHeaderTenantResolution(
        this IServiceCollection services,
        string expectedSharedSecret,
        string headerName = "X-Tenant-ID",
        string secretHeaderName = "X-Gateway-Secret")
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(expectedSharedSecret))
        {
            throw new ArgumentException("Expected shared secret cannot be null or whitespace.", nameof(expectedSharedSecret));
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy, InternalGatewayHeaderTenantResolutionStrategy>(sp =>
            new InternalGatewayHeaderTenantResolutionStrategy(
                sp.GetRequiredService<IHttpContextAccessor>(),
                expectedSharedSecret,
                headerName,
                secretHeaderName)));
        return services;
    }

    /// <summary>
    /// Registers a delegate-based tenant resolution strategy in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <param name="resolver">The delegate function used to resolve the tenant identifier.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="resolver"/> is <see langword="null"/></exception>
    public static IServiceCollection AddDelegateTenantStrategy(this IServiceCollection services, Func<System.Threading.CancellationToken, ValueTask<Result.Result<TenantId>>> resolver)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner method
        ArgumentNullException.ThrowIfNull(services);
        // Stryker disable once statement : Guard clause defensively duplicated by inner constructor
        ArgumentNullException.ThrowIfNull(resolver);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy>(new DelegateTenantResolutionStrategy(resolver)));
        return services;
    }

    /// <summary>
    /// Registers a static tenant resolution strategy in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <param name="tenantId">The static tenant identifier to always resolve.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddStaticTenantStrategy(this IServiceCollection services, TenantId tenantId)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner method
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy>(new StaticTenantResolutionStrategy(tenantId)));
        return services;
    }

    /// <summary>
    /// Registers a route-based tenant resolution strategy in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <param name="routeParameterName">The route parameter name used to extract the tenant identifier.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddRouteTenantStrategy(this IServiceCollection services, string routeParameterName = "tenantId")
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner method
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<ITenantResolutionStrategy>(sp =>
        {
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            return new RouteTenantResolutionStrategy(httpContextAccessor, routeParameterName);
        });
        return services;
    }

    /// <summary>
    /// Registers a host name and subdomain tenant resolution strategy in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddHostNameTenantStrategy(this IServiceCollection services)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner method
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<ITenantResolutionStrategy>(sp =>
        {
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            var options = sp.GetService<IOptions<HostNameTenantResolutionStrategyOptions>>();
            return new HostNameTenantResolutionStrategy(httpContextAccessor, sp, options);
        });
        return services;
    }

    /// <summary>
    /// Registers a host name and subdomain tenant resolution strategy with custom options in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <param name="configureOptions">An action to configure <see cref="HostNameTenantResolutionStrategyOptions"/>.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/></exception>
    public static IServiceCollection AddHostNameTenantStrategy(
        this IServiceCollection services,
        Action<HostNameTenantResolutionStrategyOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        return services.AddHostNameTenantStrategy();
    }

    /// <summary>
    /// Registers a URL base path segment tenant resolution strategy in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <param name="segmentIndex">The zero-based path segment index containing the tenant identifier.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddBasePathStrategy(this IServiceCollection services, int segmentIndex = 0)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner method
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<ITenantResolutionStrategy>(sp =>
        {
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            return new BasePathTenantResolutionStrategy(httpContextAccessor, sp, segmentIndex);
        });
        return services;
    }

    /// <summary>
    /// Adds the <see cref="TenantResolutionMiddleware"/> to the application request pipeline.
    /// </summary>
    /// <param name="app">The application builder instance.</param>
    /// <returns>The specified application builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/></exception>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Pipeline configuration extension method, thoroughly tested via integration.")]
    public static IApplicationBuilder UseMultiTenancy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }

    /// <summary>
    /// Enforces that a valid tenant must be resolved for this endpoint route.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The specified route handler builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Pipeline configuration extension method, thoroughly tested via integration.")]
    public static RouteHandlerBuilder RequireTenant(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Stryker disable once statement
        builder.AddEndpointFilter<RequireTenantFilter>();
        return builder;
    }

    /// <summary>
    /// Enforces that a valid tenant must be resolved for this endpoint route group.
    /// </summary>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The specified route group builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static RouteGroupBuilder RequireTenant(this RouteGroupBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Stryker disable once statement
        builder.AddEndpointFilter<RequireTenantFilter>();
        return builder;
    }

    /// <summary>
    /// Specifies that the endpoint allows anonymous or tenant-unresolved requests even when fail-closed resolution is active.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The specified route handler builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Pipeline configuration extension method, thoroughly tested via integration.")]
    public static RouteHandlerBuilder AllowAnonymousTenant(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(new AllowAnonymousTenantAttribute());
        return builder;
    }

    /// <summary>
    /// Specifies that the endpoint route group allows anonymous or tenant-unresolved requests even when fail-closed resolution is active.
    /// </summary>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The specified route group builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Pipeline configuration extension method, thoroughly tested via integration.")]
    public static RouteGroupBuilder AllowAnonymousTenant(this RouteGroupBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(new AllowAnonymousTenantAttribute());
        return builder;
    }
}
