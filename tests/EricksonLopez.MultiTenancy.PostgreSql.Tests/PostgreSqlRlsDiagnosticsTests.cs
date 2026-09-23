// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Testing;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class PostgreSqlRlsDiagnosticsTests
{
    [Fact]
    public async Task ValidateRlsPoliciesAsync_NullConnection_ThrowsArgumentNullException()
    {
        var act = () => PostgreSqlRlsDiagnostics.ValidateRlsPoliciesAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ValidateRlsPoliciesAsync_InvalidSchemaName_ThrowsArgumentException(string schemaName)
    {
        var connection = new FakeDbConnection();
        var act = () => PostgreSqlRlsDiagnostics.ValidateRlsPoliciesAsync(connection, schemaName);
        var ex = await act.Should().ThrowAsync<ArgumentException>().WithParameterName(nameof(schemaName));
        ex.Which.Message.Should().StartWith("Schema name must not be null or whitespace.");
    }

    [Fact]
    public async Task ValidateRlsPoliciesAsync_AllTablesHaveForceRls_SucceedsWithoutException()
    {
        var connection = new FakeDbConnection
        {
            DataReaderFactory = _ => new FakeDbDataReader(
                new[] { "TableName", "HasRlsEnabled", "HasForceRlsEnabled" },
                new[] { new object?[] { "invoices", true, true } })
        };

        var act = () => PostgreSqlRlsDiagnostics.ValidateRlsPoliciesAsync(connection);
        await act.Should().NotThrowAsync();
        connection.State.Should().Be(System.Data.ConnectionState.Open);
        connection.Commands.Should().ContainSingle();
        connection.Commands[0].Parameters.OfType<System.Data.IDataParameter>()
            .Should().Contain(p => p.ParameterName == "@SchemaParam" && "public".Equals(p.Value));
    }

    [Fact]
    public async Task ValidateRlsPoliciesAsync_AlreadyOpenConnection_DoesNotReopen()
    {
        var connection = new FakeDbConnection
        {
            DataReaderFactory = _ => new FakeDbDataReader(
                new[] { "TableName", "HasRlsEnabled", "HasForceRlsEnabled" },
                new[] { new object?[] { "invoices", true, true } })
        };
        connection.Open();

        var act = () => PostgreSqlRlsDiagnostics.ValidateRlsPoliciesAsync(connection);
        await act.Should().NotThrowAsync();
        connection.State.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task ValidateRlsPoliciesAsync_TableLacksForceRls_ThrowsInvalidOperationException()
    {
        var connection = new FakeDbConnection
        {
            DataReaderFactory = _ => new FakeDbDataReader(
                new[] { "TableName", "HasRlsEnabled", "HasForceRlsEnabled" },
                new[] { new object?[] { "invoices", true, false } })
        };

        var act = () => PostgreSqlRlsDiagnostics.ValidateRlsPoliciesAsync(connection);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("Table 'invoices' has Row Level Security enabled but lacks FORCE ROW LEVEL SECURITY. Execute 'ALTER TABLE invoices FORCE ROW LEVEL SECURITY;' to prevent owner bypass.");
    }
}
