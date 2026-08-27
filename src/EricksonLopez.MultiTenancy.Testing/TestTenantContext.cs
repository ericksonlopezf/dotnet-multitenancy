// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides a mutable, test-oriented implementation of <see cref="ITenantContext"/> and <see cref="ITenantContextAccessor"/> for unit and integration testing without requiring dependency injection scopes.
/// </summary>
public sealed class TestTenantContext : ITenantContext, ITenantContextAccessor
{
    /// <inheritdoc />
    public ITenantInfo? Tenant { get; set; }

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(Tenant))]
    public bool IsResolved => Tenant is not null && Tenant.Id != TenantId.Empty;

    /// <inheritdoc />
    public TenantResolutionSource Source { get; set; } = TenantResolutionSource.None;

    /// <inheritdoc />
    [SuppressMessage("Major Code Smell", "S2372:Exceptions should not be thrown from property getters", Justification = "RequiredTenant explicitly guarantees fail-fast invariant semantics when no tenant is resolved.")]
    public ITenantInfo RequiredTenant
    {
        get
        {
            if (!IsResolved || Tenant is null)
            {
                throw new TenantNotFoundException("No tenant has been resolved in the current test execution context.");
            }

            return Tenant;
        }
    }

    /// <inheritdoc />
    ITenantContext? ITenantContextAccessor.TenantContext
    {
        get => this;
        set
        {
            Tenant = value?.Tenant;
            Source = value?.Source ?? TenantResolutionSource.None;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTenantContext"/> class with no resolved tenant.
    /// </summary>
    public TestTenantContext()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTenantContext"/> class with the specified tenant and resolution source.
    /// </summary>
    /// <param name="tenant">The tenant information to bind.</param>
    /// <param name="source">The resolution source for tracking (default: <see cref="TenantResolutionSource.ExplicitScope"/>).</param>
    public TestTenantContext(ITenantInfo? tenant, TenantResolutionSource source = TenantResolutionSource.ExplicitScope)
    {
        Tenant = tenant;
        Source = source;
    }

    /// <summary>
    /// Creates a new <see cref="TestTenantContext"/> initialized with a random active tenant.
    /// </summary>
    /// <param name="tenantName">The optional display name for the generated tenant.</param>
    /// <returns>A configured <see cref="TestTenantContext"/> instance.</returns>
    public static TestTenantContext Create(string tenantName = "Test Tenant")
    {
        var tenant = new TenantInfo(TenantId.NewId(), tenantName, isActive: true);
        return new TestTenantContext(tenant, TenantResolutionSource.ExplicitScope);
    }

    /// <summary>
    /// Resets the context to an unresolved, empty state.
    /// </summary>
    public void Reset()
    {
        Tenant = null;
        Source = TenantResolutionSource.None;
    }
}
