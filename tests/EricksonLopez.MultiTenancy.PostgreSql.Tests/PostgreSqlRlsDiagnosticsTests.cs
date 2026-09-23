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
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName(nameof(schemaName));
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
        ex.Which.Message.Should().Contain("FORCE ROW LEVEL SECURITY");
        ex.Which.Message.Should().Contain("invoices");
    }
}
