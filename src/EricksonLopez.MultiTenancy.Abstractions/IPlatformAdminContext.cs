// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Represents an authenticated platform administrative execution context authorized to perform cross-tenant operations.
/// </summary>
/// <remarks>
/// Governed by ADR-004 to eliminate ambiguous null or empty tenant identifier bypasses.
/// </remarks>
public interface IPlatformAdminContext
{
    /// <summary>
    /// Gets a value indicating whether the current execution context represents an authorized platform administrator.
    /// </summary>
    bool IsPlatformAdmin { get; }

    /// <summary>
    /// Gets the audit reason or ticket reference authorizing the platform bypass operation.
    /// </summary>
    string? AuditReason { get; }
}
