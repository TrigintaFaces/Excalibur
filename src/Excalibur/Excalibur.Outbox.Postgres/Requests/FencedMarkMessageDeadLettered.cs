// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Moves a message to the dead-letter table and removes it from the outbox, but only if the presenting
/// tenure is still the current one — fence, copy and delete in a single statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this member needed a fence more urgently than any other.</b> The unfenced form matched on
/// <c>message_id</c> alone and performed a copy followed by a delete, so a superseded leader reaching its
/// attempt ceiling destroyed an outbox row that a live successor still held and had not yet delivered.
/// Every other completion this store performs leaves the row recoverable; this one does not, which is what
/// turns a handover artifact into a lost message.
/// </para>
/// <para>
/// <b>The fence step WRITES, and both mutations are conditioned on that write accepting the token.</b> The
/// fence common-table-expression upserts the per-scope high-water to the greater of the stored and
/// presented values under that row's lock, and returns it. The copy is gated on the returned high-water
/// equalling the presented token; the delete is gated on the copy having happened. A superseded tenure
/// observes a returned high-water strictly greater than its own, copies nothing, and deletes nothing.
/// Because all three execute in one statement under the fence row lock, no fresher tenure can interleave
/// between the check and the destruction.
/// </para>
/// <para>
/// <b>The delete is gated on the copy, not merely on the fence, and that ordering is load-bearing.</b>
/// After the delete the dead-letter row is the only remaining record of the message. A delete that could
/// proceed when the copy did not would destroy the message outright rather than relocate it — a strictly
/// worse outcome than the defect this statement exists to close.
/// </para>
/// <para>
/// <b>No claim term, by ruling.</b> The harm here is cross-tenure, and a fencing token refuses exactly
/// that. A per-claim identity would additionally refuse a stale cycle of the same process, which is a
/// different failure and is not what makes this path lossy — and requiring it would restrict the fix to
/// the single store that records such an identity.
/// </para>
/// <para>
/// <b>The tenant is COPIED as provenance, not used as a predicate.</b> The message id is globally unique
/// and addresses exactly one row, so a tenant term could only turn the correct row into zero rows. But the
/// delete leaves the dead-letter entry as the sole record, so a column this statement fails to carry
/// across is destroyed rather than merely unqueryable.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the terminal transition, fenced: the drain has already claimed this row cross-tenant and relocates it by its globally-unique message id, gated on the scope fence high-water. The fence bounds which dispatcher generation may destroy the row, not which tenant. This store holds no tenant context to filter by, so the statement carries no tenant term; the tenant is carried across to the dead-letter row as provenance, because the delete makes that row the only surviving record")]
internal sealed class FencedMarkMessageDeadLettered : DataRequest<FencedClaimMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedMarkMessageDeadLettered"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to dead-letter.</param>
	/// <param name="reason">The reason recorded against the dead-lettered message.</param>
	/// <param name="fencingToken">The caller's leadership tenure token.</param>
	/// <param name="outboxTableName">The qualified name of the outbox table.</param>
	/// <param name="deadLetterTableName">The qualified name of the dead-letter table.</param>
	/// <param name="fenceTableName">The qualified name of the fence control table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedMarkMessageDeadLettered(
		string messageId,
		string reason,
		long fencingToken,
		string outboxTableName,
		string deadLetterTableName,
		string fenceTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ArgumentException.ThrowIfNullOrWhiteSpace(outboxTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceTableName);

		var sql = $"""
		   WITH fence AS (
		           INSERT INTO {fenceTableName} AS f (scope_key, high_water_token)
		               VALUES (@ScopeKey, @Token)
		               ON CONFLICT (scope_key) DO UPDATE
		                   SET high_water_token = GREATEST(f.high_water_token, @Token)
		               RETURNING high_water_token
		           ),
		           moved AS (
		               INSERT INTO {deadLetterTableName} (message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
		               SELECT message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, @Reason
		               FROM {outboxTableName}
		               WHERE message_id = @MessageId
		                 AND (SELECT high_water_token FROM fence) = @Token
		               RETURNING message_id
		           ),
		           del AS (
		               DELETE FROM {outboxTableName}
		               WHERE message_id = @MessageId
		                 AND EXISTS (SELECT 1 FROM moved)
		               RETURNING message_id
		           )
		           SELECT (SELECT high_water_token FROM fence) AS HighWaterToken,
		                  (SELECT COUNT(*) FROM del)::int AS UpdatedCount,
		                  EXISTS (SELECT 1 FROM {outboxTableName} WHERE message_id = @MessageId) AS RowExists;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("Reason", reason, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleAsync<FencedClaimMutationResult>(Command).ConfigureAwait(false);
	}
}
