// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Data request that fetches a batch of legacy customer snapshots from SQL Server.
/// </summary>
/// <remarks>
/// Demonstrates the <see cref="IDataRequest{TConnection, TModel}"/> pattern with Dapper
/// for paginated batch reads from a legacy database table.
/// </remarks>
public sealed class FetchLegacyCustomerSnapshotsRequest
	: DataRequestBase<IDbConnection, IEnumerable<LegacyCustomerSnapshot>>
{
	private const string Sql = """
		SELECT ExternalId, Name, Email, Phone, ChangedAtUtc
		FROM LegacyCustomers
		ORDER BY ChangedAtUtc ASC
		OFFSET @Skip ROWS FETCH NEXT @BatchSize ROWS ONLY
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="FetchLegacyCustomerSnapshotsRequest"/> class.
	/// </summary>
	/// <param name="skip">Number of records to skip.</param>
	/// <param name="batchSize">Maximum number of records to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public FetchLegacyCustomerSnapshotsRequest(long skip, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(skip);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var parameters = new DynamicParameters();
		parameters.Add("@Skip", skip);
		parameters.Add("@BatchSize", batchSize);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var results = await connection.QueryAsync<LegacyCustomerSnapshot>(Command).ConfigureAwait(false);
			return results;
		};
	}
}
