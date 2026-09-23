// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Specifies configuration options for PostgreSQL Row Level Security session variables and execution limits.
/// </summary>
public sealed class OrganizationRlsOptions
{
    /// <summary>
    /// Gets or sets the PostgreSQL session configuration variable for the tenant identifier.
    /// Default: <c>app.current_tenant_id</c>.
    /// </summary>
    public string TenantSessionVariable { get; set; } = "app.current_tenant_id";

    /// <summary>
    /// Gets or sets the PostgreSQL session configuration variable for the company identifier.
    /// Default: <c>app.current_company_id</c>.
    /// </summary>
    public string CompanySessionVariable { get; set; } = "app.current_company_id";

    /// <summary>
    /// Gets or sets the PostgreSQL session configuration variable for the authorized branch identifiers.
    /// Default: <c>app.current_branch_ids</c>.
    /// </summary>
    public string BranchIdsSessionVariable { get; set; } = "app.current_branch_ids";

    /// <summary>
    /// Gets or sets the PostgreSQL session configuration variable indicating if all branches are allowed.
    /// Default: <c>app.all_branches_allowed</c>.
    /// </summary>
    public string AllBranchesSessionVariable { get; set; } = "app.all_branches_allowed";

    /// <summary>
    /// Gets or sets the PostgreSQL session configuration variable for RLS bypass.
    /// Default: <c>app.bypass_rls</c>.
    /// </summary>
    public string BypassRlsSessionVariable { get; set; } = "app.bypass_rls";

    /// <summary>
    /// Gets or sets the statement timeout applied to enterprise transactions.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
