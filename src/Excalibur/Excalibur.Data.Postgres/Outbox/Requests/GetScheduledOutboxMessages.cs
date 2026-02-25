// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to retrieve scheduled outbox messages that are due for delivery.
/// </summary>
internal sealed class GetScheduledOutboxMessages : DataRequest<IEnumerable<OutboundMessage>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetScheduledOutboxMessages"/> class.
	/// </summary>
	/// <param name="cutoff"> Only return messages scheduled at or before this time. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="outboxTableName"> The qualified outbox table name. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public GetScheduledOutboxMessages(
		DateTimeOffset cutoff,
		int batchSize,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			SELECT message_id, message_type, message_metadata, message_body, occurred_on, scheduled_at
			FROM {outboxTableName}
			WHERE scheduled_at IS NOT NULL
			  AND scheduled_at <= @Cutoff
			  AND dispatcher_id IS NULL
			ORDER BY scheduled_at ASC
			LIMIT @BatchSize
			""";

		var parameters = new DynamicParameters();
		parameters.Add("Cutoff", cutoff, direction: ParameterDirection.Input);
		parameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async conn =>
		{
			var rows = await conn.QueryAsync<ScheduledOutboxRow>(Command).ConfigureAwait(false);

			return rows.Select(static row => new OutboundMessage
			{
				Id = row.MessageId,
				MessageType = row.MessageType,
				Payload = System.Text.Encoding.UTF8.GetBytes(row.MessageBody ?? string.Empty),
				CreatedAt = row.OccurredOn,
				ScheduledAt = row.ScheduledAt,
				Status = OutboxStatus.Staged,
			});
		};
	}

	/// <summary>
	/// Internal row type for Dapper mapping.
	/// </summary>
	internal sealed class ScheduledOutboxRow
	{
		// ReSharper disable UnusedAutoPropertyAccessor.Local
		public string MessageId { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public string? MessageMetadata { get; set; }
		public string? MessageBody { get; set; }
		public DateTimeOffset OccurredOn { get; set; }
		public DateTimeOffset? ScheduledAt { get; set; }
		// ReSharper restore UnusedAutoPropertyAccessor.Local
	}
}
