// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Data;

namespace Excalibur.EventSourcing.SqlServer;

public sealed partial class SqlServerProjectionStore<TProjection>
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
        if (transaction.Transaction is not SqlTransaction sqlTransaction || sqlTransaction.Connection is not { } connection)
        {
            throw new InvalidOperationException("TransactionalInline requires an active SQL Server transaction.");
        }

        var tenant = RequireTenant();
        if (!string.Equals(tenant, transaction.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("TransactionalInline source and destination tenant partitions differ.");
        }

        using var configured = _connectionFactory();
        if (!string.Equals(configured.DataSource, connection.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(configured.Database, connection.Database, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("TransactionalInline source and projection must use the same configured SQL Server database.");
        }

        using var command = connection.CreateCommand();
        command.Transaction = sqlTransaction;
#pragma warning disable CA2100 // _qualifiedTable is validated and quoted by the store constructor; values are parameters.
        command.CommandText = $"""
            IF EXISTS (SELECT 1 FROM {_qualifiedTable} WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = @Tenant AND Id = @Id
                  AND (LastAppliedPosition IS NULL OR LastAppliedPosition <> @Unnumbered OR TransactionalSource IS NULL
                       OR TransactionalSource <> @Source OR TransactionalAggregateType IS NULL
                       OR TransactionalAggregateType <> @Type))
                THROW 51000, 'Transactional projection destination is retired or belongs to another source/mode/aggregate type.', 1;
            UPDATE {_qualifiedTable} SET Data = @Data, UpdatedAt = SYSDATETIMEOFFSET(), LastAppliedPosition = @Unnumbered
                WHERE TenantId = @Tenant AND Id = @Id;
            IF @@ROWCOUNT = 0
                INSERT INTO {_qualifiedTable}
                    (TenantId, Id, Data, CreatedAt, UpdatedAt, LastAppliedPosition, TransactionalSource, TransactionalAggregateType)
                VALUES (@Tenant, @Id, @Data, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Unnumbered, @Source, @Type);
            """;
#pragma warning restore CA2100
        command.Parameters.AddWithValue("@Tenant", tenant);
        command.Parameters.AddWithValue("@Id", aggregateId);
        command.Parameters.AddWithValue("@Type", aggregateType);
        command.Parameters.AddWithValue("@Source", transaction.SourceIdentity);
        command.Parameters.AddWithValue("@Data", json);
        command.Parameters.AddWithValue("@Unnumbered", ProjectionPosition.UnnumberedSentinel);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
