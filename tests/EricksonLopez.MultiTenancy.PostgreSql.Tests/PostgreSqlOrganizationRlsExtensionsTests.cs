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
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("PostgreSQL RLS context must be established inside an active transaction (is_local = true) to prevent connection pool contamination.");
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_ExecutesQueriesSuccessfully()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var branch2 = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant"),
            CompanyId = ExpectedCompanyGuid,
            BranchId = ExpectedBranchGuid,
            AllowedBranchIds = [ExpectedBranchGuid, branch2],
            AllBranchesAllowed = false
        };

        await connection.SetOrganizationRlsContextAsync(transaction, context);

        connection.Commands.Should().HaveCount(5);
        connection.Commands[0].CommandText.Should().Be("SELECT set_config('app.current_tenant_id', @TenantId, true)");
        connection.Commands[0].Parameters["TenantId"].Value.Should().Be(ExpectedTenantGuid.ToString());
        connection.Commands[1].CommandText.Should().Be("SELECT set_config('app.current_company_id', @CompanyId, true)");
        connection.Commands[1].Parameters["CompanyId"].Value.Should().Be(ExpectedCompanyGuid.ToString());
        connection.Commands[2].CommandText.Should().Be("SELECT set_config('app.all_branches_allowed', @AllBranches, true)");
        connection.Commands[2].Parameters["AllBranches"].Value.Should().Be("false");
        connection.Commands[3].CommandText.Should().Be("SELECT set_config('app.current_branch_ids', @BranchIds, true)");
        connection.Commands[3].Parameters["BranchIds"].Value.Should().Be($"{ExpectedBranchGuid},{branch2}");
        connection.Commands[4].CommandText.Should().Be("SET LOCAL statement_timeout = '30s'");
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
        var ex = await act.Should().ThrowAsync<TenantNotFoundException>();
        ex.Which.Message.Should().Be("Cannot establish PostgreSQL Organization RLS context because the tenant context is unresolved or empty.");
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

        var optionsTenant = new OrganizationRlsOptions { TenantSessionVariable = invalidName };
        var actTenant = () => connection.SetOrganizationRlsContextAsync(transaction, context, optionsTenant);
        await actTenant.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid session variable name*");

        var optionsCompany = new OrganizationRlsOptions { CompanySessionVariable = invalidName };
        var actCompany = () => connection.SetOrganizationRlsContextAsync(transaction, context, optionsCompany);
        await actCompany.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid session variable name*");

        var optionsAllBranches = new OrganizationRlsOptions { AllBranchesSessionVariable = invalidName };
        var actAllBranches = () => connection.SetOrganizationRlsContextAsync(transaction, context, optionsAllBranches);
        await actAllBranches.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid session variable name*");

        var optionsBranchIds = new OrganizationRlsOptions { BranchIdsSessionVariable = invalidName };
        var actBranchIds = () => connection.SetOrganizationRlsContextAsync(transaction, context, optionsBranchIds);
        await actBranchIds.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid session variable name*");
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
            AllBranchesAllowed = true,
            AllowedBranchIds = [ExpectedBranchGuid]
        };

        var options = new OrganizationRlsOptions
        {
            StatementTimeout = TimeSpan.FromSeconds(5)
        };

        await connection.SetOrganizationRlsContextAsync(transaction, context, options);

        connection.Commands.Should().Contain(c => c.CommandText.Contains("statement_timeout"));
        connection.Commands.Should().Contain(c => c.CommandText.Contains("AllBranches"));
        connection.Commands.Should().NotContain(c => c.CommandText.Contains("current_branch_ids"));
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_ZeroTimeout_DoesNotSetStatementTimeout()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant"),
            AllBranchesAllowed = true
        };

        var options = new OrganizationRlsOptions
        {
            StatementTimeout = TimeSpan.Zero
        };

        await connection.SetOrganizationRlsContextAsync(transaction, context, options);

        connection.Commands.Should().NotContain(c => c.CommandText.Contains("statement_timeout"));
    }

    [Fact]
    public void OrganizationRlsOptions_DefaultValues_AreCorrect()
    {
        var options = new OrganizationRlsOptions();
        options.TenantSessionVariable.Should().Be("app.current_tenant_id");
        options.CompanySessionVariable.Should().Be("app.current_company_id");
        options.BranchIdsSessionVariable.Should().Be("app.current_branch_ids");
        options.AllBranchesSessionVariable.Should().Be("app.all_branches_allowed");
        options.BypassRlsSessionVariable.Should().Be("app.bypass_rls");
        options.StatementTimeout.Should().Be(TimeSpan.FromSeconds(30));
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
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_InactiveTenant_ThrowsTenantNotFoundException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Inactive Tenant", isActive: false)
        };

        var act = () => connection.SetOrganizationRlsContextAsync(transaction, context);
        await act.Should().ThrowAsync<TenantNotFoundException>();
    }

    [Fact]
    public async Task SetOrganizationRlsContextAsync_EmptyAllowedBranchIds_DoesNotSetBranchIds()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(ExpectedTenantGuid), "Test Tenant"),
            AllBranchesAllowed = false,
            AllowedBranchIds = []
        };

        await connection.SetOrganizationRlsContextAsync(transaction, context);

        connection.Commands.Should().NotContain(c => c.CommandText.Contains("current_branch_ids"));
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
