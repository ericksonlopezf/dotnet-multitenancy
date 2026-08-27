// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Opt = Microsoft.Extensions.Options.Options;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public sealed class CustomPgTenantInfo : ITenantInfo
{
    public TenantId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}

public sealed class ReadOnlyCustomPgTenantInfo : ITenantInfo
{
    private static readonly Guid StaticGuid = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    public TenantId Id => new(StaticGuid);
    public string Name => "ReadOnly";
    public string? ConnectionString => null;
    public bool IsActive => true;
    public IReadOnlyDictionary<string, string> Properties => new Dictionary<string, string>();
}

public sealed class ExplicitCustomPgTenantInfo : ITenantInfo
{
    TenantId ITenantInfo.Id => new(Guid.Empty);
    string ITenantInfo.Name => "Explicit";
    string? ITenantInfo.ConnectionString => null;
    bool ITenantInfo.IsActive => true;
    IReadOnlyDictionary<string, string> ITenantInfo.Properties => new Dictionary<string, string>();
}

public class PostgreSqlTenantStoreTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly TenantId ExpectedTenantId = new TenantId(ExpectedGuid);
    private static readonly string[] DefaultColumns = new[] { "id", "name", "connection_string", "is_active", "properties" };
    private static readonly string[] CustomColumns = new[] { "tenant_pk", "tenant_alias", "db_uri", "enabled", "metadata" };

    // ==========================================
    // Constructor & Guard Tests
    // ==========================================

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        IOptions<PostgreSqlTenantStoreOptions> options = null!;
        var act = () => new PostgreSqlTenantStore<TenantInfo>(options);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_NullOptionsValue_ThrowsArgumentException()
    {
        var options = Opt.Create<PostgreSqlTenantStoreOptions>(null!);
        var act = () => new PostgreSqlTenantStore<TenantInfo>(options);
        act.Should().Throw<ArgumentException>().WithMessage("Options value cannot be null.*");
    }

    [Fact]
    public void Constructor_EmptyConnectionString_ThrowsArgumentException()
    {
        var act1 = () => new PostgreSqlTenantStore<TenantInfo>("");
        act1.Should().Throw<ArgumentException>().WithParameterName("connectionString")
            .WithMessage("Connection string cannot be null or whitespace.*");

        var act2 = () => new PostgreSqlTenantStore<TenantInfo>("   ");
        act2.Should().Throw<ArgumentException>().WithParameterName("connectionString")
            .WithMessage("Connection string cannot be null or whitespace.*");

        var act3 = () => new PostgreSqlTenantStore<TenantInfo>((string)null!);
        act3.Should().Throw<ArgumentException>().WithParameterName("connectionString")
            .WithMessage("Connection string cannot be null or whitespace.*");
    }

    [Fact]
    public void Constructor_NullDataSource_ThrowsArgumentNullException()
    {
        NpgsqlDataSource dataSource = null!;
        var act = () => new PostgreSqlTenantStore<TenantInfo>(dataSource);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dataSource");
    }

    [Fact]
    public void Constructor_NullConnectionFactory_ThrowsArgumentNullException()
    {
        Func<DbConnection> factory = null!;
        var act = () => new PostgreSqlTenantStore<TenantInfo>(factory);
        act.Should().Throw<ArgumentNullException>().WithParameterName("connectionFactory");
    }

    [Fact]
    public void Constructor_WithValidDataSource_InstantiatesSuccessfully()
    {
        using var ds = NpgsqlDataSource.Create("Host=localhost;Database=test;");
        var storeGen = new PostgreSqlTenantStore<TenantInfo>(ds);
        storeGen.Should().NotBeNull();

        var storeNonGen = new PostgreSqlTenantStore(ds);
        storeNonGen.Should().NotBeNull();
    }

    // ==========================================
    // GetTenantAsync Tests
    // ==========================================

    [Fact]
    public async Task GetTenantAsync_EmptyTenantId_ReturnsFailure()
    {
        var conn = new FakeDbConnection();
        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(TenantId.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
        result.Error.Description.Should().Contain("Empty");
    }

    [Fact]
    public async Task GetTenantAsync_Found_ReturnsTenantInfoWithProperties()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "AcmeCorp", "Host=db.acme.com", true, "{\"Tier\":\"Enterprise\",\"Region\":\"us-east\"}" }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ExpectedTenantId);
        result.Value.Name.Should().Be("AcmeCorp");
        result.Value.ConnectionString.Should().Be("Host=db.acme.com");
        result.Value.IsActive.Should().BeTrue();
        result.Value.Properties.Should().ContainKey("Tier").WhoseValue.Should().Be("Enterprise");
        result.Value.Properties.Should().ContainKey("Region").WhoseValue.Should().Be("us-east");

        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Be("SELECT \"id\", \"name\", \"connection_string\", \"is_active\", \"properties\" FROM \"public\".\"tenants\" WHERE \"id\" = @Id LIMIT 1;");
        cmd.Parameters["Id"].Value.Should().Be(ExpectedGuid);
    }

    [Fact]
    public async Task GetTenantAsync_NullAndDefaultDbValues_MapsDefaults()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ExpectedTenantId);
        result.Value.Name.Should().Be(string.Empty);
        result.Value.ConnectionString.Should().BeNull();
        result.Value.IsActive.Should().BeFalse();
        result.Value.Properties.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantAsync_NotFound_ReturnsNotFoundResult()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(DefaultColumns, new List<object?[]>())
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task GetTenantAsync_DatabaseException_ReturnsFailure()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => throw new InvalidOperationException("PostgreSQL connection failure")
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
        result.Error.Description.Should().Contain("PostgreSQL connection failure");
    }

    [Fact]
    public async Task GetTenantAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => throw new OperationCanceledException(cts.Token)
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var act = () => store.GetTenantAsync(ExpectedTenantId, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetTenantAsync_OperationCanceledExceptionWithoutCancellationRequested_ReturnsDatabaseError()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => throw new OperationCanceledException()
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
    }

    [Fact]
    public async Task GetTenantAsync_CustomMapper_UsesCustomMapper()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "CustomMapped", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, customMapper: reader =>
        {
            return new TenantInfo(new TenantId(reader.GetGuid(0)), "OVERRIDDEN_" + reader.GetString(1));
        });

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("OVERRIDDEN_CustomMapped");
    }

    // ==========================================
    // Generic Custom TTenant Mapping Tests
    // ==========================================

    [Fact]
    public async Task GetTenantAsync_GenericCustomTenant_MapsAllProperties()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "GenericTenant", "Host=db.generic.com", true, "{\"Plan\":\"Gold\"}" }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<CustomPgTenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ExpectedTenantId);
        result.Value.Name.Should().Be("GenericTenant");
        result.Value.ConnectionString.Should().Be("Host=db.generic.com");
        result.Value.IsActive.Should().BeTrue();
        result.Value.Properties.Should().ContainKey("Plan").WhoseValue.Should().Be("Gold");
    }

    [Fact]
    public async Task GetTenantAsync_ReadOnlyProperties_DoesNotThrowAndReturnsInstance()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "ReadOnlyTenant", "Host=db.generic.com", true, "{\"Plan\":\"Gold\"}" }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<ReadOnlyCustomPgTenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("ReadOnly");
    }

    [Fact]
    public async Task GetTenantAsync_ExplicitProperties_DoesNotThrowAndReturnsInstance()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "ExplicitTenant", "Host=db.generic.com", true, "{\"Plan\":\"Gold\"}" }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<ExplicitCustomPgTenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        ((ITenantInfo)result.Value).Name.Should().Be("Explicit");
    }

    [Fact]
    public async Task GetTenantAsync_InvalidJsonProperties_DefaultsPropertiesToNull()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "CorruptJsonTenant", null, true, "{not valid json}" }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Properties.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantAsync_FewerColumnsThanProperties_HandlesGracefully()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var cols = new[] { "id", "name", "connection_string", "is_active" }; // 4 columns
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "FourColTenant", null, true }
                };
                return new FakeDbDataReader(cols, rows);
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("FourColTenant");
        result.Value.Properties.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantAsync_EmptyPropertiesString_ReturnsEmptyProperties()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                var rows = new List<object?[]>
                {
                    new object?[] { ExpectedGuid, "EmptyPropsTenant", null, true, "   " }
                };
                return new FakeDbDataReader(DefaultColumns, rows);
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Properties.Should().BeEmpty();
    }

    // ==========================================
    // GetTenantByIdentifierAsync Tests
    // ==========================================

    [Fact]
    public async Task GetTenantByIdentifierAsync_EmptyIdentifier_ReturnsFailure()
    {
        var conn = new FakeDbConnection();
        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result1 = await store.GetTenantByIdentifierAsync("");
        result1.IsFailure.Should().BeTrue();
        result1.Error.Description.Should().Contain("''");

        var result2 = await store.GetTenantByIdentifierAsync("   ");
        result2.IsFailure.Should().BeTrue();
        result2.Error.Description.Should().Contain("'   '");

        var result3 = await store.GetTenantByIdentifierAsync(null!);
        result3.IsFailure.Should().BeTrue();
        result3.Error.Description.Should().Contain("null");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_FoundByName_ReturnsTenant()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "tenant-slug", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantByIdentifierAsync("tenant-slug");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ExpectedTenantId);
        result.Value.Name.Should().Be("tenant-slug");

        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Be("SELECT \"id\", \"name\", \"connection_string\", \"is_active\", \"properties\" FROM \"public\".\"tenants\" WHERE LOWER(\"name\") = LOWER(@Identifier) LIMIT 1;");
        cmd.Parameters["Identifier"].Value.Should().Be("tenant-slug");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_FoundByParsedGuid_ReturnsTenant()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "tenant-guid", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantByIdentifierAsync(ExpectedGuid.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ExpectedTenantId);

        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Be("SELECT \"id\", \"name\", \"connection_string\", \"is_active\", \"properties\" FROM \"public\".\"tenants\" WHERE LOWER(\"name\") = LOWER(@Identifier) OR \"id\" = @ParsedId LIMIT 1;");
        cmd.Parameters["Identifier"].Value.Should().Be(ExpectedGuid.ToString());
        cmd.Parameters["ParsedId"].Value.Should().Be(ExpectedGuid);
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NotFound_ReturnsNotFoundResult()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(DefaultColumns, new List<object?[]>())
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantByIdentifierAsync("missing-slug");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
        result.Error.Description.Should().Contain("missing-slug");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_DatabaseException_ReturnsFailure()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => throw new InvalidOperationException("PG Query error")
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantByIdentifierAsync("any-slug");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
        result.Error.Description.Should().Contain("PG Query error");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => throw new OperationCanceledException(cts.Token)
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var act = () => store.GetTenantByIdentifierAsync("slug", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_OperationCanceledExceptionWithoutCancellationRequested_ReturnsDatabaseError()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => throw new OperationCanceledException()
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantByIdentifierAsync("slug");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
    }

    // ==========================================
    // Explicit Interface Implementation Tests
    // ==========================================

    [Fact]
    public async Task ExplicitInterface_ITenantStore_GetTenantAsync_SuccessAndFailure()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                if (cmd.Parameters["Id"].Value?.ToString() == ExpectedGuid.ToString())
                {
                    return new FakeDbDataReader(
                        DefaultColumns,
                        new List<object?[]> { new object?[] { ExpectedGuid, "TenantA", null, true, null } });
                }
                return new FakeDbDataReader(DefaultColumns, new List<object?[]>());
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);
        ITenantStore untypedStore = store;

        var successResult = await untypedStore.GetTenantAsync(ExpectedTenantId);
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Id.Should().Be(ExpectedTenantId);

        var missingId = TenantId.NewId();
        var failureResult = await untypedStore.GetTenantAsync(missingId);
        failureResult.IsFailure.Should().BeTrue();
        failureResult.Error.Code.Should().Be(TenantErrors.NotFound(missingId).Code);
    }

    [Fact]
    public async Task ExplicitInterface_ITenantLookupStore_GetTenantByIdentifierAsync_SuccessAndFailure()
    {
        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd =>
            {
                if (cmd.Parameters["Identifier"].Value?.ToString() == "TenantA")
                {
                    return new FakeDbDataReader(
                        DefaultColumns,
                        new List<object?[]> { new object?[] { ExpectedGuid, "TenantA", null, true, null } });
                }
                return new FakeDbDataReader(DefaultColumns, new List<object?[]>());
            }
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);
        ITenantLookupStore lookupStore = store;

        var successResult = await lookupStore.GetTenantByIdentifierAsync("TenantA");
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Id.Should().Be(ExpectedTenantId);

        var failureResult = await lookupStore.GetTenantByIdentifierAsync("MissingTenant");
        failureResult.IsFailure.Should().BeTrue();
        failureResult.Error.Code.Should().Be("Tenant.NotFound");
    }

    // ==========================================
    // Schema, Table & SQL Escaping Tests
    // ==========================================

    [Fact]
    public async Task CustomSchemaAndTable_GeneratesExpectedQuery()
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            Schema = "tenant_catalog",
            TableName = "app_tenants",
            IdColumn = "tenant_pk",
            NameColumn = "tenant_alias",
            ConnectionStringColumn = "db_uri",
            IsActiveColumn = "enabled",
            PropertiesColumn = "metadata"
        };

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                CustomColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "CustomAlias", "Host=db", true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Be("SELECT \"tenant_pk\", \"tenant_alias\", \"db_uri\", \"enabled\", \"metadata\" FROM \"tenant_catalog\".\"app_tenants\" WHERE \"tenant_pk\" = @Id LIMIT 1;");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NullOrEmptySchema_GeneratesQueryWithoutSchemaPrefix(string? schema)
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            Schema = schema!,
            TableName = "tenants_table"
        };

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "NoSchemaTenant", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Be("SELECT \"id\", \"name\", \"connection_string\", \"is_active\", \"properties\" FROM \"tenants_table\" WHERE \"id\" = @Id LIMIT 1;");

        var identResult = await store.GetTenantByIdentifierAsync("NoSchemaTenant");
        identResult.IsSuccess.Should().BeTrue();
        conn.Commands.Last().CommandText.Should().Be("SELECT \"id\", \"name\", \"connection_string\", \"is_active\", \"properties\" FROM \"tenants_table\" WHERE LOWER(\"name\") = LOWER(@Identifier) LIMIT 1;");
    }

    [Fact]
    public async Task EscapeIdentifier_EscapesQuotesCorrectly()
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            Schema = "my\"schema",
            TableName = "my\"table"
        };

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "QuotedSchemaTenant", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Be("SELECT \"id\", \"name\", \"connection_string\", \"is_active\", \"properties\" FROM \"my\"\"schema\".\"my\"\"table\" WHERE \"id\" = @Id LIMIT 1;");
    }

    [Fact]
    public async Task GetTenantAsync_InvalidTableName_ReturnsDatabaseError()
    {
        var options = new PostgreSqlTenantStoreOptions { TableName = "" };
        var conn = new FakeDbConnection();
        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
        result.Error.Description.Should().Contain("SQL identifier cannot be null or whitespace.");
    }

    [Fact]
    public async Task CustomSelectByIdQuery_ExecutesCustomQuery()
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            CustomSelectByIdQuery = "SELECT id, name, connection_string, is_active, properties FROM custom_tenants_view WHERE id = @Id;"
        };

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "CustomViewTenant", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        conn.Commands.Single().CommandText.Should().Be("SELECT id, name, connection_string, is_active, properties FROM custom_tenants_view WHERE id = @Id;");
    }

    [Fact]
    public async Task CustomSelectByIdentifierQuery_ExecutesCustomQuery()
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            CustomSelectByIdentifierQuery = "SELECT id, name, connection_string, is_active, properties FROM custom_tenants_view WHERE name = @Identifier;"
        };

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "CustomViewTenant", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantByIdentifierAsync("CustomViewTenant");

        result.IsSuccess.Should().BeTrue();
        conn.Commands.Single().CommandText.Should().Be("SELECT id, name, connection_string, is_active, properties FROM custom_tenants_view WHERE name = @Identifier;");
    }

    // ==========================================
    // Connection Creation Tests
    // ==========================================

    [Fact]
    public async Task CreateConnectionAsync_OpenConnection_DoesNotCallOpenAgain()
    {
        var conn = new FakeDbConnection();
        conn.Open(); // Already open

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue(); // not found
        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public async Task CreateConnectionAsync_ClosedConnection_OpensConnection()
    {
        var conn = new FakeDbConnection(); // Closed

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue(); // not found
        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public async Task NoConnectionConfigured_ThrowsInvalidOperationException()
    {
        var store = new PostgreSqlTenantStore<TenantInfo>(Opt.Create(new PostgreSqlTenantStoreOptions()));

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
        result.Error.Description.Should().Contain("No PostgreSQL connection string");
    }

    [Fact]
    public async Task GetTenantAsync_WithConnectionString_AttemptsConnectionAndHandlesError()
    {
        var store = new PostgreSqlTenantStore<TenantInfo>("Host=127.0.0.1;Port=54321;Database=test;Timeout=1;");
        var result = await store.GetTenantAsync(ExpectedTenantId);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
        result.Error.Description.Should().NotContain("No PostgreSQL connection string");
    }

    [Fact]
    public async Task GetTenantAsync_WithDataSource_AttemptsConnectionAndHandlesError()
    {
        using var ds = NpgsqlDataSource.Create("Host=127.0.0.1;Port=54321;Database=test;Timeout=1;");
        var store = new PostgreSqlTenantStore<TenantInfo>(ds);
        var result = await store.GetTenantAsync(ExpectedTenantId);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
    }

    // ==========================================
    // Companion Non-Generic Class Tests
    // ==========================================

    [Fact]
    public void NonGenericCompanionClass_InstantiatesWithDifferentConstructors()
    {
        var store1 = new PostgreSqlTenantStore("Host=localhost;Database=test;");
        store1.Should().NotBeNull();

        var store2 = new PostgreSqlTenantStore(Opt.Create(new PostgreSqlTenantStoreOptions { ConnectionString = "Host=localhost" }));
        store2.Should().NotBeNull();

        var store3 = new PostgreSqlTenantStore(() => new FakeDbConnection());
        store3.Should().NotBeNull();

        var store4 = new PostgreSqlTenantStore(() => new FakeDbConnection(), new PostgreSqlTenantStoreOptions(), reader => new TenantInfo(ExpectedTenantId, "Mapped"));
        store4.Should().NotBeNull();
    }

    // ==========================================
    // Dependency Injection Extension Tests
    // ==========================================

    [Fact]
    public void AddPostgreSqlTenantStore_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlTenantStore(options =>
        {
            options.ConnectionString = "Host=db.example.com";
        });

        using var sp = services.BuildServiceProvider();
        var store = sp.GetService<ITenantStore<TenantInfo>>();
        store.Should().NotBeNull();
        store.Should().BeOfType<PostgreSqlTenantStore<TenantInfo>>();

        var lookupStore = sp.GetService<ITenantLookupStore<TenantInfo>>();
        lookupStore.Should().NotBeNull();

        var untypedStore = sp.GetService<ITenantStore>();
        untypedStore.Should().NotBeNull();

        var untypedLookup = sp.GetService<ITenantLookupStore>();
        untypedLookup.Should().NotBeNull();
    }

    [Fact]
    public void AddPostgreSqlTenantStore_WithConnectionString_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlTenantStore("Host=db.example.com;Database=multitenant;");

        using var sp = services.BuildServiceProvider();
        var store = sp.GetService<ITenantStore<TenantInfo>>();
        store.Should().NotBeNull();

        var options = sp.GetRequiredService<IOptions<PostgreSqlTenantStoreOptions>>().Value;
        options.ConnectionString.Should().Be("Host=db.example.com;Database=multitenant;");
    }

    [Fact]
    public void AddPostgreSqlTenantStore_Generic_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlTenantStore<CustomPgTenantInfo>(options =>
        {
            options.ConnectionString = "Host=db.example.com;Database=custom;";
        }, reader => new CustomPgTenantInfo { Id = ExpectedTenantId, Name = "Custom" });

        using var sp = services.BuildServiceProvider();
        var store = sp.GetService<ITenantStore<CustomPgTenantInfo>>();
        store.Should().NotBeNull();
        store.Should().BeOfType<PostgreSqlTenantStore<CustomPgTenantInfo>>();

        var lookupStore = sp.GetService<ITenantLookupStore<CustomPgTenantInfo>>();
        lookupStore.Should().NotBeNull();

        var untypedStore = sp.GetService<ITenantStore>();
        untypedStore.Should().NotBeNull();

        var untypedLookup = sp.GetService<ITenantLookupStore>();
        untypedLookup.Should().NotBeNull();
    }

    [Fact]
    public void AddPostgreSqlTenantStore_GenericWithConnectionString_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlTenantStore<CustomPgTenantInfo>("Host=db.example.com;Database=custom;");

        using var sp = services.BuildServiceProvider();
        var store = sp.GetService<ITenantStore<CustomPgTenantInfo>>();
        store.Should().NotBeNull();

        var options = sp.GetRequiredService<IOptions<PostgreSqlTenantStoreOptions>>().Value;
        options.ConnectionString.Should().Be("Host=db.example.com;Database=custom;");
    }

    [Fact]
    public void AddPostgreSqlTenantStore_NullArgs_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act1 = () => services.AddPostgreSqlTenantStore((Action<PostgreSqlTenantStoreOptions>)null!);
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => services.AddPostgreSqlTenantStore((string)null!);
        act2.Should().Throw<ArgumentException>().WithMessage("Connection string cannot be null or whitespace.*");

        var act3 = () => services.AddPostgreSqlTenantStore("   ");
        act3.Should().Throw<ArgumentException>().WithMessage("Connection string cannot be null or whitespace.*");

        var act4 = () => services.AddPostgreSqlTenantStore<CustomPgTenantInfo>((Action<PostgreSqlTenantStoreOptions>)null!);
        act4.Should().Throw<ArgumentNullException>();

        var act5 = () => services.AddPostgreSqlTenantStore<CustomPgTenantInfo>((string)null!);
        act5.Should().Throw<ArgumentException>().WithMessage("Connection string cannot be null or whitespace.*");

        var act6 = () => services.AddPostgreSqlTenantStore<CustomPgTenantInfo>("   ");
        act6.Should().Throw<ArgumentException>().WithMessage("Connection string cannot be null or whitespace.*");

        IServiceCollection nullServices = null!;
        var act7 = () => nullServices.AddPostgreSqlTenantStore(opt => { });
        act7.Should().Throw<ArgumentNullException>();

        var act8 = () => nullServices.AddPostgreSqlTenantStore("Host=db;");
        act8.Should().Throw<ArgumentNullException>();

        var act9 = () => nullServices.AddPostgreSqlTenantStore<CustomPgTenantInfo>(opt => { });
        act9.Should().Throw<ArgumentNullException>();

        var act10 = () => nullServices.AddPostgreSqlTenantStore<CustomPgTenantInfo>("Host=db;");
        act10.Should().Throw<ArgumentNullException>();
    }

    // ==========================================
    // EscapeIdentifier Boundary & Edge Case Tests
    // ==========================================

    [Theory]
    [InlineData("simple", "\"simple\"")]
    [InlineData("table_with_underscores", "\"table_with_underscores\"")]
    [InlineData("table-with-dashes", "\"table-with-dashes\"")]
    [InlineData("my\"table", "\"my\"\"table\"")]
    [InlineData("my\"\"double\"\"quote", "\"my\"\"\"\"double\"\"\"\"quote\"")]
    [InlineData("schema.name", "\"schema.name\"")]
    [InlineData("User Table", "\"User Table\"")]
    [InlineData("Select", "\"Select\"")]
    [InlineData("table;DROP TABLE students;--", "\"table;DROP TABLE students;--\"")]
    public async Task EscapeIdentifier_BoundaryAndEdgeCases_ProducesSafeEscaping(string input, string expectedEscaped)
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            TableName = input,
            Schema = string.Empty
        };

        var conn = new FakeDbConnection
        {
            DataReaderFactory = cmd => new FakeDbDataReader(
                DefaultColumns,
                new List<object?[]> { new object?[] { ExpectedGuid, "EscapedTenant", null, true, null } })
        };

        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);
        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsSuccess.Should().BeTrue();
        var cmd = conn.Commands.Single();
        cmd.CommandText.Should().Contain($"FROM {expectedEscaped} WHERE");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t\n")]
    [InlineData(null)]
    public async Task EscapeIdentifier_NullOrWhitespace_ReturnsDatabaseError(string? invalidIdentifier)
    {
        var options = new PostgreSqlTenantStoreOptions
        {
            TableName = invalidIdentifier!
        };

        var conn = new FakeDbConnection();
        var store = new PostgreSqlTenantStore<TenantInfo>(() => conn, options);

        var result = await store.GetTenantAsync(ExpectedTenantId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PostgreSqlStore.DatabaseError");
        result.Error.Description.Should().Contain("SQL identifier cannot be null or whitespace.");
    }
}
