// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Data;
using Excalibur.EventSourcing.Postgres.Requests;
using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>Scans bounded stream-key pages using one captured source and opaque round state.</summary>
internal sealed class PostgresArchiveScanner(NpgsqlDataSource dataSource, string schema, string table)
	: IEventStoreArchiveScanner
{
	internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

	/// <inheritdoc />
	public async ValueTask<ArchiveScanPage> ScanArchiveCandidatesAsync(
		ArchivePolicy policy, int scanSize, ArchiveScanCursor? continuation, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scanSize);
		if (continuation is not null && (continuation is not Cursor supplied || !ReferenceEquals(supplied.Owner, this)))
		{
			throw new ArgumentException("The continuation does not belong to this archive scanner instance.", nameof(continuation));
		}

		await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		var cursor = (Cursor?)continuation;
		if (cursor is null)
		{
			var snapshot = new ArchivePolicy
			{
				MaxAge = policy.MaxAge,
				MaxPosition = policy.MaxPosition,
				RetainRecentCount = policy.RetainRecentCount,
			};
			var instant = TimeProvider.GetUtcNow();
			// This fence bounds stream membership only. Policy evaluation reads entire selected streams.
			var qualifiedTable = PgTableName.Format(schema, table);
#pragma warning disable CA2100 // Schema and table validated by the provider table-name formatter.
			var horizon = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
				$"SELECT COALESCE(MAX(position), 0) FROM {qualifiedTable}", cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100
			cursor = new Cursor(this, snapshot, instant, horizon, null);
		}

		var request = new GetArchiveCandidatesRequest(cursor.Policy, scanSize, cursor.Instant,
			cursor.Horizon, cursor.After, schema, table, cancellationToken);
		var candidates = await connection.ResolveAsync(request).ConfigureAwait(false);
		var next = request.LastExamined is { } last
			? new Cursor(this, cursor.Policy, cursor.Instant, cursor.Horizon, last) : null;
		return new ArchiveScanPage(candidates, next);
	}

	private sealed class Cursor(PostgresArchiveScanner owner, ArchivePolicy policy, DateTimeOffset instant,
		long horizon, (string TenantId, string AggregateId, string AggregateType)? after) : ArchiveScanCursor
	{
		internal PostgresArchiveScanner Owner { get; } = owner;
		internal ArchivePolicy Policy { get; } = policy;
		internal DateTimeOffset Instant { get; } = instant;
		internal long Horizon { get; } = horizon;
		internal (string TenantId, string AggregateId, string AggregateType)? After { get; } = after;
	}
}
