// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;
using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>Reads raw archive payloads from one captured source with explicit partition addressing.</summary>
internal sealed class PostgresArchiveEventReader(NpgsqlDataSource dataSource, string schema, string table)
	: IEventStoreArchiveReader
{
	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadArchiveEventsAsync(
		KeyedTenantPartition tenant, string aggregateId, string aggregateType, long upToVersion,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentOutOfRangeException.ThrowIfNegative(upToVersion);
		await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		return await connection.ResolveAsync(new LoadEventsRequest(
			aggregateId, aggregateType, -1, tenant, upToVersion, schema, table, cancellationToken))
			.ConfigureAwait(false);
	}

}
