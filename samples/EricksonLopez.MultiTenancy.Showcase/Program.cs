// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.MultiTenancy.Authentication;
using EricksonLopez.MultiTenancy.HealthChecks;
using EricksonLopez.MultiTenancy.OpenTelemetry;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level01_QuickStart;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level02_FullConfiguration;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level03_RealWorldUseCases;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level05_BackgroundProcessing;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level08_Customization;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level09_Observability;
using EricksonLopez.MultiTenancy.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// 1. Dependency Injection Configuration
// =============================================================================

// Core Multi-Tenancy registration
builder.Services.AddMultiTenancy();

// ASP.NET Core Resolution Pipeline (Claims strategy added by default)
builder.Services.AddAspNetCoreMultiTenancy();

// Additional Resolution Strategies (Precedence order: Claims > Host > Route > Header)
builder.Services.AddHostNameTenantStrategy();
builder.Services.AddRouteTenantStrategy("tenantId");
builder.Services.AddInternalHeaderTenantResolution("gateway-secret-token");
builder.Services.AddBasePathStrategy(segmentIndex: 0);

// Seed in-memory tenant store
builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    store.AddOrUpdate(SeedTenants.Acme);
    store.AddOrUpdate(SeedTenants.Globex);
    store.AddOrUpdate(SeedTenants.InactiveTenant);
});

// Per-Tenant Options
builder.Services.AddPerTenantOptions<SampleTenantSettings, TenantInfo>();

// Per-Tenant Authentication
builder.Services.AddPerTenantAuthentication<TenantInfo>();
builder.Services.AddScoped<TenantCookieAuthenticationEvents<TenantInfo>>();

// OpenTelemetry Tracing & Metrics
builder.Services.AddMultiTenancyOpenTelemetry();

// Multi-Tenancy Health Checks
builder.Services.AddMultiTenancyHealthCheck(options =>
{
    options.IncludeDiagnosticData = true;
    options.StoreProbe = async (store, ct) =>
    {
        var result = await store.GetTenantAsync(SeedTenants.AcmeId, ct);
        return result.IsSuccess;
    };
});
builder.Services.AddHealthChecks();

// Database & Repositories (Using FakeDbConnection test double for standalone showcase execution)
builder.Services.AddScoped<DbConnection, FakeDbConnection>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<BackgroundProcessingDemo.TenantJobProcessor>();

var app = builder.Build();

// =============================================================================
// 2. Request Pipeline Configuration
// =============================================================================

// Enforce tenant resolution middleware early in the pipeline
app.UseMultiTenancy();

// Health Check Endpoint
app.MapHealthChecks("/health");

// Root information endpoint
app.MapGet("/", () => Results.Ok(new
{
    Title = "EricksonLopez.MultiTenancy Showcase",
    Description = "Official reference implementation, executable cookbook, and learning curriculum.",
    Docs = "https://github.com/ericksonlopezf/dotnet-multitenancy",
    Endpoints = new[]
    {
        "/api/showcase/info",
        "/api/quickstart/current-tenant",
        "/api/invoices",
        "/api/config/tenant-settings",
        "/health"
    }
}));

// Map all level demonstration routes
QuickStartDemo.ConfigureApp(app);
FullConfigurationDemo.MapConfigurationEndpoints(app);
RealWorldUseCasesDemo.MapEndpoints(app);
EricksonLopez.MultiTenancy.Showcase.Levels.Level11_ComprehensiveApiCoverage.ComprehensiveApiCoverageDemo.MapEndpoints(app);
app.MapShowcaseRoutes();

Console.WriteLine("==================================================================");
Console.WriteLine(" EricksonLopez.MultiTenancy Showcase & Official Reference App    ");
Console.WriteLine("==================================================================");

if (args.Length > 0 && args[0] == "--server")
{
    app.Run();
}
else
{
    await EricksonLopez.MultiTenancy.Showcase.Levels.Level11_ComprehensiveApiCoverage.ComprehensiveApiCoverageDemo.RunAsync(app.Services);
    Console.WriteLine("All multi-tenancy showcase levels completed successfully (exit 0).");
}
