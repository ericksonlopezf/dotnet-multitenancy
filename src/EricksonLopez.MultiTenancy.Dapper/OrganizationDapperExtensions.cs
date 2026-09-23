// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Linq;
using Dapper;

namespace EricksonLopez.MultiTenancy.Dapper;

/// <summary>
/// Provides extension methods for appending compound organization hierarchy parameters to Dapper queries.
/// </summary>
public static class OrganizationDapperExtensions
{
    /// <summary>
    /// Appends company identifier to a <see cref="DynamicParameters"/> collection.
    /// </summary>
    /// <param name="parameters">The dynamic parameters collection.</param>
    /// <param name="companyContext">The resolved company context.</param>
    /// <param name="parameterName">The SQL parameter name (default: <c>CompanyId</c>).</param>
    /// <returns>The dynamic parameters collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="companyContext"/> is <see langword="null"/></exception>
    public static DynamicParameters WithCompany(
        this DynamicParameters parameters,
        ICompanyContext companyContext,
        string parameterName = "CompanyId")
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(companyContext);

        if (companyContext.CompanyId.HasValue)
        {
            parameters.Add(parameterName, companyContext.CompanyId.Value, DbType.Guid);
        }

        return parameters;
    }

    /// <summary>
    /// Appends branch identifier to a <see cref="DynamicParameters"/> collection.
    /// </summary>
    /// <param name="parameters">The dynamic parameters collection.</param>
    /// <param name="branchContext">The resolved branch context.</param>
    /// <param name="parameterName">The SQL parameter name (default: <c>BranchId</c>).</param>
    /// <returns>The dynamic parameters collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="branchContext"/> is <see langword="null"/></exception>
    public static DynamicParameters WithBranch(
        this DynamicParameters parameters,
        IBranchContext branchContext,
        string parameterName = "BranchId")
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(branchContext);

        if (branchContext.BranchId.HasValue)
        {
            parameters.Add(parameterName, branchContext.BranchId.Value, DbType.Guid);
        }

        return parameters;
    }

    /// <summary>
    /// Appends full organization hierarchy parameters (Tenant, Company, Branch) to a <see cref="DynamicParameters"/> collection.
    /// </summary>
    /// <param name="parameters">The dynamic parameters collection.</param>
    /// <param name="context">The compound organization context.</param>
    /// <param name="tenantParam">The tenant parameter name.</param>
    /// <param name="companyParam">The company parameter name.</param>
    /// <param name="branchParam">The branch parameter name.</param>
    /// <returns>The dynamic parameters collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="context"/> is <see langword="null"/></exception>
    public static DynamicParameters WithOrganization(
        this DynamicParameters parameters,
        IOrganizationContext context,
        string tenantParam = "TenantId",
        string companyParam = "CompanyId",
        string branchParam = "BranchId")
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(context);

        parameters.Add(tenantParam, context.RequiredTenant.Id.Value, DbType.Guid);

        if (context.CompanyId.HasValue)
        {
            parameters.Add(companyParam, context.CompanyId.Value, DbType.Guid);
        }

        if (context.BranchId.HasValue)
        {
            parameters.Add(branchParam, context.BranchId.Value, DbType.Guid);
        }

        return parameters;
    }
}
