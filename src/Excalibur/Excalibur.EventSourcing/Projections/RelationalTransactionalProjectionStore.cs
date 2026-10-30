// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Shared tenant and state transitions for dedicated SQLite and Oracle transactional destinations.</summary>
internal sealed class RelationalTransactionalProjectionStore<T>(
    Func<DbConnection> createConnection,
    Func<DbConnection, DbTransaction?, string, IReadOnlyList<(string Name, object? Value)>, DbCommand> createCommand,
    Action<ProjectionTransaction> validateTransaction,
    string table, ITenantContext tenantContext, JsonSerializerOptions jsonOptions, bool oracle)
    : IProjectionStore<T>, ITransactionalProjectionStore<T> where T : class
{
    private static readonly string[] WriteParameters = ["Tenant", "Id", "Data", "State", "Source", "Type"];
    private string Tenant => TenantScope.Scoped(tenantContext.TenantId).TenantId;
    private string Binary(string column) => oracle ? column + " COLLATE BINARY" : column;
    private string Parameter(string name) => (oracle ? ":" : "@") + name;
    // Microsoft.Data.Sqlite binds decimal as TEXT; JSON extraction returns INTEGER/REAL.
    // Match SQLite's existing JSON numeric semantics without coercing string-valued filters.
    private string FilterParameter(string name, object? value) => !oracle && value is decimal
        ? $"CAST({Parameter(name)} AS NUMERIC)" : Parameter(name);

    [RequiresUnreferencedCode("Projection serialization may use reflection. Use generated metadata instead.")]
    [RequiresDynamicCode("Projection serialization may require dynamic code. Use generated metadata instead.")]
    public string Serialize(T projection) => JsonSerializer.Serialize(projection, jsonOptions);

    public string Serialize(T projection, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Serialize(projection, ProjectionSerializationDefaults.CreateReadModelTypeInfo(jsonOptions, typeInfo));

    public async ValueTask WriteAsync(ProjectionTransaction transaction, string aggregateId, string aggregateType,
        string json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentNullException.ThrowIfNull(json);
        validateTransaction(transaction);
        if (!string.Equals(Tenant, transaction.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("TransactionalInline source and destination tenant partitions differ.");
        }
        // Never open or dispose a connection on this path: the event store owns this exact transaction.
        var owned = (DbTransaction)transaction.Transaction;
        await WriteCoreAsync(owned.Connection!, owned, aggregateId, json, ProjectionPosition.UnnumberedSentinel,
            transaction.SourceIdentity, aggregateType, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = createCommand(connection, null,
            $"SELECT Data FROM {table} WHERE {Binary("TenantId")} = {Parameter("Tenant")} AND {Binary("Id")} = {Parameter("Id")} AND LastAppliedPosition <> -3",
            [("Tenant", Tenant), ("Id", id)]);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is string json ? JsonSerializer.Deserialize<T>(json, jsonOptions) : null;
    }

    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task UpsertAsync(string id, T projection, CancellationToken cancellationToken) =>
        IndependentWriteAsync(id, Serialize(projection), ProjectionPosition.UnplaceableSentinel, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
        IndependentWriteAsync(id, "{}", ProjectionPosition.RetiredSentinel, cancellationToken);

    private async Task IndependentWriteAsync(string id, string json, long state, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await using var connection = createConnection();
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await WriteCoreAsync(connection, transaction, id, json, state, null, null, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }

    private async Task WriteCoreAsync(DbConnection connection, DbTransaction transaction, string id, string json,
        long state, string? source, string? aggregateType, CancellationToken token)
    {
        var guard = state switch
        {
            ProjectionPosition.RetiredSentinel => "1 = 1",
            ProjectionPosition.UnnumberedSentinel => $"target.LastAppliedPosition = -1 AND {Binary("target.TransactionalSource")} = {Parameter("Source")} AND {Binary("target.TransactionalAggregateType")} = {Parameter("Type")}",
            _ => "target.LastAppliedPosition <> -3",
        };
        var assignments = $"Data = {Parameter("Data")}, LastAppliedPosition = {Parameter("State")}, TransactionalSource = {Parameter("Source")}, TransactionalAggregateType = {Parameter("Type")}";
        var columns = "TenantId, Id, Data, LastAppliedPosition, TransactionalSource, TransactionalAggregateType";
        var values = string.Join(", ", WriteParameters.Select(Parameter));
        var sql = oracle
            ? $"MERGE INTO {table} target USING (SELECT {Parameter("Tenant")} TenantId, {Parameter("Id")} Id FROM DUAL) incoming "
                + $"ON ({Binary("target.TenantId")} = incoming.TenantId AND {Binary("target.Id")} = incoming.Id) "
                + $"WHEN MATCHED THEN UPDATE SET {assignments} WHERE {guard} "
                + $"WHEN NOT MATCHED THEN INSERT ({columns}) VALUES ({values})"
            : $"INSERT INTO {table} AS target ({columns}) VALUES ({values}) ON CONFLICT (TenantId, Id) DO UPDATE SET {assignments} WHERE {guard}";
        await using var command = createCommand(connection, transaction, sql,
            [("Tenant", Tenant), ("Id", id), ("Data", json), ("State", state), ("Source", source), ("Type", aggregateType)]);
        if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException("Projection destination is retired or belongs to another source, aggregate type or write mode.");
        }
    }

    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public async Task<IReadOnlyList<T>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
    {
        var (where, parameters) = Where(filters);
        var sql = $"SELECT Data FROM {table} WHERE {where}";
        sql += options?.OrderBy is { } order
            ? $" ORDER BY {Property(order, IsNumericOrder(order))} {(options.Descending ? "DESC" : "ASC")}, Id"
            : " ORDER BY Id";
        var skip = options?.Skip ?? 0;
        var take = options?.Take;
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (take is { } count) { ArgumentOutOfRangeException.ThrowIfNegative(count); }
        if (oracle)
        {
            sql += $" OFFSET {skip.ToString(CultureInfo.InvariantCulture)} ROWS";
            if (take is { } limit) { sql += $" FETCH NEXT {limit.ToString(CultureInfo.InvariantCulture)} ROWS ONLY"; }
        }
        else { sql += $" LIMIT {(take ?? -1).ToString(CultureInfo.InvariantCulture)} OFFSET {skip.ToString(CultureInfo.InvariantCulture)}"; }
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = createCommand(connection, null, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), jsonOptions)
                ?? throw new InvalidOperationException("Projection payload deserialized to null."));
        }
        return rows;
    }

    public async Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
    {
        var (where, parameters) = Where(filters);
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = createCommand(connection, null, $"SELECT COUNT(*) FROM {table} WHERE {where}", parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private (string Sql, List<(string Name, object? Value)> Parameters) Where(IDictionary<string, object>? filters)
    {
        var clauses = new List<string> { $"{Binary("TenantId")} = {Parameter("Tenant")}", "LastAppliedPosition <> -3" };
        var parameters = new List<(string Name, object? Value)> { ("Tenant", Tenant) };
        if (filters is null) { return (string.Join(" AND ", clauses), parameters); }
        foreach (var (key, value) in filters)
        {
            var parts = key.Split(':');
            if (parts.Length > 2) { throw new ArgumentException("Invalid filter operator.", nameof(filters)); }
            var operation = parts.Length == 1 ? "eq" : parts[1];
            var name = "Filter" + parameters.Count.ToString(CultureInfo.InvariantCulture);
            var numeric = value is byte or short or int or long or float or double or decimal;
            var property = Property(parts[0], numeric);
            if (operation == "in")
            {
                if (value is not System.Collections.IEnumerable sequence || value is string)
                { throw new ArgumentException("The in filter requires a collection.", nameof(filters)); }
                var names = new List<string>();
                foreach (var item in sequence)
                {
                    var itemName = "Filter" + parameters.Count.ToString(CultureInfo.InvariantCulture);
                    names.Add(FilterParameter(itemName, item));
                    parameters.Add((itemName, item));
                }
                clauses.Add(names.Count == 0 ? "1 = 0" : property + " IN (" + string.Join(", ", names) + ")");
                continue;
            }
            var comparison = operation switch
            {
                "eq" => "=", "neq" => "<>", "gt" => ">", "gte" => ">=", "lt" => "<", "lte" => "<=",
                "contains" => "LIKE", _ => throw new ArgumentException("Unsupported projection filter operator.", nameof(filters)),
            };
            if (value is null && operation is "eq" or "neq")
            { clauses.Add(property + (operation == "eq" ? " IS NULL" : " IS NOT NULL")); continue; }
            var bound = value;
            if (operation == "contains")
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                bound = "%" + text.Replace("!", "!!", StringComparison.Ordinal).Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";
            }
            parameters.Add((name, bound));
            clauses.Add(property + " " + comparison + " " + FilterParameter(name, bound) + (operation == "contains" ? " ESCAPE '!'" : string.Empty));
        }
        return (string.Join(" AND ", clauses), parameters);
    }

    [RequiresUnreferencedCode("Ordering resolves the projection's serialized property metadata.")]
    [RequiresDynamicCode("Ordering resolves the projection's serialized property metadata.")]
    private bool IsNumericOrder(string property)
    {
        // SQLite extracts native JSON scalars; only Oracle needs an explicit numeric return type.
        if (!oracle) { return false; }
        var name = jsonOptions.PropertyNamingPolicy?.ConvertName(property) ?? property;
        var metadataOptions = new JsonSerializerOptions(jsonOptions);
        metadataOptions.MakeReadOnly(populateMissingResolver: true);
        var type = metadataOptions.GetTypeInfo(typeof(T)).Properties.FirstOrDefault(candidate => candidate.Name == name)?.PropertyType;
        if (type is null) { return false; }
        type = Nullable.GetUnderlyingType(type) ?? type;
        // Enums may be serialized as names. Preserve their existing scalar ordering semantics.
        if (type.IsEnum) { return false; }
        return Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }

    private string Property(string property, bool numeric)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        var name = jsonOptions.PropertyNamingPolicy?.ConvertName(property) ?? property;
        if (name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        { throw new ArgumentException("Projection query properties must be simple JSON property names.", nameof(property)); }
        return oracle ? $"JSON_VALUE(Data, '$.{name}'{(numeric ? " RETURNING NUMBER" : string.Empty)})" : $"json_extract(Data, '$.{name}')";
    }
}
