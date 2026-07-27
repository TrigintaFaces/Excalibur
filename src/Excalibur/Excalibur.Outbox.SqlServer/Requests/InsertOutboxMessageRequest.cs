// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to insert a message into the outbox.
/// </summary>
public sealed class InsertOutboxMessageRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsertOutboxMessageRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="message">The outbound message to insert.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public InsertOutboxMessageRequest(
		string tableName,
		OutboundMessage message,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(message);

		// Route the OutboundMessage -> outbox-row field mapping (header serialization, tenant/context carry,
		// and CreatedAt preservation) through the ONE canonical conversion so a new field lands in a single
		// place instead of drifting across a parallel inline map. Status and RetryCount are staging lifecycle
		// constants, not creation-mapping fields: a freshly staged row is always Staged with zero retries
		// (matching the Postgres/Oracle stores' hardcoded initial attempts), and the creation seam is
		// structurally incapable of carrying live lifecycle state because its OutboundMessage source has none.
		var outboxMessage = OutboxMessage.FromOutboundMessage(message);

		var sql = $"""
			INSERT INTO {tableName}
				(Id, MessageType, Payload, Headers, Destination, CreatedAt, ScheduledAt, Status,
				 RetryCount, CorrelationId, CausationId, TenantId, Priority, TargetTransports, IsMultiTransport,
				 PartitionKey, GroupKey, SequenceNumber)
			VALUES
				(@Id, @MessageType, @Payload, @Headers, @Destination, @CreatedAt, @ScheduledAt, @Status,
				 @RetryCount, @CorrelationId, @CausationId, @TenantId, @Priority, @TargetTransports, @IsMultiTransport,
				 @PartitionKey, @GroupKey, @SequenceNumber)
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", outboxMessage.MessageId);
		parameters.Add("@MessageType", outboxMessage.MessageType);
		parameters.Add("@Payload", outboxMessage.MessageBody);
		parameters.Add("@Headers", outboxMessage.MessageMetadata);
		parameters.Add("@Destination", outboxMessage.Destination);
		parameters.Add("@CreatedAt", outboxMessage.CreatedAt);
		parameters.Add("@ScheduledAt", outboxMessage.ScheduledAt);
		parameters.Add("@Status", (int)OutboxStatus.Staged);
		parameters.Add("@RetryCount", 0);
		parameters.Add("@CorrelationId", outboxMessage.CorrelationId);
		parameters.Add("@CausationId", outboxMessage.CausationId);
		// Route the tenant term through the keyed partition seam so the persisted column is NEVER NULL: a
		// message staged with no ambient tenant carries the reserved untenanted sentinel, not a null. A null
		// here would be an un-tenanted row that no tenant-scoped predicate can match — unreachable data, and
		// the fail-open-to-unscoped shape the keyed store exists to make inexpressible.
		//
		// FromStoredValue, not Scoped: the value reaching this method has not always come straight from an
		// ambient context. On a retry or a redrive it was READ BACK from the outbox row, and an untenanted
		// row stores the reserved sentinel — which Scoped() rejects by design, because its job is to guard
		// AUTHORING a partition from caller input. Hand-branching on null/whitespace only, then calling
		// Scoped, therefore threw an ArgumentException on the redrive of every untenanted message: staging
		// worked, and the retry of the same message did not. FromStoredValue is the total read-back
		// counterpart and treats null, empty, whitespace and the sentinel alike as untenanted, which is the
		// store's own contract for that column.
		var tenantPartition = KeyedTenantPartition.FromStoredValue(outboxMessage.TenantId);

		parameters.Add("@TenantId", tenantPartition.TenantId);
		parameters.Add("@Priority", outboxMessage.Priority);
		parameters.Add("@TargetTransports", outboxMessage.TargetTransports);
		parameters.Add("@IsMultiTransport", outboxMessage.IsMultiTransport);
		parameters.Add("@PartitionKey", outboxMessage.PartitionKey);
		parameters.Add("@GroupKey", outboxMessage.GroupKey);
		parameters.Add("@SequenceNumber", outboxMessage.SequenceNumber);

		Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
