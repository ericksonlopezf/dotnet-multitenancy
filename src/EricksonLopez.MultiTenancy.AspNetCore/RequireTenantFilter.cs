// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
