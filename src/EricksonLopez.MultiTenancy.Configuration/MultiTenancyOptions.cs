// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;

namespace EricksonLopez.MultiTenancy.Configuration;

/// <summary>
/// Defines the static tenant catalog used to seed <see cref="ConfigurationTenantStore{TTenant}"/>.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public class MultiTenancyOptions<TTenant> where TTenant : class, ITenantInfo, new()
{
    /// <summary>
    /// Gets or sets the collection of tenants registered at application startup.
    /// </summary>
    public List<TTenant> Tenants { get; set; } = new();
}
