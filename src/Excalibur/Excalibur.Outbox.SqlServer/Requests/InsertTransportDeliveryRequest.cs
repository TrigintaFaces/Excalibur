// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to insert a transport delivery record.
/// </summary>
public sealed class InsertTransportDeliveryRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsertTransportDeliveryRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified transports table name.</param>
	/// <param name="delivery">The transport delivery to insert.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public InsertTransportDeliveryRequest(
		string tableName,
		OutboundMessageTransport delivery,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(delivery);

		var sql = $"""
			INSERT INTO {tableName}
				(Id, MessageId, TransportName, Destination, Status, CreatedAt, RetryCount, TransportMetadata)
			VALUES
				(@Id, @MessageId, @TransportName, @Destination, @Status, @CreatedAt, @RetryCount, @TransportMetadata)
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", delivery.Id);
		parameters.Add("@MessageId", delivery.MessageId);
		parameters.Add("@TransportName", delivery.TransportName);
		parameters.Add("@Destination", delivery.Destination);
		parameters.Add("@Status", (int)delivery.Status);
		parameters.Add("@CreatedAt", delivery.CreatedAt);
		parameters.Add("@RetryCount", delivery.RetryCount);
		parameters.Add("@TransportMetadata", delivery.TransportMetadata);

		Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
