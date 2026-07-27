// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a message as failed in the outbox.
/// </summary>
public sealed class MarkMessageFailedRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkMessageFailedRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageId">The message ID to mark as failed.</param>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="retryCount">The current retry count.</param>
	/// <param name="leasedBy">
	/// Identifier of the processor marking the message failed (the same value written to <c>LeasedBy</c> when the
	/// row was claimed). The update only affects the row when it is unleased or still leased by this processor,
	/// so a stale processor cannot overwrite a message a peer has since re-claimed.
	/// </param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="nextAttemptAt">
	/// Optional per-message next-attempt time (the fine-grained backoff schedule). When provided, the row's
	/// <c>NextAttemptAt</c> column is set so the claim predicate excludes the message until this time elapses.
	/// Mutually exclusive with <paramref name="floorSeconds"/> and takes precedence.
	/// </param>
	/// <param name="floorSeconds">
	/// Optional failure-anchored visibility floor F, in seconds, for the plain (no fine-grained backoff) path.
	/// When provided (and <paramref name="nextAttemptAt"/> is <see langword="null"/>), <c>NextAttemptAt</c> is
	/// set to <c>SYSUTCDATETIME() + F</c> on the server clock, so the message is re-claimable only after F —
	/// never in the same drain cycle (no hot-loop) and never terminally (at-least-once). F must exceed the
	/// poll interval. When both are <see langword="null"/>, the column is left unchanged.
	/// </param>
	/// <remarks>
	/// The mark targets the globally-unique outbox <c>Id</c>, which addresses exactly one row, so no tenant
	/// predicate is applied: the drain is cross-tenant infrastructure and must always be able to mark the row
	/// it claimed, regardless of any ambient tenant context. Tenant isolation lives on the write/stage path
	/// (<c>TenantId</c> stamping) and on tenant-facing queries.
	/// <para>
	/// The transition releases the lease (<c>LeasedAt</c>/<c>LeasedBy</c> cleared), matching every other
	/// terminal transition: the failed message is idle, not in-flight, so its computed backoff schedule
	/// governs the next claim and statistics report it as failed rather than leased.
	/// </para>
	/// </remarks>
	public MarkMessageFailedRequest(
		string tableName,
		string messageId,
		string errorMessage,
		int retryCount,
		string leasedBy,
		int commandTimeout,
		CancellationToken cancellationToken,
		DateTimeOffset? nextAttemptAt = null,
		int? floorSeconds = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ArgumentException.ThrowIfNullOrWhiteSpace(leasedBy);

		// NextAttemptAt gates re-claim. Two mutually-exclusive sources:
		//  • nextAttemptAt (fine-grained backoff path): the caller-computed schedule, bound verbatim.
		//  • floorSeconds (plain MarkFailedAsync path): a failure-anchored visibility floor computed on the
		//    SERVER clock — TODATETIMEOFFSET(DATEADD(SECOND, @FloorSeconds, SYSUTCDATETIME()), 0) — so re-claim
		//    is deferred by at least F WITHOUT trusting a dispatcher-side clock. F exceeds the poll interval,
		//    so the plain path cannot hot-loop the drain in the same cycle, and the message is never dropped
		//    (at-least-once). When neither is supplied the column is left unchanged.
		var nextAttemptClause = nextAttemptAt.HasValue
			? ", NextAttemptAt = @NextAttemptAt"
			: floorSeconds.HasValue
				? ", NextAttemptAt = TODATETIMEOFFSET(DATEADD(SECOND, @FloorSeconds, SYSUTCDATETIME()), 0)"
				: string.Empty;

		// Release the lease on the failed transition (parity with the dead-letter/sent transitions) so the
		// backoff/floor schedule — not a lingering lease — governs the next claim; and guard on ownership so a
		// stale processor cannot mark-failed a row a peer has re-claimed. RetryCount is non-decreasing
		// (a stale late writer must not lower the count and weaken the DLQ-ceiling termination guarantee).
		// SENT IS TERMINAL AND IS NOT REVERSIBLE BY ANY MARK, current tenure or stale. The ownership guard
		// below is necessary but not sufficient on its own, because this same statement releases the lease
		// (LeasedBy = NULL) on every failed transition — so once any failure has occurred the row satisfies
		// `LeasedBy IS NULL` for every processor thereafter, and a late or duplicated MarkFailed would match
		// a row that has since been delivered. The result is a message re-delivered AFTER a successful send,
		// reported as a routine failure.
		//
		// `Status <> 2` closes that by construction rather than by timing: there is no token, ordering, or
		// lease state under which a delivered message can be moved back to Failed. It mirrors the guard the
		// sent transition already carries, and the asymmetry between the two was the defect.
		var sql = $"""
			UPDATE {tableName}
			SET Status = 3, LastError = @ErrorMessage,
			    RetryCount = CASE WHEN RetryCount > @RetryCount THEN RetryCount ELSE @RetryCount END,
			    LastAttemptAt = @LastAttemptAt,
			    LeasedAt = NULL, LeasedBy = NULL{nextAttemptClause}
			WHERE Id = @MessageId
			  AND Status <> 2
			  AND (LeasedBy IS NULL OR LeasedBy = @LeasedBy)
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@ErrorMessage", errorMessage);
		parameters.Add("@RetryCount", retryCount);
		parameters.Add("@LeasedBy", leasedBy);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);
		if (nextAttemptAt.HasValue)
		{
			parameters.Add("@NextAttemptAt", nextAttemptAt.Value);
		}
		else if (floorSeconds.HasValue)
		{
			parameters.Add("@FloorSeconds", floorSeconds.Value);
		}

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
