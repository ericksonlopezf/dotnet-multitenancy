// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides a fluent builder for constructing customizable <see cref="ITenantInfo"/> and <see cref="ITenantContext"/> instances in testing scenarios.
/// </summary>
public sealed class TenantContextBuilder
{
    private TenantId _tenantId = TenantId.NewId();
    private string _name = "Test Tenant";
    private string? _connectionString;
    private bool _isActive = true;
    private readonly Dictionary<string, string> _properties = new(StringComparer.OrdinalIgnoreCase);
    private TenantResolutionSource _source = TenantResolutionSource.ExplicitScope;

    /// <summary>
    /// Sets the tenant identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TenantContextBuilder WithId(TenantId tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>
    /// Sets the tenant identifier from a <see cref="Guid"/> value.
    /// </summary>
    /// <param name="value">The GUID value.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TenantContextBuilder WithId(Guid value)
    {
        _tenantId = new TenantId(value);
        return this;
    }

    /// <summary>
    /// Sets the tenant display name.
    /// </summary>
    /// <param name="name">The tenant display name.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/></exception>
    public TenantContextBuilder WithName(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    /// <summary>
    /// Sets the tenant-specific database connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TenantContextBuilder WithConnectionString(string? connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Sets the active state of the tenant.
    /// </summary>
    /// <param name="isActive"><see langword="true"/> if the tenant is active; otherwise, <see langword="false"/>.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TenantContextBuilder WithActive(bool isActive)
    {
        _isActive = isActive;
        return this;
    }

    /// <summary>
    /// Marks the tenant as inactive.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TenantContextBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    /// <summary>
    /// Adds or updates a custom metadata property on the tenant.
    /// </summary>
    /// <param name="key">The metadata property key.</param>
    /// <param name="value">The metadata property value.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/></exception>
    public TenantContextBuilder WithProperty(string key, string value)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner Dictionary indexer
        ArgumentNullException.ThrowIfNull(key);
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the resolution source mechanism.
    /// </summary>
    /// <param name="source">The resolution source enum value.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TenantContextBuilder WithSource(TenantResolutionSource source)
    {
        _source = source;
        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="ITenantInfo"/> metadata object.
    /// </summary>
    /// <returns>A new <see cref="ITenantInfo"/> instance.</returns>
    public ITenantInfo BuildTenantInfo()
    {
        return new TenantInfo
        {
            Id = _tenantId,
            Name = _name,
            ConnectionString = _connectionString,
            IsActive = _isActive,
            Properties = new Dictionary<string, string>(_properties, StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Builds a <see cref="TestTenantContext"/> bound to the configured tenant.
    /// </summary>
    /// <returns>A new <see cref="TestTenantContext"/> instance.</returns>
    public TestTenantContext BuildContext()
    {
        var tenantInfo = BuildTenantInfo();
        return new TestTenantContext(tenantInfo, _source);
    }
}
