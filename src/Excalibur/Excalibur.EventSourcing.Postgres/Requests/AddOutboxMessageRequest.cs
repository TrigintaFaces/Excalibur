// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Outbox;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to add a message to the Postgres event-sourced outbox.
/// </summary>
public sealed class AddOutboxMessageRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		INSERT INTO event_sourced_outbox
		    (id, aggregate_id, aggregate_type, event_type, event_data, created_at, published_at, retry_count, message_type, metadata)
		VALUES
		    (@Id, @AggregateId, @AggregateType, @EventType, @EventData, @CreatedAt, @PublishedAt, @RetryCount, @MessageType, @Metadata)
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="AddOutboxMessageRequest"/> class.
	/// </summary>
	/// <param name="message">The outbox message to add.</param>
	/// <param name="transaction">The database transaction.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public AddOutboxMessageRequest(
		OutboxMessage message,
		IDbTransaction transaction,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transaction);

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

		Command = CreateCommand(Sql, parameters, transaction: transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
