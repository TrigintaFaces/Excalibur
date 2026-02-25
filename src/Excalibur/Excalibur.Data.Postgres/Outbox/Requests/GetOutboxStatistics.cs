// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to retrieve outbox statistics from the Postgres database.
/// </summary>
internal sealed class GetOutboxStatistics : DataRequest<OutboxStatistics>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetOutboxStatistics"/> class.
	/// </summary>
	/// <param name="outboxTableName"> The qualified outbox table name. </param>
	/// <param name="deadLetterTableName"> The qualified dead letter table name. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public GetOutboxStatistics(
		string outboxTableName,
		string deadLetterTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			SELECT
				(SELECT COUNT(*) FROM {outboxTableName} WHERE dispatcher_id IS NULL) AS "TotalPending",
				(SELECT COUNT(*) FROM {outboxTableName} WHERE dispatcher_id IS NOT NULL) AS "TotalReserved",
				(SELECT COUNT(*) FROM {outboxTableName}) AS "TotalMessages",
				(SELECT COUNT(*) FROM {deadLetterTableName}) AS "TotalFailed",
				(SELECT EXTRACT(EPOCH FROM (NOW() - MIN(occurred_on)))
				 FROM {outboxTableName}
				 WHERE dispatcher_id IS NULL) AS "OldestPendingAgeSeconds"
			""";

		Command = CreateCommand(sql, cancellationToken: cancellationToken, commandTimeout: sqlTimeOutSeconds);

		ResolveAsync = async conn =>
		{
			var result = await conn.QuerySingleAsync<OutboxStatisticsRow>(Command).ConfigureAwait(false);

			return new OutboxStatistics
			{
				StagedMessageCount = result.TotalPending,
				SendingMessageCount = result.TotalReserved,
				SentMessageCount = 0, // Postgres deletes sent messages
				FailedMessageCount = result.TotalFailed,
				ScheduledMessageCount = 0,
				OldestUnsentMessageAge = result.OldestPendingAgeSeconds.HasValue
					? TimeSpan.FromSeconds(result.OldestPendingAgeSeconds.Value)
					: null,
				CapturedAt = DateTimeOffset.UtcNow
			};
		};
	}

	/// <summary>
	/// Internal row type for Dapper mapping.
	/// </summary>
	internal sealed class OutboxStatisticsRow
	{
		public int TotalPending { get; set; }
		public int TotalReserved { get; set; }
		public int TotalMessages { get; set; }
		public int TotalFailed { get; set; }
		public double? OldestPendingAgeSeconds { get; set; }
	}
}
