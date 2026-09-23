// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.AspNetCore;

/// <summary>
/// Represents the exception thrown when multiple tenant resolution strategies resolve different, conflicting tenant identifiers for the same request.
/// </summary>
public class TenantResolutionConflictException : InvalidOperationException
{
    /// <summary>
    /// Gets the first resolved tenant identifier.
    /// </summary>
    public TenantId FirstTenantId { get; }

    /// <summary>
    /// Gets the name of the strategy that resolved the first tenant identifier.
    /// </summary>
    public string? FirstStrategy { get; }

    /// <summary>
    /// Gets the second conflicting tenant identifier.
    /// </summary>
    public TenantId SecondTenantId { get; }

    /// <summary>
    /// Gets the name of the strategy that resolved the conflicting second tenant identifier.
    /// </summary>
    public string? SecondStrategy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResolutionConflictException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    // Stryker disable once block : Struct default initialization
    public TenantResolutionConflictException(string message) : base(message)
    {
        FirstTenantId = TenantId.Empty;
        SecondTenantId = TenantId.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResolutionConflictException"/> class with conflict details.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="firstTenantId">The first resolved tenant identifier.</param>
    /// <param name="firstStrategy">The name of the strategy that resolved the first tenant identifier.</param>
    /// <param name="secondTenantId">The second conflicting tenant identifier.</param>
    /// <param name="secondStrategy">The name of the strategy that resolved the second tenant identifier.</param>
    public TenantResolutionConflictException(
        string message,
        TenantId firstTenantId,
        string? firstStrategy,
        TenantId secondTenantId,
        string? secondStrategy)
        : base(message)
    {
        FirstTenantId = firstTenantId;
        FirstStrategy = firstStrategy;
        SecondTenantId = secondTenantId;
        SecondStrategy = secondStrategy;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResolutionConflictException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    // Stryker disable once block : Struct default initialization
    public TenantResolutionConflictException(string message, Exception innerException) : base(message, innerException)
    {
        FirstTenantId = TenantId.Empty;
        SecondTenantId = TenantId.Empty;
    }
}
