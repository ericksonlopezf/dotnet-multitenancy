// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class PostgreSqlOrganizationRlsExtensionsTests
{
    private static readonly Guid ExpectedTenantGuid = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid ExpectedCompanyGuid = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid ExpectedBranchGuid = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    private sealed class FakeOrgContext : IOrganizationContext
    {
        public ITenantInfo? Tenant { get; set; }
        public bool IsResolved => Tenant is not null && Tenant.Id.Value != Guid.Empty && Tenant.IsActive;
        public TenantResolutionSource Source => TenantResolutionSource.ExplicitScope;
        public Guid? CompanyId { get; set; }
        public Guid? BranchId { get; set; }
        public IReadOnlyList<Guid> AllowedBranchIds { get; set; } = [];
        public bool AllBranchesAllowed { get; set; }
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var transaction = new FakeDbTransaction(new FakeDbConnection(), IsolationLevel.ReadCommitted);
        var context = Substitute.For<IOrganizationContext>();

        var act = () => connection.SetOrganizationRlsContextAsync(transaction, context);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_NullContext_ThrowsArgumentNullException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var act = () => connection.SetOrganizationRlsContextAsync(transaction, null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_NullTransaction_ThrowsInvalidOperationException()
    {
        var connection = new FakeDbConnection();
        var context = new FakeOrgContext();

        var act = () => connection.SetOrganizationRlsContextAsync(null!, context);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_ExecutesQueriesSuccessfully()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant"),
            CompanyId = ExpectedCompanyGuid,
            BranchId = ExpectedBranchGuid,
            AllowedBranchIds = [ExpectedBranchGuid],
            AllBranchesAllowed = false
        };

        var act = () => connection.SetOrganizationRlsContextAsync(transaction, context);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_UnresolvedContext_ThrowsTenantNotFoundException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = null // Unresolved
        };

        var act = () => connection.SetOrganizationRlsContextAsync(transaction, context);
        await act.Should().ThrowAsync<TenantNotFoundException>()
            .WithMessage("*unresolved*");
    }

    [Theory]
    [InlineData("invalid-var!@#")]
    [InlineData("var with space")]
    [InlineData("var; DROP TABLE--")]
    public async Task SetOrganizationRlsContextAsync_InvalidSessionVariable_ThrowsArgumentException(string invalidName)
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant")
        };

        var options = new OrganizationRlsOptions
        {
            TenantSessionVariable = invalidName
        };

        var act = () => connection.SetOrganizationRlsContextAsync(transaction, context, options);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid session variable name*");
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_WithStatementTimeoutAndAllBranches_ExecutesExpectedQueries()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant"),
            CompanyId = null, // No company context
            AllBranchesAllowed = true
        };

        var options = new OrganizationRlsOptions
        {
            StatementTimeout = TimeSpan.FromSeconds(5)
        };

        await connection.SetOrganizationRlsContextAsync(transaction, context, options);

        connection.Commands.Should().Contain(c => c.CommandText.Contains("statement_timeout"));
        connection.Commands.Should().Contain(c => c.CommandText.Contains("AllBranches"));
    }

    [Fact]
    public async Task BeginEnterpriseTransactionAsync_NullGuards_ThrowsArgumentNullException()
    {
        FakeDbConnection nullConn = null!;
        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant")
        };

        var actNullConn = () => nullConn.BeginEnterpriseTransactionAsync(context);
        await actNullConn.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");

        var connection = new FakeDbConnection();
        var actNullCtx = () => connection.BeginEnterpriseTransactionAsync(null!);
        await actNullCtx.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task BeginEnterpriseTransactionAsync_Valid_OpensAndStartsTransaction()
    {
        var connection = new FakeDbConnection();
        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant"),
            CompanyId = ExpectedCompanyGuid
        };

        var tx = await connection.BeginEnterpriseTransactionAsync(context);

        connection.State.Should().Be(ConnectionState.Open);
        tx.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginEnterpriseTransactionAsync_Fails_RollsBackAndDisposes()
    {
        var connection = new FakeDbConnection();
        var context = new FakeOrgContext
        {
            Tenant = null // causes TenantNotFoundException
        };

        var act = () => connection.BeginEnterpriseTransactionAsync(context);
        await act.Should().ThrowAsync<TenantNotFoundException>();

        var tx = connection.Transactions.Single();
        tx.IsRolledBack.Should().BeTrue();
        tx.IsDisposed.Should().BeTrue();
    }
}
