// Copyright © Erickson Lopez. MIT License.
#pragma warning disable CA1861
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Testing;
using EricksonLopez.Result;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public sealed class TestingPackageTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly TenantId ExpectedTenantId = new(ExpectedGuid);

    // ==========================================
    // TestTenantContext Tests
    // ==========================================

    [Fact]
    public void TestTenantContext_DefaultConstructor_ShouldBeUnresolved()
    {
        var context = new TestTenantContext();

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.None);
        var act = () => _ = context.RequiredTenant;
        act.Should().Throw<TenantNotFoundException>()
            .WithMessage("No tenant has been resolved in the current test execution context.");
    }

    [Fact]
    public void TestTenantContext_ConstructorWithTenantAndDefaultSource_ShouldBeExplicitScope()
    {
        var tenant = new TenantInfo(ExpectedTenantId, "DefaultSourceTenant");
        var context = new TestTenantContext(tenant);

        context.IsResolved.Should().BeTrue();
        context.Tenant.Should().BeSameAs(tenant);
        context.Source.Should().Be(TenantResolutionSource.ExplicitScope);
        context.RequiredTenant.Should().BeSameAs(tenant);
    }

    [Fact]
    public void TestTenantContext_ConstructorWithNullTenant_ShouldBeUnresolved()
    {
        var context = new TestTenantContext(null, TenantResolutionSource.Header);

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.Header);
        var act = () => _ = context.RequiredTenant;
        act.Should().Throw<TenantNotFoundException>();
    }

    [Fact]
    public void TestTenantContext_TenantWithEmptyId_IsResolvedShouldBeFalse()
    {
        var tenant = new TenantInfo(TenantId.Empty, "EmptyTenant");
        var context = new TestTenantContext(tenant);

        context.IsResolved.Should().BeFalse();
        var act = () => _ = context.RequiredTenant;
        act.Should().Throw<TenantNotFoundException>()
            .WithMessage("The resolved tenant is invalid or has an empty identifier.");
    }

    [Fact]
    public void TestTenantContext_TenantInactive_RequiredTenantThrowsTenantInactiveException()
    {
        var tenant = new TenantInfo(ExpectedTenantId, "InactiveTenant", isActive: false);
        var context = new TestTenantContext(tenant);

        var act = () => _ = context.RequiredTenant;
        act.Should().Throw<TenantInactiveException>();
    }

    [Theory]
    [InlineData("Acme Corp")]
    [InlineData("Default Tenant")]
    public void TestTenantContext_Create_ShouldReturnResolvedContextWithActiveTenant(string name)
    {
        var context = name == "Default Tenant" ? TestTenantContext.Create() : TestTenantContext.Create(name);

        context.IsResolved.Should().BeTrue();
        context.Tenant.Should().NotBeNull();
        context.Tenant!.Name.Should().Be(name == "Default Tenant" ? "Test Tenant" : name);
        context.Tenant.IsActive.Should().BeTrue();
        context.Tenant.Id.Should().NotBe(TenantId.Empty);
        context.RequiredTenant.Name.Should().Be(name == "Default Tenant" ? "Test Tenant" : name);
        context.Source.Should().Be(TenantResolutionSource.ExplicitScope);
    }

    [Fact]
    public void TestTenantContext_Reset_ShouldClearTenantAndSource()
    {
        var context = TestTenantContext.Create();
        context.IsResolved.Should().BeTrue();

        context.Reset();

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.None);
    }

    [Fact]
    public void TestTenantContext_AsAccessor_SetNonNull_ShouldUpdateContext()
    {
        var context = new TestTenantContext();
        ITenantContextAccessor accessor = context;

        accessor.TenantContext.Should().BeSameAs(context);

        var newTenant = new TenantInfo(ExpectedTenantId, "Accessor Tenant");
        var otherContext = new TestTenantContext(newTenant, TenantResolutionSource.Header);
        accessor.TenantContext = otherContext;

        context.IsResolved.Should().BeTrue();
        context.Tenant!.Name.Should().Be("Accessor Tenant");
        context.Source.Should().Be(TenantResolutionSource.Header);
    }

    [Fact]
    public void TestTenantContext_AsAccessor_SetNull_ShouldResetContext()
    {
        var context = TestTenantContext.Create();
        ITenantContextAccessor accessor = context;

        accessor.TenantContext = null;

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.None);
    }

    // ==========================================
    // TenantContextBuilder Tests
    // ==========================================

    [Fact]
    public void TenantContextBuilder_DefaultValues_ShouldHaveTestTenantName()
    {
        var builder = new TenantContextBuilder();
        var tenant = builder.BuildTenantInfo();
        tenant.Name.Should().Be("Test Tenant");
        tenant.IsActive.Should().BeTrue();
        tenant.ConnectionString.Should().BeNull();
        tenant.Properties.Should().BeEmpty();
        tenant.Id.Should().NotBe(TenantId.Empty);

        var context = builder.BuildContext();
        context.IsResolved.Should().BeTrue();
        context.Tenant!.Name.Should().Be("Test Tenant");
        context.Source.Should().Be(TenantResolutionSource.ExplicitScope);
    }

    [Fact]
    public void TenantContextBuilder_WithIdTenantId_SetsId()
    {
        var context = new TenantContextBuilder()
            .WithId(ExpectedTenantId)
            .BuildContext();

        context.Tenant!.Id.Should().Be(ExpectedTenantId);
    }

    [Fact]
    public void TenantContextBuilder_WithIdGuid_SetsId()
    {
        var context = new TenantContextBuilder()
            .WithId(ExpectedGuid)
            .BuildContext();

        context.Tenant!.Id.Value.Should().Be(ExpectedGuid);
    }

    [Fact]
    public void TenantContextBuilder_WithName_NullName_ThrowsArgumentNullException()
    {
        var builder = new TenantContextBuilder();

        var act = () => builder.WithName(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void TenantContextBuilder_WithProperty_NullKey_ThrowsArgumentNullException()
    {
        var builder = new TenantContextBuilder();

        var act = () => builder.WithProperty(null!, "val");

        act.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void TenantContextBuilder_ShouldConstructCustomizedContext()
    {
        var context = new TenantContextBuilder()
            .WithId(ExpectedGuid)
            .WithName("Enterprise Tenant")
            .WithConnectionString("Server=db;Database=tenant_1;")
            .WithActive(true)
            .WithProperty("Tier", "Platinum")
            .WithProperty("region", "US-East")
            .WithSource(TenantResolutionSource.JwtClaim)
            .BuildContext();

        context.IsResolved.Should().BeTrue();
        context.Tenant.Should().NotBeNull();
        context.Tenant!.Id.Value.Should().Be(ExpectedGuid);
        context.Tenant.Name.Should().Be("Enterprise Tenant");
        context.Tenant.ConnectionString.Should().Be("Server=db;Database=tenant_1;");
        context.Tenant.IsActive.Should().BeTrue();
        context.Tenant.Properties["tier"].Should().Be("Platinum");
        context.Tenant.Properties["REGION"].Should().Be("US-East");
        context.Source.Should().Be(TenantResolutionSource.JwtClaim);
    }

    [Fact]
    public void TenantContextBuilder_BuildTenantInfo_ShouldReturnTenantInfoObject()
    {
        var builder = new TenantContextBuilder()
            .WithId(ExpectedTenantId)
            .WithName("Standalone Tenant")
            .WithConnectionString("Server=test;");

        var tenantInfo = builder.BuildTenantInfo();

        tenantInfo.Id.Should().Be(ExpectedTenantId);
        tenantInfo.Name.Should().Be("Standalone Tenant");
        tenantInfo.ConnectionString.Should().Be("Server=test;");
        tenantInfo.IsActive.Should().BeTrue();
    }

    [Fact]
    public void TenantContextBuilder_AsInactive_ShouldSetIsActiveToFalse()
    {
        var context = new TenantContextBuilder()
            .AsInactive()
            .BuildContext();

        context.Tenant!.IsActive.Should().BeFalse();
    }

    // ==========================================
    // FakeTenantResolutionStrategy Tests
    // ==========================================

    [Fact]
    public void FakeTenantResolutionStrategy_ConstructorTenantId_NullStrategyName_ThrowsArgumentNullException()
    {
        var act = () => new FakeTenantResolutionStrategy(ExpectedTenantId, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("strategyName");
    }

    [Fact]
    public void FakeTenantResolutionStrategy_ConstructorResult_NullStrategyName_ThrowsArgumentNullException()
    {
        var act = () => new FakeTenantResolutionStrategy(Result<TenantId>.Success(ExpectedTenantId), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("strategyName");
    }

    [Fact]
    public void FakeTenantResolutionStrategy_DefaultStrategyName_ShouldBeFakeStrategy()
    {
        var strategy1 = new FakeTenantResolutionStrategy(ExpectedTenantId);
        strategy1.StrategyName.Should().Be("FakeStrategy");

        var strategy2 = new FakeTenantResolutionStrategy(Result<TenantId>.Success(ExpectedTenantId));
        strategy2.StrategyName.Should().Be("FakeStrategy");
    }

    [Fact]
    public async Task FakeTenantResolutionStrategy_ShouldReturnPresetResultAndIncrementCount()
    {
        var strategy = new FakeTenantResolutionStrategy(ExpectedTenantId, "CustomFake");

        strategy.StrategyName.Should().Be("CustomFake");
        strategy.InvocationCount.Should().Be(0);

        var result = await strategy.ResolveTenantIdAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ExpectedTenantId);
        strategy.InvocationCount.Should().Be(1);

        var newId = TenantId.NewId();
        strategy.SetResult(newId);

        var secondResult = await strategy.ResolveTenantIdAsync();
        secondResult.Value.Should().Be(newId);
        strategy.InvocationCount.Should().Be(2);

        var failureResult = Result<TenantId>.Failure(TenantErrors.NotFound(newId));
        strategy.SetResult(failureResult);

        var thirdResult = await strategy.ResolveTenantIdAsync();
        thirdResult.IsFailure.Should().BeTrue();
        thirdResult.Error.Code.Should().Be(TenantErrors.NotFound(newId).Code);
        strategy.InvocationCount.Should().Be(3);
    }

    // ==========================================
    // FakeTenantStore Tests
    // ==========================================

    [Fact]
    public void FakeTenantStore_ConstructorNullCollection_ThrowsArgumentNullException()
    {
        var act = () => new FakeTenantStore((IEnumerable<ITenantInfo>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tenants");
    }

    [Fact]
    public void FakeTenantStore_WithTenantNull_ThrowsArgumentNullException()
    {
        var store = new FakeTenantStore();

        var act = () => store.WithTenant(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tenant");
    }

    [Fact]
    public async Task FakeTenantStore_ShouldRetrieveSeededTenantsAndHandleNotFound()
    {
        var tenant1 = new TenantInfo(ExpectedTenantId, "Tenant 1");
        var tenant2Id = TenantId.NewId();
        var tenant2 = new TenantInfo(tenant2Id, "Tenant 2");
        var store = new FakeTenantStore(new[] { tenant1 });

        var returnedStore = store.WithTenant(tenant2);
        returnedStore.Should().BeSameAs(store);

        var res1 = await store.GetTenantAsync(tenant1.Id, CancellationToken.None);
        res1.IsSuccess.Should().BeTrue();
        res1.Value.Name.Should().Be("Tenant 1");

        var res2 = await store.GetTenantAsync(tenant2Id);
        res2.IsSuccess.Should().BeTrue();
        res2.Value.Name.Should().Be("Tenant 2");

        var missingId = TenantId.NewId();
        var missingResult = await store.GetTenantAsync(missingId);
        missingResult.IsFailure.Should().BeTrue();
        missingResult.Error.Code.Should().Be(TenantErrors.NotFound(missingId).Code);

        store.RemoveTenant(tenant1.Id).Should().BeTrue();
        store.RemoveTenant(tenant1.Id).Should().BeFalse();

        var removedResult = await store.GetTenantAsync(tenant1.Id);
        removedResult.IsFailure.Should().BeTrue();
    }

    // ==========================================
    // FakeTenantStore<TTenant> Tests
    // ==========================================

    [Fact]
    public void FakeTenantStore_Generic_ConstructorNullCollection_ThrowsArgumentNullException()
    {
        var act = () => new FakeTenantStore<TenantInfo>((IEnumerable<TenantInfo>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tenants");
    }

    [Fact]
    public void FakeTenantStore_Generic_WithTenantNull_ThrowsArgumentNullException()
    {
        var store = new FakeTenantStore<TenantInfo>();

        var act = () => store.WithTenant(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tenant");
    }

    [Fact]
    public async Task FakeTenantStore_Generic_ShouldSupportTypedTenantsAndExplicitInterface()
    {
        var tenant1 = new TenantInfo(ExpectedTenantId, "Typed Tenant 1");
        var tenant2Id = TenantId.NewId();
        var tenant2 = new TenantInfo(tenant2Id, "Typed Tenant 2");

        var store = new FakeTenantStore<TenantInfo>(new[] { tenant1 });
        var returnedStore = store.WithTenant(tenant2);
        returnedStore.Should().BeSameAs(store);

        // Strongly typed calls
        var typedResult = await store.GetTenantAsync(tenant1.Id, CancellationToken.None);
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Name.Should().Be("Typed Tenant 1");

        var missingId = TenantId.NewId();
        var missingTypedResult = await store.GetTenantAsync(missingId);
        missingTypedResult.IsFailure.Should().BeTrue();
        missingTypedResult.Error.Code.Should().Be(TenantErrors.NotFound(missingId).Code);

        // Explicit ITenantStore interface calls
        ITenantStore nonGenericStore = store;
        var nonGenericSuccess = await nonGenericStore.GetTenantAsync(tenant2Id, CancellationToken.None);
        nonGenericSuccess.IsSuccess.Should().BeTrue();
        nonGenericSuccess.Value.Name.Should().Be("Typed Tenant 2");

        var nonGenericFailure = await nonGenericStore.GetTenantAsync(missingId, CancellationToken.None);
        nonGenericFailure.IsFailure.Should().BeTrue();
        nonGenericFailure.Error.Code.Should().Be(TenantErrors.NotFound(missingId).Code);
    }

    // ==========================================
    // FakeDbInfrastructure Tests
    // ==========================================

    [Fact]
    public async Task FakeDbConnection_OpenAndClose_TracksStateAndCommands()
    {
        var conn = new FakeDbConnection();
        conn.State.Should().Be(ConnectionState.Closed);
        conn.ConnectionString.Should().Be("Fake");
        conn.Database.Should().Be("FakeDb");
        conn.DataSource.Should().Be("FakeServer");
        conn.ServerVersion.Should().Be("1.0");

        conn.Open();
        conn.State.Should().Be(ConnectionState.Open);

        conn.Close();
        conn.State.Should().Be(ConnectionState.Closed);

        await conn.OpenAsync(CancellationToken.None);
        conn.State.Should().Be(ConnectionState.Open);

        conn.ChangeDatabase("NewDb"); // Should not throw

        var cmd = conn.CreateCommand();
        cmd.Should().NotBeNull();
        cmd.Connection.Should().BeSameAs(conn);
        conn.Commands.Should().ContainSingle();
    }

    [Fact]
    public async Task FakeDbConnection_Transactions_CommitAndRollbackTracking()
    {
        var conn = new FakeDbConnection();
        conn.Open();

        using (var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted))
        {
            var fakeTx = (FakeDbTransaction)tx;
            fakeTx.IsolationLevel.Should().Be(IsolationLevel.ReadCommitted);
            fakeTx.IsCommitted.Should().BeFalse();
            fakeTx.IsRolledBack.Should().BeFalse();

            fakeTx.Commit();
            fakeTx.IsCommitted.Should().BeTrue();
        }

        conn.Transactions.Should().HaveCount(1);

        var asyncTx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        var fakeAsyncTx = (FakeDbTransaction)asyncTx;
        fakeAsyncTx.IsolationLevel.Should().Be(IsolationLevel.Serializable);

        await fakeAsyncTx.RollbackAsync(CancellationToken.None);
        fakeAsyncTx.IsRolledBack.Should().BeTrue();

        await fakeAsyncTx.CommitAsync(CancellationToken.None);
        fakeAsyncTx.IsCommitted.Should().BeTrue();

        await fakeAsyncTx.DisposeAsync();
        fakeAsyncTx.IsDisposed.Should().BeTrue();

        conn.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task FakeDbCommand_ExecuteAndParameters_BehavesCorrectly()
    {
        var conn = new FakeDbConnection();
        var cmd = (FakeDbCommand)conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.CommandTimeout = 30;
        cmd.CommandType = CommandType.Text;
        cmd.DesignTimeVisible = false;
        cmd.UpdatedRowSource = UpdateRowSource.None;

        cmd.Cancel(); // Should not throw
        cmd.Prepare(); // Should not throw

        var nonQueryResult = await cmd.ExecuteNonQueryAsync(CancellationToken.None);
        nonQueryResult.Should().Be(1);

        var scalarResult = await cmd.ExecuteScalarAsync(CancellationToken.None);
        scalarResult.Should().BeNull();

        var p = cmd.CreateParameter();
        p.ParameterName = "@param1";
        p.Value = "testValue";
        p.DbType = DbType.String;
        p.Direction = ParameterDirection.Input;
        p.IsNullable = true;
        p.Size = 50;
        p.SourceColumn = "Col1";
        p.SourceColumnNullMapping = false;
        p.ResetDbType();

        cmd.Parameters.Add(p);
        cmd.Parameters.Count.Should().Be(1);
        cmd.Parameters.Contains("@param1").Should().BeTrue();
        cmd.Parameters.Contains(p).Should().BeTrue();
        cmd.Parameters.IndexOf("@param1").Should().Be(0);
        cmd.Parameters.IndexOf(p).Should().Be(0);
        cmd.Parameters["@param1"].Value.Should().Be("testValue");

        cmd.Parameters.Remove(p);
        cmd.Parameters.Count.Should().Be(0);

        cmd.Parameters.AddRange(new DbParameter[] { p });
        cmd.Parameters.Count.Should().Be(1);

        cmd.Parameters.RemoveAt("@param1");
        cmd.Parameters.Count.Should().Be(0);

        cmd.Parameters.Add(p);
        cmd.Parameters.RemoveAt(0);
        cmd.Parameters.Count.Should().Be(0);

        var freshCmd = new FakeDbCommand();
        freshCmd.CommandText.Should().BeEmpty();

        var freshParam = new FakeDbParameter();
        freshParam.ParameterName.Should().BeEmpty();
        freshParam.SourceColumn.Should().BeEmpty();
    }

    [Fact]
    public async Task FakeDbDataReader_ReadAndAccessors_ReturnsExpectedValues()
    {
        var cols = new[] { "Id", "Name", "IsActive", "Amount", "CreatedAt" };
        var idGuid = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var rows = new List<object?[]>
        {
            new object?[] { idGuid, "TenantA", true, 100.50m, now },
            new object?[] { DBNull.Value, null, false, 0m, now }
        };

        using var emptyReader = new FakeDbDataReader(cols, new List<object?[]>());
        emptyReader.HasRows.Should().BeFalse();

        using var reader = new FakeDbDataReader(cols, rows);
        reader.FieldCount.Should().Be(5);
        reader.HasRows.Should().BeTrue();
        reader.Depth.Should().Be(0);
        reader.RecordsAffected.Should().Be(2);

        var read1 = await reader.ReadAsync(CancellationToken.None);
        read1.Should().BeTrue();
        reader.GetGuid(0).Should().Be(idGuid);
        reader.GetString(1).Should().Be("TenantA");
        reader.GetBoolean(2).Should().BeTrue();
        reader.GetDecimal(3).Should().Be(100.50m);
        reader.GetDateTime(4).Should().Be(now);
        reader.GetName(0).Should().Be("Id");
        reader.GetOrdinal("Name").Should().Be(1);
        reader.GetDataTypeName(0).Should().Be(typeof(Guid).Name);
        reader.GetFieldType(0).Should().Be<Guid>();
        reader.IsDBNull(0).Should().BeFalse();
        reader["Id"].Should().Be(idGuid);
        reader[1].Should().Be("TenantA");

        var values = new object[5];
        var copied = reader.GetValues(values);
        copied.Should().Be(5);
        values[0].Should().Be(idGuid);
        values[1].Should().Be("TenantA");
        values[2].Should().Be(true);
        values[3].Should().Be(100.50m);
        values[4].Should().Be(now);

        var read2 = await reader.ReadAsync(CancellationToken.None);
        read2.Should().BeTrue();
        reader.IsDBNull(0).Should().BeTrue();
        reader.IsDBNull(1).Should().BeTrue();

        var read3 = await reader.ReadAsync(CancellationToken.None);
        read3.Should().BeFalse();

        var nextResult = await reader.NextResultAsync(CancellationToken.None);
        nextResult.Should().BeFalse();

        reader.Close();
        reader.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task FakeDbConnection_ClientId_And_DataReaderFactory_Work()
    {
        var conn = new FakeDbConnection();
        conn.ClientId = "Client_Oracle_VPD";
        conn.ClientId.Should().Be("Client_Oracle_VPD");

        var customReader = new FakeDbDataReader(new[] { "Col" }, new List<object?[]> { new object?[] { 42 } });
        conn.DataReaderFactory = cmd => customReader;

        var cmd = (FakeDbCommand)conn.CreateCommand();
        cmd.DataReaderFactory.Should().NotBeNull();
        var reader = cmd.ExecuteReader();
        reader.Should().BeSameAs(customReader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actOpen = () => conn.OpenAsync(cts.Token);
        await actOpen.Should().ThrowAsync<OperationCanceledException>();

        var actTx = () => conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cts.Token).AsTask();
        await actTx.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FakeDbTransaction_CancellationTokens_ThrowWhenCancelled()
    {
        var conn = new FakeDbConnection();
        var tx = new FakeDbTransaction(conn, IsolationLevel.Serializable);

        tx.Connection.Should().BeSameAs(conn);
        tx.Rollback();
        tx.IsRolledBack.Should().BeTrue();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actCommit = () => tx.CommitAsync(cts.Token);
        await actCommit.Should().ThrowAsync<OperationCanceledException>();

        var actRollback = () => tx.RollbackAsync(cts.Token);
        await actRollback.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FakeDbCommand_SynchronousAndAsyncExecutions_CoverAllBranches()
    {
        var conn = new FakeDbConnection();
        var tx = new FakeDbTransaction(conn, IsolationLevel.ReadCommitted);
        var cmd = (FakeDbCommand)conn.CreateCommand();

        cmd.Transaction = tx;
        cmd.Transaction.Should().BeSameAs(tx);

        cmd.ExecuteNonQuery().Should().Be(1);
        cmd.ExecuteScalar().Should().BeNull();

        var defaultReader = cmd.ExecuteReader();
        defaultReader.Should().NotBeNull();
        defaultReader.FieldCount.Should().Be(0);

        var asyncReader = await cmd.ExecuteReaderAsync(CancellationToken.None);
        asyncReader.Should().NotBeNull();
        asyncReader.FieldCount.Should().Be(0);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actNonQuery = () => cmd.ExecuteNonQueryAsync(cts.Token);
        await actNonQuery.Should().ThrowAsync<OperationCanceledException>();

        var actScalar = () => cmd.ExecuteScalarAsync(cts.Token);
        await actScalar.Should().ThrowAsync<OperationCanceledException>();

        var actReader = () => cmd.ExecuteReaderAsync(cts.Token);
        await actReader.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FakeDbDataReader_AllDataTypesAndErrorHandling_Work()
    {
        var actCtorNullCols = () => new FakeDbDataReader(null!, new List<object?[]>());
        actCtorNullCols.Should().Throw<ArgumentNullException>().WithParameterName("columnNames");

        var actCtorNullRows = () => new FakeDbDataReader(new[] { "A" }, null!);
        actCtorNullRows.Should().Throw<ArgumentNullException>().WithParameterName("rows");

        var cols = new[] { "ByteCol", "CharCol", "ShortCol", "IntCol", "LongCol", "FloatCol", "DoubleCol", "NullCol" };
        var rows = new List<object?[]>
        {
            new object?[] { (byte)255, 'Z', (short)123, 456, 789L, 1.23f, 4.56d, null }
        };

        using var reader = new FakeDbDataReader(cols, rows);
        reader.Read().Should().BeTrue();

        reader.GetByte(0).Should().Be(255);
        reader.GetBytes(0, 0, null, 0, 0).Should().Be(0);
        reader.GetChar(1).Should().Be('Z');
        reader.GetChars(1, 0, null, 0, 0).Should().Be(0);
        reader.GetInt16(2).Should().Be(123);
        reader.GetInt32(3).Should().Be(456);
        reader.GetInt64(4).Should().Be(789L);
        reader.GetFloat(5).Should().Be(1.23f);
        reader.GetDouble(6).Should().Be(4.56d);
        reader.GetFieldType(7).Should().Be<object>();
        reader.GetValue(7).Should().Be(DBNull.Value);

        var smallArray = new object[3];
        reader.GetValues(smallArray).Should().Be(3);
        smallArray[0].Should().Be((byte)255);
        smallArray[1].Should().Be('Z');
        smallArray[2].Should().Be((short)123);

        var actNullValues = () => reader.GetValues(null!);
        actNullValues.Should().Throw<ArgumentNullException>().WithParameterName("values");

        reader.NextResult().Should().BeFalse();

        var enumerator = reader.GetEnumerator();
        enumerator.Should().NotBeNull();
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
        }
        reader.IsClosed.Should().BeFalse();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actReadAsync = () => reader.ReadAsync(cts.Token);
        await actReadAsync.Should().ThrowAsync<OperationCanceledException>();

        var actNextAsync = () => reader.NextResultAsync(cts.Token);
        await actNextAsync.Should().ThrowAsync<OperationCanceledException>();

        reader.Close();
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void FakeDbParameterCollection_FullMethodCoverage()
    {
        var conn = new FakeDbConnection();
        var cmd = (FakeDbCommand)conn.CreateCommand();
        var coll = (FakeDbParameterCollection)cmd.Parameters;

        coll.SyncRoot.Should().NotBeNull();

        var actAddRangeNull = () => coll.AddRange(null!);
        actAddRangeNull.Should().Throw<ArgumentNullException>().WithParameterName("values");

        var p1 = new FakeDbParameter { ParameterName = "@p1", Value = 1 };
        var p2 = new FakeDbParameter { ParameterName = "@p2", Value = 2 };
        var p3 = new FakeDbParameter { ParameterName = "@p3", Value = 3 };

        coll.Add(p1);
        coll.Contains(p2).Should().BeFalse();
        coll.Contains("@nonexistent").Should().BeFalse();
        coll.IndexOf(p2).Should().Be(-1);
        coll.IndexOf("@nonexistent").Should().Be(-1);

        var actMissing = () => _ = coll["@nonexistent"];
        actMissing.Should().Throw<InvalidOperationException>();

        coll.Insert(0, p2);
        coll[0].Should().BeSameAs(p2);
        coll[1].Should().BeSameAs(p1);

        var targetArray = new DbParameter[2];
        coll.CopyTo(targetArray, 0);
        targetArray[0].Should().BeSameAs(p2);
        targetArray[1].Should().BeSameAs(p1);

        coll.GetEnumerator().Should().NotBeNull();

        coll.RemoveAt("@nonexistent"); // Should not throw
        coll.Count.Should().Be(2);

        coll[0] = p3;
        coll[0].Should().BeSameAs(p3);

        coll["@p3"] = p1;
        coll[0].Should().BeSameAs(p1);

        coll["@brandNew"] = p2;
        coll.Count.Should().Be(3);
        coll.Contains(p2).Should().BeTrue();

        coll.Clear();
        coll.Count.Should().Be(0);
    }

    [Fact]
    public void TenantContextBuilder_WithActive_And_WithSource_Variations()
    {
        var context = new TenantContextBuilder()
            .WithActive(false)
            .WithConnectionString(null)
            .WithSource(TenantResolutionSource.Route)
            .BuildContext();

        context.Tenant!.IsActive.Should().BeFalse();
        context.Tenant.ConnectionString.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.Route);

        var activeContext = new TenantContextBuilder()
            .WithActive(true)
            .WithConnectionString("Server=127.0.0.1;")
            .BuildContext();

        activeContext.Tenant!.IsActive.Should().BeTrue();
        activeContext.Tenant.ConnectionString.Should().Be("Server=127.0.0.1;");
    }

    [Fact]
    public async Task FakeDbConnection_Comprehensive_Tests()
    {
        var conn = new FakeDbConnection();
        conn.ConnectionString.Should().Be("Fake");
        conn.ConnectionString = null;
        conn.ConnectionString.Should().BeEmpty();
        conn.ConnectionString = "Server=localhost;Database=Test;";
        conn.ConnectionString.Should().Be("Server=localhost;Database=Test;");

        conn.Database.Should().Be("FakeDb");
        conn.DataSource.Should().Be("FakeServer");
        conn.ServerVersion.Should().Be("1.0");
        conn.State.Should().Be(ConnectionState.Closed);

        conn.ClientId.Should().BeEmpty();
        conn.ClientId = "VpdClient_123";
        conn.ClientId.Should().Be("VpdClient_123");

        conn.ChangeDatabase("NewDb");

        conn.Open();
        conn.State.Should().Be(ConnectionState.Open);
        conn.Close();
        conn.State.Should().Be(ConnectionState.Closed);

        await conn.OpenAsync(CancellationToken.None);
        conn.State.Should().Be(ConnectionState.Open);

        conn.Commands.Should().BeEmpty();
        conn.Transactions.Should().BeEmpty();

        var tx = (FakeDbTransaction)conn.BeginTransaction(IsolationLevel.ReadCommitted);
        conn.Transactions.Should().Contain(tx);
        tx.IsolationLevel.Should().Be(IsolationLevel.ReadCommitted);
        tx.Connection.Should().BeSameAs(conn);

        var txAsync = (FakeDbTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        conn.Transactions.Should().Contain(txAsync);
        txAsync.IsolationLevel.Should().Be(IsolationLevel.Serializable);

        var cmd = (FakeDbCommand)conn.CreateCommand();
        conn.Commands.Should().Contain(cmd);
        cmd.Connection.Should().BeSameAs(conn);

        var customReader = new FakeDbDataReader(new[] { "col" }, new List<object?[]> { new object?[] { 99 } });
        conn.DataReaderFactory = c => customReader;
        var cmdWithFactory = (FakeDbCommand)conn.CreateCommand();
        cmdWithFactory.DataReaderFactory.Should().NotBeNull();
        var r = cmdWithFactory.ExecuteReader();
        r.Should().BeSameAs(customReader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actOpen = () => conn.OpenAsync(cts.Token);
        await actOpen.Should().ThrowAsync<OperationCanceledException>();

        var actTx = () => conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cts.Token).AsTask();
        await actTx.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FakeDbCommand_Comprehensive_Tests()
    {
        var cmd = new FakeDbCommand();
        cmd.CommandText.Should().BeEmpty();
        cmd.CommandText = null;
        cmd.CommandText.Should().BeEmpty();
        cmd.CommandText = "SELECT * FROM Tenants";
        cmd.CommandText.Should().Be("SELECT * FROM Tenants");

        cmd.CommandTimeout = 45;
        cmd.CommandTimeout.Should().Be(45);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandType.Should().Be(CommandType.StoredProcedure);

        cmd.DesignTimeVisible = true;
        cmd.DesignTimeVisible.Should().BeTrue();
        cmd.DesignTimeVisible = false;
        cmd.DesignTimeVisible.Should().BeFalse();

        cmd.UpdatedRowSource = UpdateRowSource.FirstReturnedRecord;
        cmd.UpdatedRowSource.Should().Be(UpdateRowSource.FirstReturnedRecord);
        cmd.UpdatedRowSource = UpdateRowSource.None;
        cmd.UpdatedRowSource.Should().Be(UpdateRowSource.None);

        var conn = new FakeDbConnection();
        cmd.Connection = conn;
        cmd.Connection.Should().BeSameAs(conn);

        var tx = new FakeDbTransaction(conn, IsolationLevel.Snapshot);
        cmd.Transaction = tx;
        cmd.Transaction.Should().BeSameAs(tx);

        cmd.Cancel();
        cmd.Prepare();

        cmd.ExecuteNonQuery().Should().Be(1);
        var nonQuery = await cmd.ExecuteNonQueryAsync(CancellationToken.None);
        nonQuery.Should().Be(1);

        cmd.ExecuteScalar().Should().BeNull();
        var scalar = await cmd.ExecuteScalarAsync(CancellationToken.None);
        scalar.Should().BeNull();

        var p = cmd.CreateParameter();
        p.Should().BeOfType<FakeDbParameter>();

        var defaultReader = cmd.ExecuteReader();
        defaultReader.Should().NotBeNull();
        defaultReader.FieldCount.Should().Be(0);

        var defaultAsyncReader = await cmd.ExecuteReaderAsync(CancellationToken.None);
        defaultAsyncReader.Should().NotBeNull();
        defaultAsyncReader.FieldCount.Should().Be(0);

        var customReader = new FakeDbDataReader(new[] { "A" }, new List<object?[]> { new object?[] { 1 } });
        cmd.DataReaderFactory = c => customReader;
        cmd.DataReaderFactory.Should().NotBeNull();
        cmd.ExecuteReader().Should().BeSameAs(customReader);
        var customAsync = await cmd.ExecuteReaderAsync(CancellationToken.None);
        customAsync.Should().BeSameAs(customReader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actNonQuery = () => cmd.ExecuteNonQueryAsync(cts.Token);
        await actNonQuery.Should().ThrowAsync<OperationCanceledException>();

        var actScalar = () => cmd.ExecuteScalarAsync(cts.Token);
        await actScalar.Should().ThrowAsync<OperationCanceledException>();

        var actReader = () => cmd.ExecuteReaderAsync(cts.Token);
        await actReader.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void FakeDbParameter_Comprehensive_Tests()
    {
        var p = new FakeDbParameter();
        p.ParameterName.Should().BeEmpty();
        p.ParameterName = null;
        p.ParameterName.Should().BeEmpty();
        p.ParameterName = "@TenantId";
        p.ParameterName.Should().Be("@TenantId");

        p.SourceColumn.Should().BeEmpty();
        p.SourceColumn = null;
        p.SourceColumn.Should().BeEmpty();
        p.SourceColumn = "TenantIdentifier";
        p.SourceColumn.Should().Be("TenantIdentifier");

        p.Direction.Should().Be(ParameterDirection.Input);
        p.Direction = ParameterDirection.InputOutput;
        p.Direction.Should().Be(ParameterDirection.InputOutput);
        p.Direction = ParameterDirection.Output;
        p.Direction.Should().Be(ParameterDirection.Output);
        p.Direction = ParameterDirection.ReturnValue;
        p.Direction.Should().Be(ParameterDirection.ReturnValue);

        p.DbType = DbType.Guid;
        p.DbType.Should().Be(DbType.Guid);

        p.IsNullable = true;
        p.IsNullable.Should().BeTrue();
        p.IsNullable = false;
        p.IsNullable.Should().BeFalse();

        p.Value = "TestValue";
        p.Value.Should().Be("TestValue");
        p.Value = null;
        p.Value.Should().BeNull();

        p.SourceColumnNullMapping = true;
        p.SourceColumnNullMapping.Should().BeTrue();
        p.SourceColumnNullMapping = false;
        p.SourceColumnNullMapping.Should().BeFalse();

        p.Size = 128;
        p.Size.Should().Be(128);

        p.ResetDbType();
    }

    [Fact]
    public async Task FakeDbTransaction_Comprehensive_Tests()
    {
        var conn = new FakeDbConnection();
        var tx = new FakeDbTransaction(conn, IsolationLevel.ReadUncommitted);

        tx.Connection.Should().BeSameAs(conn);
        tx.IsolationLevel.Should().Be(IsolationLevel.ReadUncommitted);
        tx.IsCommitted.Should().BeFalse();
        tx.IsRolledBack.Should().BeFalse();
        tx.IsDisposed.Should().BeFalse();

        tx.Commit();
        tx.IsCommitted.Should().BeTrue();

        tx.Rollback();
        tx.IsRolledBack.Should().BeTrue();

        var tx2 = new FakeDbTransaction(conn, IsolationLevel.RepeatableRead);
        await tx2.CommitAsync(CancellationToken.None);
        tx2.IsCommitted.Should().BeTrue();

        await tx2.RollbackAsync(CancellationToken.None);
        tx2.IsRolledBack.Should().BeTrue();

        await tx2.DisposeAsync();
        tx2.IsDisposed.Should().BeTrue();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var tx3 = new FakeDbTransaction(conn, IsolationLevel.Serializable);
        var actCommit = () => tx3.CommitAsync(cts.Token);
        await actCommit.Should().ThrowAsync<OperationCanceledException>();

        var actRollback = () => tx3.RollbackAsync(cts.Token);
        await actRollback.Should().ThrowAsync<OperationCanceledException>();
    }
}

