// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to query failed-but-retryable messages from the Oracle <b>main</b> outbox
/// table.
/// </summary>
/// <remarks>
/// A "failed" message is a main-table row that recorded a delivery error (<c>error_message</c> is set) and
/// whose retry count is still within the ceiling (<c>attempts BETWEEN 1 AND :MaxRetries</c>) — i.e. failed
/// but not yet dead-lettered. This is design-preserving: it reads the main table (sent messages are deleted,
/// so presence + a recorded error is the "failed, retryable" signal) rather than the terminal dead-letter
/// table, which only holds messages that exceeded the ceiling.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"an operator-facing failure query: it reports messages carrying an error across the whole store, on behalf of whoever is diagnosing delivery rather than on behalf of a tenant. Its reach is bounded by the retry-count, age and batch arguments it is given, never by tenant state")]
internal sealed class GetFailedOutboxMessages : DataRequest<IEnumerable<DeadLetterRecord>>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="GetFailedOutboxMessages"/> class.
	/// </summary>
	/// <param name="outboxTableName">The fully qualified main outbox table name.</param>
	/// <param name="maxRetries">Only return failed messages whose attempt count is at most this ceiling.</param>
	/// <param name="olderThan">Only return messages that occurred before this timestamp. Pass null to skip the time filter.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="offset">Number of records to skip for pagination.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetFailedOutboxMessages(
		string outboxTableName,
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		int offset,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   SELECT message_id AS MessageId,
		          tenant_id AS TenantId,
		          message_type AS MessageType,
		          message_metadata AS MessageMetadata,
		          message_body AS MessageBody,
		          occurred_on AS OccurredOn,
		          attempts AS Attempts,
		          error_message AS ErrorMessage,
		          priority AS Priority
		   FROM {outboxTableName}
		   WHERE error_message IS NOT NULL
		     AND attempts BETWEEN 1 AND :MaxRetries
		   """;

		if (olderThan.HasValue)
		{
			sql += " AND occurred_on < :OlderThan";
		}

		sql += " ORDER BY occurred_on ASC OFFSET :Offset ROWS FETCH NEXT :BatchSize ROWS ONLY";

		// ODP.NET binds positionally (this store does not set BindByName), so parameters are added in the
		// exact textual order their placeholders appear: :MaxRetries, then :OlderThan (when present), then
		// :Offset, then :BatchSize.
		var parameters = new DynamicParameters();
		parameters.Add("MaxRetries", maxRetries, direction: ParameterDirection.Input);

		if (olderThan.HasValue)
		{
			parameters.Add("OlderThan", olderThan.Value, direction: ParameterDirection.Input);
		}

		parameters.Add("Offset", offset, direction: ParameterDirection.Input);
		parameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<DeadLetterRecord>(Command).ConfigureAwait(false);
	}
}
