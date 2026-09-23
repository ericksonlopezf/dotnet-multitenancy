// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy.AspNetCore.Options;

/// <summary>
/// Specifies configuration options for <see cref="Strategies.HostNameTenantResolutionStrategy"/>.
/// </summary>
public sealed class HostNameTenantResolutionStrategyOptions
{
    /// <summary>
    /// Gets or sets the base domain to strip from incoming hostnames (e.g., "platform.com").
    /// When set, a request to "api.tenant1.platform.com" resolves to "tenant1" rather than "api".
    /// </summary>
    public string? BaseDomain { get; set; }

    /// <summary>
    /// Gets or sets an optional regular expression pattern with a named capture group <c>(?&lt;tenant&gt;...)</c>
    /// to extract the tenant identifier from complex multi-tiered hostnames.
    /// </summary>
    public string? ExtractionPattern { get; set; }

    /// <summary>
    /// Gets or sets the zero-based subdomain segment index from the right of the base domain.
    /// Default is 0 (the immediate segment preceding the base domain).
    /// </summary>
    public int SubdomainIndexFromRight { get; set; }
}
