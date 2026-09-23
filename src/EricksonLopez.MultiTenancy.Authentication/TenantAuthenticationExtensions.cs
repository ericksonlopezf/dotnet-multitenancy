// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Authentication;

/// <summary>
/// Provides extension methods for configuring per-tenant authentication in an <see cref="IServiceCollection"/>.
/// </summary>
public static class TenantAuthenticationExtensions
{
    /// <summary>
    /// Configures core authentication options to be isolated and cached per tenant.
    /// </summary>
    /// <remarks>
    /// Enables assigning different default schemes depending on the active tenant.
    /// To configure specific scheme options per-tenant (such as JwtBearerOptions), call <see cref="MultiTenancyOptionsExtensions.AddPerTenantOptions{TOptions, TTenant}"/> for those types.
    /// </remarks>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the per-tenant authentication is registered.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPerTenantAuthentication<TTenant>(this IServiceCollection services)
        where TTenant : class, ITenantInfo
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner AddPerTenantOptions
        ArgumentNullException.ThrowIfNull(services);

        // Core authentication options (e.g., DefaultScheme, DefaultAuthenticateScheme)
        services.AddPerTenantOptions<AuthenticationOptions, TTenant>();

        // Ensure Cookies can be isolated if they are used, as it's the most common stateful auth
        services.AddPerTenantOptions<CookieAuthenticationOptions, TTenant>();

        return services;
    }

    /// <summary>
    /// Configures core authentication options to be isolated and cached per tenant, with dynamic scheme selection.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
    /// <param name="services">The service collection to which the per-tenant authentication is registered.</param>
    /// <param name="configure">The action to configure per-tenant authentication options.</param>
    /// <returns>The specified service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPerTenantAuthentication<TTenant>(
        this IServiceCollection services,
        Action<TenantAuthenticationOptions> configure)
        where TTenant : class, ITenantInfo
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TenantAuthenticationOptions();
        configure(options);

        services.Configure(configure);

        if (options.DefaultSchemeSelector is not null)
        {
            services.AddPerTenantOptions<AuthenticationOptions, TTenant>((authOptions, tenant) =>
            {
                var scheme = options.DefaultSchemeSelector(tenant);
                if (!string.IsNullOrEmpty(scheme))
                {
                    authOptions.DefaultScheme = scheme;
                }
            });
            services.AddPerTenantOptions<CookieAuthenticationOptions, TTenant>();
        }
        else
        {
            services.AddPerTenantAuthentication<TTenant>();
        }

        return services;
    }
}
