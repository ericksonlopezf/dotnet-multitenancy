// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Authentication;

/// <summary>
/// Validates cookie authentication principals against the ambient tenant context.
/// </summary>
/// <remarks>
/// Ensures that a cookie issued for one tenant is rejected when accessed in a different tenant context.
/// </remarks>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "TTenant generic type parameter aligns with strongly-typed multi-tenancy dependency injection patterns.")]
public class TenantCookieAuthenticationEvents<TTenant> : CookieAuthenticationEvents
    where TTenant : class, ITenantInfo
{
    private readonly string _tenantClaimType;
    private readonly bool _requireTenantClaim;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCookieAuthenticationEvents{TTenant}"/> class.
    /// </summary>
    /// <param name="tenantClaimType">The claim type used to store the tenant identifier in the cookie (default: <c>tenant_id</c>).</param>
    /// <param name="requireTenantClaim">
    /// If <see langword="true"/>, rejects authenticated principals that lack a tenant claim when a tenant context is active (default: <see langword="true"/>).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="tenantClaimType"/> is <see langword="null"/></exception>
    public TenantCookieAuthenticationEvents(string tenantClaimType = "tenant_id", bool requireTenantClaim = true)
    {
        _tenantClaimType = tenantClaimType ?? throw new ArgumentNullException(nameof(tenantClaimType));
        _requireTenantClaim = requireTenantClaim;
    }

    /// <inheritdoc />
    public override Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantContextAccessor = context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>();
        var currentTenantContext = tenantContextAccessor.TenantContext;

        if (currentTenantContext?.Tenant != null && context.Principal?.Identity?.IsAuthenticated == true)
        {
            var principalTenantId = context.Principal.FindFirst(_tenantClaimType)?.Value;

            if (principalTenantId is null)
            {
                if (_requireTenantClaim)
                {
                    context.RejectPrincipal();
                    return Task.CompletedTask;
                }
            }
            else if (!string.Equals(principalTenantId, currentTenantContext.Tenant.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                context.RejectPrincipal();
                return Task.CompletedTask;
            }
        }

        return base.ValidatePrincipal(context);
    }
}
