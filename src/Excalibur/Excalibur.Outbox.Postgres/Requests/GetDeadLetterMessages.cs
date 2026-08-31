// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Represents a data request to query failed messages from the Postgres dead letter table.
/// </summary>
/// <remarks>
/// Queries messages that have been moved to the dead letter table due to exceeding the
/// maximum retry count. Supports pagination via LIMIT/OFFSET and filtering by retry count
/// and timestamp.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"an operator-facing dead-letter query: it reads the dead-letter table estate-wide for diagnosis and redrive, not on behalf of a tenant. Its reach is bounded by the retry-count, age and batch arguments it is given, never by tenant state")]
public sealed class GetDeadLetterMessages : DataRequest<IEnumerable<DeadLetterRecord>>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="GetDeadLetterMessages"/> class.
	/// </summary>
	/// <param name="deadLetterTableName">The fully qualified dead letter table name.</param>
	/// <param name="maxRetries">Only return messages with attempts exceeding this count.</param>
	/// <param name="olderThan">Only return messages that occurred before this timestamp. Pass null to skip time filter.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="offset">Number of records to skip for pagination.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetDeadLetterMessages(
		string deadLetterTableName,
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
		          error_message AS ErrorMessage
		   FROM {deadLetterTableName}
		   WHERE attempts > @MaxRetries
		   """;

		if (olderThan.HasValue)
		{
			sql += " AND occurred_on < @OlderThan";
		}

		sql += " ORDER BY occurred_on ASC LIMIT @BatchSize OFFSET @Offset";

		var parameters = new DynamicParameters();
		parameters.Add("MaxRetries", maxRetries, direction: ParameterDirection.Input);
		parameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);
		parameters.Add("Offset", offset, direction: ParameterDirection.Input);

		if (olderThan.HasValue)
		{
			parameters.Add("OlderThan", olderThan.Value, direction: ParameterDirection.Input);
		}

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<DeadLetterRecord>(Command).ConfigureAwait(false);
	}
}

/// <summary>
/// Represents a record from the dead letter table.
/// </summary>
public sealed class DeadLetterRecord
{
	/// <summary>
	/// Gets or sets the message identifier.
	/// </summary>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the tenant the message originated in, carried as provenance so a replay re-enters the
	/// same tenant.
	/// </summary>
	/// <value>
	/// The originating tenant. A message that was staged without a tenant carries the reserved untenanted
	/// key rather than an empty or absent value, so this is never <see langword="null"/> for a row written
	/// by a current version of the store.
	/// </value>
	/// <remarks>
	/// Rows written before the dead-letter table carried a tenant column read back as the reserved
	/// untenanted key after the upgrade script runs, because their originating tenant was never recorded
	/// and cannot be recovered — the move deletes the source row. Treat this value as attribution only for
	/// entries created after that upgrade.
	/// </remarks>
	public string TenantId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	public string MessageMetadata { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized message body as raw bytes.
	/// </summary>
	public byte[] MessageBody { get; set; } = [];

	/// <summary>
	/// Gets or sets when the message was created.
	/// </summary>
	public DateTimeOffset OccurredOn { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts.
	/// </summary>
	public int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the error message from the last failure.
	/// </summary>
	public string? ErrorMessage { get; set; }
}
