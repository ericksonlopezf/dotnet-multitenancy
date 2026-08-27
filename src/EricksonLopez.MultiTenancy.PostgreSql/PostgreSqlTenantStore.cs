// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Provides a PostgreSQL-backed store for resolving tenant metadata from a database table.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public class PostgreSqlTenantStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicConstructors)] TTenant>
    : ITenantLookupStore<TTenant>
    where TTenant : class, ITenantInfo
{
    private readonly PostgreSqlTenantStoreOptions _options;
    private readonly NpgsqlDataSource? _dataSource;
    private readonly Func<DbConnection>? _connectionFactory;
    private readonly Func<DbDataReader, TTenant>? _customMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore{TTenant}"/> class with configuration options.
    /// </summary>
    /// <param name="options">The store configuration options.</param>
    /// <param name="dataSource">The optional PostgreSQL data source.</param>
    /// <param name="customMapper">The optional custom mapping delegate from <see cref="DbDataReader"/> to <typeparamref name="TTenant"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="options"/>.Value is <see langword="null"/></exception>
    public PostgreSqlTenantStore(
        IOptions<PostgreSqlTenantStoreOptions> options,
        NpgsqlDataSource? dataSource = null,
        Func<DbDataReader, TTenant>? customMapper = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentException("Options value cannot be null.", nameof(options));
        _dataSource = dataSource;
        _customMapper = customMapper;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore{TTenant}"/> class with a connection string.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    public PostgreSqlTenantStore(
        string connectionString,
        Func<DbDataReader, TTenant>? customMapper = null)
        : this(Microsoft.Extensions.Options.Options.Create(new PostgreSqlTenantStoreOptions { ConnectionString = connectionString }), null, customMapper)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore{TTenant}"/> class with an <see cref="NpgsqlDataSource"/>.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dataSource"/> is <see langword="null"/></exception>
    public PostgreSqlTenantStore(
        NpgsqlDataSource dataSource,
        Func<DbDataReader, TTenant>? customMapper = null)
        : this(Microsoft.Extensions.Options.Options.Create(new PostgreSqlTenantStoreOptions()), dataSource ?? throw new ArgumentNullException(nameof(dataSource)), customMapper)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore{TTenant}"/> class with a custom connection factory.
    /// </summary>
    /// <param name="connectionFactory">The delegate returning an open or openable <see cref="DbConnection"/>.</param>
    /// <param name="options">The optional custom store options.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connectionFactory"/> is <see langword="null"/></exception>
    public PostgreSqlTenantStore(
        Func<DbConnection> connectionFactory,
        PostgreSqlTenantStoreOptions? options = null,
        Func<DbDataReader, TTenant>? customMapper = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options ?? new PostgreSqlTenantStoreOptions();
        _customMapper = customMapper;
    }

    /// <inheritdoc />
    public async Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId.IsEmpty)
        {
            return Result<TTenant>.Failure(TenantErrors.InvalidId("Empty"));
        }

        try
        {
            // Stryker disable once boolean
            await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();

            var query = _options.CustomSelectByIdQuery ?? BuildSelectByIdQuery();
            command.CommandText = query;

            var param = command.CreateParameter();
            param.ParameterName = "Id";
            param.Value = tenantId.Value;
            param.DbType = DbType.Guid;
            command.Parameters.Add(param);

            // Stryker disable once boolean
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
            // Stryker disable once boolean
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var tenant = MapTenant(reader);
                return Result<TTenant>.Success(tenant);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<TTenant>.Failure(Error.Failure(
                "PostgreSqlStore.DatabaseError",
                $"Failed to query tenant from PostgreSQL: {ex.Message}"));
        }

        return Result<TTenant>.Failure(TenantErrors.NotFound(tenantId));
    }

    /// <inheritdoc />
    public async Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<TTenant>.Failure(TenantErrors.InvalidId(identifier ?? "null"));
        }

        try
        {
            // Stryker disable once boolean
            await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();

            bool isGuid = TenantId.TryCreate(identifier, out var parsedId);
            var query = _options.CustomSelectByIdentifierQuery ?? BuildSelectByIdentifierQuery(isGuid);
            command.CommandText = query;

            var paramIdentifier = command.CreateParameter();
            paramIdentifier.ParameterName = "Identifier";
            paramIdentifier.Value = identifier;
            paramIdentifier.DbType = DbType.String;
            command.Parameters.Add(paramIdentifier);

            if (isGuid)
            {
                var paramParsedId = command.CreateParameter();
                paramParsedId.ParameterName = "ParsedId";
                paramParsedId.Value = parsedId.Value;
                paramParsedId.DbType = DbType.Guid;
                command.Parameters.Add(paramParsedId);
            }

            // Stryker disable once boolean
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
            // Stryker disable once boolean
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var tenant = MapTenant(reader);
                return Result<TTenant>.Success(tenant);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<TTenant>.Failure(Error.Failure(
                "PostgreSqlStore.DatabaseError",
                $"Failed to query tenant identifier from PostgreSQL: {ex.Message}"));
        }

        return Result<TTenant>.Failure(Error.NotFound(
            "Tenant.NotFound",
            $"Tenant with identifier '{identifier}' was not found in PostgreSQL store."));
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean, statement : ConfigureAwait optimization
        var result = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantLookupStore.GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean, statement : ConfigureAwait optimization
        var result = await GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }

    [ExcludeFromCodeCoverage(Justification = "Requires live PostgreSQL database to open physical connection")]
    private async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connectionFactory is not null)
        {
            var conn = _connectionFactory();
            if (conn.State != ConnectionState.Open)
            {
                // Stryker disable once boolean
                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            return conn;
        }

        // Stryker disable once block, equality, boolean : Requires live PostgreSQL database to open connection
        if (_dataSource is not null)
        {
            // Stryker disable once boolean : Requires live PostgreSQL database to open connection
            return await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            var conn = new NpgsqlConnection(_options.ConnectionString);
            // Stryker disable once statement, boolean : Requires live PostgreSQL database to open connection
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            return conn;
        }

        throw new InvalidOperationException(
            "No PostgreSQL connection string, NpgsqlDataSource, or connection factory was configured for PostgreSqlTenantStore.");
    }

    private string BuildSelectByIdQuery()
    {
        var table = EscapeIdentifier(_options.TableName);
        var schema = string.IsNullOrWhiteSpace(_options.Schema) ? null : EscapeIdentifier(_options.Schema);
        var fullTable = schema is null ? table : $"{schema}.{table}";

        var idCol = EscapeIdentifier(_options.IdColumn);
        var nameCol = EscapeIdentifier(_options.NameColumn);
        var connCol = EscapeIdentifier(_options.ConnectionStringColumn);
        var activeCol = EscapeIdentifier(_options.IsActiveColumn);
        var propsCol = EscapeIdentifier(_options.PropertiesColumn);

        return $"SELECT {idCol}, {nameCol}, {connCol}, {activeCol}, {propsCol} FROM {fullTable} WHERE {idCol} = @Id LIMIT 1;";
    }

    private string BuildSelectByIdentifierQuery(bool includeParsedId)
    {
        var table = EscapeIdentifier(_options.TableName);
        var schema = string.IsNullOrWhiteSpace(_options.Schema) ? null : EscapeIdentifier(_options.Schema);
        var fullTable = schema is null ? table : $"{schema}.{table}";

        var idCol = EscapeIdentifier(_options.IdColumn);
        var nameCol = EscapeIdentifier(_options.NameColumn);
        var connCol = EscapeIdentifier(_options.ConnectionStringColumn);
        var activeCol = EscapeIdentifier(_options.IsActiveColumn);
        var propsCol = EscapeIdentifier(_options.PropertiesColumn);

        var whereClause = includeParsedId
            ? $"WHERE LOWER({nameCol}) = LOWER(@Identifier) OR {idCol} = @ParsedId"
            : $"WHERE LOWER({nameCol}) = LOWER(@Identifier)";

        return $"SELECT {idCol}, {nameCol}, {connCol}, {activeCol}, {propsCol} FROM {fullTable} {whereClause} LIMIT 1;";
    }

    private static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("SQL identifier cannot be null or whitespace.", nameof(identifier));
        }

        var cleaned = identifier.Replace("\"", "\"\"");
        return $"\"{cleaned}\"";
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:MembersAnnotated", Justification = "TTenant members are preserved by DynamicallyAccessedMembers attribute.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:MembersAnnotated", Justification = "TTenant members are preserved by DynamicallyAccessedMembers attribute.")]
    private TTenant MapTenant(DbDataReader reader)
    {
        if (_customMapper is not null)
        {
            return _customMapper(reader);
        }

        var idGuid = reader.GetGuid(0);
        var tenantId = new TenantId(idGuid);
        var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var connectionString = reader.IsDBNull(2) ? null : reader.GetString(2);
        var isActive = !reader.IsDBNull(3) && reader.GetBoolean(3);
        var properties = ReadProperties(reader, 4);

        if (typeof(TTenant) == typeof(TenantInfo))
        {
            var info = new TenantInfo(tenantId, name, connectionString, isActive)
            {
                Properties = properties ?? new Dictionary<string, string>()
            };
            return (TTenant)(object)info;
        }

        // Generic instantiation for custom types
        var tenantInstance = Activator.CreateInstance<TTenant>();

        var idProp = typeof(TTenant).GetProperty(nameof(ITenantInfo.Id));
        if (idProp is not null && idProp.CanWrite)
        {
            idProp.SetValue(tenantInstance, tenantId);
        }

        var nameProp = typeof(TTenant).GetProperty(nameof(ITenantInfo.Name));
        if (nameProp is not null && nameProp.CanWrite)
        {
            nameProp.SetValue(tenantInstance, name);
        }

        var connProp = typeof(TTenant).GetProperty(nameof(ITenantInfo.ConnectionString));
        if (connProp is not null && connProp.CanWrite)
        {
            connProp.SetValue(tenantInstance, connectionString);
        }

        var activeProp = typeof(TTenant).GetProperty(nameof(ITenantInfo.IsActive));
        if (activeProp is not null && activeProp.CanWrite)
        {
            activeProp.SetValue(tenantInstance, isActive);
        }

        var propsProp = typeof(TTenant).GetProperty(nameof(ITenantInfo.Properties));
        if (propsProp is not null && propsProp.CanWrite && properties is not null)
        {
            propsProp.SetValue(tenantInstance, properties);
        }

        return tenantInstance;
    }

    private static Dictionary<string, string>? ReadProperties(DbDataReader reader, int ordinal)
    {
        if (reader.FieldCount <= ordinal || reader.IsDBNull(ordinal))
        {
            return null;
        }

        try
        {
            var raw = reader.GetString(ordinal);
            // Stryker disable once block : Guard clause defensively duplicated by JsonSerializer.Deserialize error handling
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonSerializer.Deserialize(raw, PostgreSqlTenantStoreJsonContext.Default.DictionaryStringString);
        }
        // Stryker disable once block : Catch block body return null is semantically equivalent to falling through to default return
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Provides a PostgreSQL-backed store for resolving tenant metadata, using <see cref="TenantInfo"/> as the default metadata type.
/// </summary>
public sealed class PostgreSqlTenantStore : PostgreSqlTenantStore<TenantInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore"/> class with configuration options.
    /// </summary>
    /// <param name="options">The store configuration options.</param>
    /// <param name="dataSource">The optional PostgreSQL data source.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    public PostgreSqlTenantStore(
        IOptions<PostgreSqlTenantStoreOptions> options,
        NpgsqlDataSource? dataSource = null,
        Func<DbDataReader, TenantInfo>? customMapper = null)
        : base(options, dataSource, customMapper)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore"/> class with a connection string.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    public PostgreSqlTenantStore(
        string connectionString,
        Func<DbDataReader, TenantInfo>? customMapper = null)
        : base(connectionString, customMapper)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore"/> class with an <see cref="NpgsqlDataSource"/>.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    public PostgreSqlTenantStore(
        NpgsqlDataSource dataSource,
        Func<DbDataReader, TenantInfo>? customMapper = null)
        : base(dataSource, customMapper)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlTenantStore"/> class with a custom connection factory.
    /// </summary>
    /// <param name="connectionFactory">The delegate returning an open or openable <see cref="DbConnection"/>.</param>
    /// <param name="options">The optional custom store options.</param>
    /// <param name="customMapper">The optional custom mapping delegate.</param>
    public PostgreSqlTenantStore(
        Func<DbConnection> connectionFactory,
        PostgreSqlTenantStoreOptions? options = null,
        Func<DbDataReader, TenantInfo>? customMapper = null)
        : base(connectionFactory, options, customMapper)
    {
    }
}
