// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// SQL Server-backed implementation of <see cref="ILegacyCustomerSnapshotSource"/> using the
/// connection factory pattern consistent with the rest of the Excalibur framework.
/// </summary>
/// <remarks>
/// <para>
/// This demonstrates the production pattern for batch-reading from a legacy database:
/// <list type="bullet">
/// <item><c>Func&lt;SqlConnection&gt;</c> — connection factory (one fresh connection per batch)</item>
/// <item><see cref="FetchLegacyCustomerSnapshotsRequest"/> — encapsulates SQL + parameters as a DataRequest</item>
/// <item>ADO.NET connection pooling manages the underlying TCP connections automatically</item>
/// </list>
/// </para>
/// <para>
/// The connection factory pattern supports multi-database scenarios naturally.
/// Each source gets its own factory with its own connection string:
/// <code>
/// // Legacy customers from Server A
/// services.AddSingleton&lt;ILegacyCustomerSnapshotSource&gt;(
///     new SqlLegacyCustomerSnapshotSource(() =&gt; new SqlConnection(serverAConnectionString)));
///
/// // Orders backfill from Server B (different source, different factory)
/// services.AddSingleton&lt;ILegacyOrderSnapshotSource&gt;(
///     new SqlLegacyOrderSnapshotSource(() =&gt; new SqlConnection(serverBConnectionString)));
/// </code>
/// </para>
/// </remarks>
public sealed class SqlLegacyCustomerSnapshotSource : ILegacyCustomerSnapshotSource
{
	private readonly Func<SqlConnection> _connectionFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlLegacyCustomerSnapshotSource"/> class.
	/// </summary>
	/// <param name="connectionFactory">Factory that creates SQL connections to the legacy database.</param>
	public SqlLegacyCustomerSnapshotSource(Func<SqlConnection> connectionFactory)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
	}

	/// <inheritdoc />
	public async Task<IEnumerable<LegacyCustomerSnapshot>> FetchBatchAsync(
		long skip,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var request = new FetchLegacyCustomerSnapshotsRequest(skip, batchSize, cancellationToken);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		return await request.ResolveAsync(connection).ConfigureAwait(false);
	}
}
