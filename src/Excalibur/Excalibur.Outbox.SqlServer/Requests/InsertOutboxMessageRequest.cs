// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

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
	/// <remarks>
	/// This framework request binds its fixed primitive input and output schema without runtime emit.
	/// Replacing a known parameter value is observed at execution; custom parameter metadata, templates
	/// and output callbacks are not an extension contract. Custom IDataRequest implementations are unchanged.
	/// </remarks>
	[RequiresUnreferencedCode("Legacy outbox metadata serialization uses reflection. Use an explicit OutboxMetadataSerialization registration overload.")]
	[RequiresDynamicCode("Legacy outbox metadata serialization may require runtime code generation. Use an explicit OutboxMetadataSerialization registration overload.")]
	public InsertOutboxMessageRequest(
		string tableName,
		OutboundMessage message,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
		: this(tableName, ConvertLegacyMessage(tableName, message), transaction, commandTimeout, cancellationToken)
	{
	}

	internal InsertOutboxMessageRequest(
		string tableName,
		OutboxMessage outboxMessage,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(outboxMessage);

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
		parameters.Add("@ScheduledAt", outboxMessage.ScheduledAt, DbType.DateTimeOffset);
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
			await StaticDbCommand.ExecuteAsync(connection, Command, command =>
			{
				StaticDbCommand.AddParameter(command, "Id", StaticDbCommand.Input<string>(Parameters, "Id"), DbType.String);
				StaticDbCommand.AddParameter(command, "MessageType", StaticDbCommand.Input<string>(Parameters, "MessageType"), DbType.String);
				StaticDbCommand.AddParameter(command, "Payload", StaticDbCommand.RequiredInput<byte[]>(Parameters, "Payload"), DbType.Binary);
				StaticDbCommand.AddParameter(command, "Headers", StaticDbCommand.Input<string>(Parameters, "Headers"), DbType.String);
				StaticDbCommand.AddNullableStringParameter(command, "Destination", StaticDbCommand.Input<string?>(Parameters, "Destination"));
				StaticDbCommand.AddParameter(command, "CreatedAt", StaticDbCommand.Input<DateTimeOffset>(Parameters, "CreatedAt"), DbType.DateTimeOffset);
				StaticDbCommand.AddParameter(command, "ScheduledAt", StaticDbCommand.Input<DateTimeOffset?>(Parameters, "ScheduledAt"), DbType.DateTimeOffset);
				StaticDbCommand.AddParameter(command, "Status", StaticDbCommand.Input<int>(Parameters, "Status"), DbType.Int32);
				StaticDbCommand.AddParameter(command, "RetryCount", StaticDbCommand.Input<int>(Parameters, "RetryCount"), DbType.Int32);
				StaticDbCommand.AddNullableStringParameter(command, "CorrelationId", StaticDbCommand.Input<string?>(Parameters, "CorrelationId"));
				StaticDbCommand.AddNullableStringParameter(command, "CausationId", StaticDbCommand.Input<string?>(Parameters, "CausationId"));
				StaticDbCommand.AddParameter(command, "TenantId", StaticDbCommand.Input<string>(Parameters, "TenantId"), DbType.String);
				StaticDbCommand.AddParameter(command, "Priority", StaticDbCommand.Input<int>(Parameters, "Priority"), DbType.Int32);
				StaticDbCommand.AddNullableStringParameter(command, "TargetTransports", StaticDbCommand.Input<string?>(Parameters, "TargetTransports"));
				StaticDbCommand.AddParameter(command, "IsMultiTransport", StaticDbCommand.Input<bool>(Parameters, "IsMultiTransport"), DbType.Boolean);
				StaticDbCommand.AddNullableStringParameter(command, "PartitionKey", StaticDbCommand.Input<string?>(Parameters, "PartitionKey"));
				StaticDbCommand.AddNullableStringParameter(command, "GroupKey", StaticDbCommand.Input<string?>(Parameters, "GroupKey"));
				StaticDbCommand.AddParameter(command, "SequenceNumber", StaticDbCommand.Input<long>(Parameters, "SequenceNumber"), DbType.Int64);
			}).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Legacy outbox metadata serialization discovers consumer types.")]
	[RequiresDynamicCode("Legacy outbox metadata serialization may require runtime code generation.")]
	private static OutboxMessage ConvertLegacyMessage(string tableName, OutboundMessage message)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(message);
		return OutboxMessage.FromOutboundMessage(message);
	}

}
