// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get transport deliveries for a message.
/// </summary>
public sealed class GetTransportDeliveriesRequest : DataRequestBase<IDbConnection, IEnumerable<OutboundMessageTransport>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetTransportDeliveriesRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified transports table name.</param>
	/// <param name="messageId">The message ID to get deliveries for.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetTransportDeliveriesRequest(
		string tableName,
		string messageId,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var sql = $"""
			SELECT Id, MessageId, TransportName, Destination, Status, CreatedAt, AttemptedAt, SentAt,
				   RetryCount, LastError, TransportMetadata
			FROM {tableName}
			WHERE MessageId = @MessageId
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var rows = await connection.QueryAsync<TransportDeliveryRow>(Command).ConfigureAwait(false);
			return rows.Select(MapRowToTransport);
		};
	}

	private static OutboundMessageTransport MapRowToTransport(TransportDeliveryRow row)
	{
		return new OutboundMessageTransport
		{
			Id = row.Id ?? string.Empty,
			MessageId = row.MessageId,
			TransportName = row.TransportName,
			Destination = row.Destination,
			Status = (TransportDeliveryStatus)row.Status,
			CreatedAt = row.CreatedAt,
			AttemptedAt = row.AttemptedAt,
			SentAt = row.SentAt,
			RetryCount = row.RetryCount,
			LastError = row.LastError,
			TransportMetadata = row.TransportMetadata
		};
	}

	private sealed class TransportDeliveryRow
	{
		public string? Id { get; set; }
		public string MessageId { get; set; } = string.Empty;
		public string TransportName { get; set; } = string.Empty;
		public string? Destination { get; set; }
		public int Status { get; set; }
		public DateTimeOffset CreatedAt { get; set; }
		public DateTimeOffset? AttemptedAt { get; set; }
		public DateTimeOffset? SentAt { get; set; }
		public int RetryCount { get; set; }
		public string? LastError { get; set; }
		public string? TransportMetadata { get; set; }
	}
}
