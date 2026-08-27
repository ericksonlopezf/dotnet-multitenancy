// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Represents an ambient tenant context containing strongly-typed tenant metadata.
/// </summary>
/// <remarks>
/// This type is immutable after construction.
/// </remarks>
/// <typeparam name="TTenant">The type of tenant metadata contained in this context.</typeparam>
public sealed class TenantContext<TTenant> : ITenantContext<TTenant>
    where TTenant : class, ITenantInfo
{
    /// <inheritdoc />
    public TTenant? Tenant { get; }

    /// <inheritdoc />
    ITenantInfo? ITenantContext.Tenant => Tenant;

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(Tenant))]
    public bool IsResolved => Tenant is not null && !Tenant.Id.IsEmpty && Tenant.IsActive;

    /// <inheritdoc />
    public TenantResolutionSource Source { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContext{TTenant}"/> class with the specified tenant metadata and resolution source.
    /// </summary>
    /// <param name="tenant">The resolved tenant metadata, or <see langword="null"/> if unassigned.</param>
    /// <param name="source">The mechanism through which the tenant was resolved.</param>
    public TenantContext(TTenant? tenant, TenantResolutionSource source = TenantResolutionSource.None)
    {
        Tenant = tenant;
        Source = tenant is not null ? source : TenantResolutionSource.None;
    }

    /// <summary>
    /// Represents an unassigned, empty tenant context.
    /// </summary>
    public static readonly TenantContext<TTenant> Empty = new(null, TenantResolutionSource.None);
}

/// <summary>
/// Provides factory methods and static instances for <see cref="ITenantContext"/>.
/// </summary>
public static class TenantContext
{
    /// <summary>
    /// Creates a new <see cref="ITenantContext"/> instance with the specified tenant metadata and resolution source.
    /// </summary>
    /// <param name="tenant">The tenant metadata, or <see langword="null"/> if unassigned.</param>
    /// <param name="source">The mechanism through which the tenant was resolved.</param>
    /// <returns>A new immutable <see cref="ITenantContext"/> instance.</returns>
    public static ITenantContext Create(ITenantInfo? tenant, TenantResolutionSource source = TenantResolutionSource.None)
        => new TenantContext<ITenantInfo>(tenant, source);

    /// <summary>
    /// Represents an empty <see cref="ITenantContext"/> instance with no resolved tenant.
    /// </summary>
    public static readonly ITenantContext Empty = new TenantContext<ITenantInfo>(null, TenantResolutionSource.None);
}
