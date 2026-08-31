// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get outbox statistics.
/// </summary>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"estate-wide counters for operational monitoring: the store method that reaches this request takes no tenant argument, and this store reads no ambient tenant context, so there is no tenant to count by. The figures describe the whole table and are reported to an operator, not to a tenant")]
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
				-- "sending" = currently CLAIMED/in-flight (a dispatcher holds the lease), mirroring the
				-- Postgres provider's TotalReserved (COUNT WHERE dispatcher_id IS NOT NULL). OutboxStatus.Sending
				-- (Status=1) is never persisted on the single-row outbox (concurrency is lease-guarded, not
				-- status-guarded), so counting Status=1 reported a permanently-0 signal. LeasedAt is set on claim
				-- (GetUnsentMessagesRequest) and cleared to NULL on every terminal transition, so LeasedAt IS NOT
				-- NULL is exactly the set of in-flight messages.
				SUM(CASE WHEN LeasedAt IS NOT NULL THEN 1 ELSE 0 END) AS SendingCount,
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
