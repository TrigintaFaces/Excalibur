// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Buries a message under a tenure fence, in one statement, and reports whether the fence decided the
/// outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the member where an accepted stale token does the most damage.</b> A superseded leader that
/// buries a message the live leader then delivers leaves it simultaneously DELIVERED and sitting unreplayed
/// in the dead-letter queue -- so an operator draining that queue re-executes work that already succeeded.
/// The failure report at least leaves a retriable row behind; this one is terminal.
/// </para>
/// <para>
/// <b>It carries no claim term, deliberately, and that is not an oversight.</b> The unfenced dead-letter
/// statement has always relied on Status as its sole discriminator, because the retry-ceiling transition
/// can legitimately run after the lease has lapsed -- at that moment an ownership guard would admit anyone,
/// so it discriminates nothing. The fence is the guard that still means something there: it bounds which
/// dispatcher GENERATION may bury, which is precisely the distinction the lease can no longer make.
/// </para>
/// <para>
/// <b>One statement, for the same reason as the failure path.</b> Reading the high-water and then writing
/// would answer a question about an earlier instant than the one the mutation runs in. The fence MERGE
/// takes an UPDLOCK/HOLDLOCK range lock on the scope row and holds it for the transaction, so a competing
/// leader blocks rather than interleaving.
/// </para>
/// <para>
/// The terminal-status exclusion is retained alongside the fence: a message already Sent or DeadLettered is
/// not buried again, which is true of the current tenure as much as a superseded one.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox Id is the table's primary key, so this statement already addresses at most one row. The drain claims across tenants and buries a row by the globally-unique Id the claim returned, gated on the tenure fence -- which bounds the dispatcher generation, not the tenant. A tenant term could only subtract the row the claim returned, never redirect the statement to a different one")]
internal sealed class FencedMarkMessageDeadLetteredRequest : DataRequestBase<IDbConnection, FencedClaimMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedMarkMessageDeadLetteredRequest"/> class.
	/// </summary>
	/// <param name="outboxTableName">The qualified outbox table name.</param>
	/// <param name="fenceTableName">The qualified fence control table name.</param>
	/// <param name="messageId">The message being buried.</param>
	/// <param name="reason">The reason recorded against the burial.</param>
	/// <param name="fencingToken">The presented tenure token.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedMarkMessageDeadLetteredRequest(
		string outboxTableName,
		string fenceTableName,
		string messageId,
		string reason,
		long fencingToken,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			SET NOCOUNT ON;

			DECLARE @FenceResult TABLE (HighWaterToken BIGINT);
			DECLARE @HighWater BIGINT;
			DECLARE @Updated INT;
			DECLARE @Exists BIT;
			DECLARE @Terminal BIT;

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
			SET Status = 5, LastError = @Reason, LastAttemptAt = @LastAttemptAt, LeasedAt = NULL, LeasedBy = NULL
			WHERE Id = @MessageId
			  AND Status NOT IN (2, 5)
			  AND @HighWater = @FencingToken;

			SET @Updated = @@ROWCOUNT;

			SELECT @Exists = CASE WHEN EXISTS (
				SELECT 1 FROM {outboxTableName} WHERE Id = @MessageId) THEN 1 ELSE 0 END;

			-- The same predicate the mutation refused on, so a terminal row is reported as terminal rather
			-- than as a claim someone else holds.
			SELECT @Terminal = CASE WHEN EXISTS (
				SELECT 1 FROM {outboxTableName} WHERE Id = @MessageId AND Status IN (2, 5)) THEN 1 ELSE 0 END;

			SELECT @HighWater AS HighWaterToken, @Updated AS UpdatedCount, @Exists AS RowExists, @Terminal AS IsTerminal;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Scope", outboxTableName);
		parameters.Add("@FencingToken", fencingToken);
		parameters.Add("@MessageId", messageId);
		parameters.Add("@Reason", reason);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QuerySingleAsync<FencedClaimMutationResult>(Command).ConfigureAwait(false);
	}
}
