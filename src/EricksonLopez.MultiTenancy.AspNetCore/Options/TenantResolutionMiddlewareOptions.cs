// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy.AspNetCore.Options;

/// <summary>
/// Configures runtime behavior for the <see cref="TenantResolutionMiddleware"/>.
/// </summary>
public sealed class TenantResolutionMiddlewareOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to terminate the request with HTTP 404 (Not Found)
    /// when a tenant identifier was resolved by a strategy but could not be found in the tenant store.
    /// Default is <see langword="true"/> (fail-closed, safe-by-default).
    /// </summary>
    public bool FailOnStoreMiss { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to return an RFC 7807 ProblemDetails response with
    /// HTTP 409 (Conflict) when multiple conflicting tenant resolution strategies evaluate different tenant IDs,
    /// rather than throwing an unhandled <see cref="TenantResolutionConflictException"/>.
    /// Default is <see langword="false"/> to preserve exception throwing for custom error handlers.
    /// </summary>
    public bool WriteProblemDetailsOnConflict { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether strategy names and internal identifiers are included
    /// in the ProblemDetails error response when a tenant resolution conflict occurs.
    /// Default is <see langword="false"/> to avoid disclosing internal architecture details to clients.
    /// </summary>
    public bool IncludeStrategyDetailsInConflictResponse { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate that an authenticated principal's tenant claim matches the resolved tenant.
    /// When <see langword="true"/>, if the principal is authenticated and has a tenant claim that does not match the resolved tenant,
    /// the request is rejected with HTTP 403 (Forbidden).
    /// Default is <see langword="true"/> (fail-closed, safe-by-default).
    /// </summary>
    public bool ValidateAuthenticatedPrincipalTenantClaim { get; set; } = true;

    /// <summary>
    /// Gets or sets the primary claim type used to identify the tenant on an authenticated principal.
    /// Defaults to <c>"tenant_id"</c>.
    /// </summary>
    public string TenantClaimType { get; set; } = "tenant_id";
}
