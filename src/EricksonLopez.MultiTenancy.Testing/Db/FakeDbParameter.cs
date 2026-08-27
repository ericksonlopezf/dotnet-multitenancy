// Copyright © Erickson Lopez. MIT License.
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides an in-memory fake implementation of <see cref="DbParameter"/>.
/// </summary>
public class FakeDbParameter : DbParameter
{
    private string _parameterName = string.Empty;
    private string _sourceColumn = string.Empty;

    /// <inheritdoc />
    public override DbType DbType { get; set; }

    /// <inheritdoc />
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    /// <inheritdoc />
    public override bool IsNullable { get; set; }

    /// <inheritdoc />
    [AllowNull]
    public override string ParameterName
    {
        get => _parameterName;
        set => _parameterName = value ?? string.Empty;
    }

    /// <inheritdoc />
    [AllowNull]
    public override string SourceColumn
    {
        get => _sourceColumn;
        set => _sourceColumn = value ?? string.Empty;
    }

    /// <inheritdoc />
    public override object? Value { get; set; }

    /// <inheritdoc />
    public override bool SourceColumnNullMapping { get; set; }

    /// <inheritdoc />
    public override int Size { get; set; }

    /// <inheritdoc />
    public override void ResetDbType() { }
}
