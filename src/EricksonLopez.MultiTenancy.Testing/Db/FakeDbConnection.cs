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
/// Provides an in-memory fake implementation of <see cref="DbConnection"/> for testing database isolation and command execution.
/// </summary>
public class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    private string _connectionString = "Fake";

    /// <inheritdoc />
    [AllowNull]
    public override string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? string.Empty;
    }

    /// <inheritdoc />
    public override string Database => "FakeDb";

    /// <inheritdoc />
    public override string DataSource => "FakeServer";

    /// <inheritdoc />
    public override string ServerVersion => "1.0";

    /// <inheritdoc />
    public override ConnectionState State => _state;

    /// <summary>
    /// Gets the list of executed commands on this connection.
    /// </summary>
    public List<FakeDbCommand> Commands { get; } = new();

    /// <summary>
    /// Gets the list of transactions started on this connection.
    /// </summary>
    public List<FakeDbTransaction> Transactions { get; } = new();

    /// <summary>
    /// Gets or sets the client identifier (used by Oracle VPD testing).
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional factory to produce custom <see cref="DbDataReader"/> instances for commands.
    /// </summary>
    public Func<FakeDbCommand, DbDataReader>? DataReaderFactory { get; set; }

    /// <inheritdoc />
    public override void ChangeDatabase(string databaseName) { }

    /// <inheritdoc />
    public override void Close() => _state = ConnectionState.Closed;

    /// <inheritdoc />
    public override void Open() => _state = ConnectionState.Open;

    /// <inheritdoc />
    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var tx = new FakeDbTransaction(this, isolationLevel);
        Transactions.Add(tx);
        return tx;
    }

    /// <inheritdoc />
    protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tx = new FakeDbTransaction(this, isolationLevel);
        Transactions.Add(tx);
        return new(tx);
    }

    /// <inheritdoc />
    protected override DbCommand CreateDbCommand()
    {
        var cmd = new FakeDbCommand { Connection = this };
        if (DataReaderFactory is not null)
        {
            cmd.DataReaderFactory = DataReaderFactory;
        }
        Commands.Add(cmd);
        return cmd;
    }
}
