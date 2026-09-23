// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EricksonLopez.MultiTenancy.AspNetCore.Routing;

/// <summary>
/// Provides an ASP.NET Core route constraint that validates whether a route parameter is a valid <see cref="TenantId"/>.
/// </summary>
public sealed class TenantRouteConstraint : IRouteConstraint
{
    /// <inheritdoc />
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (values.TryGetValue(routeKey, out var value) && value is not null)
        {
            var stringValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            return TenantId.TryCreate(stringValue, out _);
        }

        return false;
    }
}
