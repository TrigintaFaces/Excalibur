// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Records a failed delivery attempt on a main-table outbox message: sets the authoritative retry count
/// and the last error on the row so a sub-ceiling failure is trackable by <c>GetFailedMessages</c> /
/// <c>GetStatistics</c> without a status column (design-preserving). Distinct from dead-lettering, which is
/// the terminal move at the retry ceiling.
/// </summary>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim mutation path: the drain has already claimed this row cross-tenant and marks failed it by its globally-unique message id. This store holds no tenant context to filter by - an outbox store reads no ambient tenant context and accepts a tenant only as an explicit argument - so the statement carries no tenant term, and a caller that supplies a message id it did not obtain from a claim reaches the row behind it. Isolation on this table is established where the row is written, by stamping tenant_id")]
internal sealed class SetOutboxMessageFailed : DataRequest<int>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="SetOutboxMessageFailed"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the failed message.</param>
	/// <param name="retryCount">
	/// The retry-attempt count reported by the caller. Applied as <c>GREATEST(attempts, :RetryCount)</c> so the
	/// persisted count is non-decreasing across re-claims and a stale lower report cannot weaken termination.
	/// </param>
	/// <param name="errorMessage">The error describing the failure, stored as the message's last error.</param>
	/// <param name="dispatcherId">
	/// The identifier of the dispatcher reporting the failure. The update applies only when the message is
	/// unreserved or reserved by this same dispatcher, so a caller can never clear a reservation it does not hold.
	/// </param>
	/// <param name="failureBackoffFloorSeconds">
	/// The failure-anchored visibility floor F, in seconds. The message becomes re-claimable only after
	/// <c>SYSTIMESTAMP + F</c>; F must exceed the poll interval so the plain failure path cannot hot-loop the drain.
	/// </param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SetOutboxMessageFailed(
		string messageId,
		int retryCount,
		string errorMessage,
		string dispatcherId,
		int failureBackoffFloorSeconds,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// Canonical MarkFailedAsync re-claimability contract: a failed (sub-ceiling) message is neither
		// terminal nor immediately re-claimable. Stamp a failure-anchored visibility floor
		// next_attempt_at = SYSTIMESTAMP + F (the SERVER clock — never a dispatcher-side timestamp), so the
		// claim predicate (next_attempt_at <= SYSTIMESTAMP) defers re-delivery by at least F. F exceeds the
		// poll interval, so the plain (no-computed-backoff) path cannot hot-loop the drain in the same cycle;
		// fine-grained backoff remains SetOutboxMessageBackoff. The floor is applied on the reserved AND the
		// unreserved-input path (stage-then-fail-without-claim, dispatcher_timeout IS NULL) — closing the
		// zero-backoff hole where "let the reservation expire" would give floor = 0. Freeing the reservation
		// is safe because the floor, not the lease, now governs the next claim. Mirrors the Postgres fix +
		// the InMemory reference.
		//
		// attempts = GREATEST(attempts, :RetryCount): attempts are non-decreasing across re-claims, so a stale
		// late failure report with a lower count cannot lower the authoritative value and weaken the
		// processor's DLQ-ceiling (termination) guarantee.
		//
		// The reservation-ownership guard (unreserved, or held by THIS dispatcher) makes reservation-theft
		// unrepresentable: a failure reported against a lease a DIFFERENT dispatcher now holds matches no row
		// and is a no-op (no double-concurrent-delivery). The IS NULL arm is load-bearing — staging a message
		// and reporting it failed without ever reserving it is a supported path, floored the same way.
		//
		// Ownership is an EXACT-PREFIX match on the per-call claim token: the reserve path stamps
		// dispatcher_id = "{dispatcherId}:{guid}" (unique per claim to bound the select-back), so a bare
		// "dispatcher_id = :DispatcherId" matched ZERO rows and this whole update was a silent no-op on Oracle.
		// Match the process-stable dispatcher prefix exactly via SUBSTR(dispatcher_id, 1, LEN) = "{dispatcherId}:".
		// The prefix and its length are computed in C# (not SQL LENGTH()) so each parameter is referenced ONCE
		// (ODP.NET positional bind + DynamicParameters cannot re-add a name) AND no LIKE wildcard is involved —
		// MachineName may contain '_'/'%', which a LIKE prefix would treat as wildcards and widen the guard.
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = GREATEST(attempts, :RetryCount),
		               error_message = :ErrorMessage,
		               dispatcher_id = NULL,
		               dispatcher_timeout = NULL,
		               next_attempt_at = SYSTIMESTAMP + NUMTODSINTERVAL(:FailureBackoffFloorSeconds, 'SECOND')
		           WHERE message_id = :MessageId
		             AND (dispatcher_id IS NULL OR SUBSTR(dispatcher_id, 1, :DispatcherPrefixLen) = :DispatcherPrefix)
		   """;

		// ODP.NET binds positionally (this store does not set BindByName), so parameters are added in the
		// exact textual order their placeholders appear: :RetryCount, :ErrorMessage, :FailureBackoffFloorSeconds,
		// :MessageId, :DispatcherPrefixLen, :DispatcherPrefix. Literal NULLs keep dispatcher_id/dispatcher_timeout
		// out of the bind list. Adding a parameter out of this order would silently bind it to the wrong
		// placeholder — the order below mirrors the statement above and must stay that way.
		var parameters = new DynamicParameters();
		parameters.Add("RetryCount", retryCount, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);
		parameters.Add("FailureBackoffFloorSeconds", failureBackoffFloorSeconds, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefixLen", dispatcherId.Length + 1, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefix", dispatcherId + ":", direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
