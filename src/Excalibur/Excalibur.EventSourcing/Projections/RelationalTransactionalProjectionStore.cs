// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Shared tenant and state transitions for dedicated SQLite and Oracle transactional destinations.</summary>
internal sealed partial class RelationalTransactionalProjectionStore<T>(
    Func<DbConnection> createConnection,
    Func<DbConnection, DbTransaction?, string, IReadOnlyList<(string Name, object? Value)>, DbCommand> createCommand,
    Action<ProjectionTransaction> validateTransaction,
    string table, ITenantContext tenantContext, JsonSerializerOptions jsonOptions,
    RelationalProjectionDialect dialect)
    : IProjectionStore<T>, ITransactionalProjectionStore<T>, IPositionedProjectionStore<T> where T : class
{
    /// <summary>Which existing row an upsert's UPDATE arm is allowed to overwrite.</summary>
    /// <remarks>
    /// The guard is chosen by the OPERATION, never derived from the position value being written. It used
    /// to be a switch over that value, which silently made one legal combination inexpressible: writing
    /// the unnumbered sentinel outside the transactional path selected the transactional source guard, so
    /// a positioned store could not record "a complete fold whose coordinate is unknown" at all.
    /// </remarks>
    private enum UpsertGuard
    {
        /// <summary>Any row, including a retired one. Only deletion may overwrite a deletion marker.</summary>
        Unconditional,

        /// <summary>A row owned by this same transactional source and aggregate type.</summary>
        TransactionalSource,

        /// <summary>Any live row. A retired key refuses the write.</summary>
        NotRetired,
    }

    private static readonly string[] WriteParameters = ["Tenant", "Id", "Data", "State", "Source", "Type"];
    private string Tenant => TenantScope.Scoped(tenantContext.TenantId).TenantId;
    // Thin forwarders. The engine's answers live in the dialect; these keep the call sites in both
    // partials reading as SQL construction rather than as dialect lookups.
    private string Binary(string column) => dialect.Collate(column);
    private string Parameter(string name) => dialect.Parameter(name);
    private string FilterParameter(string name, object? value) => dialect.FilterParameter(name, value);

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
            UpsertGuard.TransactionalSource, transaction.SourceIdentity, aggregateType, cancellationToken).ConfigureAwait(false);
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
        return value is string json ? Deserialize(json) : null;
    }

    /// <summary>Reads a stored read-model body through this store's canonical options.</summary>
    /// <remarks>
    /// One deserialization site for the whole store, including its positioned half. A second call site
    /// with its own options bag is how a read path drifts from the wire shape the write path produced,
    /// and the drift is silent -- the payload loads, with fields quietly missing.
    /// </remarks>
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    private T? Deserialize(string? json) => json is null ? null : JsonSerializer.Deserialize<T>(json, jsonOptions);

    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task UpsertAsync(string id, T projection, CancellationToken cancellationToken) =>
        IndependentWriteAsync(id, Serialize(projection), ProjectionPosition.UnplaceableSentinel,
            UpsertGuard.NotRetired, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
        IndependentWriteAsync(id, "{}", ProjectionPosition.RetiredSentinel,
            UpsertGuard.Unconditional, cancellationToken);

    private async Task IndependentWriteAsync(string id, string json, long state, UpsertGuard guard, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await using var connection = createConnection();
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await WriteCoreAsync(connection, transaction, id, json, state, guard, null, null, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }

    private async Task WriteCoreAsync(DbConnection connection, DbTransaction transaction, string id, string json,
        long state, UpsertGuard guardKind, string? source, string? aggregateType, CancellationToken token)
    {
        var guard = guardKind switch
        {
            UpsertGuard.Unconditional => "1 = 1",
            UpsertGuard.TransactionalSource => $"target.LastAppliedPosition = -1 AND {Binary("target.TransactionalSource")} = {Parameter("Source")} AND {Binary("target.TransactionalAggregateType")} = {Parameter("Type")}",
            _ => "target.LastAppliedPosition <> -3",
        };
        var assignments = $"Data = {Parameter("Data")}, LastAppliedPosition = {Parameter("State")}, TransactionalSource = {Parameter("Source")}, TransactionalAggregateType = {Parameter("Type")}";
        var columns = "TenantId, Id, Data, LastAppliedPosition, TransactionalSource, TransactionalAggregateType";
        var values = string.Join(", ", WriteParameters.Select(Parameter));
        IReadOnlyList<(string Name, object? Value)> parameters =
            [("Tenant", Tenant), ("Id", id), ("Data", json), ("State", state), ("Source", source), ("Type", aggregateType)];

        async Task<int> Write(string statement)
        {
            await using var command = createCommand(connection, transaction, statement, parameters);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        if (dialect.AtomicUpsert(table, columns, values, assignments, guard) is { } atomic)
        {
            if (await Write(atomic).ConfigureAwait(false) != 1)
            {
                throw new InvalidOperationException("Projection destination is retired or belongs to another source, aggregate type or write mode.");
            }

            return;
        }

        // THE ENGINE HAS NO ATOMIC MATCH-OR-INSERT, so the decision is sequenced here rather than
        // delegated to one statement, and the shape is the contract rather than a workaround. Its only
        // atomic insert-if-absent primitive is the unique index, which answers by raising, so the
        // index's answer is read as the conflict signal it is and translated into the outcome the guard
        // already specifies.
        var guarded = $"UPDATE {table} target SET {assignments} "
            + $"WHERE {Binary("target.TenantId")} = {Parameter("Tenant")} AND {Binary("target.Id")} = {Parameter("Id")} AND {guard}";

        if (await Write(guarded).ConfigureAwait(false) == 1)
        {
            return;
        }

        int inserted;
        DbException? conflict = null;
        try
        {
            inserted = await Write(dialect.InsertIfAbsent(table, columns, values)).ConfigureAwait(false);
        }
        catch (DbException error) when (dialect.IsDuplicateKey(error))
        {
            // A competing session committed this key between the UPDATE and the INSERT. The engine rolls
            // back the offending STATEMENT and leaves the transaction usable, so the guarded UPDATE is
            // re-issued below against the row that now exists. That is the same decision the atomic
            // providers reach in one statement, which is why this is not a retry of a failed guess.
            conflict = error;
            inserted = 0;
        }

        if (inserted == 1 || await Write(guarded).ConfigureAwait(false) == 1)
        {
            return;
        }

        // NOTHING WAS WRITTEN, and there are two reasons for that which must not be reported alike. A
        // guard refusing an EXISTING row is this operation's declared refusal. A duplicate-key violation
        // with NO row behind it is a different constraint reporting a different fault -- a unique index a
        // consumer added over their own business key, say -- and calling that "retired" would name the
        // wrong cause and discard the engine's own diagnosis.
        //
        // This is also why correctness here does not rest on the constraint's NAME. The shipped primary
        // key is now named, which makes an error message legible, but a name is not load-bearing: the
        // row either exists or it does not, and that question is answered by looking.
        if (conflict is not null
            && await ReadStoredPositionAsync(connection, transaction, id, token).ConfigureAwait(false) is null)
        {
            ExceptionDispatchInfo.Capture(conflict).Throw();
        }

        throw new InvalidOperationException("Projection destination is retired or belongs to another source, aggregate type or write mode.");
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
        sql += dialect.Page(skip, take);
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = createCommand(connection, null, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(Deserialize(reader.GetString(0))
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
        if (!dialect.TypedJsonNumbers) { return false; }
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
        return dialect.JsonValue(name, numeric);
    }
}
