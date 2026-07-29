// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Data.Observability;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Oracle.Requests;

using Microsoft.Extensions.Logging;

using global::Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// Oracle Database implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// Provides atomic snapshot operations with upsert (insert-or-update) semantics using an Oracle
/// <c>MERGE</c> statement. Stores only the latest snapshot per aggregate (no snapshot history).
/// </remarks>
public sealed class OracleSnapshotStore : ISnapshotStore
{
	private readonly Func<OracleConnection> _connectionFactory;
	private readonly ILogger<OracleSnapshotStore> _logger;
	private readonly ITenantContext? _tenantContext;
	private readonly string _schema;
	private readonly string _table;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSnapshotStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="logger">The logger instance.</param>
	public OracleSnapshotStore(string connectionString, ILogger<OracleSnapshotStore> logger)
		: this(CreateConnectionFactory(connectionString), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSnapshotStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="OracleConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "EXCALIBUR".</param>
	/// <param name="table">The snapshot store table name. Default: "EVENTSTORESNAPSHOTS".</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, every
	/// read, save, and delete is restricted to the resolved tenant's own rows.
	/// </param>
	public OracleSnapshotStore(
		Func<OracleConnection> connectionFactory,
		ILogger<OracleSnapshotStore> logger,
		string schema = "EXCALIBUR",
		string table = "EVENTSTORESNAPSHOTS",
		ITenantContext? tenantContext = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_schema = schema;
		_table = table;
		_tenantContext = tenantContext;
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
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var snapshot = await connection.ResolveAsync(
					new GetLatestSnapshotRequest(aggregateId, aggregateType, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
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
				WriteStoreTelemetry.Providers.Oracle,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(ISnapshot snapshot, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var scope = TenantScope.FromContext(_tenantContext);

			try
			{
				_ = await connection.ResolveAsync(
						new SaveSnapshotRequest(snapshot, scope, cancellationToken, _schema, _table))
					.ConfigureAwait(false);
			}
			catch (Exception ex) when (IsUniqueConstraintViolation(ex))
			{
				// MERGE is not immune to a unique-constraint violation. Two sessions saving the same
				// aggregate concurrently can both evaluate NOT MATCHED before either commits, and both take
				// the INSERT branch; the second gets ORA-00001. This is documented Oracle behaviour, not a
				// defect in the statement, and it is ordinary here -- several instances may snapshot the
				// same aggregate at once.
				//
				// Retrying once is sufficient and correct: the row now exists, so the retry takes the
				// MATCHED branch, where the version guard decides whether this snapshot is newer. A losing
				// retry updates nothing, which is the same outcome it would have had if the two saves had
				// been serialised. A second violation cannot come from this race and is left to surface.
				_ = await connection.ResolveAsync(
						new SaveSnapshotRequest(snapshot, scope, cancellationToken, _schema, _table))
					.ConfigureAwait(false);
			}

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
				WriteStoreTelemetry.Providers.Oracle,
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
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			_ = await connection.ResolveAsync(
					new DeleteSnapshotsRequest(aggregateId, aggregateType, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			_logger.LogDebug("Deleted snapshots for {AggregateType}/{AggregateId}", aggregateType, aggregateId);
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
				WriteStoreTelemetry.Providers.Oracle,
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
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			_ = await connection.ResolveAsync(
					new DeleteSnapshotsOlderThanRequest(aggregateId, aggregateType, olderThanVersion, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
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
				WriteStoreTelemetry.Providers.Oracle,
				"delete_older_than",
				result,
				stopwatch.Elapsed);
		}
	}

	private static Func<OracleConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return () => new OracleConnection(connectionString);
	}

	/// <summary>
	/// Determines whether an exception is an Oracle unique-constraint violation (ORA-00001).
	/// </summary>
	/// <remarks>
	/// The exception arrives wrapped: the data-access layer surfaces provider errors as
	/// <see cref="OperationFailedException"/>, so the chain is walked rather than the outermost type
	/// inspected. Matching only ORA-00001 keeps this narrow -- any other Oracle error still propagates.
	/// </remarks>
	/// <param name="exception">The exception to inspect.</param>
	/// <returns><see langword="true"/> when the chain contains ORA-00001.</returns>
	private static bool IsUniqueConstraintViolation(Exception exception)
	{
		const int OracleUniqueConstraintViolated = 1;

		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current is OracleException { Number: OracleUniqueConstraintViolated })
			{
				return true;
			}
		}

		return false;
	}
}
