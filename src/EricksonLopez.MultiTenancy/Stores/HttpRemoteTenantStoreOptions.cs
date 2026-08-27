// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.Stores;

/// <summary>
/// Provides configuration options for <see cref="HttpRemoteTenantStore{TTenant}"/>.
/// </summary>
public sealed class HttpRemoteTenantStoreOptions
{
    /// <summary>
    /// Gets or sets the base address of the remote tenant service.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the endpoint template used to look up a tenant by <see cref="TenantId"/>.
    /// </summary>
    public string EndpointTemplate { get; set; } = "/api/tenants/{0}";

    /// <summary>
    /// Gets or sets the endpoint template used to look up a tenant by string identifier.
    /// </summary>
    public string IdentifierEndpointTemplate { get; set; } = "/api/tenants/by-identifier/{0}";

    /// <summary>
    /// Gets or sets the request timeout duration for remote store lookups.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
