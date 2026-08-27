// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.Stores;

/// <summary>
/// Provides a tenant store that queries a remote HTTP endpoint for tenant metadata.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public sealed class HttpRemoteTenantStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TTenant>
    : ITenantLookupStore<TTenant>
    where TTenant : class, ITenantInfo
{
    private readonly HttpClient _httpClient;
    private readonly HttpRemoteTenantStoreOptions _options;
    private static readonly JsonSerializerOptions _defaultJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRemoteTenantStore{TTenant}"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to perform remote requests.</param>
    /// <param name="options">The remote store configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <see langword="null"/></exception>
    public HttpRemoteTenantStore(HttpClient httpClient, IOptions<HttpRemoteTenantStoreOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new HttpRemoteTenantStoreOptions();

        if (_options.BaseAddress is not null && _httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = _options.BaseAddress;
        }

        if (_options.Timeout > TimeSpan.Zero)
        {
            _httpClient.Timeout = _options.Timeout;
        }
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "TTenant members are preserved via DynamicallyAccessedMembers attribute.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "TTenant members are preserved via DynamicallyAccessedMembers attribute.")]
    public async Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId.IsEmpty)
        {
            return Result<TTenant>.Failure(TenantErrors.InvalidId("Empty"));
        }

        var endpoint = string.Format(CultureInfo.InvariantCulture, _options.EndpointTemplate, tenantId.Value);

        try
        {
            // Stryker disable once boolean : ConfigureAwait is not verifiable
            using var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<TTenant>.Failure(TenantErrors.NotFound(tenantId));
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result<TTenant>.Failure(Error.Failure(
                    "RemoteStore.HttpError",
                    $"Remote tenant store returned HTTP status {(int)response.StatusCode} ({response.ReasonPhrase})."));
            }

            // Stryker disable once boolean : ConfigureAwait is not verifiable
            var tenant = await response.Content.ReadFromJsonAsync<TTenant>(_defaultJsonOptions, cancellationToken).ConfigureAwait(false);
            if (tenant is null)
            {
                return Result<TTenant>.Failure(TenantErrors.NotFound(tenantId));
            }

            return Result<TTenant>.Success(tenant);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<TTenant>.Failure(Error.Failure(
                "RemoteStore.Unavailable",
                $"Failed to communicate with remote tenant store: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean : ConfigureAwait is not verifiable
        var result = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "TTenant members are preserved via DynamicallyAccessedMembers attribute.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "TTenant members are preserved via DynamicallyAccessedMembers attribute.")]
    public async Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<TTenant>.Failure(TenantErrors.InvalidId(identifier ?? "null"));
        }

        var endpoint = string.Format(CultureInfo.InvariantCulture, _options.IdentifierEndpointTemplate, Uri.EscapeDataString(identifier));

        try
        {
            // Stryker disable once boolean : ConfigureAwait is not verifiable
            using var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<TTenant>.Failure(Error.NotFound(
                    "Tenant.NotFound",
                    $"Tenant with identifier '{identifier}' was not found in remote store."));
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result<TTenant>.Failure(Error.Failure(
                    "RemoteStore.HttpError",
                    $"Remote tenant store returned HTTP status {(int)response.StatusCode} ({response.ReasonPhrase})."));
            }

            // Stryker disable once boolean : ConfigureAwait is not verifiable
            var tenant = await response.Content.ReadFromJsonAsync<TTenant>(_defaultJsonOptions, cancellationToken).ConfigureAwait(false);
            if (tenant is null)
            {
                return Result<TTenant>.Failure(Error.NotFound(
                    "Tenant.NotFound",
                    $"Tenant with identifier '{identifier}' was not found in remote store."));
            }

            return Result<TTenant>.Success(tenant);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<TTenant>.Failure(Error.Failure(
                "RemoteStore.Unavailable",
                $"Failed to communicate with remote tenant store: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantLookupStore.GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean : ConfigureAwait is not verifiable
        var result = await GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }
}
