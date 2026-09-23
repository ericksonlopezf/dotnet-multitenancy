// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy.AspNetCore;

/// <summary>
/// Defines a marker contract indicating that an endpoint allows anonymous or tenant-unresolved requests
/// even when fail-closed tenant resolution is enabled.
/// </summary>
public interface IAllowAnonymousTenantMetadata
{
}
