// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;

namespace EricksonLopez.MultiTenancy.Showcase.Infrastructure;

/// <summary>
/// Domain model for an invoice partitioned by tenant.
/// </summary>
public record Invoice : ITenantEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public TenantId TenantId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = "Draft";
}

/// <summary>
/// Domain model for a customer account.
/// </summary>
public record Customer : ITenantEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public TenantId TenantId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Repository interface for invoice operations within the active tenant context.
/// </summary>
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reference implementation of a multi-tenant repository using Dapper and ITenantContext.
/// Enforces Layer 1 and 2 of the Defense-in-Depth isolation model.
/// </summary>
public class InvoiceRepository : IInvoiceRepository
{
    private readonly DbConnection _connection;
    private readonly ITenantContext _tenantContext;

    public InvoiceRepository(DbConnection connection, ITenantContext tenantContext)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Explicitly verify the tenant context before query execution
        var tenant = _tenantContext.RequiredTenant;

        var parameters = _tenantContext.CreateTenantParameters();
        parameters.Add("Id", id, DbType.Guid);

        const string sql = "SELECT id, tenant_id AS TenantId, invoice_number AS InvoiceNumber, amount, issued_at_utc AS IssuedAtUtc, status " +
                           "FROM invoices WHERE id = @Id AND tenant_id = @TenantId";

        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<Invoice>(command);
    }

    public async Task<IEnumerable<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var parameters = _tenantContext.CreateTenantParameters();

        const string sql = "SELECT id, tenant_id AS TenantId, invoice_number AS InvoiceNumber, amount, issued_at_utc AS IssuedAtUtc, status " +
                           "FROM invoices WHERE tenant_id = @TenantId ORDER BY issued_at_utc DESC";

        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        return await _connection.QueryAsync<Invoice>(command);
    }

    public async Task<int> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var tenant = _tenantContext.RequiredTenant;

        var parameters = new DynamicParameters();
        parameters.WithTenant(_tenantContext);
        parameters.Add("Id", invoice.Id, DbType.Guid);
        parameters.Add("InvoiceNumber", invoice.InvoiceNumber, DbType.String);
        parameters.Add("Amount", invoice.Amount, DbType.Decimal);
        parameters.Add("IssuedAtUtc", invoice.IssuedAtUtc, DbType.DateTime);
        parameters.Add("Status", invoice.Status, DbType.String);

        const string sql = "INSERT INTO invoices (id, tenant_id, invoice_number, amount, issued_at_utc, status) " +
                           "VALUES (@Id, @TenantId, @InvoiceNumber, @Amount, @IssuedAtUtc, @Status)";

        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(command);
    }
}

/// <summary>
/// Pre-seeded tenant metadata identifiers for testing and demonstration.
/// </summary>
public static class SeedTenants
{
    public static readonly TenantId AcmeId = TenantId.Create("11111111-1111-1111-1111-111111111111");
    public static readonly TenantId GlobexId = TenantId.Create("22222222-2222-2222-2222-222222222222");
    public static readonly TenantId InactiveTenantId = TenantId.Create("33333333-3333-3333-3333-333333333333");

    public static readonly TenantInfo Acme = new(
        AcmeId,
        "acme-corp",
        "Host=localhost;Database=acme_db;Username=postgres;Password=secret",
        isActive: true);

    public static readonly TenantInfo Globex = new(
        GlobexId,
        "globex",
        "Host=localhost;Database=globex_db;Username=postgres;Password=secret",
        isActive: true);

    public static readonly TenantInfo InactiveTenant = new(
        InactiveTenantId,
        "suspended-tenant",
        null,
        isActive: false);
}
