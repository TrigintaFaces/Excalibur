// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to move an outbox message to the dead letter table in the Oracle database.
/// </summary>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim mutation path: the drain has already claimed this row cross-tenant and moves it by its globally-unique message id. This store holds no tenant context to filter by - an outbox store reads no ambient tenant context and accepts a tenant only as an explicit argument - so the statement carries no tenant term, and a caller that supplies a message id it did not obtain from a claim reaches the row behind it. Isolation on this table is established where the row is written, by stamping tenant_id")]
public sealed class MoveOutboxMessageToDeadLetter : DataRequest<int>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="MoveOutboxMessageToDeadLetter"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to move.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="deadLetterTableName">The name of the dead letter table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// <para>
	/// The copy-to-DLQ and delete target the globally-unique outbox <c>message_id</c>, which addresses exactly
	/// one row, so no tenant predicate is applied: the drain is cross-tenant infrastructure and must always be
	/// able to move the row it claimed, regardless of any ambient tenant context.
	/// </para>
	/// <para>
	/// The tenant is nonetheless COPIED, as provenance. The delete leaves the dead-letter row as the only
	/// remaining record of the message, so a term this statement does not carry across is destroyed rather
	/// than merely unqueryable: an operator could no longer attribute the entry, and a redrive could no
	/// longer re-enter the partition the message came from. It is copied from the outbox row, whose tenant
	/// column is total, so the value is always present and is never inferred here.
	/// </para>
	/// </remarks>
	public MoveOutboxMessageToDeadLetter(
		string messageId,
		string outboxTableName,
		string deadLetterTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// Oracle: two DML statements must run in a single PL/SQL anonymous block (no client-side ';' batching).
		// Lock the source before copying. Check archive/delete counts inside the command
		// so trigger-suppressed deletion raises and rolls back the entire terminal transition.
		var sql = $"""
		   DECLARE
		   v_inserted NUMBER;
		   v_deleted NUMBER;
		   v_moved NUMBER := 0;
		   BEGIN
		   FOR src IN (SELECT message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts FROM {outboxTableName}
		   WHERE message_id = :MessageId FOR UPDATE) LOOP
		   INSERT INTO {deadLetterTableName} (message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
		   VALUES (src.message_id, src.tenant_id, src.message_type, src.message_metadata, src.message_body,
		   src.occurred_on, src.attempts, :ErrorMessage);
		   v_inserted := SQL%ROWCOUNT;
		   IF v_inserted = 1 THEN
		   DELETE FROM {outboxTableName} WHERE message_id = src.message_id;
		   v_deleted := SQL%ROWCOUNT;
		   IF v_deleted != v_inserted THEN
		   RAISE_APPLICATION_ERROR(-20001, 'Dead-letter archive/delete count mismatch');
		   END IF;
		   v_moved := v_inserted;
		   ELSIF v_inserted != 0 THEN
		   RAISE_APPLICATION_ERROR(-20001, 'Unexpected dead-letter archive count');
		   END IF;
		   END LOOP;
		   :MovedCount := v_moved;
		   END;
		   """;

		// Anonymous PL/SQL blocks return -1 from ExecuteNonQuery; export the actual move count.
        var parameters = new DynamicParameters();
        parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
        parameters.Add("ErrorMessage", string.Empty, direction: ParameterDirection.Input);
		parameters.Add("MovedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(parameters),
			commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			_ = await conn.ExecuteAsync(Command).ConfigureAwait(false);
			return parameters.Get<int>("MovedCount");
		};
	}
}
