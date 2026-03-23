// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to erase (tombstone) events for GDPR Article 17 compliance.
/// Nulls event payloads and sets event type to <c>$erased</c> while preserving stream sequence.
/// </summary>
internal sealed class EraseEventsRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		UPDATE EventStoreEvents
		SET EventData = NULL,
		    EventType = '$erased',
		    Metadata = @ErasureMetadata
		WHERE AggregateId = @AggregateId
		  AND AggregateType = @AggregateType
		  AND EventType <> '$erased'
		""";

	public EraseEventsRequest(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var erasureMetadata = $"{{\"erased\":true,\"erasureRequestId\":\"{erasureRequestId}\"}}";

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@ErasureMetadata", erasureMetadata);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
