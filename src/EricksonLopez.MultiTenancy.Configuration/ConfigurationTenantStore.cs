// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Options;

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

/// <summary>
/// Provides a tenant store that resolves tenant metadata from static application configuration.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public sealed class ConfigurationTenantStore<TTenant> : ITenantLookupStore<TTenant>
    where TTenant : class, ITenantInfo, new()
{
    private readonly IOptionsMonitor<MultiTenancyOptions<TTenant>> _optionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationTenantStore{TTenant}"/> class.
    /// </summary>
    /// <param name="optionsMonitor">The options monitor containing the tenant configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="optionsMonitor"/> is <see langword="null"/></exception>
    public ConfigurationTenantStore(IOptionsMonitor<MultiTenancyOptions<TTenant>> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        _optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenants = _optionsMonitor.CurrentValue.Tenants;
        var match = tenants?.FirstOrDefault(t => t.Id == tenantId);
        if (match is not null)
        {
            return Task.FromResult(Result<TTenant>.Success(match));
        }

        return Task.FromResult(Result<TTenant>.Failure(TenantErrors.NotFound(tenantId)));
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean, statement : ConfigureAwait optimization
        var res = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (res.IsSuccess)
        {
            return Result<ITenantInfo>.Success(res.Value);
        }

        return Result<ITenantInfo>.Failure(res.Error);
    }

    /// <inheritdoc />
    public Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var tenants = _optionsMonitor.CurrentValue.Tenants;
        var match = tenants?.FirstOrDefault(t => string.Equals(t.Name, identifier, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return Task.FromResult(Result<TTenant>.Success(match));
        }

        // Check if the identifier is actually a valid TenantId string
        if (TenantId.TryCreate(identifier, out var parsedId))
        {
            return GetTenantAsync(parsedId, cancellationToken);
        }

        return Task.FromResult(Result<TTenant>.Failure(Error.NotFound("Tenant.NotFound", $"Tenant with identifier '{identifier}' was not found in configuration.")));
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantLookupStore.GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean, statement : ConfigureAwait optimization
        var res = await GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (res.IsSuccess)
        {
            return Result<ITenantInfo>.Success(res.Value);
        }

        return Result<ITenantInfo>.Failure(res.Error);
    }
}
