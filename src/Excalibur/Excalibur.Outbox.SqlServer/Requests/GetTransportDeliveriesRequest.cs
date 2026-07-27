// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

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
	/// <param name="tenant">
	/// The partition whose rows the caller may see. Supplied by the store from its tenant context rather than
	/// read here, following the same shape as the dead-letter queue in this package.
	/// </param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetTransportDeliveriesRequest(
		string tableName,
		string messageId,
		KeyedTenantPartition tenant,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(tenant);

		// The tenant term is evaluated BY THE DATABASE, not by filtering the returned rows: a caller scoped to
		// one tenant supplying another tenant's MessageId must receive nothing from the engine, rather than
		// receive the rows and have them dropped afterwards.
		//
		// This predicate lives in the provider store rather than in a decorator, deliberately and per the
		// tenant-ownership manifest: the outbox drain is intentionally cross-tenant, so a tenant-scoping
		// decorator would read the ambient tenant as absent and stall it. The inbox resolves the same tension
		// the same way — the predicate goes inside the provider store.
		//
		// COALESCE covers rows written before the transports table carried its own tenant column: those hold
		// NULL, and NULL = @TenantId is never true, so a bare equality would hide legacy rows from every
		// tenant instead of showing them to their owner.
		const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

		var sql = $"""
			SELECT Id, MessageId, TransportName, Destination, Status, CreatedAt, AttemptedAt, SentAt,
				   RetryCount, LastError, TransportMetadata
			FROM {tableName}
			WHERE MessageId = @MessageId{tenantPredicate}
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@TenantId", tenant.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

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
