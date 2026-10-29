// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;

namespace Excalibur.EventSourcing.Postgres;

public sealed partial class PostgresProjectionStore<TProjection>
{
    private PostgresProjectionStore(PostgresProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var parts = destination.Split('/');
        if (parts.Length != 3 || parts[0] != "postgres" || !ValidIdentifierRegex().IsMatch(parts[1]) || !ValidIdentifierRegex().IsMatch(parts[2]))
        {
            throw new ArgumentException("The destination must be an exact postgres schema/table identity.", nameof(destination));
        }
        _dataSource = source._dataSource;
        _logger = source._logger;
        _tenantContext = new GenerationTenantContext(source._tenantContext);
        _jsonOptions = source._jsonOptions;
        _tableName = parts[2];
        _qualifiedTable = PgTableName.Format(parts[1], parts[2]);
        _destinationIdentity = destination;
    }

    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new PostgresProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader(store._tenantContext, (store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, store.GetByIdAsync, store.QueryAsync, store.CountAsync);
    }

    IProjectionGenerationReader<TProjection> IAotProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity, JsonTypeInfo<TProjection> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var effectiveTypeInfo = GenerationReadMetadata(typeInfo);
        var store = new PostgresProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader(store._tenantContext, (store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, 
            (id, token) => store.GetByIdCoreAsync(id, json => JsonSerializer.Deserialize(json, effectiveTypeInfo), token),
            (filters, options, token) => store.QueryCoreAsync(filters, options, json => JsonSerializer.Deserialize(json, effectiveTypeInfo), token),
            store.CountAsync);
    }

    // The persisted document shape is this store's, never the caller's. Generated metadata whose context
    // declares a different naming policy -- or omits a converter the write path applied -- describes a
    // different document, and deserializing through it yields defaults or wrong values with no error.
    // Only the resolver is taken from the caller, so the read stays trim-safe.
    private volatile GenerationReadBinding? _generationReadBinding;

    private sealed record GenerationReadBinding(JsonTypeInfo<TProjection> Source, JsonTypeInfo<TProjection> Effective);

    private JsonTypeInfo<TProjection> GenerationReadMetadata(JsonTypeInfo<TProjection> typeInfo)
    {
        // OpenReader runs once per read operation, so the derived options are memoised: a fresh
        // JsonSerializerOptions carries a cold metadata cache. A benign race only recomputes.
        var cached = _generationReadBinding;
        if (cached is not null && ReferenceEquals(cached.Source, typeInfo))
        {
            return cached.Effective;
        }
        var effective = ProjectionSerializationDefaults.CreateReadModelTypeInfo(_jsonOptions, typeInfo);
        _generationReadBinding = new GenerationReadBinding(typeInfo, effective);
        return effective;
    }

    private sealed class GenerationReader(
        ITenantContext tenant,
        string? destinationScope,
        Func<string, CancellationToken, Task<TProjection?>> get,
        Func<IDictionary<string, object>?, QueryOptions?, CancellationToken, Task<IReadOnlyList<TProjection>>> query,
        Func<IDictionary<string, object>?, CancellationToken, Task<long>> count) : IProjectionGenerationReader<TProjection>
    {
        public string? DestinationScope { get { _ = tenant.TenantId; return destinationScope; } }
        public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => get(id, cancellationToken);
        public Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => query(filters, options, cancellationToken);
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => count(filters, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GenerationTenantContext(ITenantContext source) : ITenantContext
    {
        private readonly string _tenant = TenantScope.Scoped(source.TenantId).TenantId;
        public string TenantId
        {
            get
            {
                if (!string.Equals(source.TenantId, _tenant, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts.");
                }
                return _tenant;
            }
        }
        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }
    private string? ReadDestinationScope()
    {
        using var connection = _dataSource.CreateConnection();
        if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.Database)) { return null; }
        return "postgres/" + Uri.EscapeDataString(connection.DataSource) + "/" + Uri.EscapeDataString(connection.Database);
    }

    private sealed class ConfiguredDestinationScope(string scope) : IProjectionDestinationScope
    {
        public string Scope => scope;
    }

    private static System.Data.Common.DbCommand CreateReadCommand(System.Data.Common.DbConnection connection, string sql, Dapper.DynamicParameters parameters)
    {
        var command = connection.CreateCommand();
#pragma warning disable CA2100 // SQL is constructed locally from validated identifiers and parameterized filters.
        command.CommandText = sql;
#pragma warning restore CA2100
        foreach (var name in parameters.ParameterNames)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = parameters.Get<object>(name) ?? DBNull.Value;
            _ = command.Parameters.Add(parameter);
        }
        return command;
    }

}
