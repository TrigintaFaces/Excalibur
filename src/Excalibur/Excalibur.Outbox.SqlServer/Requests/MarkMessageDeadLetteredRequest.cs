// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to transition a message to the terminal <c>DeadLettered</c> status in the outbox after its
/// retry policy is exhausted.
/// </summary>
/// <remarks>
/// Sets <c>Status = 5</c> (<c>OutboxStatus.DeadLettered</c>) and clears the delivery lease so the message is
/// structurally excluded from every claim predicate and can never be re-claimed or re-dead-lettered.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox Id is the table's primary key, so this statement already addresses at most one row. The drain claims across tenants and hands back a row addressed by that globally-unique Id, so the mark must be able to address the row the claim returned; a tenant term could only subtract that row, never redirect the statement to a different one")]
internal sealed class MarkMessageDeadLetteredRequest : DataRequestBase<IDbConnection, ClaimMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkMessageDeadLetteredRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageId">The message ID to dead-letter.</param>
	/// <param name="reason">The reason the message was dead-lettered.</param>
	/// <param name="attemptCount">The observed logical delivery attempt count.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// The mark targets the globally-unique outbox <c>Id</c>, which addresses exactly one row, so no tenant
	/// predicate is applied: the drain is cross-tenant infrastructure and must always be able to mark the row
	/// it claimed, regardless of any ambient tenant context. Tenant isolation lives on the write/stage path
	/// (<c>TenantId</c> stamping) and on tenant-facing queries.
	/// </remarks>
	public MarkMessageDeadLetteredRequest(
		string tableName,
		string messageId,
		string reason,
		int attemptCount,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);

		// Status 5 = DeadLettered (terminal). Clear the lease so a stale-lease sweep cannot resurrect it.
		//
		// The terminal exclusion is NOT redundant with that lease clear -- it is required BECAUSE of it.
		// Nulling LeasedBy removes the only evidence a later completion could be judged against, so from
		// that moment an ownership guard admits anyone. Status is the sole remaining discriminator, and
		// without this term a second dead-letter writes another DLQ row for one message, and a message
		// already SENT can be recorded as retry-exhausted.
		// The three discriminators come back in the SAME batch as the mutation. A row count alone cannot
		// say WHY nothing was updated: the WHERE refuses an absent row and an already-terminal row alike,
		// and the caller's correct response differs -- a terminal row means someone else already finished
		// this message, an absent row means the dead-letter entry it just wrote describes nothing. Probing
		// in a second round trip would answer about a row that may have moved in between.
		var sql = $"""
			SET NOCOUNT ON;

			DECLARE @Updated INT;

			UPDATE {tableName}
			SET Status = 5, LastError = @Reason, RetryCount = CASE WHEN RetryCount < @AttemptCount THEN @AttemptCount ELSE RetryCount END,
			    LastAttemptAt = @LastAttemptAt, LeasedAt = NULL, LeasedBy = NULL
			WHERE Id = @MessageId
			  AND Status NOT IN (2, 5);

			SET @Updated = @@ROWCOUNT;

			SELECT
				@Updated AS UpdatedCount,
				CASE WHEN EXISTS (SELECT 1 FROM {tableName} WHERE Id = @MessageId) THEN 1 ELSE 0 END AS RowExists,
				CASE WHEN EXISTS (SELECT 1 FROM {tableName} WHERE Id = @MessageId AND Status IN (2, 5)) THEN 1 ELSE 0 END AS IsTerminal;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@Reason", reason);
		parameters.Add("@AttemptCount", attemptCount, DbType.Int32);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QuerySingleAsync<ClaimMutationResult>(Command).ConfigureAwait(false);
	}
}
