// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Enforces that a valid, active tenant context is present for mapped endpoints.
/// </summary>
public sealed class RequireTenantFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var tenantContext = context.HttpContext.RequestServices.GetService<ITenantContext>();
        if (tenantContext is null || !tenantContext.IsResolved)
        {
            return Results.Problem(
                title: "Tenant Required",
                detail: "A valid tenant identifier is required to access this resource.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Stryker disable once boolean
        return await next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Provides extension methods for ASP.NET Core multi-tenancy registration and pipeline configuration.
/// </summary>
public static class AspNetCoreMultiTenancyExtensions
{
    /// <summary>
    /// Registers ASP.NET Core tenant resolution strategies and context accessor in the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the services are registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    public static IServiceCollection AddAspNetCoreMultiTenancy(this IServiceCollection services)
    {
        // Null check delegated to inner extensions
        services.AddHttpContextAccessor();
        services.AddMultiTenancy();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy, ClaimTenantResolutionStrategy>());

        return services;
    }

    /// <summary>
    /// Registers a header-based tenant resolution strategy for internal service-to-service communication.
    /// </summary>
    /// <remarks>
    /// This strategy should not be used for endpoints exposed to external clients due to tenant spoofing risks.
    /// </remarks>
    /// <param name="services">The service collection to which the strategy is registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    public static IServiceCollection AddInternalHeaderTenantResolution(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>());
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
            return new HostNameTenantResolutionStrategy(httpContextAccessor, sp);
        });
        return services;
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
}
