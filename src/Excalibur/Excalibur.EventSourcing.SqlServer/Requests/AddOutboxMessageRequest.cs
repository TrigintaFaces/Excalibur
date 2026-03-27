// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Outbox;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to add a message to the event-sourced outbox.
/// </summary>
public sealed class AddOutboxMessageRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AddOutboxMessageRequest"/> class.
	/// </summary>
	/// <param name="message">The outbox message to add.</param>
	/// <param name="transaction">The database transaction.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "dbo".</param>
	/// <param name="table">The outbox table name. Default: "EventSourcedOutbox".</param>
	public AddOutboxMessageRequest(
		OutboxMessage message,
		IDbTransaction transaction,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventSourcedOutbox")
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transaction);

		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			INSERT INTO {qualifiedTable}
			    (Id, AggregateId, AggregateType, EventType, EventData, CreatedAt, PublishedAt, RetryCount, MessageType, Metadata)
			VALUES
			    (@Id, @AggregateId, @AggregateType, @EventType, @EventData, @CreatedAt, @PublishedAt, @RetryCount, @MessageType, @Metadata)
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", message.Id);
		parameters.Add("@AggregateId", message.AggregateId);
		parameters.Add("@AggregateType", message.AggregateType);
		parameters.Add("@EventType", message.EventType);
		parameters.Add("@EventData", message.EventData);
		parameters.Add("@CreatedAt", message.CreatedAt);
		parameters.Add("@PublishedAt", message.PublishedAt);
		parameters.Add("@RetryCount", message.RetryCount);
		parameters.Add("@MessageType", message.MessageType);
		parameters.Add("@Metadata", message.Metadata);

		Command = CreateCommand(sql, parameters, transaction: transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
