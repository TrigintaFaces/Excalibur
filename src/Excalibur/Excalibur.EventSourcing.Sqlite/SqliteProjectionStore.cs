// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Data;
using Excalibur.Data.Validation;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>Tenant-partitioned aggregate-local models stored in the SQLite event transaction.</summary>
/// <typeparam name="TProjection">Read model type.</typeparam>
/// <remarks>Provision its dedicated schema explicitly. This store does not expose global positioned writes.</remarks>
public sealed class SqliteProjectionStore<TProjection> : IProjectionStore<TProjection>, ITransactionalProjectionStore<TProjection>,
    IProjectionTenantBoundary, IProjectionDestinationIdentity where TProjection : class
{
    private readonly RelationalTransactionalProjectionStore<TProjection> _store;
    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly string _table;

    /// <summary>Creates a projection store for an explicitly provisioned destination.</summary>
    /// <param name="connectionString">The same database used by the event store.</param>
    /// <param name="tenantContext">Tenant partition for every operation.</param>
    /// <param name="tableName">Base table name; projection version qualification is applied.</param>
    /// <param name="jsonOptions">Read-model serialization options.</param>
    public SqliteProjectionStore(string connectionString, ITenantContext tenantContext, string tableName,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _connectionString = connectionString;
        _tenantContext = tenantContext;
        _table = ProjectionVersionConvention.Qualify(tableName, typeof(TProjection), '_');
        SqlIdentifierValidator.ThrowIfInvalid(_table, nameof(tableName));
        _store = new(() => new SqliteConnection(_connectionString), CreateCommand, ValidateTransaction,
            "[" + _table + "]", tenantContext, jsonOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions(), oracle: false);
    }

    ProjectionTenantBoundary IProjectionTenantBoundary.Boundary => ProjectionTenantBoundary.Partition(TenantScope.Scoped(_tenantContext.TenantId).TenantId);
    string IProjectionDestinationIdentity.Identity => "sqlite:" + _table;

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => _store.GetByIdAsync(id, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken) => _store.UpsertAsync(id, projection, cancellationToken);
    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken) => _store.DeleteAsync(id, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => _store.QueryAsync(filters, options, cancellationToken);
    /// <inheritdoc />
    public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => _store.CountAsync(filters, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization may use reflection. Use generated metadata instead.")]
    [RequiresDynamicCode("Projection serialization may require dynamic code. Use generated metadata instead.")]
    public string Serialize(TProjection projection) => _store.Serialize(projection);
    /// <inheritdoc />
    public string Serialize(TProjection projection, JsonTypeInfo<TProjection> typeInfo) => _store.Serialize(projection, typeInfo);
    /// <inheritdoc />
    public ValueTask WriteAsync(ProjectionTransaction transaction, string aggregateId, string aggregateType, string json, CancellationToken cancellationToken) =>
        _store.WriteAsync(transaction, aggregateId, aggregateType, json, cancellationToken);

    private void ValidateTransaction(ProjectionTransaction transaction)
    {
        if (transaction.Transaction is not SqliteTransaction { Connection: { } connection })
        { throw new InvalidOperationException("TransactionalInline requires an active SQLite transaction."); }
        var configured = new SqliteConnectionStringBuilder(_connectionString);
        var actual = new SqliteConnectionStringBuilder(connection.ConnectionString);
        var sharedMemory = configured.Mode == SqliteOpenMode.Memory && actual.Mode == SqliteOpenMode.Memory
            && configured.Cache == SqliteCacheMode.Shared && actual.Cache == SqliteCacheMode.Shared
            && !string.IsNullOrWhiteSpace(configured.DataSource) && configured.DataSource != ":memory:"
            && !configured.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && string.Equals(configured.DataSource, actual.DataSource, StringComparison.Ordinal);
        var disk = configured.Mode != SqliteOpenMode.Memory && actual.Mode != SqliteOpenMode.Memory
            && !string.IsNullOrWhiteSpace(configured.DataSource) && !string.IsNullOrWhiteSpace(actual.DataSource)
            && configured.DataSource != ":memory:" && actual.DataSource != ":memory:"
            && !configured.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && !actual.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(configured.DataSource), connection.DataSource,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        if (!sharedMemory && !disk)
        { throw new InvalidOperationException("TransactionalInline requires the same SQLite file or explicitly shared named memory database; private memory and URI aliases are unsupported."); }
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, string sql,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // Internal helper uses validated identifiers and bound values.
        command.CommandText = sql;
#pragma warning restore CA2100
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
