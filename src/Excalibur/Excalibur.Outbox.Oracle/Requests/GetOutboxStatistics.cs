// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to retrieve outbox statistics from the Oracle database.
/// </summary>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"estate-wide counters for operational monitoring: the store method that reaches this request takes no tenant argument, and this store reads no ambient tenant context, so there is no tenant to count by. The figures describe the whole table and are reported to an operator, not to a tenant")]
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
		// "Pending/staged" = unclaimed, not-yet-failed, and either not scheduled or due — so a failed
		// (error_message set) or future-scheduled row does not masquerade as staged. "Failed" counts
		// sub-ceiling failures still in the main table (error_message set) PLUS terminal dead-lettered rows.
		// "Scheduled" counts future-scheduled rows. Sent is not tracked (Oracle deletes on send — the
		// delete-on-sent lifecycle is an intentional, separately-decided contract).
		var sql = $"""
			SELECT
				(SELECT COUNT(*) FROM {outboxTableName}
				 WHERE dispatcher_id IS NULL
				   AND error_message IS NULL
				   AND (scheduled_at IS NULL OR scheduled_at <= SYSTIMESTAMP)) AS "TotalPending",
				(SELECT COUNT(*) FROM {outboxTableName} WHERE dispatcher_id IS NOT NULL) AS "TotalReserved",
				(SELECT COUNT(*) FROM {outboxTableName}) AS "TotalMessages",
				(SELECT COUNT(*) FROM {outboxTableName} WHERE error_message IS NOT NULL)
					+ (SELECT COUNT(*) FROM {deadLetterTableName}) AS "TotalFailed",
				(SELECT COUNT(*) FROM {outboxTableName} WHERE scheduled_at > SYSTIMESTAMP) AS "TotalScheduled",
				-- ROUND is load-bearing, not cosmetic: without it this query THROWS.
				--
				-- Subtracting two Oracle DATEs yields a NUMBER of DAYS, and one second is
				-- 0.000011574074074074... -- a repeating fraction Oracle carries at full precision.
				-- Multiplying by 86400 keeps ~38 significant digits, and converting a NUMBER that wide to
				-- .NET decimal overflows, so the read fails with "Error parsing column
				-- OldestPendingAgeSeconds" wrapped around an OverflowException rather than returning a
				-- number. It only fires when a pending row EXISTS to measure an age from, which is why it
				-- surfaced as an intermittent failure in whichever statistics test happened to have one.
				--
				-- Both operands are already CAST to DATE, so the true resolution is one second; rounding to
				-- milliseconds bounds the width without discarding anything the expression can represent.
				(SELECT ROUND((CAST(SYSTIMESTAMP AS DATE) - CAST(MIN(occurred_on) AS DATE)) * 86400, 3)
				 FROM {outboxTableName}
				 WHERE dispatcher_id IS NULL
				   AND error_message IS NULL
				   AND (scheduled_at IS NULL OR scheduled_at <= SYSTIMESTAMP)) AS "OldestPendingAgeSeconds"
			""";

		Command = CreateCommand(sql, cancellationToken: cancellationToken, commandTimeout: sqlTimeOutSeconds);

		ResolveAsync = async conn =>
		{
			var result = await conn.QuerySingleAsync<OutboxStatisticsRow>(Command).ConfigureAwait(false);

			return new OutboxStatistics
			{
				StagedMessageCount = result.TotalPending,
				SendingMessageCount = result.TotalReserved,
				SentMessageCount = 0, // Oracle deletes sent messages (delete-on-sent lifecycle)
				FailedMessageCount = result.TotalFailed,
				ScheduledMessageCount = result.TotalScheduled,
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
		public int TotalScheduled { get; set; }
		public double? OldestPendingAgeSeconds { get; set; }
	}
}
