// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using Dapper;

namespace EricksonLopez.MultiTenancy.Dapper;

/// <summary>
/// Provides extension methods for appending multi-tenant parameters to Dapper queries.
/// </summary>
public static class TenantDapperExtensions
{
    /// <summary>
    /// Appends the current tenant identifier to a <see cref="DynamicParameters"/> collection.
    /// </summary>
    /// <param name="parameters">The dynamic parameters collection to which the tenant identifier is added.</param>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="parameterName">The SQL parameter name (default: <c>TenantId</c>).</param>
    /// <returns>The specified dynamic parameters collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    public static DynamicParameters WithTenant(
        this DynamicParameters parameters,
        ITenantContext tenantContext,
        string parameterName = "TenantId")
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(tenantContext);

        parameters.Add(parameterName, tenantContext.RequiredTenant.Id.Value, DbType.Guid);
        return parameters;
    }

    /// <summary>
    /// Creates a new <see cref="DynamicParameters"/> instance containing the current tenant identifier.
    /// </summary>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="parameterName">The SQL parameter name (default: <c>TenantId</c>).</param>
    /// <returns>A new <see cref="DynamicParameters"/> instance initialized with the tenant identifier.</returns>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current context</exception>
    public static DynamicParameters CreateTenantParameters(
        this ITenantContext tenantContext,
        string parameterName = "TenantId")
    {
        var parameters = new DynamicParameters();
        return parameters.WithTenant(tenantContext, parameterName);
    }
}
