// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to erase (tombstone) events for GDPR Article 17 compliance.
/// Nulls event payloads and sets event type to <c>$erased</c> while preserving stream sequence.
/// </summary>
internal sealed class EraseEventsRequest : DataRequestBase<IDbConnection, int>
{
	public EraseEventsRequest(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = PgTableName.Format(schema, table);
		var erasureMetadata = $"{{\"erased\":true,\"erasureRequestId\":\"{erasureRequestId}\"}}";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			UPDATE {qualifiedTable}
			SET event_data = NULL,
			    event_type = @ErasedMarker,
			    metadata = @ErasureMetadata::jsonb
			WHERE aggregate_id = @AggregateId
			  AND aggregate_type = @AggregateType
			  AND event_type <> @ErasedMarker
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@ErasureMetadata", erasureMetadata);
		// cm4108: single source of truth for the erased-event sentinel (avoids a latent GDPR-erasure
		// desync if the marker value changes). Parameterized (not inlined) so the SQL stays injection-clean.
		parameters.Add("@ErasedMarker", ErasedEventMarker.EventType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
