// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Security.Cryptography;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Only executor-validated ASCII SQL identifiers are interpolated; every operation value is a typed SQL parameter.")]
internal sealed class SqlServerOperationContext(string server, string database, string schema,
	string eventsTable, string tenant, byte[] identity, byte[] fingerprint)
{
	private static readonly AsyncLocal<SqlServerOperationContext?> Ambient = new();
	private int _appendAttempted;
	private int _closed;
	private readonly Guid _attemptId = Guid.NewGuid();
	internal static SqlServerOperationContext? Current { get => Ambient.Value; set => Ambient.Value = value; }
	internal bool AppendAttempted => Volatile.Read(ref _appendAttempted) != 0;
	internal bool ReceiptBelongsToThisAttempt { get; private set; }

	internal void EnterAppend()
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
		if (Interlocked.CompareExchange(ref _appendAttempted, 1, 0) != 0)
		{
			throw new InvalidOperationException("An idempotent SQL operation supports exactly one nonempty append attempt.");
		}
	}

	internal void Close() => Interlocked.Exchange(ref _closed, 1);

	internal void ValidateDestination(SqlConnection connection, string appendSchema, string appendTable, string appendTenant)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
		if (!string.Equals(server, connection.DataSource, StringComparison.OrdinalIgnoreCase)
			|| !string.Equals(database, connection.Database, StringComparison.Ordinal)
			|| !string.Equals(schema, appendSchema, StringComparison.Ordinal)
			|| !string.Equals(eventsTable, appendTable, StringComparison.Ordinal)
			|| !string.Equals(tenant, appendTenant, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The operation and event append must use the same SQL destination and tenant.");
		}
	}

	internal async Task WriteReceiptAsync(SqlConnection connection, SqlTransaction transaction,
		string appendSchema, string appendTable, string appendTenant, string aggregateId, string aggregateType,
		AppendResult append, CancellationToken cancellationToken)
	{
		ValidateDestination(connection, appendSchema, appendTable, appendTenant);
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = $"INSERT INTO [{schema}].[{eventsTable}Operations] (OperationId,AttemptId,Fingerprint,AggregateId,AggregateType,NextExpectedVersion,FirstEventPosition,LastEventPosition) VALUES (@id,@attempt,@fingerprint,@aggregateId,@aggregateType,@version,@first,@last);";
		BindIdentity(command);
		_ = command.Parameters.Add("@attempt", SqlDbType.UniqueIdentifier).Value = _attemptId;
		_ = command.Parameters.Add("@aggregateId", SqlDbType.NVarChar, 450).Value = aggregateId;
		_ = command.Parameters.Add("@aggregateType", SqlDbType.NVarChar, 450).Value = aggregateType;
		_ = command.Parameters.Add("@version", SqlDbType.BigInt).Value = append.NextExpectedVersion;
		_ = command.Parameters.Add("@first", SqlDbType.BigInt).Value = append.FirstEventPosition
			?? throw new InvalidOperationException("An operation receipt requires positioned events.");
		_ = command.Parameters.Add("@last", SqlDbType.BigInt).Value = append.LastEventPosition
			?? throw new InvalidOperationException("An operation receipt requires positioned events.");
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	internal async Task<SqlServerOperationReceipt?> ReadReceiptAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		ValidateDestination(connection, schema, eventsTable, tenant);
		await using var command = connection.CreateCommand();
		command.CommandText = $"SELECT Fingerprint,AggregateId,AggregateType,NextExpectedVersion,FirstEventPosition,LastEventPosition,AttemptId FROM [{schema}].[{eventsTable}Operations] WHERE OperationId=@id;";
		BindIdentity(command);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			return null;
		}
		if (!CryptographicOperations.FixedTimeEquals(fingerprint, (byte[])reader.GetValue(0)))
		{
			throw new SqlServerOperationConflictException();
		}
		ReceiptBelongsToThisAttempt = reader.GetGuid(6) == _attemptId;
		return new SqlServerOperationReceipt(reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5));
	}

	private void BindIdentity(SqlCommand command)
	{
		_ = command.Parameters.Add("@id", SqlDbType.Binary, 32).Value = identity;
		_ = command.Parameters.Add("@fingerprint", SqlDbType.Binary, 32).Value = fingerprint;
	}
}


