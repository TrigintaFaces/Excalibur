// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
	/// Identifier of the processor marking the message failed (the same value written to <c>LeasedBy</c> when the
	/// row was claimed). The update only affects the row when it is unleased or still leased by this processor,
	/// so a stale processor cannot overwrite a message a peer has since re-claimed.
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
		parameters.Add("@LeasedBy", leasedBy);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);
		if (nextAttemptAt.HasValue)
		{
			parameters.Add("@NextAttemptDelayMs", ToServerDelayMilliseconds(nextAttemptAt.Value));
		}

		if (floorSeconds.HasValue)
		{
			parameters.Add("@FloorSeconds", floorSeconds.Value);
		}

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}

	/// <summary>
	/// Converts the caller's absolute next-attempt instant into the DELAY it represents, so the statement can
	/// re-anchor it on the server clock.
	/// </summary>
	/// <param name="nextAttemptAt">The next-attempt instant the caller computed from its own clock.</param>
	/// <returns>The delay in milliseconds, which is negative when the schedule has already elapsed.</returns>
	/// <remarks>
	/// <para>
	/// The caller computes this instant as "now, plus a backoff" against its OWN clock; the claim predicate
	/// reads the stored column back against the SERVER's. Binding the instant verbatim therefore straddles two
	/// clocks, and where the dispatcher runs ahead of the database the message stays invisible for the whole
	/// skew AFTER its backoff has genuinely elapsed — a due message withheld, bounded by nothing but the skew.
	/// Recovering the duration here and re-adding it to <c>SYSUTCDATETIME()</c> in the statement keeps the
	/// dispatcher's intent and puts one clock on both sides of the comparison.
	/// </para>
	/// <para>
	/// An already-elapsed schedule yields a NEGATIVE delay, which is deliberate: composed with the floor it
	/// simply loses to it, and with no floor configured it makes the message due immediately, which is what an
	/// elapsed schedule means. The value is clamped to the range <c>DATEADD</c> accepts.
	/// </para>
	/// </remarks>
	private static int ToServerDelayMilliseconds(DateTimeOffset nextAttemptAt)
	{
		var delayMs = (nextAttemptAt - DateTimeOffset.UtcNow).TotalMilliseconds;

		return delayMs <= int.MinValue
			? int.MinValue
			: delayMs >= int.MaxValue ? int.MaxValue : (int)delayMs;
	}
}
