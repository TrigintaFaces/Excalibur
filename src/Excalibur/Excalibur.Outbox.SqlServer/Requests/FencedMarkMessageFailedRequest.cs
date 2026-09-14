// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Records a delivery failure under BOTH a tenure fence and the claim the row was handed under, in one
/// statement, and reports which of those two gates decided the outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the fence and the claim are both present, and neither subsumes the other.</b> The fence answers
/// "is this dispatcher generation still the leader?"; the claim answers "is this the reservation this row
/// was handed under?". A statement carrying only the fence admits a stale cycle of a live tenure -- the
/// tenure is current, but this particular claim was already lost and re-granted. One carrying only the
/// claim admits a superseded leader whose claim happens to still be stamped on the row. Both gates, or the
/// window stays open in one direction.
/// </para>
/// <para>
/// <b>Why it is one statement rather than a check followed by a write.</b> Reading the high-water and then
/// updating would answer a question about an earlier instant than the one the mutation runs in, which is
/// exactly the defect this request exists to remove: a superseded leader can win that gap. The fence
/// MERGE takes an UPDLOCK/HOLDLOCK range lock on the scope row and holds it for the rest of the
/// transaction, so a competing leader blocks rather than interleaving. The UPDATE then reads the value the
/// MERGE just established, inside the same transaction and behind the same lock.
/// </para>
/// <para>
/// <b>The fence MERGE mirrors <see cref="EnforceOutboxFenceRequest"/> deliberately</b> -- same hints, same
/// monotonic advance -- so the two paths cannot drift on the locking discipline. Advancing to the presented
/// token is monotonic: a superseded token leaves the recorded value untouched, and the comparison below
/// then reports the refusal rather than the statement silently doing nothing.
/// </para>
/// <para>
/// <b>The result distinguishes three different "no rows" outcomes</b>, because collapsing them is what made
/// the unfenced path unreadable: the fence refused (a newer tenure exists, stop draining), the claim was
/// lost (this row was re-granted, keep going with the rest), or the row is simply gone.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox Id is the table's primary key, so this statement already addresses at most one row. The drain claims across tenants and completes a row by the globally-unique Id the claim returned, gated on the tenure fence and on the claim identity stamped at reservation -- those bound which dispatcher generation and which claim may act, not which tenant. A tenant term could only subtract the row the claim returned, never redirect the statement to a different one")]
internal sealed class FencedMarkMessageFailedRequest : DataRequestBase<IDbConnection, FencedClaimMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedMarkMessageFailedRequest"/> class.
	/// </summary>
	/// <param name="outboxTableName">The qualified outbox table name.</param>
	/// <param name="fenceTableName">The qualified fence control table name.</param>
	/// <param name="messageId">The message whose failure is being recorded.</param>
	/// <param name="errorMessage">The failure detail to record.</param>
	/// <param name="retryCount">The attempt count this failure represents.</param>
	/// <param name="nextAttemptAt">The computed next-attempt instant, or null for no computed schedule.</param>
	/// <param name="floorSeconds">The minimum backoff floor, or null when the caller applies none.</param>
	/// <param name="fencingToken">The presented tenure token.</param>
	/// <param name="claimIdentity">The claim identity stamped on the row at reservation.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedMarkMessageFailedRequest(
		string outboxTableName,
		string fenceTableName,
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset? nextAttemptAt,
		int? floorSeconds,
		long fencingToken,
		string claimIdentity,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		var nextAttemptClause = OutboxFailureMark.NextAttemptClause(nextAttemptAt.HasValue, floorSeconds.HasValue);

		// The terminal-status exclusion is composed from the shared fragment's own reasoning rather than
		// restated: Sent and DeadLettered must never be reversed by a failure report, and that is true of a
		// report from the CURRENT claim holder as much as a superseded one.
		var sql = $"""
			SET NOCOUNT ON;

			DECLARE @FenceResult TABLE (HighWaterToken BIGINT);
			DECLARE @HighWater BIGINT;
			DECLARE @Updated INT;
			DECLARE @Exists BIT;

			MERGE {fenceTableName} WITH (UPDLOCK, HOLDLOCK) AS f
			USING (SELECT @Scope AS OutboxTable) AS s ON (f.OutboxTable = s.OutboxTable)
			WHEN MATCHED THEN
				UPDATE SET HighWaterToken =
					CASE WHEN f.HighWaterToken < @FencingToken THEN @FencingToken ELSE f.HighWaterToken END
			WHEN NOT MATCHED THEN
				INSERT (OutboxTable, HighWaterToken) VALUES (@Scope, @FencingToken)
			OUTPUT INSERTED.HighWaterToken INTO @FenceResult;

			SELECT TOP 1 @HighWater = HighWaterToken FROM @FenceResult;

			UPDATE {outboxTableName}
			{OutboxFailureMark.SetClause}{nextAttemptClause}
			WHERE Id = @MessageId
			  AND Status NOT IN (2, 5)
			  AND LeasedBy = @ClaimIdentity
			  AND @HighWater = @FencingToken;

			SET @Updated = @@ROWCOUNT;

			SELECT @Exists = CASE WHEN EXISTS (
				SELECT 1 FROM {outboxTableName} WHERE Id = @MessageId) THEN 1 ELSE 0 END;

			SELECT @HighWater AS HighWaterToken, @Updated AS UpdatedCount, @Exists AS RowExists;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Scope", outboxTableName);
		parameters.Add("@FencingToken", fencingToken);
		parameters.Add("@MessageId", messageId);
		parameters.Add("@ErrorMessage", errorMessage);
		parameters.Add("@RetryCount", retryCount);
		parameters.Add("@ClaimIdentity", claimIdentity);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);
		if (nextAttemptAt.HasValue)
		{
			parameters.Add("@NextAttemptDelayMs", OutboxFailureMark.ToServerDelayMilliseconds(nextAttemptAt.Value));
		}

		if (floorSeconds.HasValue)
		{
			parameters.Add("@FloorSeconds", floorSeconds.Value);
		}

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QuerySingleAsync<FencedClaimMutationResult>(Command).ConfigureAwait(false);
	}
}
