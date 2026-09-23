// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides access to branch / operational location context within a multi-tenant hierarchy.
/// </summary>
public interface IBranchContext
{
    /// <summary>
    /// Gets the actively selected branch identifier for the current scope, or <see langword="null"/> if unassigned.
    /// </summary>
    Guid? BranchId { get; }

    /// <summary>
    /// Gets the list of authorized branch identifiers for the current user/caller.
    /// </summary>
    IReadOnlyList<Guid> AllowedBranchIds { get; }

    /// <summary>
    /// Gets a value indicating whether the caller is authorized for all branches within the active company.
    /// </summary>
    bool AllBranchesAllowed { get; }
}
