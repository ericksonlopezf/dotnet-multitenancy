// Copyright © Erickson Lopez. MIT License.
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Stores;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Opt = Microsoft.Extensions.Options.Options;

namespace EricksonLopez.MultiTenancy.UnitTests.Stores;

public class HttpRemoteTenantStoreTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new TenantId(AlphaGuid);

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var options = Opt.Create(new HttpRemoteTenantStoreOptions());
        var act = () => new HttpRemoteTenantStore<TenantInfo>(null!, options);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public async Task GetTenantAsync_EmptyTenantId_ReturnsFailure()
    {
        var handler = new TestHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantAsync(TenantId.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
        result.Error.Description.Should().Be("'Empty' is not a valid tenant identifier.");
    }

    [Fact]
    public async Task GetTenantAsync_SuccessfulResponse_ReturnsTenant()
    {
        var json = $"{{\"Id\":\"{AlphaGuid}\",\"Name\":\"Acme Corp\",\"ConnectionString\":\"Host=db.acme.com\",\"IsActive\":true}}";

        var handler = new TestHttpMessageHandler((req, ct) =>
        {
            req.RequestUri!.PathAndQuery.Should().Be($"/api/tenants/{AlphaGuid}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(AlphaTenantId);
        result.Value.Name.Should().Be("Acme Corp");

        // Non-generic ITenantStore call
        var nonGenericStore = (ITenantStore)store;
        var nonGenericResult = await nonGenericStore.GetTenantAsync(AlphaTenantId);
        nonGenericResult.IsSuccess.Should().BeTrue();
        nonGenericResult.Value.Id.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task GetTenantAsync_NotFound404_ReturnsFailure()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
        result.Error.Description.Should().Be($"Tenant '{AlphaTenantId.Value}' was not found.");
    }

    [Fact]
    public async Task GetTenantAsync_ServerError500_ReturnsFailure()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RemoteStore.HttpError");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_SuccessfulResponse_ReturnsTenant()
    {
        var json = $"{{\"Id\":\"{AlphaGuid}\",\"Name\":\"acme\",\"ConnectionString\":\"Host=db.acme.com\",\"IsActive\":true}}";

        var handler = new TestHttpMessageHandler((req, ct) =>
        {
            req.RequestUri!.PathAndQuery.Should().Be("/api/tenants/by-identifier/acme");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantByIdentifierAsync("acme");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(AlphaTenantId);
        result.Value.Name.Should().Be("acme");

        // Non-generic ITenantLookupStore call
        var lookupStore = (ITenantLookupStore)store;
        var lookupResult = await lookupStore.GetTenantByIdentifierAsync("acme");
        lookupResult.IsSuccess.Should().BeTrue();
        lookupResult.Value.Id.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_EmptyIdentifier_ReturnsFailure()
    {
        var handler = new TestHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var resultEmpty = await store.GetTenantByIdentifierAsync("");
        resultEmpty.IsFailure.Should().BeTrue();
        resultEmpty.Error.Code.Should().Be("Tenant.InvalidId");
        resultEmpty.Error.Description.Should().Be("'' is not a valid tenant identifier.");

        var resultWhitespace = await store.GetTenantByIdentifierAsync("   ");
        resultWhitespace.IsFailure.Should().BeTrue();
        resultWhitespace.Error.Code.Should().Be("Tenant.InvalidId");
        resultWhitespace.Error.Description.Should().Be("'   ' is not a valid tenant identifier.");

        var resultNull = await store.GetTenantByIdentifierAsync(null!);
        resultNull.IsFailure.Should().BeTrue();
        resultNull.Error.Code.Should().Be("Tenant.InvalidId");
        resultNull.Error.Description.Should().Be("'null' is not a valid tenant identifier.");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NotFound_ReturnsFailure()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantByIdentifierAsync("missing-tenant");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
        result.Error.Description.Should().Be("Tenant with identifier 'missing-tenant' was not found in remote store.");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_ServerError_ReturnsFailure()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantByIdentifierAsync("acme");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RemoteStore.HttpError");
    }

    [Fact]
    public async Task GetTenantAsync_NullDeserializedBody_ReturnsNotFound()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NullDeserializedBody_ReturnsNotFound()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantByIdentifierAsync("acme");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task GetTenantAsync_NetworkException_ReturnsUnavailableError()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            throw new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RemoteStore.Unavailable");
        result.Error.Description.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NetworkException_ReturnsUnavailableError()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            throw new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantByIdentifierAsync("acme");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RemoteStore.Unavailable");
        result.Error.Description.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task GetTenantAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => store.GetTenantAsync(AlphaTenantId, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => store.GetTenantByIdentifierAsync("acme", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NonGenericInterfaces_FailurePaths_ReturnFailureResults()
    {
        var handler = new TestHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        ITenantStore untypedStore = store;
        var res1 = await untypedStore.GetTenantAsync(AlphaTenantId);
        res1.IsFailure.Should().BeTrue();

        ITenantLookupStore untypedLookup = store;
        var res2 = await untypedLookup.GetTenantByIdentifierAsync("missing");
        res2.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTenantByIdentifierAsync_InvalidStrings_ReturnsInvalidIdError(string? identifier)
    {
        var handler = new TestHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://tenants.example.com") };
        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, Opt.Create(new HttpRemoteTenantStoreOptions()));

        var result = await store.GetTenantByIdentifierAsync(identifier!);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public void Constructor_OptionsConfiguration_AppliesBaseAddressAndTimeout()
    {
        var handler = new TestHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);

        var options = Opt.Create(new HttpRemoteTenantStoreOptions
        {
            BaseAddress = new Uri("https://custom.tenants.com"),
            Timeout = TimeSpan.FromSeconds(15)
        });

        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, options);
        httpClient.BaseAddress.Should().Be(new Uri("https://custom.tenants.com"));
        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Constructor_OptionsWithZeroTimeoutAndExistingBaseAddress_PreservesDefaults()
    {
        var handler = new TestHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://existing.tenants.com") };

        var options = Opt.Create(new HttpRemoteTenantStoreOptions
        {
            BaseAddress = new Uri("https://custom.tenants.com"),
            Timeout = TimeSpan.Zero
        });

        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, options);
        httpClient.BaseAddress.Should().Be(new Uri("https://existing.tenants.com"));
    }

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        var handler = new TestHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);

        var store = new HttpRemoteTenantStore<TenantInfo>(httpClient, null!);
        store.Should().NotBeNull();
    }


    [Fact]
    public void AddHttpRemoteTenantStore_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddHttpRemoteTenantStore<TenantInfo>(options =>
        {
            options.BaseAddress = new Uri("https://tenants.service.internal");
        });

        using var sp = services.BuildServiceProvider();
        var store = sp.GetService<ITenantStore<TenantInfo>>();
        store.Should().NotBeNull();
        store.Should().BeOfType<HttpRemoteTenantStore<TenantInfo>>();

        var lookupStore = sp.GetService<ITenantLookupStore<TenantInfo>>();
        lookupStore.Should().NotBeNull();
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}

