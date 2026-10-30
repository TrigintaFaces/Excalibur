// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Npgsql;
using Excalibur.Data;

namespace Excalibur.EventSourcing.Postgres;

public sealed partial class PostgresProjectionStore<TProjection>
{
    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization may use reflection. Use generated metadata instead.")]
    [RequiresDynamicCode("Projection serialization may require dynamic code. Use generated metadata instead.")]
    public string Serialize(TProjection projection) => JsonSerializer.Serialize(projection, _jsonOptions);

    /// <inheritdoc />
    public string Serialize(TProjection projection, JsonTypeInfo<TProjection> typeInfo) =>
        JsonSerializer.Serialize(projection, ProjectionSerializationDefaults.CreateReadModelTypeInfo(_jsonOptions, typeInfo));

    /// <inheritdoc />
    public async ValueTask WriteAsync(ProjectionTransaction transaction, string aggregateId, string aggregateType,
        string json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentNullException.ThrowIfNull(json);
        if (transaction.Transaction is not NpgsqlTransaction postgresTransaction || postgresTransaction.Connection is not { } connection)
        {
            throw new InvalidOperationException("TransactionalInline requires an active PostgreSQL transaction.");
        }
        var tenant = RequireTenant();
        if (!string.Equals(tenant, transaction.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("TransactionalInline source and destination tenant partitions differ.");
        }
        using var configured = _dataSource.CreateConnection();
        if (!string.Equals(configured.DataSource, connection.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(configured.Database, connection.Database, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("TransactionalInline source and projection must use the same configured PostgreSQL database.");
        }
        await using var command = connection.CreateCommand();
        command.Transaction = postgresTransaction;
#pragma warning disable CA2100 // Constructor validates and quotes the table; all values are parameters.
        command.CommandText = $"""
            INSERT INTO {_qualifiedTable} AS destination
                (tenant_id, id, data, created_at, updated_at, last_applied_position, transactional_source, transactional_aggregate_type)
            VALUES (@Tenant, @Id, CAST(@Data AS jsonb), CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, @Unnumbered, @Source, @Type)
            ON CONFLICT (id, tenant_id) DO UPDATE
                SET data = EXCLUDED.data, updated_at = CURRENT_TIMESTAMP, last_applied_position = @Unnumbered
                WHERE destination.last_applied_position = @Unnumbered
                  AND destination.transactional_source = @Source
                  AND destination.transactional_aggregate_type = @Type
            """;
#pragma warning restore CA2100
        command.Parameters.AddWithValue("Tenant", tenant);
        command.Parameters.AddWithValue("Id", aggregateId);
        command.Parameters.AddWithValue("Data", json);
        command.Parameters.AddWithValue("Source", transaction.SourceIdentity);
        command.Parameters.AddWithValue("Type", aggregateType);
        command.Parameters.AddWithValue("Unnumbered", ProjectionPosition.UnnumberedSentinel);
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException("Transactional projection destination is retired or belongs to another source/mode/aggregate type.");
        }
    }
}
