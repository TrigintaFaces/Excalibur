// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get transport deliveries for a message.
/// </summary>
/// <remarks>
/// This framework-owned request uses its fixed constructor-defined primitive input schema. Replacing a
/// known parameter value through <c>Parameters</c> is observed at execution; parameter DbType,
/// direction, size, templates and output callbacks are not an extensibility contract of this request.
/// Custom <c>IDataRequest</c> implementations retain their own execution behavior.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.ForeignKeyConfined,
	"this read is not tenant-confined, and the argument is authorization rather than exclusion. A tenant term here would not have kept a foreign row out of the result -- every row reachable through MessageId belongs to the one message named -- it would have made the result EMPTY for a caller scoped to a different tenant than the id it supplied. That is a real confinement and it is deliberately given up: the read backs the cross-tenant drain's per-transport decisions, and scoping it to an ambient tenant returned nothing for every tenanted message and stalled multi-transport delivery. A caller is therefore trusted with any message id it can name, and authorizing the caller to name it belongs to the caller")]
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

		// No tenant term. MessageId is a foreign key to the outbox table's globally-unique Id, so every row this
		// read can return belongs to that one message and therefore to one tenant. That fact does NOT justify
		// dropping the term on its own, and the distinction matters more here than on the updates: for a read
		// the term never kept a foreign row out of the result, it made the result EMPTY for a caller scoped to
		// a different tenant than the id it supplied. Dropping it gives up that confinement deliberately,
		// because the drain that consumes this read is cross-tenant by design and holds no ambient tenant, so
		// scoping the read returned nothing for every tenanted message and stalled multi-transport delivery.
		// A caller is trusted with any message id it can name. (The pair MessageId/TransportName is not itself
		// unique, so this returns a set, not a row.)
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
			var rows = await StaticDbCommand.QueryAsync(connection, Command, command =>
			{
				StaticDbCommand.AddParameter(command, "MessageId", StaticDbCommand.Input<string>(Parameters, "MessageId"), DbType.String);
			}, static reader => new TransportDeliveryRow
			{
				Id = StaticDbValue.String(reader, 0),
				MessageId = StaticDbValue.String(reader, 1) ?? string.Empty,
				TransportName = StaticDbValue.String(reader, 2) ?? string.Empty,
				Destination = StaticDbValue.String(reader, 3),
				Status = StaticDbValue.Int32(reader, 4),
				CreatedAt = StaticDbValue.DateTimeOffset(reader, 5) ?? default,
				AttemptedAt = StaticDbValue.DateTimeOffset(reader, 6),
				SentAt = StaticDbValue.DateTimeOffset(reader, 7),
				RetryCount = StaticDbValue.Int32(reader, 8),
				LastError = StaticDbValue.String(reader, 9),
				TransportMetadata = StaticDbValue.String(reader, 10),
			}).ConfigureAwait(false);
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
