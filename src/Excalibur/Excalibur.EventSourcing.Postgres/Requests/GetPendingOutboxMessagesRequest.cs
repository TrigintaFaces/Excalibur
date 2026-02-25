// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Outbox;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to get pending (unpublished) outbox messages from the Postgres outbox.
/// </summary>
public sealed class GetPendingOutboxMessagesRequest : DataRequestBase<IDbConnection, IReadOnlyList<OutboxMessage>>
{
	private const string Sql = """
		SELECT id AS Id, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
		       event_type AS EventType, event_data AS EventData, created_at AS CreatedAt,
		       published_at AS PublishedAt, retry_count AS RetryCount,
		       message_type AS MessageType, metadata AS Metadata
		FROM event_sourced_outbox
		WHERE published_at IS NULL
		ORDER BY created_at ASC
		LIMIT @BatchSize
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="GetPendingOutboxMessagesRequest"/> class.
	/// </summary>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetPendingOutboxMessagesRequest(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var results = await connection.QueryAsync<OutboxMessageData>(Command).ConfigureAwait(false);
			return results.Select(r => new OutboxMessage
			{
				Id = r.Id,
				AggregateId = r.AggregateId,
				AggregateType = r.AggregateType,
				EventType = r.EventType,
				EventData = r.EventData,
				CreatedAt = r.CreatedAt,
				PublishedAt = r.PublishedAt,
				RetryCount = r.RetryCount,
				MessageType = r.MessageType,
				Metadata = r.Metadata,
			}).ToList();
		};
	}

	private sealed record OutboxMessageData(
		Guid Id,
		string AggregateId,
		string AggregateType,
		string EventType,
		string EventData,
		DateTimeOffset CreatedAt,
		DateTimeOffset? PublishedAt,
		int RetryCount,
		string MessageType,
		string? Metadata);
}
