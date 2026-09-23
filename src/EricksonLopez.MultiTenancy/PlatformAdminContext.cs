// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides a concrete implementation of <see cref="IPlatformAdminContext"/> representing an authorized platform administration context.
/// </summary>
public sealed class PlatformAdminContext : IPlatformAdminContext
{
    /// <inheritdoc />
    public bool IsPlatformAdmin { get; }

    /// <inheritdoc />
    public string? AuditReason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformAdminContext"/> class.
    /// </summary>
    /// <param name="isPlatformAdmin">Indicates whether the administrative bypass is active.</param>
    /// <param name="auditReason">The mandatory or optional audit trail reason for this cross-tenant execution.</param>
    public PlatformAdminContext(bool isPlatformAdmin = true, string? auditReason = null)
    {
        IsPlatformAdmin = isPlatformAdmin;
        AuditReason = auditReason;
    }

    /// <summary>
    /// Represents the default empty non-administrative platform context.
    /// </summary>
    public static readonly PlatformAdminContext None = new(isPlatformAdmin: false);
}
