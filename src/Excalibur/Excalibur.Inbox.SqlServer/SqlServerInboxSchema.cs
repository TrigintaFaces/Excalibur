// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.Inbox.SqlServer;

/// <summary>One SQL catalog handshake, shared by inbox CRUD and atomic outbox completion.</summary>
/// <remarks>The schema must remain stable after successful verification. Connections and transactions are borrowed.</remarks>
internal sealed class SqlServerInboxSchema(string tableName, int commandTimeoutSeconds, bool requireTenant)
{
	private sealed record Verified(bool HasTenantColumn);
	private Verified? _verified;
	internal bool? HasTenantColumn => Volatile.Read(ref _verified)?.HasTenantColumn;

	internal async ValueTask<bool> EnsureAsync(SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _verified) is { } verified)
		{
			return verified.HasTenantColumn;
		}
		var primaryKeyColumns = new List<string>();
		await using (var command = new SqlCommand("""
			SELECT c.name
			FROM sys.indexes i
			JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
			JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
			WHERE i.object_id = OBJECT_ID(@TableName) AND i.is_primary_key = 1
			ORDER BY ic.key_ordinal
			""", connection, transaction))
		{
			command.CommandTimeout = commandTimeoutSeconds;
			_ = command.Parameters.Add("@TableName", SqlDbType.NVarChar, 517);
			command.Parameters["@TableName"].Value = tableName;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				primaryKeyColumns.Add(reader.GetString(0));
			}
		}
		bool? tenantNullable;
		await using (var command = new SqlCommand(
			"SELECT c.is_nullable FROM sys.columns c WHERE c.object_id = OBJECT_ID(@TableName) AND c.name = N'TenantId'", connection, transaction))
		{
			command.CommandTimeout = commandTimeoutSeconds;
			_ = command.Parameters.Add("@TableName", SqlDbType.NVarChar, 517);
			command.Parameters["@TableName"].Value = tableName;
			var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			tenantNullable = value is bool nullable ? nullable : null;
		}
		var result = new Verified(InboxSchemaContract.Verify(tableName, requireTenant, primaryKeyColumns,
			tenantNullable, "MessageId", "HandlerType", "TenantId"));
		// Publish only complete successes. Concurrent first reads converge under the stable-schema contract.
		return (Interlocked.CompareExchange(ref _verified, result, null) ?? result).HasTenantColumn;
	}

	internal void VerifyEntryTenant(bool hasTenantColumn, string? tenantId) =>
		InboxSchemaContract.VerifyTenantAdmissible(tableName, hasTenantColumn,
			!string.IsNullOrWhiteSpace(tenantId), tenantId);
}
