// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Globalization;

using Dapper;

using Excalibur.Data;
using Excalibur.Data.DataProcessing;

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Data request that fetches a batch of legacy customer snapshots from SQL Server
/// using cursor-based pagination.
/// </summary>
/// <remarks>
/// Demonstrates the <see cref="IDataRequest{TConnection, TModel}"/> pattern with Dapper
/// for cursor-paginated batch reads from a legacy database table.
/// The cursor is the <c>ChangedAtUtc</c> timestamp of the last record in the previous batch.
/// </remarks>
public sealed class FetchLegacyCustomerSnapshotsRequest
	: DataRequestBase<IDbConnection, CursorFetchResult<LegacyCustomerSnapshot>>
{
	private const string SqlWithCursor = """
		SELECT TOP (@BatchSize) ExternalId, Name, Email, Phone, ChangedAtUtc
		FROM LegacyCustomers
		WHERE ChangedAtUtc > @CursorValue
		ORDER BY ChangedAtUtc ASC
		""";

	private const string SqlNoCursor = """
		SELECT TOP (@BatchSize) ExternalId, Name, Email, Phone, ChangedAtUtc
		FROM LegacyCustomers
		ORDER BY ChangedAtUtc ASC
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="FetchLegacyCustomerSnapshotsRequest"/> class.
	/// </summary>
	/// <param name="cursor">Opaque cursor (ChangedAtUtc as ticks), or null to start from beginning.</param>
	/// <param name="batchSize">Maximum number of records to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public FetchLegacyCustomerSnapshotsRequest(string? cursor, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);

		string sql;
		if (cursor is not null)
		{
			var cursorTicks = long.Parse(cursor, CultureInfo.InvariantCulture);
			parameters.Add("@CursorValue", new DateTime(cursorTicks, DateTimeKind.Utc));
			sql = SqlWithCursor;
		}
		else
		{
			sql = SqlNoCursor;
		}

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var results = (await connection.QueryAsync<LegacyCustomerSnapshot>(Command).ConfigureAwait(false)).ToList();

			// Produce the next cursor from the last record's timestamp
			string? nextCursor = results.Count > 0
				? results[^1].ChangedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)
				: null;

			return new CursorFetchResult<LegacyCustomerSnapshot>(results, nextCursor);
		};
	}
}
