// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get scheduled messages from the outbox.
/// </summary>
public sealed class GetScheduledMessagesRequest : DataRequestBase<IDbConnection, IEnumerable<OutboxMessageRow>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetScheduledMessagesRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="scheduledBefore">Get messages scheduled before this time.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetScheduledMessagesRequest(
		string tableName,
		DateTimeOffset scheduledBefore,
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
			FROM {tableName}
			WHERE Status = 0 -- Staged
				AND ScheduledAt IS NOT NULL
				AND ScheduledAt <= @ScheduledBefore
			ORDER BY ScheduledAt ASC
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@ScheduledBefore", scheduledBefore);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QueryAsync<OutboxMessageRow>(Command).ConfigureAwait(false);
	}
}
