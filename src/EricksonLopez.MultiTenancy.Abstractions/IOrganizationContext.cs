// Copyright © Erickson Lopez. MIT License.
namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Defines a unified organization hierarchy contract combining tenant, company, and branch isolation contexts.
/// </summary>
public interface IOrganizationContext : ITenantContext, ICompanyContext, IBranchContext
{
}
