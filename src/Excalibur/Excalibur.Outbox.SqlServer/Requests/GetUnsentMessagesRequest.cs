// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get unsent messages from the outbox.
/// </summary>
public sealed class GetUnsentMessagesRequest : DataRequestBase<IDbConnection, IEnumerable<OutboxMessageRow>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetUnsentMessagesRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetUnsentMessagesRequest(
		string tableName,
		int batchSize,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			SELECT TOP (@BatchSize)
				Id, MessageType, Payload, Headers, Destination, CreatedAt, ScheduledAt, SentAt,
				Status, RetryCount, LastError, LastAttemptAt, CorrelationId, CausationId,
				TenantId, Priority, TargetTransports, IsMultiTransport
			FROM {tableName} WITH (UPDLOCK, READPAST)
			WHERE Status IN (0, 3, 4) -- Staged, Failed, PartiallyFailed
				AND (ScheduledAt IS NULL OR ScheduledAt <= @Now)
			ORDER BY Priority DESC, CreatedAt ASC
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@Now", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QueryAsync<OutboxMessageRow>(Command).ConfigureAwait(false);
	}
}

/// <summary>
/// Internal record for Dapper mapping from outbox database row.
/// </summary>
public sealed class OutboxMessageRow
{
	/// <summary>The message ID.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>The message type name.</summary>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>The serialized message payload.</summary>
	public byte[] Payload { get; set; } = [];

	/// <summary>The serialized headers JSON.</summary>
	public string? Headers { get; set; }

	/// <summary>The destination.</summary>
	public string Destination { get; set; } = string.Empty;

	/// <summary>When the message was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Optional scheduled delivery time.</summary>
	public DateTimeOffset? ScheduledAt { get; set; }

	/// <summary>When the message was sent.</summary>
	public DateTimeOffset? SentAt { get; set; }

	/// <summary>Current status.</summary>
	public int Status { get; set; }

	/// <summary>Number of retry attempts.</summary>
	public int RetryCount { get; set; }

	/// <summary>Last error message if failed.</summary>
	public string? LastError { get; set; }

	/// <summary>When the last attempt was made.</summary>
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>Correlation ID for tracing.</summary>
	public string? CorrelationId { get; set; }

	/// <summary>Causation ID for tracing.</summary>
	public string? CausationId { get; set; }

	/// <summary>Tenant ID for multi-tenancy.</summary>
	public string? TenantId { get; set; }

	/// <summary>Message priority.</summary>
	public int Priority { get; set; }

	/// <summary>Target transports for multi-transport delivery.</summary>
	public string? TargetTransports { get; set; }

	/// <summary>Whether this is a multi-transport message.</summary>
	public bool IsMultiTransport { get; set; }
}
