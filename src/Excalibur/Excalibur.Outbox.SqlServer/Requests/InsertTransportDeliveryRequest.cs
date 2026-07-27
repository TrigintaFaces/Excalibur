// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

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
	/// <param name="tenantId">
	/// The tenant term of the parent outbox message. Supplied by the caller rather than read from ambient
	/// context: the store deliberately reads no ambient tenant, and taking it from the parent row keeps the
	/// child's tenant identical to its parent's by construction, inside the same transaction.
	/// </param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public InsertTransportDeliveryRequest(
		string tableName,
		OutboundMessageTransport delivery,
		string? tenantId,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(delivery);

		var sql = $"""
			INSERT INTO {tableName}
				(Id, MessageId, TransportName, Destination, Status, CreatedAt, RetryCount, TransportMetadata, TenantId)
			VALUES
				(@Id, @MessageId, @TransportName, @Destination, @Status, @CreatedAt, @RetryCount, @TransportMetadata, @TenantId)
			""";

		// Route the tenant term through the keyed partition seam so the persisted column is NEVER NULL,
		// mirroring what the parent row's insert does. A NULL here would be a transport row that no
		// tenant-scoped predicate can match — unreachable data, and the fail-open shape the keyed seam exists
		// to make inexpressible. Untenanted is a named partition, not an absent tenant.
		//
		// The sibling type is named in prose deliberately without an "is"/"as" preceding it: the decorator
		// capability guard scans this directory for capability probes by regex, and a comment reading
		// "as <Type>" is indistinguishable to it from the C# operator. That guard is right to be broad;
		// the cheap side of the fix is here.
		var tenantPartition = KeyedTenantPartition.FromStoredValue(tenantId);

		var parameters = new DynamicParameters();
		parameters.Add("@TenantId", tenantPartition.TenantId);
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
