// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle.Requests;

/// <summary>
/// Buries a message under a tenure fence, in one PL/SQL block, and reports whether the fence decided the
/// outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the member where an accepted stale token does the most damage.</b> A superseded leader that
/// buries a message the live leader then delivers leaves it simultaneously DELIVERED and sitting
/// unreplayed in the dead-letter table -- so an operator draining that table re-executes work that already
/// succeeded. A failure report at least leaves a retriable row behind; this transition is terminal, and on
/// Oracle it is doubly so, because burial physically MOVES the row out of the outbox table.
/// </para>
/// <para>
/// <b>The move must be inside the fence, not merely near it.</b> Both statements sit under the same
/// <c>IF v_high = :Token</c> guard, so a refused tenure performs neither the insert nor the delete. Gating
/// only the delete would copy the row into the dead-letter table and leave the original in place -- a
/// message that is simultaneously buried and deliverable, which is worse than either outcome alone.
/// </para>
/// <para>
/// <b>It carries no claim term, deliberately.</b> The retry-ceiling transition can legitimately run after
/// the reservation has lapsed, and at that moment an ownership guard admits anyone, so it discriminates
/// nothing. The fence is the guard that still means something there: it bounds which dispatcher
/// GENERATION may bury, which is precisely the distinction the lapsed claim can no longer make. This
/// matches the unfenced statement, which relies on the row's own presence for the same reason.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox message_id is the table's primary key, so this block already addresses at most one row, and the dead-letter copy carries the row's own tenant_id forward. The drain claims across tenants and buries a row by the globally-unique message_id the claim returned, gated on the tenure fence -- which bounds the dispatcher generation, not the tenant")]
internal sealed class FencedMarkMessageDeadLettered : DataRequestBase<IDbConnection, FenceMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedMarkMessageDeadLettered"/> class.
	/// </summary>
	/// <param name="messageId">The message being buried.</param>
	/// <param name="reason">The reason recorded against the burial.</param>
	/// <param name="fencingToken">The presented tenure token.</param>
	/// <param name="outboxTableName">The qualified outbox table name.</param>
	/// <param name="deadLetterTableName">The qualified dead-letter table name.</param>
	/// <param name="fenceTableName">The qualified fence control table name.</param>
	/// <param name="sqlTimeOutSeconds">Command timeout in seconds.</param>
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
		var sql = $"""
		   DECLARE
		       v_high NUMBER;
		       v_moved NUMBER := 0;
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
		           INSERT INTO {deadLetterTableName} (message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
		           SELECT message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, :Reason
		           FROM {outboxTableName}
		           WHERE message_id = :MessageId;

		           DELETE FROM {outboxTableName} WHERE message_id = :MessageId;
		           v_moved := SQL%ROWCOUNT;
		       END IF;
		       :OutHigh := v_high;
		       :OutDeleted := v_moved;
		   END;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("Reason", reason, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("OutHigh", dbType: DbType.Int64, direction: ParameterDirection.Output);
		parameters.Add("OutDeleted", dbType: DbType.Int64, direction: ParameterDirection.Output);

		Command = new CommandDefinition(
			sql,
			new OracleDynamicParameters(parameters),
			commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async conn =>
		{
			_ = await conn.ExecuteAsync(Command).ConfigureAwait(false);
			return new FenceMutationResult
			{
				HighWaterToken = parameters.Get<long>("OutHigh"),
				DeletedCount = parameters.Get<long>("OutDeleted"),
			};
		};
	}
}
