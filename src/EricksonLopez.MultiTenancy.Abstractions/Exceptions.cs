// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Represents the exception that is thrown when a required tenant cannot be resolved or is not present in the current ambient execution context.
/// </summary>
public class TenantNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class.
    /// </summary>
    public TenantNotFoundException()
        : base("The required tenant could not be resolved from the ambient execution context.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public TenantNotFoundException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a <see langword="null"/> reference if no inner exception is specified.</param>
    public TenantNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}

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
