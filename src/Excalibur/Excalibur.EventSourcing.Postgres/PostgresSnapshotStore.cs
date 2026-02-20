// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Postgres.Requests;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Postgres implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic snapshot operations with upsert (insert-or-update) semantics.
/// Uses INSERT ... ON CONFLICT for thread-safe concurrent snapshot saves.
/// Stores only the latest snapshot per aggregate (no snapshot history).
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: NpgsqlDataSource for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// <para>
/// Uses ValueTask for interface compliance, though SQL operations are inherently async.
/// </para>
/// </remarks>
public sealed class PostgresSnapshotStore : ISnapshotStore
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly ILogger<PostgresSnapshotStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSnapshotStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="PostgresSnapshotStore(NpgsqlDataSource, ILogger{PostgresSnapshotStore})"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public PostgresSnapshotStore(string connectionString, ILogger<PostgresSnapshotStore> logger)
		: this(CreateDataSource(connectionString), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSnapshotStore"/> class with an NpgsqlDataSource.
	/// </summary>
	/// <param name="dataSource">
	/// An <see cref="NpgsqlDataSource"/> that manages connection pooling.
	/// Using NpgsqlDataSource is the recommended pattern per Npgsql documentation.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, IEventStoreDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <see cref="IDb"/> abstraction</description></item>
	/// </list>
	/// </remarks>
	public PostgresSnapshotStore(
		NpgsqlDataSource dataSource,
		ILogger<PostgresSnapshotStore> logger)
	{
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

			var snapshot = await connection.ResolveAsync(
					new GetLatestSnapshotRequest(aggregateId, aggregateType, cancellationToken))
				.ConfigureAwait(false);

			if (snapshot == null)
			{
				result = WriteStoreTelemetry.Results.NotFound;
			}

			return snapshot;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Postgres,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

			_ = await connection.ResolveAsync(
					new SaveSnapshotRequest(snapshot, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Saved snapshot for {AggregateType}/{AggregateId} at version {Version}",
				snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Postgres,
				"save",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

			_ = await connection.ResolveAsync(
					new DeleteSnapshotsRequest(aggregateId, aggregateType, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Deleted snapshots for {AggregateType}/{AggregateId}",
				aggregateType, aggregateId);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Postgres,
				"delete",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

			_ = await connection.ResolveAsync(
					new DeleteSnapshotsOlderThanRequest(aggregateId, aggregateType, olderThanVersion, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Deleted snapshots older than version {Version} for {AggregateType}/{AggregateId}",
				olderThanVersion, aggregateType, aggregateId);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Postgres,
				"delete_older_than",
				result,
				stopwatch.Elapsed);
		}
	}

	private static NpgsqlDataSource CreateDataSource(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return NpgsqlDataSource.Create(connectionString);
	}
}
