// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Specifies the mechanism through which the tenant was resolved in the current execution context.
/// </summary>
/// <remarks>
/// Used for audit trails, security event logging, and conflict detection.
/// </remarks>
public enum TenantResolutionSource
{
    /// <summary>
    /// Specifies that no tenant has been resolved.
    /// </summary>
    None = 0,

    /// <summary>
    /// Specifies that the tenant was resolved from an authenticated JWT claim (for example, <c>tenant_id</c>).
    /// </summary>
    JwtClaim = 1,

    /// <summary>
    /// Specifies that the tenant was resolved from the request route template.
    /// </summary>
    Route = 2,

    /// <summary>
    /// Specifies that the tenant was resolved from the request host header or subdomain.
    /// </summary>
    Host = 3,

    /// <summary>
    /// Specifies that the tenant was resolved from a custom HTTP request header.
    /// </summary>
    Header = 4,

    /// <summary>
    /// Specifies that the tenant was explicitly established using an execution scope factory.
    /// </summary>
    ExplicitScope = 5,

    /// <summary>
    /// Specifies that an elevated platform administration context was used for cross-tenant operations.
    /// </summary>
    PlatformAdmin = 6,

    /// <summary>
    /// Specifies that the tenant was resolved from a background job execution context.
    /// </summary>
    BackgroundJob = 7,

    /// <summary>
    /// Specifies that the tenant was resolved from metadata attached to a message in a broker or queue.
    /// </summary>
    MessageMetadata = 8,
}
