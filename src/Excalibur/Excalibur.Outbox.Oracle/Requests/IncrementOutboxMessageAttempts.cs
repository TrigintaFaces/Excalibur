// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to increment the attempt count of an outbox message in the Oracle database.
/// </summary>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim mutation path: the drain has already claimed this row cross-tenant and counts an attempt against it by its globally-unique message id. This store holds no tenant context to filter by - an outbox store reads no ambient tenant context and accepts a tenant only as an explicit argument - so the statement carries no tenant term, and a caller that supplies a message id it did not obtain from a claim reaches the row behind it. Isolation on this table is established where the row is written, by stamping tenant_id")]
public sealed class IncrementOutboxMessageAttempts : DataRequest<int>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="IncrementOutboxMessageAttempts"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to increment attempts for.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public IncrementOutboxMessageAttempts(string messageId, string outboxTableName, int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1
		           WHERE message_id = :MessageId
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
