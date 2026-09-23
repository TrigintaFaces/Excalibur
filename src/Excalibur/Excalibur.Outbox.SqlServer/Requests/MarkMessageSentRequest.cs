// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a message as sent in the outbox.
/// </summary>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox Id is the table's primary key, so this statement already addresses at most one row. The drain claims across tenants and hands back a row addressed by that globally-unique Id, so the mark must be able to address the row the claim returned; a tenant term could only subtract that row, never redirect the statement to a different one")]
internal sealed class MarkMessageSentRequest : DataRequestBase<IDbConnection, MarkSentMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkMessageSentRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageId">The message ID to mark as sent.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="fencingToken">
	/// The fencing token for the caller's current leadership tenure, or <see langword="null"/> when no
	/// fencing applies. When non-null, the update is applied only if <paramref name="fencingToken"/> is
	/// greater than or equal to the recorded fencing high-water mark; the row's <c>FencingToken</c> column
	/// is atomically advanced to this value as part of the same update. A zero-rows-affected result with a
	/// stale token indicates a superseded leader; the caller is responsible for distinguishing that from a
	/// not-found row and throwing <c>StaleOutboxFencingTokenException</c>.
	/// <para>
	/// The high-water mark is read from the durable <c>OutboxFence</c> control table (keyed by
	/// <paramref name="fenceScope"/>), not from <c>MAX(FencingToken)</c> over the message rows. Because
	/// cleanup never deletes that control row, the high-water outlives the purge of sent, token-bearing
	/// rows, so a superseded leader's stale token stays rejected across a cleanup. The presented token is
	/// compared inside the update statement itself, so the guard and the mutation are one atomic step.
	/// </para>
	/// </param>
	/// <param name="fenceTableName">The qualified durable fence control table name.</param>
	/// <param name="fenceScope">The fence scope key — the qualified outbox table name this fence guards.</param>
	/// <remarks>
	/// The mark targets the globally-unique outbox <c>Id</c>, which addresses exactly one row, so no tenant
	/// predicate is applied: the drain is cross-tenant infrastructure and must always be able to mark the row
	/// it claimed, regardless of any ambient tenant context. Tenant isolation lives on the write/stage path
	/// (<c>TenantId</c> stamping) and on tenant-facing queries.
	/// </remarks>
	public MarkMessageSentRequest(
		string tableName,
		string messageId,
		int commandTimeout,
		long? fencingToken,
		string fenceTableName,
		string fenceScope,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceScope);

		// THE FENCE STEP WRITES, AND THE MUTATION IS CONDITIONED ON THAT WRITE ACCEPTING THE TOKEN.
		//
		// The guard used to be a scalar subquery over the fence table inside the UPDATE's own WHERE. That
		// takes no lock and advances nothing, so under snapshot-based read-committed -- the default on some
		// hosted SQL Server offerings -- it resolves against a version taken at statement start and an
		// advance committing during the statement is invisible by construction. A leader superseded DURING
		// its own mark-sent therefore still applied the write: its predicate had already been satisfied
		// against a high-water that no longer held.
		//
		// A read cannot close that window however it is written, because reading is the problem. The fence
		// must CLAIM: the MERGE advances the durable high-water monotonically under UPDLOCK + HOLDLOCK and
		// yields the resulting value, and the UPDATE below compares the presented token against THAT value
		// rather than against a fresh read. The two run inside one explicit transaction, which is what makes
		// the range lock span both -- in autocommit each statement is its own transaction and the lock is
		// released before the UPDATE begins, which is the same gap wearing a different shape. A concurrent
		// leader advancing the high-water now blocks on the range lock until this transaction ends, so it
		// observes the advance and this caller cannot be superseded mid-statement.
		//
		// Both hints are required, for the reason the standalone fence request documents: HOLDLOCK alone
		// leaves two leaders each holding a shared range lock and each needing to convert it, so the engine
		// resolves the cycle by killing one as a deadlock victim; UPDATE locks are not mutually compatible,
		// so the second leader blocks instead of dying.
		//
		// The unfenced path (@FencingToken IS NULL) skips the MERGE entirely and keeps its original
		// semantics -- no fence, no advance, no lock taken on a table it does not use.
		// SET NOCOUNT ON is load-bearing, not hygiene. The caller reads rows-affected to decide whether the
		// mark was refused, and a multi-statement batch returns the SUM of every statement's rowcount. The
		// fence MERGE always affects exactly one row, so without this the batch reports 1 even when the
		// guarded UPDATE matched nothing -- a refusal indistinguishable from a success, which is the same
		// defect this guard exists to prevent, relocated into the guard's own return value. The rowcount the
		// caller sees is therefore taken explicitly from the UPDATE alone, below.
		var sql = $"""
			SET NOCOUNT ON;
			SET XACT_ABORT ON;
			BEGIN TRANSACTION;

			DECLARE @HighWater bigint = NULL;

			IF @FencingToken IS NOT NULL
			BEGIN
				DECLARE @Advanced TABLE (HighWaterToken bigint);

				MERGE {fenceTableName} WITH (UPDLOCK, HOLDLOCK) AS f
				USING (SELECT @FenceScope AS OutboxTable) AS s ON (f.OutboxTable = s.OutboxTable)
				WHEN MATCHED THEN
					UPDATE SET HighWaterToken =
						CASE WHEN @FencingToken >= f.HighWaterToken THEN @FencingToken ELSE f.HighWaterToken END
				WHEN NOT MATCHED THEN
					INSERT (OutboxTable, HighWaterToken) VALUES (@FenceScope, @FencingToken)
				OUTPUT INSERTED.HighWaterToken INTO @Advanced;

				SELECT TOP (1) @HighWater = HighWaterToken FROM @Advanced;
			END

			UPDATE {tableName}
			SET Status = 2, SentAt = @SentAt, LastError = NULL,
				-- Release the lease on completion. A sent message is terminal and holds no lease, but
				-- these columns were left populated, and GetStatistics counts "Sending" as
				-- (LeasedAt IS NOT NULL) with no status predicate -- so every message ever sent stayed
				-- in that count forever and SendingMessageCount grew without bound for the life of the
				-- table, reporting long-completed messages as still in flight.
				LeasedAt = NULL, LeasedBy = NULL,
				FencingToken = CASE WHEN @FencingToken IS NULL THEN FencingToken ELSE @FencingToken END
			WHERE Id = @MessageId
				AND Status NOT IN (2, 5)
				AND (@FencingToken IS NULL OR @FencingToken >= @HighWater);

			DECLARE @Marked int = @@ROWCOUNT;

			-- Evaluated INSIDE the mutating transaction, under the fence row lock the MERGE above still
			-- holds. The caller used to establish this with a follow-up SELECT on its own connection
			-- state, which cannot distinguish "the row is gone" from "the row changed after we looked".
			DECLARE @Exists bit = CASE WHEN EXISTS (
				SELECT 1 FROM {tableName} WHERE Id = @MessageId) THEN 1 ELSE 0 END;

			COMMIT TRANSACTION;

			SELECT @HighWater AS HighWaterToken, @Marked AS UpdatedCount, @Exists AS RowExists;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@SentAt", DateTimeOffset.UtcNow);
		parameters.Add("@FencingToken", fencingToken);
		parameters.Add("@FenceScope", fenceScope);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		// QuerySingle, not ExecuteScalar: the statement now projects three values, all computed inside
		// the mutating transaction. The rowcount alone cannot tell a fencing refusal from a not-found,
		// which is the defect this shape removes.
		ResolveAsync = async connection =>
			await connection.QuerySingleAsync<MarkSentMutationResult>(Command).ConfigureAwait(false);
	}
}
