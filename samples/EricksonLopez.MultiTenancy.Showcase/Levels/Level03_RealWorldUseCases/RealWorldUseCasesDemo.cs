// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Authentication;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Testing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level03_RealWorldUseCases;

/// <summary>
/// Level 3 — Real World Use Cases: Dapper repositories, Minimal API route protection with RequireTenant, and per-tenant cookie authentication.
/// </summary>
public static class RealWorldUseCasesDemo
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // 1. Register fake/test database connection for showcase execution
        services.AddScoped<DbConnection, FakeDbConnection>();

        // 2. Register application repository
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        // 3. Register per-tenant authentication schemes and Cookie event handler
        services.AddPerTenantAuthentication<TenantInfo>();
        services.AddScoped<TenantCookieAuthenticationEvents<TenantInfo>>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        // Protected group using RequireTenant() extension on RouteGroupBuilder
        var protectedGroup = app.MapGroup("/api/invoices")
            .RequireTenant();

        // GET /api/invoices
        protectedGroup.MapGet("/", async (IInvoiceRepository repository, CancellationToken ct) =>
        {
            var invoices = await repository.GetAllAsync(ct);
            return Results.Ok(invoices);
        });

        // GET /api/invoices/{id}
        protectedGroup.MapGet("/{id:guid}", async (Guid id, IInvoiceRepository repository, CancellationToken ct) =>
        {
            var invoice = await repository.GetByIdAsync(id, ct);
            return invoice is not null ? Results.Ok(invoice) : Results.NotFound();
        });

        // POST /api/invoices
        protectedGroup.MapPost("/", async (Invoice newInvoice, IInvoiceRepository repository, ITenantContext tenantContext, CancellationToken ct) =>
        {
            // Stamp entity with active tenant identifier
            var invoiceToCreate = newInvoice with
            {
                TenantId = tenantContext.RequiredTenant.Id
            };

            await repository.CreateAsync(invoiceToCreate, ct);
            return Results.Created($"/api/invoices/{invoiceToCreate.Id}", invoiceToCreate);
        });

        // Individual endpoint protection using RequireTenant() on RouteHandlerBuilder
        app.MapGet("/api/admin/tenant-summary", (ITenantContext tenantContext) =>
        {
            var tenant = tenantContext.RequiredTenant;
            return Results.Ok(new
            {
                Summary = $"Tenant summary for {tenant.Name}",
                Active = tenant.IsActive,
                Properties = tenant.Properties
            });
        }).RequireTenant();
    }
}
