// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides an in-memory fake implementation of <see cref="DbDataReader"/>.
/// </summary>
[SuppressMessage("Design", "CA1010:Generic interface should also be implemented", Justification = "DbDataReader ADO.NET abstraction hierarchy.")]
public class FakeDbDataReader : DbDataReader
{
    private readonly List<string> _columnNames;
    private readonly List<object?[]> _rows;
    private int _currentRowIndex = -1;
    private bool _isClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDbDataReader"/> class.
    /// </summary>
    /// <param name="columnNames">The column names for the reader schema.</param>
    /// <param name="rows">The data rows to expose through the reader.</param>
    /// <exception cref="ArgumentNullException"><paramref name="columnNames"/> or <paramref name="rows"/> is <see langword="null"/></exception>
    public FakeDbDataReader(IEnumerable<string> columnNames, IEnumerable<object?[]> rows)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(rows);
        _columnNames = columnNames.ToList();
        _rows = rows.ToList();
    }

    /// <inheritdoc />
    public override int FieldCount => _columnNames.Count;

    /// <inheritdoc />
    public override bool HasRows => _rows.Count > 0;

    /// <inheritdoc />
    public override bool IsClosed => _isClosed;

    /// <inheritdoc />
    public override int RecordsAffected => _rows.Count;

    /// <inheritdoc />
    public override int Depth => 0;

    /// <inheritdoc />
    public override object this[int ordinal] => GetValue(ordinal);

    /// <inheritdoc />
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <inheritdoc />
    public override bool Read()
    {
        if (_isClosed) return false;
        _currentRowIndex++;
        return _currentRowIndex < _rows.Count;
    }

    /// <inheritdoc />
    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    /// <inheritdoc />
    public override bool NextResult() => false;

    /// <inheritdoc />
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public override void Close() => _isClosed = true;

    /// <inheritdoc />
    public override bool IsDBNull(int ordinal)
    {
        var val = _rows[_currentRowIndex][ordinal];
        return val is null or DBNull;
    }

    /// <inheritdoc />
    public override object GetValue(int ordinal) => _rows[_currentRowIndex][ordinal] ?? DBNull.Value;

    /// <inheritdoc />
    public override int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }

    /// <inheritdoc />
    public override string GetName(int ordinal) => _columnNames[ordinal];

    /// <inheritdoc />
    public override int GetOrdinal(string name) => _columnNames.FindIndex(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    /// <inheritdoc />
    public override Type GetFieldType(int ordinal) => _rows[_currentRowIndex][ordinal]?.GetType() ?? typeof(object);

    /// <inheritdoc />
    public override bool GetBoolean(int ordinal) => (bool)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override byte GetByte(int ordinal) => (byte)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;

    /// <inheritdoc />
    public override char GetChar(int ordinal) => (char)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;

    /// <inheritdoc />
    public override Guid GetGuid(int ordinal) => (Guid)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override short GetInt16(int ordinal) => (short)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override int GetInt32(int ordinal) => (int)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override long GetInt64(int ordinal) => (long)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override float GetFloat(int ordinal) => (float)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override double GetDouble(int ordinal) => (double)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override string GetString(int ordinal) => (string)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override decimal GetDecimal(int ordinal) => (decimal)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override DateTime GetDateTime(int ordinal) => (DateTime)_rows[_currentRowIndex][ordinal]!;

    /// <inheritdoc />
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);
}
