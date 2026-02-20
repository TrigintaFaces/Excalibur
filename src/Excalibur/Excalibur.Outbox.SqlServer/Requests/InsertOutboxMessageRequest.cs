// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to insert a message into the outbox.
/// </summary>
public sealed class InsertOutboxMessageRequest : DataRequestBase<IDbConnection, int>
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	/// <summary>
	/// Initializes a new instance of the <see cref="InsertOutboxMessageRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="message">The outbound message to insert.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public InsertOutboxMessageRequest(
		string tableName,
		OutboundMessage message,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(message);

		var sql = $"""
			INSERT INTO {tableName}
				(Id, MessageType, Payload, Headers, Destination, CreatedAt, ScheduledAt, Status,
				 RetryCount, CorrelationId, CausationId, TenantId, Priority, TargetTransports, IsMultiTransport)
			VALUES
				(@Id, @MessageType, @Payload, @Headers, @Destination, @CreatedAt, @ScheduledAt, @Status,
				 @RetryCount, @CorrelationId, @CausationId, @TenantId, @Priority, @TargetTransports, @IsMultiTransport)
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", message.Id);
		parameters.Add("@MessageType", message.MessageType);
		parameters.Add("@Payload", message.Payload);
		parameters.Add("@Headers", message.Headers.Count > 0
			? JsonSerializer.Serialize(message.Headers, JsonOptions)
			: null);
		parameters.Add("@Destination", message.Destination);
		parameters.Add("@CreatedAt", message.CreatedAt);
		parameters.Add("@ScheduledAt", message.ScheduledAt);
		parameters.Add("@Status", (int)message.Status);
		parameters.Add("@RetryCount", message.RetryCount);
		parameters.Add("@CorrelationId", message.CorrelationId);
		parameters.Add("@CausationId", message.CausationId);
		parameters.Add("@TenantId", message.TenantId);
		parameters.Add("@Priority", message.Priority);
		parameters.Add("@TargetTransports", message.TargetTransports);
		parameters.Add("@IsMultiTransport", message.IsMultiTransport);

		Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
