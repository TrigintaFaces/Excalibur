// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
		var sql = $"""
		   BEGIN
		           INSERT INTO {deadLetterTableName} (message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
		           SELECT message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, :ErrorMessage
		           FROM {outboxTableName}
		           WHERE message_id = :MessageIdInsert;

		           DELETE FROM {outboxTableName}
		           WHERE message_id = :MessageIdDelete;
		   END;
		   """;

		// ODP.NET binds positionally (this store does not set BindByName), so a placeholder appearing more
		// than once consumes one parameter PER occurrence and parameters must be added in textual order.
		// The reused message_id is given a distinct name per occurrence, both bound to the same id, so the
		// block binds correctly regardless of BindByName.
		var parameters = new DynamicParameters();
		parameters.Add("ErrorMessage", string.Empty, direction: ParameterDirection.Input);
		parameters.Add("MessageIdInsert", messageId, direction: ParameterDirection.Input);
		parameters.Add("MessageIdDelete", messageId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
