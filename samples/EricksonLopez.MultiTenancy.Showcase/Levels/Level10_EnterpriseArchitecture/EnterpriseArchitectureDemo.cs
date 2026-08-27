// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.OpenTelemetry;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Testing;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level10_EnterpriseArchitecture;

/// <summary>
/// Level 10 — Enterprise Architecture: 4-layer defense in depth, clean architecture, and testing harnesses.
/// </summary>
public static class EnterpriseArchitectureDemo
{
    /// <summary>
    /// Enterprise Application Service coordinating business logic across tenant boundaries.
    /// </summary>
    public class OrderProcessingService
    {
        private readonly IInvoiceRepository _repository;
        private readonly ITenantContext _tenantContext;
        private readonly ITenantTraceEnricher _traceEnricher;

        public OrderProcessingService(
            IInvoiceRepository repository,
            ITenantContext tenantContext,
            ITenantTraceEnricher traceEnricher)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
            _traceEnricher = traceEnricher ?? throw new ArgumentNullException(nameof(traceEnricher));
        }

        public async Task<Invoice> ProcessOrderAsync(decimal amount, CancellationToken ct = default)
        {
            // Layer 1: Application verification & Telemetry enrichment
            _traceEnricher.Enrich(_tenantContext);
            var tenant = _tenantContext.RequiredTenant;

            var invoice = new Invoice
            {
                TenantId = tenant.Id,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6]}",
                Amount = amount,
                IssuedAtUtc = DateTime.UtcNow,
                Status = "Confirmed"
            };

            // Layer 2: Repository parameterization
            await _repository.CreateAsync(invoice, ct);
            return invoice;
        }
    }

    /// <summary>
    /// Demonstrates how enterprise unit tests use TenantContextBuilder and TestTenantContext.
    /// </summary>
    public static async Task RunEnterpriseTestHarnessAsync()
    {
        Console.WriteLine("--- Running Enterprise Test Harness with Test Doubles ---");

        // 1. Build test tenant using fluent builder
        var testContext = new TenantContextBuilder()
            .WithId(Guid.NewGuid())
            .WithName("enterprise-sandbox")
            .WithActive(true)
            .WithProperty("Compliance", "SOC2")
            .WithSource(TenantResolutionSource.ExplicitScope)
            .BuildContext();

        // 2. Setup fake database connection
        using var fakeConn = new FakeDbConnection();
        var repository = new InvoiceRepository(fakeConn, testContext);
        var enricher = new TenantTraceEnricher();

        var service = new OrderProcessingService(repository, testContext, enricher);

        // 3. Execute business logic within test harness
        var createdInvoice = await service.ProcessOrderAsync(1499.99m);

        Console.WriteLine($"[Test Passed] Order processed successfully: Invoice={createdInvoice.InvoiceNumber}, TenantId={createdInvoice.TenantId}");

        // 4. Demonstrate TestTenantContext.Create() — a lighter alternative to the fluent builder
        //    for quick one-liner test setup
        var quickContext = TestTenantContext.Create(tenantName: "quick-test-tenant");
        Console.WriteLine($"Quick context tenant: {quickContext.RequiredTenant.Name} (IsResolved={quickContext.IsResolved})");

        // 5. Demonstrate Reset() — clears the context for re-use across multiple test steps
        quickContext.Reset();
        Console.WriteLine($"After reset: IsResolved={quickContext.IsResolved}, Source={quickContext.Source}");
    }
}
