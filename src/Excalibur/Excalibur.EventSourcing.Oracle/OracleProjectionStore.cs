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
using Oracle.ManagedDataAccess.Client;
using Excalibur.EventSourcing.Oracle.Requests;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>Tenant-partitioned aggregate-local models stored in the Oracle event transaction.</summary>
/// <typeparam name="TProjection">Read model type.</typeparam>
/// <remarks>Provision its dedicated schema explicitly. This store does not expose global positioned writes.</remarks>
public sealed class OracleProjectionStore<TProjection> : IProjectionStore<TProjection>, ITransactionalProjectionStore<TProjection>,
    IProjectionTenantBoundary, IProjectionDestinationIdentity where TProjection : class
{
    private readonly RelationalTransactionalProjectionStore<TProjection> _store;
    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly string _table;
    private readonly string _qualifiedTable;

    /// <summary>Creates a projection store for an explicitly provisioned destination.</summary>
    /// <param name="connectionString">The same database used by the event store.</param>
    /// <param name="tenantContext">Tenant partition for every operation.</param>
    /// <param name="tableName">Base table name; projection version qualification is applied.</param>
    /// <param name="jsonOptions">Read-model serialization options.</param>
    /// <param name="schemaName">Explicit Oracle schema owning the projection table.</param>
    public OracleProjectionStore(string connectionString, ITenantContext tenantContext, string tableName,
        JsonSerializerOptions? jsonOptions = null, string schemaName = "EXCALIBUR")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _connectionString = connectionString;
        _tenantContext = tenantContext;
        _table = ProjectionVersionConvention.Qualify(tableName, typeof(TProjection), '_').ToUpperInvariant();
        _qualifiedTable = OracleTableName.Format(schemaName, _table);
        SqlIdentifierValidator.ThrowIfInvalid(_table, nameof(tableName));
        _store = new(() => new OracleConnection(_connectionString), CreateCommand, ValidateTransaction,
            _qualifiedTable, tenantContext, jsonOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions(), oracle: true);
    }

    ProjectionTenantBoundary IProjectionTenantBoundary.Boundary => ProjectionTenantBoundary.Partition(TenantScope.Scoped(_tenantContext.TenantId).TenantId);
    string IProjectionDestinationIdentity.Identity => "oracle:" + _qualifiedTable;

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
        if (transaction.Transaction is not OracleTransaction { Connection: { } connection })
        { throw new InvalidOperationException("TransactionalInline requires an active Oracle transaction."); }
        var configured = new OracleConnectionStringBuilder(_connectionString);
        var actual = new OracleConnectionStringBuilder(connection.ConnectionString);
        if (!string.Equals(configured.DataSource, actual.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(configured.UserID, actual.UserID, StringComparison.Ordinal))
        { throw new InvalidOperationException("TransactionalInline requires the same configured Oracle service and database user."); }
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, string sql,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.Transaction = (OracleTransaction?)transaction;
#pragma warning disable CA2100 // Internal helper uses validated identifiers and bound values.
        command.CommandText = sql;
#pragma warning restore CA2100
        foreach (var (name, value) in parameters)
        {
            var parameter = new OracleParameter();
            if (name == "Data") { parameter.OracleDbType = OracleDbType.Clob; }
            else if (value is null) { parameter.OracleDbType = OracleDbType.Varchar2; }
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
