// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get outbox statistics.
/// </summary>
public sealed class GetOutboxStatisticsRequest : DataRequestBase<IDbConnection, OutboxStatistics>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetOutboxStatisticsRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetOutboxStatisticsRequest(
		string tableName,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			SELECT
				SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS StagedCount,
				SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS SendingCount,
				SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS SentCount,
				SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS FailedCount,
				SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END) AS PartiallyFailedCount,
				SUM(CASE WHEN ScheduledAt IS NOT NULL AND Status = 0 THEN 1 ELSE 0 END) AS ScheduledCount,
				MIN(CASE WHEN Status IN (0, 3, 4) THEN CreatedAt ELSE NULL END) AS OldestUnsentCreatedAt
			FROM {tableName}
			""";

		Command = CreateCommand(sql, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var result = await connection.QuerySingleOrDefaultAsync<StatisticsRow>(Command).ConfigureAwait(false);

			return new OutboxStatistics
			{
				StagedMessageCount = result?.StagedCount ?? 0,
				SendingMessageCount = result?.SendingCount ?? 0,
				SentMessageCount = result?.SentCount ?? 0,
				FailedMessageCount = (result?.FailedCount ?? 0) + (result?.PartiallyFailedCount ?? 0),
				ScheduledMessageCount = result?.ScheduledCount ?? 0,
				OldestUnsentMessageAge = result?.OldestUnsentCreatedAt.HasValue == true
					? DateTimeOffset.UtcNow - result.OldestUnsentCreatedAt.Value
					: null
			};
		};
	}

	private sealed class StatisticsRow
	{
		public int StagedCount { get; set; }
		public int SendingCount { get; set; }
		public int SentCount { get; set; }
		public int FailedCount { get; set; }
		public int PartiallyFailedCount { get; set; }
		public int ScheduledCount { get; set; }
		public DateTimeOffset? OldestUnsentCreatedAt { get; set; }
	}
}
