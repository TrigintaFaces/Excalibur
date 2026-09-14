// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle.Requests;

/// <summary>
/// Records a delivery failure under BOTH a tenure fence and the claim the row was handed under, in one
/// PL/SQL block, and reports which of those two gates decided the outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both gates, and neither subsumes the other.</b> The fence answers "is this dispatcher generation
/// still the leader?"; the claim answers "is this the reservation the row was handed under?". A statement
/// carrying only the fence admits a stale cycle of a live tenure; one carrying only the claim admits a
/// superseded leader whose claim happens to still be stamped on the row.
/// </para>
/// <para>
/// <b>The claim term is an EXACT match, not the process prefix.</b> The unfenced path matches
/// <c>SUBSTR(dispatcher_id, 1, LEN)</c> against the process-stable prefix, because it has no better
/// evidence available: a bare equality there matched zero rows and made the whole update a silent no-op,
/// since the claim stamps <c>{dispatcherId}:{guid}</c> per reservation. This member is handed the full
/// claim identity, so it can assert the stronger thing, and does.
/// </para>
/// <para>
/// <b>The fence block mirrors <see cref="FencedDeleteOutboxMessage"/> deliberately</b> -- the same MERGE,
/// the same <c>GREATEST</c> monotonic advance, and the same <c>DUP_VAL_ON_INDEX</c> fallback for the race
/// where two tenures create the scope row at once -- so the two paths cannot drift on the fencing
/// discipline. Advancing to the presented token is monotonic, so a superseded token leaves the recorded
/// value untouched and the comparison below reports the refusal rather than silently doing nothing.
/// </para>
/// <para>
/// <b>Named binding.</b> <c>OracleDynamicParameters</c> sets <c>BindByName</c>, so each value is supplied
/// once regardless of how many times its placeholder appears in the block. The positional-order discipline
/// the unfenced statements in this package must observe does not apply here.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox message_id is the table's primary key, so this block already addresses at most one row. The drain claims across tenants and completes a row by the globally-unique message_id the claim returned, gated on the tenure fence and on the claim identity stamped at reservation -- those bound which dispatcher generation and which claim may act, not which tenant. A tenant term could only subtract the row the claim returned, never redirect the block to a different one")]
internal sealed class FencedSetOutboxMessageFailed : DataRequestBase<IDbConnection, FencedClaimMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedSetOutboxMessageFailed"/> class.
	/// </summary>
	/// <param name="messageId">The message whose failure is being recorded.</param>
	/// <param name="errorMessage">The failure detail to record.</param>
	/// <param name="retryCount">The attempt count this failure represents.</param>
	/// <param name="floorSeconds">The minimum backoff floor applied to the next attempt.</param>
	/// <param name="claimIdentity">The claim identity stamped on the row at reservation.</param>
	/// <param name="fencingToken">The presented tenure token.</param>
	/// <param name="outboxTableName">The qualified outbox table name.</param>
	/// <param name="fenceTableName">The qualified fence control table name.</param>
	/// <param name="sqlTimeOutSeconds">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedSetOutboxMessageFailed(
		string messageId,
		string errorMessage,
		int retryCount,
		int floorSeconds,
		string claimIdentity,
		long fencingToken,
		string outboxTableName,
		string fenceTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// v_exists answers a question the row count cannot: a zero update is either "the claim moved on"
		// or "the row is gone", and collapsing those is what made the unfenced path unreadable.
		var sql = $"""
		   DECLARE
		       v_high NUMBER;
		       v_updated NUMBER := 0;
		       v_exists NUMBER := 0;
		   BEGIN
		       BEGIN
		           MERGE INTO {fenceTableName} f
		           USING (SELECT :ScopeKey AS scope_key, :Token AS tok FROM dual) src
		           ON (f.scope_key = src.scope_key)
		           WHEN MATCHED THEN UPDATE SET f.high_water_token = GREATEST(f.high_water_token, src.tok)
		           WHEN NOT MATCHED THEN INSERT (scope_key, high_water_token) VALUES (src.scope_key, src.tok);
		       EXCEPTION
		           WHEN DUP_VAL_ON_INDEX THEN
		               UPDATE {fenceTableName} SET high_water_token = GREATEST(high_water_token, :Token) WHERE scope_key = :ScopeKey;
		       END;
		       SELECT high_water_token INTO v_high FROM {fenceTableName} WHERE scope_key = :ScopeKey;
		       IF v_high = :Token THEN
		           UPDATE {outboxTableName}
		               SET attempts = GREATEST(attempts, :RetryCount),
		                   error_message = :ErrorMessage,
		                   dispatcher_id = NULL,
		                   dispatcher_timeout = NULL,
		                   next_attempt_at = SYSTIMESTAMP + NUMTODSINTERVAL(:FloorSeconds, 'SECOND')
		               WHERE message_id = :MessageId
		                 AND dispatcher_id = :ClaimIdentity;
		           v_updated := SQL%ROWCOUNT;
		       END IF;
		       SELECT COUNT(*) INTO v_exists FROM {outboxTableName} WHERE message_id = :MessageId;
		       :OutHigh := v_high;
		       :OutUpdated := v_updated;
		       :OutExists := v_exists;
		   END;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("RetryCount", retryCount, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);
		parameters.Add("FloorSeconds", floorSeconds, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("ClaimIdentity", claimIdentity, direction: ParameterDirection.Input);
		parameters.Add("OutHigh", dbType: DbType.Int64, direction: ParameterDirection.Output);
		parameters.Add("OutUpdated", dbType: DbType.Int64, direction: ParameterDirection.Output);
		parameters.Add("OutExists", dbType: DbType.Int64, direction: ParameterDirection.Output);

		Command = new CommandDefinition(
			sql,
			new OracleDynamicParameters(parameters),
			commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async conn =>
		{
			_ = await conn.ExecuteAsync(Command).ConfigureAwait(false);
			return new FencedClaimMutationResult
			{
				HighWaterToken = parameters.Get<long>("OutHigh"),
				UpdatedCount = (int)parameters.Get<long>("OutUpdated"),
				RowExists = parameters.Get<long>("OutExists") > 0,
			};
		};
	}
}
