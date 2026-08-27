// Copyright © Erickson Lopez. MIT License.
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides standardized domain and infrastructure error descriptors for multi-tenancy operations.
/// </summary>
public static class TenantErrors
{
    /// <summary>
    /// Creates an error indicating that the specified tenant was not found in storage.
    /// </summary>
    /// <param name="tenantId">The identifier of the missing tenant.</param>
    /// <returns>An <see cref="Error"/> representing the missing tenant condition.</returns>
    public static Error NotFound(TenantId tenantId) =>
        Error.NotFound("Tenant.NotFound", $"Tenant '{tenantId}' was not found.");

    /// <summary>
    /// Represents an error indicating that a required tenant could not be resolved from the ambient execution context.
    /// </summary>
    public static readonly Error Unresolved =
        Error.NotFound("Tenant.Unresolved", "The required tenant could not be resolved from the ambient execution context.");

    /// <summary>
    /// Creates an error indicating that the specified tenant is inactive or suspended.
    /// </summary>
    /// <param name="tenantId">The identifier of the inactive tenant.</param>
    /// <returns>An <see cref="Error"/> representing the inactive tenant condition.</returns>
    public static Error Inactive(TenantId tenantId) =>
        Error.Validation("Tenant.Inactive", $"Tenant '{tenantId}' is marked as inactive or suspended.");

    /// <summary>
    /// Creates an error indicating that the provided value is not a valid tenant identifier.
    /// </summary>
    /// <param name="value">The invalid identifier string representation.</param>
    /// <returns>An <see cref="Error"/> representing the invalid identifier condition.</returns>
    public static Error InvalidId(string? value) =>
        Error.Validation("Tenant.InvalidId", $"'{value ?? string.Empty}' is not a valid tenant identifier.");

    /// <summary>
    /// Creates an error indicating that a tenant resolution strategy failed to execute or extract an identifier.
    /// </summary>
    /// <param name="strategyName">The name of the strategy that failed.</param>
    /// <param name="reason">An optional description explaining the cause of the failure.</param>
    /// <returns>An <see cref="Error"/> representing the strategy failure condition.</returns>
    public static Error StrategyFailed(string strategyName, string? reason = null) =>
        Error.Failure("Tenant.ResolutionFailed", string.IsNullOrWhiteSpace(reason)
            ? $"Resolution strategy '{strategyName}' failed to resolve a tenant."
            : $"Resolution strategy '{strategyName}' failed: {reason}");
}
