// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Represents the exception that is thrown when a resolved tenant exists but is currently inactive or suspended.
/// </summary>
public class TenantInactiveException : Exception
{
    /// <summary>
    /// Gets the identifier of the inactive tenant.
    /// </summary>
    public TenantId TenantId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantInactiveException"/> class with the specified tenant identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the inactive tenant.</param>
    public TenantInactiveException(TenantId tenantId)
        : base($"Tenant '{tenantId}' is marked as inactive or suspended.")
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantInactiveException"/> class with the specified tenant identifier and error message.
    /// </summary>
    /// <param name="tenantId">The identifier of the inactive tenant.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public TenantInactiveException(TenantId tenantId, string message)
        : base(message)
    {
        TenantId = tenantId;
    }
}
