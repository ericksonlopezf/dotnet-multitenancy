// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level05_BackgroundProcessing;

/// <summary>
/// Level 5 — Background Processing: Isolated DI scopes and deterministic execution for non-HTTP background jobs.
/// </summary>
public static class BackgroundProcessingDemo
{
    /// <summary>
    /// Service executing background work within an isolated tenant scope.
    /// </summary>
    public class TenantJobProcessor
    {
        private readonly ITenantScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<TenantJobProcessor> _logger;

        public TenantJobProcessor(
            ITenantScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<TenantJobProcessor> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _tenantStore = tenantStore ?? throw new ArgumentNullException(nameof(tenantStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes a tenant-isolated background task deterministically.
        /// </summary>
        public async Task<int> ProcessInvoicesForTenantAsync(TenantId tenantId, CancellationToken ct = default)
        {
            // 1. Fetch tenant metadata from store
            var storeResult = await _tenantStore.GetTenantAsync(tenantId, ct);
            if (!storeResult.IsSuccess)
            {
                throw new TenantNotFoundException($"Cannot process job: Tenant '{tenantId}' not found.");
            }

            var tenant = storeResult.Value;

            // 2. Create isolated DI scope with pre-populated tenant context
            // Source = BackgroundJob for audit log trail
            await using var scope = _scopeFactory.CreateScope(tenant, TenantResolutionSource.BackgroundJob);

            // 3. Resolve tenant-scoped dependencies from the scope's ServiceProvider
            var tenantContext = scope.TenantContext;
            var invoiceRepository = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

            _logger.LogInformation(
                "Processing background job for tenant '{TenantName}' (ID: {TenantId}, Source: {Source})",
                tenant.Name,
                tenant.Id,
                tenantContext.Source);

            // 4. Perform domain logic within isolation boundary
            var invoices = await invoiceRepository.GetAllAsync(ct);
            int count = 0;
            foreach (var inv in invoices)
            {
                count++;
            }

            return count;
        }
    }
}
