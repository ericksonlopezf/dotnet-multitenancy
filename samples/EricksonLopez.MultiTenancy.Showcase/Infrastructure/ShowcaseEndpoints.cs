// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level00_Conceptual;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level04_AdvancedIntegration;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level05_BackgroundProcessing;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level06_ErrorHandling;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level07_Scalability;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level09_Observability;
using EricksonLopez.MultiTenancy.Showcase.Levels.Level10_EnterpriseArchitecture;
using EricksonLopez.MultiTenancy.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Showcase.Infrastructure;

/// <summary>
/// Mappings for executable showcase demonstration endpoints across all 10 learning levels.
/// </summary>
public static class ShowcaseEndpoints
{
    public static void MapShowcaseRoutes(this WebApplication app)
    {
        var showcase = app.MapGroup("/api/showcase");

        showcase.MapGet("/info", () => Results.Ok(new
        {
            Project = "EricksonLopez.MultiTenancy Showcase",
            Version = "1.0.0",
            Description = "Official reference implementation, executable cookbook, and learning curriculum for EricksonLopez.MultiTenancy.",
            Levels = new[]
            {
                "Level 00: Conceptual Foundation (TenantId, ITenantInfo, Invariants)",
                "Level 01: Quick Start (Minimal API setup, DI, Middleware)",
                "Level 02: Full Configuration (Multi-strategy, per-tenant options, cached store)",
                "Level 03: Real World Use Cases (Dapper repository, RequireTenant filter, Cookie auth)",
                "Level 04: Advanced Integration (PostgreSQL RLS, SQL Server, MySQL, Oracle, SQLite)",
                "Level 05: Background Processing (ITenantScopeFactory, non-HTTP isolated scopes)",
                "Level 06: Error Handling (Fail-closed ADR-008 conflict detection, inactive tenant guards)",
                "Level 07: Scalability (SQLite Database-per-tenant, CachedTenantStore)",
                "Level 08: Customization (Strongly-typed custom tenant models and strategies)",
                "Level 09: Observability (OpenTelemetry ActivitySource, Metrics, HealthChecks)",
                "Level 10: Enterprise Architecture (4-layer Defense-in-Depth, Clean Architecture)"
            }
        }));

        // Level 0: Conceptual
        showcase.MapGet("/levels/level0", () =>
        {
            ConceptualOverview.RunConceptualExamples();
            return Results.Ok(new { Status = "Completed", Level = 0, Description = "Check server stdout for conceptual overview." });
        });

        // Level 4: Dialects
        showcase.MapGet("/levels/level4/postgresql", async (ITenantContext tenantContext, CancellationToken ct) =>
        {
            using var fakeConn = new FakeDbConnection();
            await AdvancedIntegrationDemo.ExecutePostgreSqlRlsWorkflowAsync(fakeConn, tenantContext, ct);
            return Results.Ok(new { Status = "Success", Dialect = "PostgreSQL RLS SET LOCAL", Commands = fakeConn.Commands.Count });
        }).RequireTenant();

        showcase.MapGet("/levels/level4/sqlserver", async (ITenantContext tenantContext, CancellationToken ct) =>
        {
            using var fakeConn = new FakeDbConnection();
            await AdvancedIntegrationDemo.ExecuteSqlServerSessionContextWorkflowAsync(fakeConn, tenantContext, ct);
            return Results.Ok(new { Status = "Success", Dialect = "SQL Server SESSION_CONTEXT", Commands = fakeConn.Commands.Count });
        }).RequireTenant();

        showcase.MapGet("/levels/level4/mysql", async (ITenantContext tenantContext, CancellationToken ct) =>
        {
            using var fakeConn = new FakeDbConnection();
            await AdvancedIntegrationDemo.ExecuteMySqlSessionVariableWorkflowAsync(fakeConn, tenantContext, ct);
            return Results.Ok(new { Status = "Success", Dialect = "MySQL Session Variable", Commands = fakeConn.Commands.Count });
        }).RequireTenant();

        showcase.MapGet("/levels/level4/oracle", async (ITenantContext tenantContext, CancellationToken ct) =>
        {
            using var fakeConn = new FakeDbConnection();
            await AdvancedIntegrationDemo.ExecuteOracleVpdWorkflowAsync(fakeConn, tenantContext, ct);
            return Results.Ok(new { Status = "Success", Dialect = "Oracle VPD DBMS_SESSION", Commands = fakeConn.Commands.Count });
        }).RequireTenant();

        showcase.MapGet("/levels/level4/sqlite", async (ITenantContext tenantContext, CancellationToken ct) =>
        {
            using var fakeConn = new FakeDbConnection();
            await AdvancedIntegrationDemo.ExecuteSqliteWorkflowAsync(fakeConn, tenantContext, ct);
            return Results.Ok(new { Status = "Success", Dialect = "SQLite Temp Table Isolation", Commands = fakeConn.Commands.Count });
        }).RequireTenant();

        // Level 5: Background Jobs
        showcase.MapPost("/levels/level5/background-job", async (
            BackgroundProcessingDemo.TenantJobProcessor processor,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenant = tenantContext.RequiredTenant;
            int count = await processor.ProcessInvoicesForTenantAsync(tenant.Id, ct);
            return Results.Ok(new { Status = "Processed", Tenant = tenant.Name, InvoicesHandled = count });
        }).RequireTenant();

        // Level 6: Error Handling & Conflicts
        showcase.MapGet("/levels/level6/conflicts", async () =>
        {
            await ErrorHandlingDemo.DemonstrateConflictDetectionAsync();
            ErrorHandlingDemo.DemonstrateInactiveTenantHandling();
            ErrorHandlingDemo.DemonstrateUnresolvedContextHandling();
            return Results.Ok(new { Status = "Completed", Description = "Fail-closed conflict detection and tenant guards demonstrated." });
        });

        // Level 7: Scalability
        showcase.MapGet("/levels/level7/scalability", async () =>
        {
            ScalabilityDemo.DemonstrateDatabasePerTenant();
            await ScalabilityDemo.DemonstrateCachedStoreAsync();
            return Results.Ok(new { Status = "Completed", Description = "SQLite Database-per-tenant and CachedTenantStore demonstrated." });
        });

        // Level 9: Observability
        showcase.MapGet("/levels/level9/telemetry", (ITenantContext tenantContext) =>
        {
            ObservabilityDemo.DemonstrateTelemetryWorkflow(tenantContext);
            return Results.Ok(new { Status = "Completed", Description = "OpenTelemetry Activity and Metrics recorded." });
        });

        // Level 10: Enterprise Architecture
        showcase.MapGet("/levels/level10/enterprise", async () =>
        {
            await EnterpriseArchitectureDemo.RunEnterpriseTestHarnessAsync();
            return Results.Ok(new { Status = "Success", Description = "Enterprise 4-layer defense in depth and testing harness completed." });
        });
    }
}
