// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides an in-memory fake implementation of <see cref="DbParameterCollection"/>.
/// </summary>
[SuppressMessage("Design", "CA1010:Generic interface should also be implemented", Justification = "DbParameterCollection ADO.NET abstraction hierarchy.")]
public class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = new();

    /// <inheritdoc />
    public override int Count => _parameters.Count;

    /// <inheritdoc />
    public override object SyncRoot => ((ICollection)_parameters).SyncRoot;

    /// <inheritdoc />
    public override int Add(object value)
    {
        _parameters.Add((DbParameter)value);
        return _parameters.Count - 1;
    }

    /// <inheritdoc />
    public override void AddRange(Array values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var v in values)
        {
            Add(v);
        }
    }

    /// <inheritdoc />
    public override void Clear() => _parameters.Clear();

    /// <inheritdoc />
    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);

    /// <inheritdoc />
    public override bool Contains(string value) => _parameters.Any(p => string.Equals(p.ParameterName, value, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);

    /// <inheritdoc />
    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    /// <inheritdoc />
    protected override DbParameter GetParameter(int index) => _parameters[index];

    /// <inheritdoc />
    protected override DbParameter GetParameter(string parameterName) =>
        _parameters.First(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

    /// <inheritdoc />
    public override int IndexOf(string parameterName) =>
        _parameters.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

    /// <inheritdoc />
    public override void Remove(object value) => _parameters.Remove((DbParameter)value);

    /// <inheritdoc />
    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    /// <inheritdoc />
    public override void RemoveAt(string parameterName)
    {
        var idx = IndexOf(parameterName);
        if (idx >= 0) _parameters.RemoveAt(idx);
    }

    /// <inheritdoc />
    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

    /// <inheritdoc />
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var idx = IndexOf(parameterName);
        if (idx >= 0) _parameters[idx] = value;
        else _parameters.Add(value);
    }
}
