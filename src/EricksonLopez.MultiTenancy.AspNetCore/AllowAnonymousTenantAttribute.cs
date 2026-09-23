// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.AspNetCore;

/// <summary>
/// Specifies that the target endpoint allows anonymous or tenant-unresolved requests
/// even when <see cref="Options.TenantResolutionMiddlewareOptions.FailOnStoreMiss"/> is enabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowAnonymousTenantAttribute : Attribute, IAllowAnonymousTenantMetadata
{
}
