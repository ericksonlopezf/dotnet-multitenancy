// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Represents an immutable, strongly-typed tenant identifier backed by a <see cref="Guid"/>.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.TenantIdJsonConverter))]
public readonly record struct TenantId : IEquatable<TenantId>, IComparable<TenantId>, IComparable
{
    /// <summary>
    /// Gets the underlying <see cref="Guid"/> value of this tenant identifier.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Gets a value indicating whether this identifier is empty.
    /// </summary>
    public bool IsEmpty => Value == Guid.Empty;

    /// <summary>
    /// Represents an empty, unassigned tenant identifier.
    /// </summary>
    public static readonly TenantId Empty = new(Guid.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantId"/> struct with the specified GUID value.
    /// </summary>
    /// <param name="value">The underlying GUID value.</param>
    public TenantId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new <see cref="TenantId"/> from the specified GUID value.
    /// </summary>
    /// <param name="value">The GUID value to wrap.</param>
    /// <returns>A new <see cref="TenantId"/> instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see cref="Guid.Empty"/></exception>
    public static TenantId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier cannot be an empty GUID.", nameof(value));
        }

        return new TenantId(value);
    }

    /// <summary>
    /// Creates a new <see cref="TenantId"/> by parsing a GUID string representation.
    /// </summary>
    /// <remarks>
    /// Accepts standard GUID formats: D, N, B, P, X.
    /// </remarks>
    /// <param name="value">The string representation of the GUID to parse.</param>
    /// <returns>A new <see cref="TenantId"/> instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty, consists only of white-space characters, or is not a valid GUID</exception>
    public static TenantId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Tenant identifier cannot be null or whitespace.", nameof(value));
        }

        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            throw new ArgumentException($"'{value}' is not a valid tenant identifier. Expected a non-empty GUID.", nameof(value));
        }

        return new TenantId(guid);
    }

    /// <summary>
    /// Generates a new unique <see cref="TenantId"/> backed by a new random <see cref="Guid"/>.
    /// </summary>
    /// <returns>A new unique <see cref="TenantId"/> instance.</returns>
    public static TenantId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a <see cref="TenantId"/> from a GUID value wrapped in a <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="value">The GUID value.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the <see cref="TenantId"/> if valid; otherwise, a failure result.</returns>
    public static Result<TenantId> From(Guid value)
    {
        if (value == Guid.Empty)
        {
            return TenantErrors.InvalidId("00000000-0000-0000-0000-000000000000");
        }

        return new TenantId(value);
    }

    /// <summary>
    /// Creates a <see cref="TenantId"/> from a string representation wrapped in a <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="value">The string representation of the GUID to parse.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the <see cref="TenantId"/> if valid; otherwise, a failure result.</returns>
    public static Result<TenantId> From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            return TenantErrors.InvalidId(value);
        }

        return new TenantId(guid);
    }

    /// <summary>
    /// Attempts to create a <see cref="TenantId"/> from the specified string representation.
    /// </summary>
    /// <param name="value">The string representation of the GUID to parse.</param>
    /// <param name="tenantId">
    /// When this method returns, contains the parsed <see cref="TenantId"/> if the parse operation succeeded;
    /// otherwise, <see cref="Empty"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryCreate(string? value, out TenantId tenantId)
    {
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            tenantId = Empty;
            return false;
        }

        tenantId = new TenantId(guid);
        return true;
    }

    /// <summary>
    /// Attempts to create a <see cref="TenantId"/> from the specified <see cref="Guid"/> value.
    /// </summary>
    /// <param name="value">The GUID value to validate.</param>
    /// <param name="tenantId">
    /// When this method returns, contains the created <see cref="TenantId"/> if <paramref name="value"/> is non-empty;
    /// otherwise, <see cref="Empty"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the GUID is not <see cref="Guid.Empty"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryCreate(Guid value, out TenantId tenantId)
    {
        if (value == Guid.Empty)
        {
            tenantId = Empty;
            return false;
        }

        tenantId = new TenantId(value);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(TenantId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is TenantId other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(TenantId)}", nameof(obj));
    }

    /// <summary>
    /// Determines whether a specified <see cref="TenantId"/> is less than another specified <see cref="TenantId"/>.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator <(TenantId left, TenantId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether a specified <see cref="TenantId"/> is less than or equal to another specified <see cref="TenantId"/>.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than or equal to <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator <=(TenantId left, TenantId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether a specified <see cref="TenantId"/> is greater than another specified <see cref="TenantId"/>.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator >(TenantId left, TenantId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether a specified <see cref="TenantId"/> is greater than or equal to another specified <see cref="TenantId"/>.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator >=(TenantId left, TenantId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Implicitly converts a <see cref="TenantId"/> to its underlying <see cref="Guid"/> value.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to convert.</param>
    /// <returns>The underlying <see cref="Guid"/> value.</returns>
    public static implicit operator Guid(TenantId tenantId) => tenantId.Value;

    /// <summary>
    /// Explicitly converts a <see cref="Guid"/> to a <see cref="TenantId"/>.
    /// </summary>
    /// <param name="value">The GUID value to convert.</param>
    /// <returns>A new <see cref="TenantId"/> initialized with the specified GUID.</returns>
    public static explicit operator TenantId(Guid value) => new(value);
}
