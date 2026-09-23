// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.Authentication;

/// <summary>
/// Specifies options for configuring per-tenant authentication schemes.
/// </summary>
public class TenantAuthenticationOptions
{
    /// <summary>
    /// Gets or sets a delegate that dynamically selects the default authentication scheme for the active tenant.
    /// </summary>
    public Func<ITenantInfo, string?>? DefaultSchemeSelector { get; set; }
}
