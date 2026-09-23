// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides access to ambient company/legal entity context within a multi-tenant hierarchy.
/// </summary>
public interface ICompanyContext
{
    /// <summary>
    /// Gets the resolved company identifier for the current scope, or <see langword="null"/> if unassigned.
    /// </summary>
    Guid? CompanyId { get; }

    /// <summary>
    /// Gets a value indicating whether a valid company has been resolved for the current scope.
    /// </summary>
    bool HasCompanyContext => CompanyId.HasValue && CompanyId.Value != Guid.Empty;
}
