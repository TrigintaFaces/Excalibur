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
/// <remarks>Provision its dedicated schema explicitly.</remarks>
public sealed class SqliteProjectionStore<TProjection> : IProjectionStore<TProjection>, ITransactionalProjectionStore<TProjection>, IPositionedProjectionStore<TProjection>,
    IProjectionTenantBoundary, IProjectionDestinationIdentity where TProjection : class
{
    private readonly RelationalTransactionalProjectionStore<TProjection> _store;
    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly string _table;
    /// <summary>Which apply path owns this destination. Decided at construction, never probed.</summary>
    private readonly ProjectionDestinationKind _destinationKind;


    /// <summary>Creates a projection store for an explicitly provisioned destination.</summary>
    /// <param name="connectionString">The same database used by the event store.</param>
    /// <param name="tenantContext">Tenant partition for every operation.</param>
    /// <param name="tableName">Base table name; projection version qualification is applied.</param>
    /// <param name="jsonOptions">Read-model serialization options.</param>
    /// <param name="destinationKind">
    /// Which apply path owns this destination. Defaults to
    /// <see cref="ProjectionDestinationKind.Positioned"/>, the narrower claim. Pass
    /// <see cref="ProjectionDestinationKind.Transactional"/> for a destination written inside the event
    /// store's append transaction; that store then declines the positioned capability.
    /// </param>
    public SqliteProjectionStore(string connectionString, ITenantContext tenantContext, string tableName,
        JsonSerializerOptions? jsonOptions = null,
        ProjectionDestinationKind destinationKind = ProjectionDestinationKind.Positioned)
    {
        _destinationKind = destinationKind;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _connectionString = connectionString;
        _tenantContext = tenantContext;
        _table = ProjectionVersionConvention.Qualify(tableName, typeof(TProjection), '_');
        SqlIdentifierValidator.ThrowIfInvalid(_table, nameof(tableName));
        _store = new(() => new SqliteConnection(_connectionString), CreateCommand, ValidateTransaction,
            "[" + _table + "]", tenantContext, jsonOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions(),
            // SQLITE_CONSTRAINT. Supplied for completeness rather than need: every statement this store
            // issues decides match-or-insert inside one index operation, so a losing writer is already
            // reported as zero rows affected and this predicate is not reached.
            RelationalProjectionDialect.ForSqlite(static error => error is SqliteException { SqliteErrorCode: 19 }));
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

    /// <summary>Resolves a capability from this store or the shared relational store beneath it.</summary>
    /// <remarks>
    /// This type holds an inner projection store, so the store interface's default answer -- "answer for
    /// myself" -- would hide every capability the inner store carries. Answering for this instance first
    /// keeps the provider-specific capabilities (tenant boundary, destination identity) resolvable, and
    /// delegating otherwise keeps the shared relational capabilities resolvable through it.
    /// </remarks>
    /// <param name="serviceType">The capability being requested.</param>
    /// <returns>The capability, or <see langword="null"/> when neither store provides it.</returns>
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        // A DESTINATION BELONGS TO ONE APPLY PATH, so this instance declines the capability it does not
        // own. See ProjectionDestinationKind for why the kind is declared at construction rather than
        // inferred: these two providers ship ONE table shape serving both roles, so there is nothing in
        // the schema to read, and the previous guard was a CHECK constraint reporting through a provider
        // exception the positioned contract does not declare.
        if (serviceType == typeof(IPositionedProjectionStore<TProjection>)
            && _destinationKind == ProjectionDestinationKind.Transactional)
        {
            return null;
        }

        if (serviceType == typeof(ITransactionalProjectionStore<TProjection>)
            && _destinationKind == ProjectionDestinationKind.Positioned)
        {
            return null;
        }

        return serviceType.IsInstanceOfType(this) ? this : ((IServiceProvider)_store).GetService(serviceType);
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(string id, CancellationToken cancellationToken) =>
        _store.GetWithPositionAsync(id, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task UpsertUnnumberedAsync(string id, TProjection projection, CancellationToken cancellationToken) =>
        _store.UpsertUnnumberedAsync(id, projection, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(string id, TProjection projection, long? expectedPosition, long newPosition, CancellationToken cancellationToken) =>
        _store.UpsertAtPositionAsync(id, projection, expectedPosition, newPosition, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task<ProjectionRefoldResult> RefoldAtPositionAsync(string id, TProjection projection, long atPosition, CancellationToken cancellationToken) =>
        _store.RefoldAtPositionAsync(id, projection, atPosition, cancellationToken);
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task<ProjectionRebuildResult> RebuildAtPositionAsync(string id, TProjection projection, long newPosition, CancellationToken cancellationToken) =>
        _store.RebuildAtPositionAsync(id, projection, newPosition, cancellationToken);

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
