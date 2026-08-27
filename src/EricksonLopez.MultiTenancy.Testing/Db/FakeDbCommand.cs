// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides an in-memory fake implementation of <see cref="DbCommand"/>.
/// </summary>
public class FakeDbCommand : DbCommand
{
    private string _commandText = string.Empty;

    /// <inheritdoc />
    [AllowNull]
    public override string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }

    /// <inheritdoc />
    public override int CommandTimeout { get; set; }

    /// <inheritdoc />
    public override CommandType CommandType { get; set; }

    /// <inheritdoc />
    public override bool DesignTimeVisible { get; set; }

    /// <inheritdoc />
    public override UpdateRowSource UpdatedRowSource { get; set; }

    /// <inheritdoc />
    protected override DbConnection? DbConnection { get; set; }

    /// <inheritdoc />
    protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();

    /// <inheritdoc />
    protected override DbTransaction? DbTransaction { get; set; }

    /// <summary>
    /// Gets or sets an optional factory to produce custom <see cref="DbDataReader"/> instances.
    /// </summary>
    public Func<FakeDbCommand, DbDataReader>? DataReaderFactory { get; set; }

    /// <inheritdoc />
    public override void Cancel() { }

    /// <inheritdoc />
    public override int ExecuteNonQuery() => 1;

    /// <inheritdoc />
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(1);
    }

    /// <inheritdoc />
    public override object? ExecuteScalar() => null;

    /// <inheritdoc />
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc />
    public override void Prepare() { }

    /// <inheritdoc />
    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    /// <inheritdoc />
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        if (DataReaderFactory is not null)
        {
            return DataReaderFactory(this);
        }
        return new FakeDbDataReader(new List<string>(), new List<object?[]>());
    }

    /// <inheritdoc />
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ExecuteDbDataReader(behavior));
    }
}
