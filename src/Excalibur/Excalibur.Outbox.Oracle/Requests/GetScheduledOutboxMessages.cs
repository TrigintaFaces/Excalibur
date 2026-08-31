// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to retrieve scheduled outbox messages that are due for delivery.
/// </summary>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the scheduled-message sweep feeds the same cross-tenant drain: it selects rows whose scheduled time has arrived across every tenant and returns each row's tenant_id so the processor can re-establish the owning tenant. Its reach is bounded by the schedule and batch arguments, never by tenant state")]
internal sealed class GetScheduledOutboxMessages : DataRequest<IEnumerable<OutboundMessage>>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="GetScheduledOutboxMessages"/> class.
	/// </summary>
	/// <param name="cutoff"> Only return messages scheduled at or before this time. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="outboxTableName"> The qualified outbox table name. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "JSON deserialization of stored message metadata into a header dictionary mirrors the reserved-message reload path; consumers requiring AOT should use source-generated serialization.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "JSON deserialization of stored message metadata requires runtime code generation; mirrors the reserved-message reload path.")]
	public GetScheduledOutboxMessages(
		DateTimeOffset cutoff,
		int batchSize,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			SELECT message_id AS MessageId, message_type AS MessageType, message_metadata AS MessageMetadata,
			       message_body AS MessageBody, tenant_id AS TenantId, destination AS Destination, occurred_on AS OccurredOn, scheduled_at AS ScheduledAt,
			       attempts AS Attempts
			FROM {outboxTableName}
			WHERE scheduled_at IS NOT NULL
			  AND scheduled_at <= :Cutoff
			  AND dispatcher_id IS NULL
			ORDER BY scheduled_at ASC
			FETCH FIRST :BatchSize ROWS ONLY
			""";

		var parameters = new DynamicParameters();
		parameters.Add("Cutoff", cutoff, direction: ParameterDirection.Input);
		parameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async conn =>
		{
			var rows = await conn.QueryAsync<ScheduledOutboxRow>(Command).ConfigureAwait(false);

			return rows.Select(static row => new OutboundMessage
			{
				Id = row.MessageId,
				MessageType = row.MessageType,
				Payload = row.MessageBody ?? [],
				TenantId = row.TenantId,
				Destination = row.Destination ?? string.Empty,
				CreatedAt = row.OccurredOn,
				ScheduledAt = row.ScheduledAt,
				Status = OutboxStatus.Staged,

				// The attempt count must survive a scheduled reload, or a repeatedly failing scheduled
				// message restarts at zero on every poll and never reaches the dead-letter ceiling.
				RetryCount = row.Attempts,

				// Rehydrate the persisted metadata into Headers so scheduled-reload preserves headers
				// (correlation/trace/etc.) rather than dropping them — mirrors the reserved-message path.
				Headers = string.IsNullOrEmpty(row.MessageMetadata)
					? new Dictionary<string, object>(StringComparer.Ordinal)
					: System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(row.MessageMetadata, Excalibur.Dispatch.EventSerializationDefaults.Canonical)
					  ?? new Dictionary<string, object>(StringComparer.Ordinal),
			});
		};
	}

	/// <summary>
	/// Internal row type for Dapper mapping.
	/// </summary>
	internal sealed class ScheduledOutboxRow
	{
		// ReSharper disable UnusedAutoPropertyAccessor.Local
		public string MessageId { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public string? MessageMetadata { get; set; }
		public byte[]? MessageBody { get; set; }
		public string? TenantId { get; set; }
		public string? Destination { get; set; }
		public DateTimeOffset OccurredOn { get; set; }
		public DateTimeOffset? ScheduledAt { get; set; }
		public int Attempts { get; set; }
		// ReSharper restore UnusedAutoPropertyAccessor.Local
	}
}
