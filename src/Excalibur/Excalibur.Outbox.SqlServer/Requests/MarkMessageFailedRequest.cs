// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a message as failed in the outbox.
/// </summary>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox Id is the table's primary key, so this statement already addresses at most one row. The drain claims across tenants and hands back a row addressed by that globally-unique Id, so the mark must be able to address the row the claim returned; a tenant term could only subtract that row, never redirect the statement to a different one")]
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
	/// BARE processor identity of the caller marking the message failed. This is deliberately NOT the value
	/// stored in <c>LeasedBy</c>: the claim stamps a per-call <c>{processorId}:{claimId}</c>, so an equality
	/// test against a bare identity matches no claimed row at all. The guard matches it by PREFIX, and the
	/// update still only affects the row when it is unleased or leased by THIS processor, so a stale
	/// processor cannot overwrite a message a peer has since re-claimed.
	/// </param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="nextAttemptAt">
	/// Optional per-message next-attempt time (the fine-grained backoff schedule). When provided, the row's
	/// <c>NextAttemptAt</c> column is set so the claim predicate excludes the message until this time elapses.
	/// COMPOSED with <paramref name="floorSeconds"/> rather than replacing it: the column receives the later of
	/// the two, so this schedule can only ever defer the next attempt beyond F, never bring it forward.
	/// </param>
	/// <param name="floorSeconds">
	/// Optional failure-anchored visibility floor F, in seconds. It applies on EVERY failure path, including
	/// the fine-grained backoff one: <c>NextAttemptAt</c> is set to at least <c>SYSUTCDATETIME() + F</c> on the
	/// server clock, so the message is re-claimable only after F — never in the same drain cycle (no hot-loop)
	/// and never terminally (at-least-once). F must exceed the poll interval. When both are
	/// <see langword="null"/>, the column is left unchanged.
	/// </param>
	/// <remarks>
	/// The mark targets the globally-unique outbox <c>Id</c>, which addresses exactly one row, so no tenant
	/// predicate is applied: the drain is cross-tenant infrastructure and must always be able to mark the row
	/// it claimed, regardless of any ambient tenant context. Tenant isolation lives on the write/stage path
	/// (<c>TenantId</c> stamping) and on tenant-facing queries. The Postgres and Oracle providers address this
	/// statement the same way, so the mark behaves identically across providers.
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

		// NextAttemptAt gates re-claim. The caller's computed schedule and the configured floor F are
		// COMPOSED, not alternatives: the column receives the later of the two, so the fine-grained backoff can
		// only push the next attempt further out than F, never pull it in. Treating the caller's value as an
		// override was the defect — it made the message re-claimable roughly a second after failure on the very
		// path production prefers, while the same failure without the capability correctly waited F.
		var nextAttemptClause = OutboxFailureMark.NextAttemptClause(nextAttemptAt.HasValue, floorSeconds.HasValue);

		// Composed from the shared fragments rather than written out here, so this path and the batch path
		// cannot drift apart on the guards or the floor again.
		var sql = $"""
			UPDATE {tableName}
			{OutboxFailureMark.SetClause}{nextAttemptClause}
			WHERE Id = @MessageId
			{OutboxFailureMark.Guards}
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@ErrorMessage", errorMessage);
		parameters.Add("@RetryCount", retryCount);
		OutboxFailureMark.AddLeaseOwnership(parameters, leasedBy);
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
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}

}
